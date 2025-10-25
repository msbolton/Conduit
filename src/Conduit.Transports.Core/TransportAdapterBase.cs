using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Base class for transport adapter implementations.
    /// Provides common functionality and template methods for specific transports.
    /// </summary>
    public abstract class TransportAdapterBase : ITransport
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, ITransportSubscription> _subscriptions = new();
        private bool _disposed;
        protected readonly object _stateLock = new();
        protected TransportStatistics _statistics = new();

        /// <inheritdoc/>
        public abstract TransportType Type { get; }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Gets the transport configuration.
        /// </summary>
        protected TransportConfiguration Configuration { get; }

        /// <summary>
        /// Initializes a new instance of the TransportAdapterBase class.
        /// </summary>
        /// <param name="configuration">The transport configuration</param>
        /// <param name="logger">Optional logger</param>
        protected TransportAdapterBase(TransportConfiguration configuration, ILogger? logger = null)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
        }

        /// <inheritdoc/>
        public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (IsConnected)
                {
                    _logger?.LogWarning("Transport '{Name}' is already connected", Name);
                    return;
                }
            }

            _logger?.LogInformation("Connecting transport '{Name}' ({Type})", Name, Type);

            try
            {
                _statistics.ConnectionAttempts++;

                await ConnectCoreAsync(cancellationToken);

                lock (_stateLock)
                {
                    IsConnected = true;
                }

                _statistics.SuccessfulConnections++;
                _statistics.ActiveConnections++;

                _logger?.LogInformation("Transport '{Name}' connected successfully", Name);
            }
            catch (Exception ex)
            {
                _statistics.ConnectionFailures++;
                _logger?.LogError(ex, "Failed to connect transport '{Name}'", Name);
                throw new TransportException($"Failed to connect transport '{Name}'", ex);
            }
        }

        /// <inheritdoc/>
        public virtual async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (!IsConnected)
                {
                    _logger?.LogWarning("Transport '{Name}' is not connected", Name);
                    return;
                }
            }

            _logger?.LogInformation("Disconnecting transport '{Name}'", Name);

            try
            {
                await DisconnectCoreAsync(cancellationToken);

                lock (_stateLock)
                {
                    IsConnected = false;
                }

                _statistics.Disconnections++;
                _statistics.ActiveConnections--;

                _logger?.LogInformation("Transport '{Name}' disconnected successfully", Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disconnecting transport '{Name}'", Name);
                throw new TransportException($"Failed to disconnect transport '{Name}'", ex);
            }
        }

        /// <inheritdoc/>
        public virtual async Task SendAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _logger?.LogDebug("Sending message {MessageId} via transport '{Name}'", message.Id, Name);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await SendCoreAsync(message, null, cancellationToken);

                stopwatch.Stop();
                UpdateSendStatistics(true, stopwatch.ElapsedMilliseconds);

                _logger?.LogDebug("Message {MessageId} sent successfully in {ElapsedMs}ms", message.Id, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UpdateSendStatistics(false, stopwatch.ElapsedMilliseconds);

                _logger?.LogError(ex, "Failed to send message {MessageId} via transport '{Name}'", message.Id, Name);
                throw new TransportException($"Failed to send message via transport '{Name}'", ex);
            }
        }

        /// <inheritdoc/>
        public virtual async Task SendAsync(IMessage message, string destination, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentNullException(nameof(destination));

            _logger?.LogDebug("Sending message {MessageId} to '{Destination}' via transport '{Name}'", message.Id, destination, Name);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await SendCoreAsync(message, destination, cancellationToken);

                stopwatch.Stop();
                UpdateSendStatistics(true, stopwatch.ElapsedMilliseconds);

                _logger?.LogDebug("Message {MessageId} sent to '{Destination}' successfully in {ElapsedMs}ms", message.Id, destination, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UpdateSendStatistics(false, stopwatch.ElapsedMilliseconds);

                _logger?.LogError(ex, "Failed to send message {MessageId} to '{Destination}' via transport '{Name}'", message.Id, destination, Name);
                throw new TransportException($"Failed to send message to '{destination}' via transport '{Name}'", ex);
            }
        }

        /// <inheritdoc/>
        public virtual async Task<ITransportSubscription> SubscribeAsync(
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = await SubscribeCoreAsync(null, handler, cancellationToken);

            _subscriptions.TryAdd(subscription.SubscriptionId, subscription);
            _statistics.ActiveSubscriptions = _subscriptions.Count;

            _logger?.LogInformation("Created subscription {SubscriptionId} on transport '{Name}'", subscription.SubscriptionId, Name);

            return subscription;
        }

        /// <inheritdoc/>
        public virtual async Task<ITransportSubscription> SubscribeAsync(
            string source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = await SubscribeCoreAsync(source, handler, cancellationToken);

            _subscriptions.TryAdd(subscription.SubscriptionId, subscription);
            _statistics.ActiveSubscriptions = _subscriptions.Count;

            _logger?.LogInformation("Created subscription {SubscriptionId} for source '{Source}' on transport '{Name}'", subscription.SubscriptionId, source, Name);

            return subscription;
        }

        /// <inheritdoc/>
        public virtual TransportStatistics GetStatistics()
        {
            return _statistics.Snapshot();
        }

        /// <summary>
        /// Core implementation of connect logic.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract Task ConnectCoreAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Core implementation of disconnect logic.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract Task DisconnectCoreAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Core implementation of send logic.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken);

        /// <summary>
        /// Core implementation of subscribe logic.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract Task<ITransportSubscription> SubscribeCoreAsync(
            string? source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken);

        /// <summary>
        /// Updates send statistics.
        /// </summary>
        protected virtual void UpdateSendStatistics(bool success, long elapsedMs)
        {
            if (success)
            {
                _statistics.MessagesSent++;
                var totalTime = _statistics.AverageSendTimeMs * (_statistics.MessagesSent - 1);
                _statistics.AverageSendTimeMs = (totalTime + elapsedMs) / _statistics.MessagesSent;
            }
            else
            {
                _statistics.SendFailures++;
            }

            _statistics.LastUpdated = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Updates receive statistics.
        /// </summary>
        protected virtual void UpdateReceiveStatistics(bool success, long elapsedMs)
        {
            if (success)
            {
                _statistics.MessagesReceived++;
                var totalTime = _statistics.AverageReceiveTimeMs * (_statistics.MessagesReceived - 1);
                _statistics.AverageReceiveTimeMs = (totalTime + elapsedMs) / _statistics.MessagesReceived;
            }
            else
            {
                _statistics.ReceiveFailures++;
            }

            _statistics.LastUpdated = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Removes a subscription from the collection.
        /// </summary>
        protected virtual void RemoveSubscription(string subscriptionId)
        {
            if (_subscriptions.TryRemove(subscriptionId, out _))
            {
                _statistics.ActiveSubscriptions = _subscriptions.Count;
                _logger?.LogInformation("Removed subscription {SubscriptionId} from transport '{Name}'", subscriptionId, Name);
            }
        }

        /// <summary>
        /// Ensures the transport is connected.
        /// </summary>
        protected void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException($"Transport '{Name}' is not connected");
            }
        }

        /// <summary>
        /// Throws if the transport has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Disposes the transport.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the transport.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during transport disposal");
                }

                foreach (var subscription in _subscriptions.Values)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Exception thrown by transport operations.
    /// </summary>
    public class TransportException : Exception
    {
        public TransportException(string message) : base(message) { }
        public TransportException(string message, Exception innerException) : base(message, innerException) { }
    }
}
