using System.Text.Json;
using Conduit.Api;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace Conduit.Transports.ZeroMq;

/// <summary>
/// ZeroMQ transport implementation supporting multiple messaging patterns
/// </summary>
public class ZeroMqTransport : TransportAdapterBase
{
    private readonly ZeroMqConfiguration _config;
    private NetMQSocket? _socket;
    private NetMQPoller? _poller;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isServerSocket;

    /// <inheritdoc />
    public override TransportType Type => TransportType.ZeroMq;

    /// <inheritdoc />
    public override string Name => $"ZeroMQ-{_config.Pattern}";

    /// <summary>
    /// Initializes a new instance of the ZeroMqTransport class
    /// </summary>
    public ZeroMqTransport(ZeroMqConfiguration configuration, ILogger<ZeroMqTransport>? logger = null)
        : base(configuration, logger)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _socket = CreateSocket(_config.Pattern);
            ConfigureSocket(_socket);

            if (!string.IsNullOrWhiteSpace(_config.BindAddress))
            {
                _socket.Bind(_config.BindAddress);
                _isServerSocket = true;
            }
            else if (!string.IsNullOrWhiteSpace(_config.ConnectAddress))
            {
                _socket.Connect(_config.ConnectAddress);
                _isServerSocket = false;
            }
            else if (_config.ConnectAddresses.Count > 0)
            {
                foreach (var address in _config.ConnectAddresses)
                {
                    _socket.Connect(address);
                }
                _isServerSocket = false;
            }

            // Setup subscription topics for SUB sockets
            if (_socket is SubscriberSocket subSocket)
            {
                if (_config.SubscriptionTopics.Count > 0)
                {
                    foreach (var topic in _config.SubscriptionTopics)
                    {
                        subSocket.Subscribe(topic);
                    }
                }
                else
                {
                    // Subscribe to all messages
                    subSocket.SubscribeToAnyTopic();
                }
            }

