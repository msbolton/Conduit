using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.AMQP;
using Conduit.Api;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.ActiveMq
{
    /// <summary>
    /// ActiveMQ Artemis transport implementation using AMQP 1.0.
    /// </summary>
    public class ActiveMqTransport : TransportAdapterBase
    {
        private readonly ActiveMqConfiguration _configuration;
        private readonly ActiveMqMessageConverter _messageConverter;
        private readonly ILogger<ActiveMqTransport> _logger;
        private readonly ConcurrentDictionary<string, ActiveMqSubscription> _subscriptions;

        private IConnectionFactory? _connectionFactory;
        private IConnection? _connection;
        private ISession? _session;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the ActiveMqTransport class.
        /// </summary>
        /// <param name="configuration">The ActiveMQ configuration</param>
        /// <param name="logger">The logger instance</param>
        public ActiveMqTransport(
            ActiveMqConfiguration configuration,
            ILogger<ActiveMqTransport> logger)
            : base(TransportType.Amqp, "ActiveMQ", configuration, logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageConverter = new ActiveMqMessageConverter();
            _subscriptions = new ConcurrentDictionary<string, ActiveMqSubscription>();
        }

        /// <summary>
        /// Establishes connection to the ActiveMQ broker.
        /// </summary>
        protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection != null && _connection.IsStarted)
                {
                    _logger.LogDebug("Already connected to ActiveMQ broker");
                    return;
                }

                _logger.LogInformation("Connecting to ActiveMQ broker at {BrokerUri}", _configuration.BrokerUri);

                // Create connection factory
                var connectionUri = _configuration.BuildConnectionUri();
                _connectionFactory = new NmsConnectionFactory(connectionUri);

                // Create connection
                if (!string.IsNullOrEmpty(_configuration.Username))
                {
                    _connection = await Task.Run(() =>
                        _connectionFactory.CreateConnection(_configuration.Username, _configuration.Password),
                        cancellationToken);
                }
                else
                {
                    _connection = await Task.Run(() =>
                        _connectionFactory.CreateConnection(),
                        cancellationToken);
                }

                // Set client ID if specified (required for durable subscriptions)
                if (!string.IsNullOrEmpty(_configuration.ClientId))
                {
                    _connection.ClientId = _configuration.ClientId;
                }

                // Set close timeout
                _connection.CloseTimeout = TimeSpan.FromMilliseconds(_configuration.CloseTimeout);

                // Set exception listener
                _connection.ExceptionListener += OnConnectionException;

                // Start the connection
                await Task.Run(() => _connection.Start(), cancellationToken);

                // Create session
                var ackMode = MapAcknowledgementMode(_configuration.AcknowledgementMode);
                _session = await Task.Run(() => _connection.CreateSession(ackMode), cancellationToken);

                _logger.LogInformation("Connected to ActiveMQ broker successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to ActiveMQ broker");
                await CleanupConnectionAsync();
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Disconnects from the ActiveMQ broker.
        /// </summary>
        protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Disconnecting from ActiveMQ broker");

                // Stop all subscriptions
                foreach (var subscription in _subscriptions.Values)
                {
                    await subscription.DisposeAsync();
                }
                _subscriptions.Clear();

                await CleanupConnectionAsync();

                _logger.LogInformation("Disconnected from ActiveMQ broker");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Sends a message to the broker.
        /// </summary>
        protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
        {
            if (_session == null)
                throw new InvalidOperationException("Not connected to ActiveMQ broker");

            if (string.IsNullOrEmpty(destination))
                throw new ArgumentException("Destination must be specified for ActiveMQ transport", nameof(destination));

            _logger.LogDebug("Sending message {MessageId} to {Destination}", message.MessageId, destination);

            // Convert Conduit message to transport message
            var transportMessage = CreateTransportMessage(message, destination);

            // Convert to NMS message
            var nmsMessage = _messageConverter.ToNmsMessage(transportMessage, _session);

            // Get or create destination
            var nmsDestination = GetDestination(destination);

            // Create producer and send
            await Task.Run(() =>
            {
                using var producer = _session.CreateProducer(nmsDestination);

                // Configure producer
                producer.DeliveryMode = _configuration.PersistentDelivery
                    ? MsgDeliveryMode.Persistent
                    : MsgDeliveryMode.NonPersistent;

                producer.Priority = MapPriority(_configuration.DefaultMessagePriority);

                if (_configuration.DefaultTimeToLive > 0)
                {
                    producer.TimeToLive = TimeSpan.FromMilliseconds(_configuration.DefaultTimeToLive);
                }

                // Send the message
                producer.Send(nmsMessage);
            }, cancellationToken);

            _logger.LogDebug("Message {MessageId} sent successfully", message.MessageId);
        }

        /// <summary>
        /// Creates a subscription to receive messages.
        /// </summary>
        protected override async Task<ITransportSubscription> SubscribeCoreAsync(
            string? source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken)
        {
            if (_session == null)
                throw new InvalidOperationException("Not connected to ActiveMQ broker");

            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Source must be specified for ActiveMQ subscriptions", nameof(source));

            _logger.LogInformation("Creating subscription to {Source}", source);

            // Get or create destination
            var destination = GetDestination(source);

            // Create subscription
            var subscription = await Task.Run(() =>
            {
                var consumer = _session.CreateConsumer(destination);
                return new ActiveMqSubscription(
                    Guid.NewGuid().ToString(),
                    source,
                    consumer,
                    _messageConverter,
                    handler,
                    _logger);
            }, cancellationToken);

            // Store subscription
            _subscriptions.TryAdd(subscription.Id, subscription);

            _logger.LogInformation("Subscription to {Source} created with ID {SubscriptionId}", source, subscription.Id);

            return subscription;
        }

        /// <summary>
        /// Cleans up connection resources.
        /// </summary>
        private async Task CleanupConnectionAsync()
        {
            if (_session != null)
            {
                try
                {
                    await Task.Run(() => _session.Close());
                    await Task.Run(() => _session.Dispose());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing session");
                }
                finally
                {
                    _session = null;
                }
            }

            if (_connection != null)
            {
                try
                {
                    _connection.ExceptionListener -= OnConnectionException;
                    await Task.Run(() => _connection.Stop());
                    await Task.Run(() => _connection.Close());
                    await Task.Run(() => _connection.Dispose());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing connection");
                }
                finally
                {
                    _connection = null;
                }
            }

            _connectionFactory = null;
        }

        /// <summary>
        /// Gets or creates a destination from a URI string.
        /// </summary>
        private IDestination GetDestination(string destinationUri)
        {
            if (_session == null)
                throw new InvalidOperationException("Session is not available");

            // Parse destination URI format: queue://name or topic://name
            var parts = destinationUri.Split("://", 2);
            if (parts.Length != 2)
            {
                // Default to queue if no prefix
                return _session.GetQueue(destinationUri);
            }

            var type = parts[0].ToLowerInvariant();
            var name = parts[1];

            return type switch
            {
                "queue" => _session.GetQueue(name),
                "topic" => _session.GetTopic(name),
                "temp-queue" when _configuration.AllowTemporaryQueues => _session.CreateTemporaryQueue(),
                "temp-topic" when _configuration.AllowTemporaryTopics => _session.CreateTemporaryTopic(),
                _ => throw new ArgumentException($"Invalid destination type: {type}", nameof(destinationUri))
            };
        }

        /// <summary>
        /// Handles connection exceptions.
        /// </summary>
        private void OnConnectionException(Exception exception)
        {
            _logger.LogError(exception, "ActiveMQ connection exception occurred");

            // Mark as disconnected
            OnConnectionStateChanged(false);

            // Attempt reconnection if auto-reconnect is enabled
            if (_configuration.Connection.AutoReconnect)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CleanupConnectionAsync();
                        await ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect to ActiveMQ broker");
                    }
                });
            }
        }

        /// <summary>
        /// Maps acknowledgement mode to NMS AcknowledgementMode.
        /// </summary>
        private AcknowledgementMode MapAcknowledgementMode(AcknowledgementMode mode)
        {
            return mode switch
            {
                AcknowledgementMode.AutoAcknowledge => Apache.NMS.AcknowledgementMode.AutoAcknowledge,
                AcknowledgementMode.ClientAcknowledge => Apache.NMS.AcknowledgementMode.ClientAcknowledge,
                AcknowledgementMode.DupsOkAcknowledge => Apache.NMS.AcknowledgementMode.DupsOkAcknowledge,
                AcknowledgementMode.Transactional => Apache.NMS.AcknowledgementMode.Transactional,
                AcknowledgementMode.IndividualAcknowledge => Apache.NMS.AcknowledgementMode.IndividualAcknowledge,
                _ => Apache.NMS.AcknowledgementMode.AutoAcknowledge
            };
        }

        /// <summary>
        /// Maps byte priority to NMS MsgPriority.
        /// </summary>
        private MsgPriority MapPriority(byte priority)
        {
            return priority switch
            {
                <= 1 => MsgPriority.Lowest,
                2 or 3 => MsgPriority.VeryLow,
                4 => MsgPriority.Low,
                5 or 6 => MsgPriority.Normal,
                7 or 8 => MsgPriority.High,
                9 => MsgPriority.VeryHigh,
                _ => MsgPriority.Highest
            };
        }

        /// <summary>
        /// Disposes the transport and releases resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connectionLock.Wait();
                try
                {
                    CleanupConnectionAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    _connectionLock.Release();
                    _connectionLock.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
