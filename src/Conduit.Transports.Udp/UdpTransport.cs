using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Serialization;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Udp
{
    /// <summary>
    /// UDP transport implementation with multicast and broadcast support.
    /// </summary>
    public class UdpTransport : TransportAdapterBase
    {
        private readonly UdpConfiguration _configuration;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<UdpTransport> _logger;
        private readonly ConcurrentDictionary<string, UdpSubscription> _subscriptions;

        public override TransportType Type => TransportType.Custom;
        public override string Name => "UDP";

        private UdpClient? _udpClient;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private IPEndPoint? _remoteEndpoint;

        /// <summary>
        /// Initializes a new instance of the UdpTransport class.
        /// </summary>
        /// <param name="configuration">The UDP configuration</param>
        /// <param name="serializer">The message serializer</param>
        /// <param name="logger">The logger instance</param>
        public UdpTransport(
            UdpConfiguration configuration,
            IMessageSerializer serializer,
            ILogger<UdpTransport> logger)
            : base(configuration, logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subscriptions = new ConcurrentDictionary<string, UdpSubscription>();
        }

        /// <summary>
        /// Establishes the UDP socket connection.
        /// </summary>
        protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            _configuration.Validate();

            _logger.LogInformation("Starting UDP transport on {Host}:{Port}", _configuration.Host, _configuration.Port);

            // Create UDP client
            if (_configuration.UseIPv6)
            {
                _udpClient = new UdpClient(AddressFamily.InterNetworkV6);
                _udpClient.Client.DualMode = true; // Support both IPv4 and IPv6
            }
            else
            {
                _udpClient = new UdpClient(AddressFamily.InterNetwork);
            }

            // Configure socket options
            ConfigureSocket();

            // Bind to local endpoint
            var localEndpoint = new IPEndPoint(
                IPAddress.Parse(_configuration.Host),
                _configuration.Port);

            _udpClient.Client.Bind(localEndpoint);

            _logger.LogInformation("UDP socket bound to {Endpoint}", localEndpoint);

            // Join multicast group if specified
            if (!string.IsNullOrEmpty(_configuration.MulticastGroup))
            {
                await JoinMulticastGroupAsync();
            }

            // Set remote endpoint if specified
            if (!string.IsNullOrEmpty(_configuration.RemoteHost) && _configuration.RemotePort.HasValue)
            {
                _remoteEndpoint = new IPEndPoint(
                    IPAddress.Parse(_configuration.RemoteHost),
                    _configuration.RemotePort.Value);

                _logger.LogInformation("Remote endpoint set to {RemoteEndpoint}", _remoteEndpoint);
            }

            // Start receiving
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), cancellationToken);

            _logger.LogInformation("UDP transport started successfully");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Disconnects the UDP transport.
        /// </summary>
        protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Disconnecting UDP transport");

            // Stop receiving
            _receiveCts?.Cancel();

            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Leave multicast group if joined
            if (!string.IsNullOrEmpty(_configuration.MulticastGroup) && _udpClient != null)
            {
                try
                {
                    var multicastAddress = IPAddress.Parse(_configuration.MulticastGroup);
                    _udpClient.DropMulticastGroup(multicastAddress);
                    _logger.LogInformation("Left multicast group {MulticastGroup}", _configuration.MulticastGroup);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error leaving multicast group");
                }
            }

            // Close UDP client
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            _receiveCts?.Dispose();
            _receiveCts = null;

            // Stop all subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                await subscription.DisposeAsync();
            }
            _subscriptions.Clear();

            _logger.LogInformation("UDP transport disconnected");
        }

        /// <summary>
        /// Sends a message via UDP.
        /// </summary>
        protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
        {
            if (_udpClient == null)
                throw new InvalidOperationException("UDP transport not connected");

            var transportMessage = new TransportMessage
            {
                MessageId = message.MessageId,
                CorrelationId = message.CorrelationId,
                CausationId = message.CausationId,
                Payload = _serializer.Serialize(message),
                ContentType = _serializer.MimeType,
                MessageType = message.GetType().Name,
                Source = Name,
                Destination = destination ?? "udp",
                Timestamp = DateTimeOffset.UtcNow
            };
            var data = _serializer.Serialize(transportMessage);

            if (data.Length > _configuration.MaxDatagramSize)
            {
                if (_configuration.EnableFragmentation)
                {
                    await SendFragmentedAsync(data, destination, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Message size {data.Length} exceeds max datagram size {_configuration.MaxDatagramSize}. " +
                        "Enable fragmentation or reduce message size.");
                }
            }
            else
            {
                await SendDatagramAsync(data, destination, cancellationToken);
            }

            _logger.LogDebug("Sent UDP message {MessageId} ({Size} bytes)", message.MessageId, data.Length);
        }

        /// <summary>
        /// Creates a subscription to receive messages.
        /// </summary>
        protected override async Task<ITransportSubscription> SubscribeCoreAsync(
            string? source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken)
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var subscription = new UdpSubscription(subscriptionId, source, handler, _logger);

            _subscriptions.TryAdd(subscriptionId, subscription);

            _logger.LogInformation("Created UDP subscription {SubscriptionId} for source {Source}",
                subscriptionId, source ?? "all");

            await Task.CompletedTask;
            return subscription;
        }

        /// <summary>
        /// Configures socket options.
        /// </summary>
        private void ConfigureSocket()
        {
            if (_udpClient == null)
                return;

            var socket = _udpClient.Client;

            // Set buffer sizes
            socket.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            socket.SendBufferSize = _configuration.SendBufferSize;

            // Set broadcast
            socket.EnableBroadcast = _configuration.AllowBroadcast;

            // Set reuse address
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);

            // Set exclusive address use (Windows only)
            if (OperatingSystem.IsWindows() && !_configuration.ReuseAddress)
            {
                socket.ExclusiveAddressUse = _configuration.ExclusiveAddressUse;
            }

            // Set timeouts
            if (_configuration.ReceiveTimeout > 0)
            {
                socket.ReceiveTimeout = _configuration.ReceiveTimeout;
            }

            if (_configuration.SendTimeout > 0)
            {
                socket.SendTimeout = _configuration.SendTimeout;
            }

            _logger.LogDebug("UDP socket configured with buffer sizes: RX={ReceiveBufferSize}, TX={SendBufferSize}",
                _configuration.ReceiveBufferSize, _configuration.SendBufferSize);
        }

        /// <summary>
        /// Joins a multicast group.
        /// </summary>
        private async Task JoinMulticastGroupAsync()
        {
            if (_udpClient == null || string.IsNullOrEmpty(_configuration.MulticastGroup))
                return;

            var multicastAddress = IPAddress.Parse(_configuration.MulticastGroup);

            if (!string.IsNullOrEmpty(_configuration.MulticastInterface))
            {
                var interfaceAddress = IPAddress.Parse(_configuration.MulticastInterface);
                _udpClient.JoinMulticastGroup(multicastAddress, interfaceAddress);
            }
            else
            {
                _udpClient.JoinMulticastGroup(multicastAddress, _configuration.MulticastTimeToLive);
            }

            // Set multicast loopback
            _udpClient.MulticastLoopback = _configuration.MulticastLoopback;

            _logger.LogInformation("Joined multicast group {MulticastGroup} with TTL {TTL}",
                _configuration.MulticastGroup, _configuration.MulticastTimeToLive);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sends a datagram.
        /// </summary>
        private async Task SendDatagramAsync(byte[] data, string? destination, CancellationToken cancellationToken)
        {
            if (_udpClient == null)
                throw new InvalidOperationException("UDP client not initialized");

            IPEndPoint? endpoint = null;

            // Determine destination endpoint
            if (!string.IsNullOrEmpty(destination))
            {
                // Parse destination as host:port
                var parts = destination.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                {
                    endpoint = new IPEndPoint(IPAddress.Parse(parts[0]), port);
                }
            }

            endpoint ??= _remoteEndpoint;

            if (endpoint != null)
            {
                await _udpClient.SendAsync(data, data.Length, endpoint);
            }
            else if (_configuration.AllowBroadcast)
            {
                // Broadcast
                var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _configuration.RemotePort ?? _configuration.Port);
                await _udpClient.SendAsync(data, data.Length, broadcastEndpoint);
            }
            else if (!string.IsNullOrEmpty(_configuration.MulticastGroup))
            {
                // Multicast
                var multicastEndpoint = new IPEndPoint(
                    IPAddress.Parse(_configuration.MulticastGroup),
                    _configuration.Port);
                await _udpClient.SendAsync(data, data.Length, multicastEndpoint);
            }
            else
            {
                throw new InvalidOperationException("No destination specified and no default remote endpoint configured");
            }
        }

        /// <summary>
        /// Sends a fragmented message.
        /// </summary>
        private async Task SendFragmentedAsync(byte[] data, string? destination, CancellationToken cancellationToken)
        {
            var fragmentSize = _configuration.FragmentSize;
            var totalFragments = (data.Length + fragmentSize - 1) / fragmentSize;
            var fragmentId = Guid.NewGuid().ToString();

            _logger.LogDebug("Fragmenting message into {TotalFragments} fragments of {FragmentSize} bytes",
                totalFragments, fragmentSize);

            for (int i = 0; i < totalFragments; i++)
            {
                var offset = i * fragmentSize;
                var length = Math.Min(fragmentSize, data.Length - offset);
                var fragment = new byte[length];
                Array.Copy(data, offset, fragment, 0, length);

                // TODO: Add fragment header (fragmentId, index, total)
                // For now, just send the fragment
                await SendDatagramAsync(fragment, destination, cancellationToken);
            }
        }

        /// <summary>
        /// Receive loop for incoming datagrams.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting UDP receive loop");

            while (!cancellationToken.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var data = result.Buffer;
                    var remoteEndpoint = result.RemoteEndPoint;

                    _logger.LogDebug("Received UDP datagram from {RemoteEndpoint} ({Size} bytes)",
                        remoteEndpoint, data.Length);

                    // Deserialize message
                    var transportMessage = _serializer.Deserialize<TransportMessage>(data);

                    if (transportMessage == null)
                    {
                        _logger.LogWarning("Received null transport message from {RemoteEndpoint}", remoteEndpoint);
                        continue;
                    }

                    // Set source to remote endpoint if not specified
                    if (string.IsNullOrEmpty(transportMessage.Source))
                    {
                        transportMessage.Source = remoteEndpoint.ToString();
                    }

                    // Invoke matching subscriptions
                    foreach (var subscription in _subscriptions.Values)
                    {
                        if (subscription.IsPaused)
                            continue;

                        // Check if subscription matches source
                        if (subscription.Source == null ||
                            subscription.Source == transportMessage.Source ||
                            subscription.Source == "all")
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await subscription.HandleMessageAsync(transportMessage);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in subscription handler for message {MessageId}",
                                        transportMessage.MessageId);
                                }
                            }, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("UDP receive loop cancelled");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("UDP client disposed, stopping receive loop");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving UDP datagram");
                }
            }

            _logger.LogDebug("UDP receive loop stopped");
        }

        /// <summary>
        /// Disposes the transport.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }

            base.Dispose(disposing);
        }
    }
}
