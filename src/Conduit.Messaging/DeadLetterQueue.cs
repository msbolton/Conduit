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
    /// Manages failed messages that couldn't be processed successfully.
    /// </summary>
    public class DeadLetterQueue : IDisposable
    {
        private readonly ConcurrentQueue<DeadLetterEntry> _queue;
        private readonly ConcurrentDictionary<string, DeadLetterEntry> _index;
        private readonly int _maxCapacity;
        private readonly TimeSpan _retentionPeriod;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _semaphore;
        private long _totalEnqueued;
        private long _totalDequeued;
        private long _totalReprocessed;
        private long _totalExpired;
        private bool _disposed;

        /// <summary>
        /// Event raised when a message is added to the dead letter queue.
        /// </summary>
        public event EventHandler<DeadLetterEventArgs>? MessageAdded;

        /// <summary>
        /// Event raised when the queue reaches capacity.
        /// </summary>
        public event EventHandler<QueueCapacityEventArgs>? CapacityReached;

        /// <summary>
        /// Gets the current count of messages in the queue.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Gets whether the queue is at capacity.
        /// </summary>
        public bool IsAtCapacity => Count >= _maxCapacity;

        /// <summary>
        /// Gets the total number of messages enqueued.
        /// </summary>
        public long TotalEnqueued => Interlocked.Read(ref _totalEnqueued);

        /// <summary>
        /// Gets the total number of messages dequeued.
        /// </summary>
        public long TotalDequeued => Interlocked.Read(ref _totalDequeued);

        /// <summary>
        /// Gets the total number of messages reprocessed.
        /// </summary>
        public long TotalReprocessed => Interlocked.Read(ref _totalReprocessed);

        /// <summary>
        /// Gets the total number of messages expired.
        /// </summary>
        public long TotalExpired => Interlocked.Read(ref _totalExpired);

        /// <summary>
        /// Initializes a new instance of the DeadLetterQueue class.
        /// </summary>
        public DeadLetterQueue(
            int maxCapacity = 10000,
            TimeSpan? retentionPeriod = null)
        {
            Guard.AgainstNegativeOrZero(maxCapacity, nameof(maxCapacity));

            _maxCapacity = maxCapacity;
            _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
            _queue = new ConcurrentQueue<DeadLetterEntry>();
            _index = new ConcurrentDictionary<string, DeadLetterEntry>();
            _semaphore = new SemaphoreSlim(1, 1);

            // Start cleanup timer
            _cleanupTimer = new Timer(
                CleanupExpiredMessages,
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Adds a failed message to the dead letter queue.
        /// </summary>
        public async Task<DeadLetterEntry> AddAsync(
            IMessage message,
            Exception exception,
            MessageContext? context = null)
        {
            Guard.AgainstNull(message, nameof(message));
            Guard.AgainstNull(exception, nameof(exception));

            await _semaphore.WaitAsync();
            try
            {
                // Check capacity
                if (Count >= _maxCapacity)
                {
                    await HandleCapacityReachedAsync();
                }

                var entry = new DeadLetterEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Message = message,
                    MessageType = message.GetType().FullName ?? message.GetType().Name,
                    Exception = exception,
                    ErrorMessage = exception.Message,
                    StackTrace = exception.StackTrace,
                    Context = context,
                    EnqueuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(_retentionPeriod),
                    RetryCount = context?.RetryCount ?? 0,
                    CorrelationId = context?.CorrelationId,
                    Headers = new Dictionary<string, string>(message.Headers ?? new Dictionary<string, string>())
                };

                // Extract additional error details
                ExtractErrorDetails(entry, exception);

                _queue.Enqueue(entry);
                _index[entry.Id] = entry;
                Interlocked.Increment(ref _totalEnqueued);

                // Raise event
                MessageAdded?.Invoke(this, new DeadLetterEventArgs(entry));

                return entry;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Retrieves a message from the dead letter queue by ID.
        /// </summary>
        public DeadLetterEntry? GetById(string id)
        {
            Guard.AgainstNullOrEmpty(id, nameof(id));
            return _index.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// Retrieves messages from the dead letter queue.
        /// </summary>
        public IEnumerable<DeadLetterEntry> GetMessages(
            int? limit = null,
            Func<DeadLetterEntry, bool>? filter = null)
        {
            var messages = _queue.ToList();

            if (filter != null)
            {
                messages = messages.Where(filter).ToList();
            }

            if (limit.HasValue)
            {
                messages = messages.Take(limit.Value).ToList();
            }

            return messages;
        }

        /// <summary>
        /// Retrieves messages by correlation ID.
        /// </summary>
        public IEnumerable<DeadLetterEntry> GetByCorrelationId(string correlationId)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            return _queue.Where(e => e.CorrelationId == correlationId);
        }

        /// <summary>
        /// Retrieves messages by message type.
        /// </summary>
        public IEnumerable<DeadLetterEntry> GetByMessageType(string messageType)
        {
            Guard.AgainstNullOrEmpty(messageType, nameof(messageType));
            return _queue.Where(e => e.MessageType.Contains(messageType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves messages by error type.
        /// </summary>
        public IEnumerable<DeadLetterEntry> GetByErrorType(Type exceptionType)
        {
            Guard.AgainstNull(exceptionType, nameof(exceptionType));
            var typeName = exceptionType.FullName ?? exceptionType.Name;
            return _queue.Where(e => e.ExceptionType == typeName);
        }

        /// <summary>
        /// Attempts to reprocess a message.
        /// </summary>
        public async Task<ReprocessResult> ReprocessAsync(
            string id,
            Func<IMessage, Task> processor,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(id, nameof(id));
            Guard.AgainstNull(processor, nameof(processor));

            if (!_index.TryGetValue(id, out var entry))
            {
                return new ReprocessResult
                {
                    Success = false,
                    Error = "Message not found in dead letter queue"
                };
            }

            try
            {
                entry.ReprocessAttempts++;
                entry.LastReprocessedAt = DateTimeOffset.UtcNow;
                entry.State = DeadLetterState.Reprocessing;

                await processor(entry.Message);

                entry.State = DeadLetterState.Reprocessed;
                entry.ReprocessedAt = DateTimeOffset.UtcNow;
                Interlocked.Increment(ref _totalReprocessed);

                // Remove from queue after successful reprocessing
                await RemoveAsync(id);

                return new ReprocessResult
                {
                    Success = true,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                entry.State = DeadLetterState.Failed;
                entry.LastReprocessError = ex.Message;

                return new ReprocessResult
                {
                    Success = false,
                    Error = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Attempts to reprocess multiple messages.
        /// </summary>
        public async Task<BatchReprocessResult> ReprocessBatchAsync(
            IEnumerable<string> ids,
            Func<IMessage, Task> processor,
            int maxParallelism = 5,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(ids, nameof(ids));
            Guard.AgainstNull(processor, nameof(processor));

            var idList = ids.ToList();
            var result = new BatchReprocessResult
            {
                TotalMessages = idList.Count
            };

            var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            var tasks = idList.Select(async id =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var reprocessResult = await ReprocessAsync(id, processor, cancellationToken);
                    if (reprocessResult.Success)
                    {
                        result.SuccessfulCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add(new ReprocessError
                        {
                            MessageId = id,
                            Error = reprocessResult.Error
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return result;
        }

        /// <summary>
        /// Removes a message from the dead letter queue.
        /// </summary>
        public async Task<bool> RemoveAsync(string id)
        {
            Guard.AgainstNullOrEmpty(id, nameof(id));

            await _semaphore.WaitAsync();
            try
            {
                if (_index.TryRemove(id, out var entry))
                {
                    // Note: We can't efficiently remove from ConcurrentQueue,
                    // so we mark it as removed and it will be cleaned up later
                    entry.State = DeadLetterState.Removed;
                    Interlocked.Increment(ref _totalDequeued);
                    return true;
                }

                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Clears all messages from the dead letter queue.
        /// </summary>
        public async Task ClearAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                while (_queue.TryDequeue(out _))
                {
                    Interlocked.Increment(ref _totalDequeued);
                }

                _index.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets statistics about the dead letter queue.
        /// </summary>
        public DeadLetterStatistics GetStatistics()
        {
            var messages = _queue.ToList();
            var activeMessages = messages.Where(m => m.State != DeadLetterState.Removed).ToList();

            var stats = new DeadLetterStatistics
            {
                CurrentCount = activeMessages.Count,
                TotalEnqueued = TotalEnqueued,
                TotalDequeued = TotalDequeued,
                TotalReprocessed = TotalReprocessed,
                TotalExpired = TotalExpired,
                OldestMessageAge = activeMessages.Any()
                    ? DateTimeOffset.UtcNow - activeMessages.Min(m => m.EnqueuedAt)
                    : null,
                NewestMessageAge = activeMessages.Any()
                    ? DateTimeOffset.UtcNow - activeMessages.Max(m => m.EnqueuedAt)
                    : null
            };

            // Group by message type
            stats.MessageTypeBreakdown = activeMessages
                .GroupBy(m => m.MessageType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by exception type
            stats.ErrorTypeBreakdown = activeMessages
                .Where(m => !string.IsNullOrEmpty(m.ExceptionType))
                .GroupBy(m => m.ExceptionType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Calculate retry statistics
            if (activeMessages.Any())
            {
                stats.AverageRetryCount = activeMessages.Average(m => m.RetryCount);
                stats.MaxRetryCount = activeMessages.Max(m => m.RetryCount);
            }

            return stats;
        }

        private void ExtractErrorDetails(DeadLetterEntry entry, Exception exception)
        {
            entry.ExceptionType = exception.GetType().FullName ?? exception.GetType().Name;

            // Extract inner exception details
            if (exception.InnerException != null)
            {
                entry.InnerExceptionMessage = exception.InnerException.Message;
                entry.InnerExceptionType = exception.InnerException.GetType().FullName;
            }

            // Extract aggregate exception details
            if (exception is AggregateException aggregateException)
            {
                entry.AggregateErrors = aggregateException.InnerExceptions
                    .Select(e => new ErrorDetail
                    {
                        Message = e.Message,
                        Type = e.GetType().FullName ?? e.GetType().Name,
                        StackTrace = e.StackTrace
                    })
                    .ToList();
            }
        }

        private async Task HandleCapacityReachedAsync()
        {
            // Remove oldest messages (FIFO)
            var toRemove = Count - (_maxCapacity * 9 / 10); // Remove 10% when at capacity

            for (int i = 0; i < toRemove; i++)
            {
                if (_queue.TryDequeue(out var entry))
                {
                    _index.TryRemove(entry.Id, out _);
                    Interlocked.Increment(ref _totalDequeued);
                }
            }

            // Raise event
            CapacityReached?.Invoke(this, new QueueCapacityEventArgs(_maxCapacity, toRemove));

            await Task.CompletedTask;
        }

        private void CleanupExpiredMessages(object? state)
        {
            var now = DateTimeOffset.UtcNow;
            var expiredIds = _index.Values
                .Where(e => e.ExpiresAt < now || e.State == DeadLetterState.Removed)
                .Select(e => e.Id)
                .ToList();

            foreach (var id in expiredIds)
            {
                if (_index.TryRemove(id, out var entry))
                {
                    if (entry.ExpiresAt < now)
                    {
                        Interlocked.Increment(ref _totalExpired);
                    }
                }
            }

            // Rebuild queue without expired/removed entries
            if (expiredIds.Any())
            {
                var activeEntries = _queue
                    .Where(e => !expiredIds.Contains(e.Id) && e.State != DeadLetterState.Removed)
                    .ToList();

                while (_queue.TryDequeue(out _)) { }

                foreach (var entry in activeEntries)
                {
                    _queue.Enqueue(entry);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents an entry in the dead letter queue.
    /// </summary>
    public class DeadLetterEntry
    {
        public string Id { get; set; } = "";
        public IMessage Message { get; set; } = null!;
        public string MessageType { get; set; } = "";
        public Exception Exception { get; set; } = null!;
        public string ErrorMessage { get; set; } = "";
        public string? StackTrace { get; set; }
        public string ExceptionType { get; set; } = "";
        public string? InnerExceptionMessage { get; set; }
        public string? InnerExceptionType { get; set; }
        public List<ErrorDetail>? AggregateErrors { get; set; }
        public MessageContext? Context { get; set; }
        public DateTimeOffset EnqueuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ReprocessedAt { get; set; }
        public DateTimeOffset? LastReprocessedAt { get; set; }
        public int RetryCount { get; set; }
        public int ReprocessAttempts { get; set; }
        public string? LastReprocessError { get; set; }
        public string? CorrelationId { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public DeadLetterState State { get; set; } = DeadLetterState.Active;
    }

    /// <summary>
    /// State of a dead letter entry.
    /// </summary>
    public enum DeadLetterState
    {
        Active,
        Reprocessing,
        Reprocessed,
        Failed,
        Expired,
        Removed
    }

    /// <summary>
    /// Represents error details.
    /// </summary>
    public class ErrorDetail
    {
        public string Message { get; set; } = "";
        public string Type { get; set; } = "";
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Result of a reprocess operation.
    /// </summary>
    public class ReprocessResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public Exception? Exception { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }
    }

    /// <summary>
    /// Result of a batch reprocess operation.
    /// </summary>
    public class BatchReprocessResult
    {
        public int TotalMessages { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
        public List<ReprocessError> Errors { get; } = new();
        public double SuccessRate => TotalMessages > 0 ? (double)SuccessfulCount / TotalMessages : 0;
    }

    /// <summary>
    /// Represents a reprocess error.
    /// </summary>
    public class ReprocessError
    {
        public string MessageId { get; set; } = "";
        public string? Error { get; set; }
    }

    /// <summary>
    /// Statistics about the dead letter queue.
    /// </summary>
    public class DeadLetterStatistics
    {
        public int CurrentCount { get; set; }
        public long TotalEnqueued { get; set; }
        public long TotalDequeued { get; set; }
        public long TotalReprocessed { get; set; }
        public long TotalExpired { get; set; }
        public TimeSpan? OldestMessageAge { get; set; }
        public TimeSpan? NewestMessageAge { get; set; }
        public double? AverageRetryCount { get; set; }
        public int? MaxRetryCount { get; set; }
        public Dictionary<string, int> MessageTypeBreakdown { get; set; } = new();
        public Dictionary<string, int> ErrorTypeBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for dead letter events.
    /// </summary>
    public class DeadLetterEventArgs : EventArgs
    {
        public DeadLetterEntry Entry { get; }

        public DeadLetterEventArgs(DeadLetterEntry entry)
        {
            Entry = entry;
        }
    }

    /// <summary>
    /// Event arguments for queue capacity events.
    /// </summary>
    public class QueueCapacityEventArgs : EventArgs
    {
        public int MaxCapacity { get; }
        public int MessagesRemoved { get; }

        public QueueCapacityEventArgs(int maxCapacity, int messagesRemoved)
        {
            MaxCapacity = maxCapacity;
            MessagesRemoved = messagesRemoved;
        }
    }
}