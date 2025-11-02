using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// Represents a subscription to TCP messages.
    /// </summary>
    public class TcpSubscription : ITransportSubscription
    {
        private readonly Func<TransportMessage, Task> _handler;
        private readonly ILogger _logger;

        private volatile bool _isPaused;
        private volatile bool _isDisposed;
        private long _messagesReceived;

        /// <summary>
        /// Initializes a new instance of the TcpSubscription class.
        /// </summary>
        /// <param name="id">The subscription ID</param>
        /// <param name="source">The source filter (null for all sources)</param>
        /// <param name="handler">The message handler</param>
        /// <param name="logger">The logger instance</param>
        public TcpSubscription(
            string id,
            string? source,
            Func<TransportMessage, Task> handler,
            ILogger logger)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? string.Empty;
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _isPaused = false;
            _isDisposed = false;
        }

        /// <summary>
        /// Gets the subscription ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the source filter (empty for all sources).
        /// </summary>
        public string Source { get; }

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
        /// Gets a value indicating whether the subscription is paused.
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Pauses message delivery for this subscription.
        /// </summary>
        public Task PauseAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TcpSubscription));

            _logger.LogInformation("Pausing subscription {SubscriptionId}", Id);
            _isPaused = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resumes message delivery for this subscription.
        /// </summary>
        public Task ResumeAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TcpSubscription));

            _logger.LogInformation("Resuming subscription {SubscriptionId}", Id);
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

            _logger.LogInformation("Unsubscribing subscription {SubscriptionId}", Id);

            await DisposeAsync();
        }

        /// <summary>
        /// Handles an incoming message.
        /// </summary>
        /// <param name="message">The transport message</param>
        internal async Task HandleMessageAsync(TransportMessage message)
        {
            if (_isDisposed || _isPaused)
                return;

            try
            {
                await _handler(message);
                Interlocked.Increment(ref _messagesReceived);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message {MessageId} in subscription {SubscriptionId}",
                    message.MessageId, Id);
                throw;
            }
        }

        /// <summary>
        /// Disposes the subscription.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Disposes the subscription asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _logger.LogInformation("Subscription {SubscriptionId} disposed", Id);

            await Task.CompletedTask;
        }
    }
}
