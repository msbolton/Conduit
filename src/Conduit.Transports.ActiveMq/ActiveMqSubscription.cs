using System;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.ActiveMq
{
    /// <summary>
    /// Represents a subscription to an ActiveMQ destination.
    /// </summary>
    public class ActiveMqSubscription : ITransportSubscription
    {
        private readonly IMessageConsumer _consumer;
        private readonly ActiveMqMessageConverter _messageConverter;
        private readonly Func<TransportMessage, Task> _handler;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts;

        private volatile bool _isPaused;
        private volatile bool _isDisposed;
        private long _messagesReceived;

        /// <summary>
        /// Initializes a new instance of the ActiveMqSubscription class.
        /// </summary>
        /// <param name="id">The subscription ID</param>
        /// <param name="source">The source destination</param>
        /// <param name="consumer">The NMS message consumer</param>
        /// <param name="messageConverter">The message converter</param>
        /// <param name="handler">The message handler</param>
        /// <param name="logger">The logger instance</param>
        public ActiveMqSubscription(
            string id,
            string source,
            IMessageConsumer consumer,
            ActiveMqMessageConverter messageConverter,
            Func<TransportMessage, Task> handler,
            ILogger logger)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            _messageConverter = messageConverter ?? throw new ArgumentNullException(nameof(messageConverter));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _cts = new CancellationTokenSource();
            _isPaused = false;
            _isDisposed = false;

            // Start listening
            _consumer.Listener += OnMessageReceived;
        }

        /// <summary>
        /// Gets the subscription ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the subscription identifier.
        /// </summary>
        public string SubscriptionId => Id;

        /// <summary>
        /// Gets whether this subscription is active.
        /// </summary>
        public bool IsActive => !_isDisposed && !_isPaused;

        /// <summary>
        /// Gets the number of messages received through this subscription.
        /// </summary>
        public long MessagesReceived => _messagesReceived;

        /// <summary>
        /// Gets the source destination.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets a value indicating whether the subscription is paused.
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Pauses message delivery for this subscription.
        /// </summary>
        public Task PauseAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ActiveMqSubscription));

            _logger.LogInformation("Pausing subscription {SubscriptionId} for {Source}", Id, Source);
            _isPaused = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resumes message delivery for this subscription.
        /// </summary>
        public Task ResumeAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ActiveMqSubscription));

            _logger.LogInformation("Resuming subscription {SubscriptionId} for {Source}", Id, Source);
            _isPaused = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unsubscribes and releases resources.
        /// </summary>
        public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return;

            _logger.LogInformation("Unsubscribing from {Source} (subscription {SubscriptionId})", Source, Id);

            await DisposeAsync();
        }

        /// <summary>
        /// Handles incoming NMS messages.
        /// </summary>
        private void OnMessageReceived(IMessage nmsMessage)
        {
            if (_isDisposed || _isPaused)
                return;

            try
            {
                // Convert NMS message to transport message
                var transportMessage = _messageConverter.FromNmsMessage(nmsMessage);

                _logger.LogDebug(
                    "Received message {MessageId} from {Source} (subscription {SubscriptionId})",
                    transportMessage.MessageId,
                    Source,
                    Id);

                // Invoke handler asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _handler(transportMessage);
                        Interlocked.Increment(ref _messagesReceived);

                        // Acknowledge if using client acknowledgement
                        if (nmsMessage.NMSDeliveryMode == MsgDeliveryMode.Persistent)
                        {
                            nmsMessage.Acknowledge();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error handling message {MessageId} from {Source}",
                            transportMessage.MessageId,
                            Source);

                        // For transactional sessions, this would trigger a rollback
                        // For client ack, we don't acknowledge the message
                        throw;
                    }
                }, _cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Source}", Source);
            }
        }

        /// <summary>
        /// Disposes the subscription asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                // Stop listening
                _consumer.Listener -= OnMessageReceived;

                // Cancel any pending operations
                _cts.Cancel();

                // Close and dispose consumer
                await Task.Run(() =>
                {
                    _consumer.Close();
                    _consumer.Dispose();
                });

                _cts.Dispose();

                _logger.LogInformation("Subscription {SubscriptionId} disposed", Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing subscription {SubscriptionId}", Id);
            }
        }

        /// <summary>
        /// Disposes the subscription.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