            // Start poller for receiving messages
            if (NeedsPoller(_config.Pattern))
            {
                _poller = new NetMQPoller { _socket };
                _poller.RunAsync();
            }

        }, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                _poller?.Stop();
                _poller?.Dispose();
                _poller = null;

                _socket?.Dispose();
                _socket = null;
            }
            catch (Exception)
            {
                // TransportAdapterBase will handle logging the exception
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
    {
        if (_socket == null)
            throw new InvalidOperationException("Socket is not connected");

        await Task.Run(() =>
        {
            var jsonPayload = JsonSerializer.Serialize(message, _jsonOptions);
            var transportMessage = new TransportMessage
            {
                MessageId = message.MessageId,
                MessageType = message.GetType().AssemblyQualifiedName ?? message.GetType().FullName ?? "Unknown",
                Payload = System.Text.Encoding.UTF8.GetBytes(jsonPayload),
                Headers = message.Headers?.ToDictionary(h => h.Key, h => (object)(h.Value?.ToString() ?? string.Empty)) ?? new Dictionary<string, object>(),
                Timestamp = DateTimeOffset.UtcNow,
                Source = _config.Name
            };

            if (!string.IsNullOrWhiteSpace(destination))
            {
                transportMessage.Destination = destination;
            }

            var messageJson = JsonSerializer.Serialize(transportMessage, _jsonOptions);

            switch (_config.Pattern)
            {
                case ZeroMqPattern.PubSub:
                    SendPubSubMessage(messageJson, destination);
                    break;

                case ZeroMqPattern.RequestReply:
                    SendRequestReplyMessage(messageJson);
                    break;

                case ZeroMqPattern.PushPull:
                    SendPushPullMessage(messageJson);
                    break;

                case ZeroMqPattern.Pair:
                    SendPairMessage(messageJson);
                    break;

                case ZeroMqPattern.RouterDealer:
                    SendRouterDealerMessage(messageJson, destination);
                    break;

                default:
                    throw new NotSupportedException($"Pattern {_config.Pattern} is not supported for sending");
            }

        }, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<ITransportSubscription> SubscribeCoreAsync(
        string? source,
        Func<TransportMessage, Task> handler,
        CancellationToken cancellationToken)
    {
        if (_socket == null)
            throw new InvalidOperationException("Socket is not connected");

        return await Task.Run(() =>
        {
            var subscription = new ZeroMqSubscription(
                Guid.NewGuid().ToString(),
                source,
                handler,
                this);

            // Setup message receiving based on pattern
            switch (_config.Pattern)
            {
                case ZeroMqPattern.PubSub:
                    SetupPubSubReceiver(subscription);
                    break;

                case ZeroMqPattern.RequestReply:
                    SetupRequestReplyReceiver(subscription);
                    break;

                case ZeroMqPattern.PushPull:
                    SetupPushPullReceiver(subscription);
                    break;

                case ZeroMqPattern.Pair:
                    SetupPairReceiver(subscription);
                    break;

                case ZeroMqPattern.RouterDealer:
                    SetupRouterDealerReceiver(subscription);
                    break;

                default:
                    throw new NotSupportedException($"Pattern {_config.Pattern} is not supported for receiving");
            }

            return subscription;
        }, cancellationToken);
    }

    private NetMQSocket CreateSocket(ZeroMqPattern pattern)
    {
        return pattern switch
        {
            ZeroMqPattern.PubSub => _isServerSocket || !string.IsNullOrWhiteSpace(_config.BindAddress)
                ? new PublisherSocket() : new SubscriberSocket(),
            ZeroMqPattern.RequestReply => _isServerSocket || !string.IsNullOrWhiteSpace(_config.BindAddress)
                ? new ResponseSocket() : new RequestSocket(),
            ZeroMqPattern.PushPull => _isServerSocket || !string.IsNullOrWhiteSpace(_config.BindAddress)
                ? new PushSocket() : new PullSocket(),
            ZeroMqPattern.Pair => new PairSocket(),
            ZeroMqPattern.RouterDealer => _isServerSocket || !string.IsNullOrWhiteSpace(_config.BindAddress)
                ? new RouterSocket() : new DealerSocket(),
            _ => throw new NotSupportedException($"Pattern {pattern} is not supported")
        };
    }

    private void ConfigureSocket(NetMQSocket socket)
    {
        // Configure basic socket options with correct NetMQ property names
        socket.Options.SendHighWatermark = _config.SendHighWaterMark;
        socket.Options.ReceiveHighWatermark = _config.ReceiveHighWaterMark;
        socket.Options.Linger = TimeSpan.FromMilliseconds(_config.LingerMs);
        socket.Options.TcpKeepalive = _config.TcpKeepAlive;
        socket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(_config.TcpKeepAliveInterval);

        // Set socket identity if specified
        if (!string.IsNullOrWhiteSpace(_config.SocketIdentity))
        {
            socket.Options.Identity = System.Text.Encoding.UTF8.GetBytes(_config.SocketIdentity);
        }
    }

    private bool NeedsPoller(ZeroMqPattern pattern)
    {
        // Patterns that need polling for receiving messages
        return pattern == ZeroMqPattern.PubSub ||
               pattern == ZeroMqPattern.RequestReply ||
               pattern == ZeroMqPattern.PushPull ||
               pattern == ZeroMqPattern.Pair ||
               pattern == ZeroMqPattern.RouterDealer;
    }

    private void SendPubSubMessage(string messageJson, string? topic)
    {
        if (_socket is PublisherSocket pubSocket)
        {
            var topicToUse = topic ?? "default";
            if (_config.UseFraming)
            {
                pubSocket.SendMoreFrame(topicToUse).SendFrame(messageJson);
            }
            else
            {
                pubSocket.SendFrame($"{topicToUse}:{messageJson}");
            }
        }
        else
        {
            throw new InvalidOperationException("Socket is not a PublisherSocket");
        }
    }

    private void SendRequestReplyMessage(string messageJson)
    {
        if (_socket is RequestSocket reqSocket)
        {
            reqSocket.SendFrame(messageJson);
        }
        else
        {
            throw new InvalidOperationException("Socket is not a RequestSocket");
        }
    }

    private void SendPushPullMessage(string messageJson)
    {
        if (_socket is PushSocket pushSocket)
        {
            pushSocket.SendFrame(messageJson);
        }
        else
        {
            throw new InvalidOperationException("Socket is not a PushSocket");
        }
    }

    private void SendPairMessage(string messageJson)
    {
        if (_socket is PairSocket pairSocket)
        {
            pairSocket.SendFrame(messageJson);
        }
        else
        {
            throw new InvalidOperationException("Socket is not a PairSocket");
        }
    }

    private void SendRouterDealerMessage(string messageJson, string? destination)
    {
        if (_socket is RouterSocket routerSocket)
        {
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination is required for Router pattern");

            routerSocket.SendMoreFrame(destination).SendFrame(messageJson);
        }
        else if (_socket is DealerSocket dealerSocket)
        {
            dealerSocket.SendFrame(messageJson);
        }
        else
        {
            throw new InvalidOperationException("Socket is not a Router or Dealer socket");
        }
    }

    private void SetupPubSubReceiver(ZeroMqSubscription subscription)
    {
        if (_socket is SubscriberSocket subSocket)
        {
            subSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    string messageJson;
                    string? topic = null;

                    if (_config.UseFraming && args.Socket.TryReceiveFrameString(out var topicFrame))
                    {
                        topic = topicFrame;
                        if (args.Socket.TryReceiveFrameString(out var messageFrame))
                        {
                            messageJson = messageFrame;
                        }
                        else
                        {
                            return; // Invalid message format
                        }
                    }
                    else if (args.Socket.TryReceiveFrameString(out var fullMessage))
                    {
                        var parts = fullMessage.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            topic = parts[0];
                            messageJson = parts[1];
                        }
                        else
                        {
                            messageJson = fullMessage;
                        }
                    }
                    else
                    {
                        return; // No message available
                    }

                    await ProcessReceivedMessage(messageJson, subscription);
                }
                catch (Exception)
                {
                    // TransportAdapterBase will handle statistics updates
                    throw;
                }
            };
        }
    }

    private void SetupRequestReplyReceiver(ZeroMqSubscription subscription)
    {
        if (_socket is ResponseSocket repSocket)
        {
            repSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    if (args.Socket.TryReceiveFrameString(out var messageJson))
                    {
                        await ProcessReceivedMessage(messageJson, subscription);

                        // Send acknowledgment response
                        repSocket.SendFrame("ACK");
                    }
                }
                catch (Exception ex)
                {
                    repSocket.SendFrame($"ERROR:{ex.Message}");
                    throw;
                }
            };
        }
    }

    private void SetupPushPullReceiver(ZeroMqSubscription subscription)
    {
        if (_socket is PullSocket pullSocket)
        {
            pullSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    if (args.Socket.TryReceiveFrameString(out var messageJson))
                    {
                        await ProcessReceivedMessage(messageJson, subscription);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            };
        }
    }

    private void SetupPairReceiver(ZeroMqSubscription subscription)
    {
        if (_socket is PairSocket pairSocket)
        {
            pairSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    if (args.Socket.TryReceiveFrameString(out var messageJson))
                    {
                        await ProcessReceivedMessage(messageJson, subscription);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            };
        }
    }

    private void SetupRouterDealerReceiver(ZeroMqSubscription subscription)
    {
        if (_socket is RouterSocket routerSocket)
        {
            routerSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    if (args.Socket.TryReceiveFrameString(out var clientId) &&
                        args.Socket.TryReceiveFrameString(out var messageJson))
                    {
                        await ProcessReceivedMessage(messageJson, subscription);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            };
        }
        else if (_socket is DealerSocket dealerSocket)
        {
            dealerSocket.ReceiveReady += async (sender, args) =>
            {
                try
                {
                    if (args.Socket.TryReceiveFrameString(out var messageJson))
                    {
                        await ProcessReceivedMessage(messageJson, subscription);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            };
        }
    }

    private async Task ProcessReceivedMessage(string messageJson, ZeroMqSubscription subscription)
    {
        var transportMessage = JsonSerializer.Deserialize<TransportMessage>(messageJson, _jsonOptions);
        if (transportMessage != null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await subscription.Handler(transportMessage);
            stopwatch.Stop();

            // Update subscription statistics
            subscription.IncrementMessagesReceived();

            // Let TransportAdapterBase handle the receive statistics
            UpdateReceiveStatistics(true, stopwatch.ElapsedMilliseconds);
        }
    }

    internal void RemoveSubscriptionInternal(string subscriptionId)
    {
        // Use the base class method for proper subscription management
        RemoveSubscription(subscriptionId);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _poller?.Stop();
            _poller?.Dispose();
            _socket?.Dispose();
        }
        base.Dispose(disposing);
    }
}