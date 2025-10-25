using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Messaging
{
    /// <summary>
    /// Manages event subscriptions for the message bus.
    /// </summary>
    public class SubscriptionManager
    {
        private readonly ConcurrentDictionary<Type, List<IMessageSubscription>> _subscriptions;
        private readonly ReaderWriterLockSlim _lock;

        /// <summary>
        /// Initializes a new instance of the SubscriptionManager class.
        /// </summary>
        public SubscriptionManager()
        {
            _subscriptions = new ConcurrentDictionary<Type, List<IMessageSubscription>>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Adds a subscription for a specific message type.
        /// </summary>
        public void AddSubscription(Type messageType, IMessageSubscription subscription)
        {
            Guard.AgainstNull(messageType, nameof(messageType));
            Guard.AgainstNull(subscription, nameof(subscription));

            _lock.EnterWriteLock();
            try
            {
                var subscriptions = _subscriptions.GetOrAdd(messageType, _ => new List<IMessageSubscription>());
                subscriptions.Add(subscription);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a subscription for a specific message type.
        /// </summary>
        public bool RemoveSubscription(Type messageType, IMessageSubscription subscription)
        {
            Guard.AgainstNull(messageType, nameof(messageType));
            Guard.AgainstNull(subscription, nameof(subscription));

            _lock.EnterWriteLock();
            try
            {
                if (_subscriptions.TryGetValue(messageType, out var subscriptions))
                {
                    var removed = subscriptions.Remove(subscription);

                    // Clean up empty subscription lists
                    if (subscriptions.Count == 0)
                    {
                        _subscriptions.TryRemove(messageType, out _);
                    }

                    return removed;
                }

                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all subscriptions for a specific message type.
        /// </summary>
        public IEnumerable<IMessageSubscription> GetSubscriptions(Type messageType)
        {
            Guard.AgainstNull(messageType, nameof(messageType));

            _lock.EnterReadLock();
            try
            {
                if (_subscriptions.TryGetValue(messageType, out var subscriptions))
                {
                    return subscriptions.ToList(); // Return a copy to avoid concurrent modification
                }

                return Enumerable.Empty<IMessageSubscription>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all active subscriptions for a specific message type.
        /// </summary>
        public IEnumerable<IMessageSubscription> GetActiveSubscriptions(Type messageType)
        {
            return GetSubscriptions(messageType).Where(s => s.IsActive);
        }

        /// <summary>
        /// Gets the count of active subscriptions.
        /// </summary>
        public int GetActiveSubscriptionCount()
        {
            _lock.EnterReadLock();
            try
            {
                return _subscriptions.Values.Sum(list => list.Count(s => s.IsActive));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the count of all subscriptions.
        /// </summary>
        public int GetTotalSubscriptionCount()
        {
            _lock.EnterReadLock();
            try
            {
                return _subscriptions.Values.Sum(list => list.Count);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets subscription statistics.
        /// </summary>
        public SubscriptionStatistics GetStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                var stats = new SubscriptionStatistics
                {
                    TotalSubscriptions = GetTotalSubscriptionCount(),
                    ActiveSubscriptions = GetActiveSubscriptionCount(),
                    MessageTypesWithSubscriptions = _subscriptions.Count,
                    SubscriptionsByType = _subscriptions.ToDictionary(
                        kvp => kvp.Key.Name,
                        kvp => kvp.Value.Count)
                };

                return stats;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all subscriptions.
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var subscriptionList in _subscriptions.Values)
                {
                    foreach (var subscription in subscriptionList)
                    {
                        subscription.Dispose();
                    }
                }

                _subscriptions.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Disposes of the subscription manager.
        /// </summary>
        public void Dispose()
        {
            Clear();
            _lock?.Dispose();
        }
    }

    /// <summary>
    /// Interface for message subscriptions.
    /// </summary>
    public interface IMessageSubscription : IDisposable
    {
        string Id { get; }
        bool IsActive { get; }
        SubscriptionOptions Options { get; }
        Task HandleAsync(object message, MessageContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Generic message subscription implementation.
    /// </summary>
    public class MessageSubscription<TMessage> : IMessageSubscription
        where TMessage : IMessage
    {
        private readonly Action<TMessage> _handler;
        private readonly Func<TMessage, bool>? _filter;
        private bool _isActive;

        public string Id { get; }
        public SubscriptionOptions Options { get; }
        public bool IsActive => _isActive;

        public Func<TMessage, bool>? Filter => _filter;

        public MessageSubscription(
            string id,
            Action<TMessage> handler,
            Func<TMessage, bool>? filter = null,
            SubscriptionOptions? options = null)
        {
            Id = id;
            _handler = handler;
            _filter = filter;
            Options = options ?? new SubscriptionOptions();
            _isActive = true;
        }

        public async Task HandleAsync(object message, MessageContext context, CancellationToken cancellationToken)
        {
            if (!_isActive)
            {
                return;
            }

            if (message is TMessage typedMessage)
            {
                // Apply filter if present
                if (_filter != null && !_filter(typedMessage))
                {
                    return;
                }

                // Handle based on execution mode
                if (Options.ExecutionMode == SubscriptionExecutionMode.Synchronous)
                {
                    _handler(typedMessage);
                }
                else
                {
                    await Task.Run(() => _handler(typedMessage), cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            _isActive = false;
        }
    }

    /// <summary>
    /// Options for message subscriptions.
    /// </summary>
    public class SubscriptionOptions
    {
        /// <summary>
        /// Gets or sets whether to ignore errors in the subscription handler.
        /// </summary>
        public bool IgnoreErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets the execution mode for the subscription.
        /// </summary>
        public SubscriptionExecutionMode ExecutionMode { get; set; } = SubscriptionExecutionMode.Asynchronous;

        /// <summary>
        /// Gets or sets the maximum number of concurrent handler executions.
        /// </summary>
        public int MaxConcurrency { get; set; } = 1;

        /// <summary>
        /// Gets or sets the timeout for handler execution.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the priority of the subscription.
        /// </summary>
        public Priority Priority { get; set; } = Priority.Normal;

        /// <summary>
        /// Gets or sets whether the subscription should be durable (survive restarts).
        /// </summary>
        public bool IsDurable { get; set; } = false;

        /// <summary>
        /// Gets or sets custom metadata for the subscription.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Execution mode for subscriptions.
    /// </summary>
    public enum SubscriptionExecutionMode
    {
        /// <summary>
        /// Execute handler synchronously.
        /// </summary>
        Synchronous,

        /// <summary>
        /// Execute handler asynchronously on a thread pool thread.
        /// </summary>
        Asynchronous
    }

    /// <summary>
    /// Priority levels for message processing.
    /// </summary>
    public enum Priority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    /// <summary>
    /// Statistics about subscriptions.
    /// </summary>
    public class SubscriptionStatistics
    {
        public int TotalSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int InactiveSubscriptions => TotalSubscriptions - ActiveSubscriptions;
        public int MessageTypesWithSubscriptions { get; set; }
        public Dictionary<string, int> SubscriptionsByType { get; set; } = new();
    }
}