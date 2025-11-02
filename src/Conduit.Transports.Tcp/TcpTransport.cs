using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Serialization;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// TCP/Socket transport implementation.
    /// </summary>
    public class TcpTransport : TransportAdapterBase
    {
        private readonly TcpConfiguration _configuration;
        private readonly IMessageSerializer _serializer;
        private readonly ILogger<TcpTransport> _logger;
        private readonly ConcurrentDictionary<string, TcpSubscription> _subscriptions;

        public override TransportType Type => TransportType.Tcp;
        public override string Name => "TCP";

        private TcpServer? _server;
        private TcpClientManager? _clientManager;

        /// <summary>
        /// Initializes a new instance of the TcpTransport class.
        /// </summary>
        /// <param name="configuration">The TCP configuration</param>
        /// <param name="serializer">The message serializer</param>
        /// <param name="logger">The logger instance</param>
        public TcpTransport(
            TcpConfiguration configuration,
            IMessageSerializer serializer,
            ILogger<TcpTransport> logger)
            : base(configuration, logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subscriptions = new ConcurrentDictionary<string, TcpSubscription>();
        }

        /// <summary>
        /// Establishes the TCP connection or starts the server.
        /// </summary>
        protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            _configuration.Validate();

            if (_configuration.IsServer)
            {
                _logger.LogInformation("Starting TCP server mode on {Host}:{Port}",
                    _configuration.Host, _configuration.Port);

                _server = new TcpServer(_configuration, _logger);

                // Subscribe to server events
                _server.MessageReceived += OnServerMessageReceivedAsync;
                _server.ConnectionAccepted += connId =>
                {
                    _logger.LogInformation("Server accepted connection {ConnectionId}", connId);
                };
                _server.ConnectionClosed += connId =>
                {
                    _logger.LogInformation("Server connection {ConnectionId} closed", connId);
                };

                await _server.StartAsync(cancellationToken);

                _logger.LogInformation("TCP server started successfully");
            }
            else
            {
                _logger.LogInformation("Starting TCP client mode, connecting to {Host}:{Port}",
                    _configuration.RemoteHost, _configuration.RemotePort);

                _clientManager = new TcpClientManager(_configuration, _logger);

                // Subscribe to client events
                _clientManager.MessageReceived += OnClientMessageReceivedAsync;

                // Create initial connection to verify connectivity
                var testConnection = await _clientManager.GetConnectionAsync(cancellationToken);
                await _clientManager.ReturnConnectionAsync(testConnection);

                _logger.LogInformation("TCP client connected successfully");
            }
        }

        /// <summary>
        /// Disconnects the TCP transport.
        /// </summary>
        protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Disconnecting TCP transport");

            // Stop all subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                await subscription.DisposeAsync();
            }
            _subscriptions.Clear();

            // Stop server or client
            if (_server != null)
            {
                await _server.StopAsync(cancellationToken);
                await _server.DisposeAsync();
                _server = null;
            }

            if (_clientManager != null)
            {
                await _clientManager.DisposeAsync();
                _clientManager = null;
            }

            _logger.LogInformation("TCP transport disconnected");
        }

        /// <summary>
        /// Sends a message over TCP.
        /// </summary>
        protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(destination) && _configuration.IsServer)
            {
                // Server mode: broadcast to all connections
                if (_server == null)
                    throw new InvalidOperationException("Server not started");

                var transportMessage = new TransportMessage
                {
                    MessageId = message.MessageId,
                    CorrelationId = message.CorrelationId,
                    CausationId = message.CausationId,
                    Payload = _serializer.Serialize(message),
                    ContentType = _serializer.MimeType,
                    MessageType = message.GetType().Name,
                    Source = Name,
                    Destination = "broadcast",
                    Timestamp = DateTimeOffset.UtcNow
                };
                var data = _serializer.Serialize(transportMessage);

                await _server.BroadcastAsync(data, cancellationToken);

                _logger.LogDebug("Broadcasted message {MessageId} to all connections", message.MessageId);
            }
            else if (!string.IsNullOrEmpty(destination) && _configuration.IsServer)
            {
                // Server mode: send to specific connection
                if (_server == null)
                    throw new InvalidOperationException("Server not started");

                var transportMessage = new TransportMessage
                {
                    MessageId = message.MessageId,
                    CorrelationId = message.CorrelationId,
                    CausationId = message.CausationId,
                    Payload = _serializer.Serialize(message),
                    ContentType = _serializer.MimeType,
                    MessageType = message.GetType().Name,
                    Source = Name,
                    Destination = destination,
                    Timestamp = DateTimeOffset.UtcNow
                };
                var data = _serializer.Serialize(transportMessage);

                await _server.SendToConnectionAsync(destination, data, cancellationToken);

                _logger.LogDebug("Sent message {MessageId} to connection {Destination}", message.MessageId, destination);
            }
            else
            {
                // Client mode: send to server
                if (_clientManager == null)
                    throw new InvalidOperationException("Client not started");

                var transportMessage = new TransportMessage
                {
                    MessageId = message.MessageId,
                    CorrelationId = message.CorrelationId,
                    CausationId = message.CausationId,
                    Payload = _serializer.Serialize(message),
                    ContentType = _serializer.MimeType,
                    MessageType = message.GetType().Name,
                    Source = Name,
                    Destination = destination ?? "server",
                    Timestamp = DateTimeOffset.UtcNow
                };
                var data = _serializer.Serialize(transportMessage);

                await _clientManager.SendMessageAsync(data, cancellationToken);

                _logger.LogDebug("Sent message {MessageId} to server", message.MessageId);
            }
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
            var subscription = new TcpSubscription(subscriptionId, source, handler, _logger);

            _subscriptions.TryAdd(subscriptionId, subscription);

            _logger.LogInformation("Created subscription {SubscriptionId} for source {Source}", subscriptionId, source ?? "all");

            await Task.CompletedTask;
            return subscription;
        }

        /// <summary>
        /// Handles messages received from the server (server mode).
        /// </summary>
        private async Task OnServerMessageReceivedAsync(byte[] data, string connectionId)
        {
            try
            {
                var transportMessage = _serializer.Deserialize<TransportMessage>(data);

                if (transportMessage == null)
                {
                    _logger.LogWarning("Received null message from connection {ConnectionId}", connectionId);
                    return;
                }

                // Set source to connection ID if not specified
                if (string.IsNullOrEmpty(transportMessage.Source))
                {
                    transportMessage.Source = connectionId;
                }

                _logger.LogDebug("Received message {MessageId} from connection {ConnectionId}",
                    transportMessage.MessageId, connectionId);

                // Invoke matching subscriptions
                foreach (var subscription in _subscriptions.Values)
                {
                    if (subscription.IsPaused)
                        continue;

                    // Check if subscription matches source
                    if (subscription.Source == null || subscription.Source == connectionId || subscription.Source == "all")
                    {
                        await subscription.HandleMessageAsync(transportMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from connection {ConnectionId}", connectionId);
            }
        }

        /// <summary>
        /// Handles messages received from the server (client mode).
        /// </summary>
        private async Task OnClientMessageReceivedAsync(byte[] data, string connectionId)
        {
            try
            {
                var transportMessage = _serializer.Deserialize<TransportMessage>(data);

                if (transportMessage == null)
                {
                    _logger.LogWarning("Received null message");
                    return;
                }

                _logger.LogDebug("Received message {MessageId}", transportMessage.MessageId);

                // Invoke all subscriptions (client mode typically receives from single server)
                foreach (var subscription in _subscriptions.Values)
                {
                    if (!subscription.IsPaused)
                    {
                        await subscription.HandleMessageAsync(transportMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message");
            }
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
