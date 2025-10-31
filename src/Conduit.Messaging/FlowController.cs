using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Conduit.Common;

namespace Conduit.Messaging
{
    /// <summary>
    /// Controls message flow and provides backpressure management.
    /// </summary>
    public class FlowController : IFlowController
    {
        private readonly int _maxConcurrentMessages;
        private readonly int _maxQueueSize;
        private readonly int _rateLimit;
        private readonly TimeSpan _rateLimitWindow;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly Channel<FlowControlTask> _taskChannel;
        private readonly ConcurrentDictionary<string, FlowMetrics> _metrics;
        private readonly Timer _metricsResetTimer;
        private readonly RateLimiter _rateLimiter;
        private long _totalProcessed;
        private long _totalRejected;
        private long _totalThrottled;
        private long _currentLoad;
        private bool _disposed;

        /// <summary>
        /// Event raised when backpressure is applied.
        /// </summary>
        public event EventHandler<BackpressureEventArgs>? BackpressureApplied;

        /// <summary>
        /// Event raised when flow is restored.
        /// </summary>
        public event EventHandler? FlowRestored;

        /// <summary>
        /// Gets whether the controller is healthy.
        /// </summary>
        public bool IsHealthy => _currentLoad < (_maxConcurrentMessages * 0.9);

        /// <summary>
        /// Gets the current queue depth.
        /// </summary>
        public int QueueDepth => _taskChannel.Reader.Count;

        /// <summary>
        /// Gets the current load percentage.
        /// </summary>
        public double LoadPercentage => (double)_currentLoad / _maxConcurrentMessages * 100;

        /// <summary>
        /// Gets whether backpressure is currently active.
        /// </summary>
        public bool IsBackpressureActive => _currentLoad >= _maxConcurrentMessages;

        /// <summary>
        /// Initializes a new instance of the FlowController class.
        /// </summary>
        public FlowController(
            int maxConcurrentMessages = 100,
            int rateLimit = 1000,
            int maxQueueSize = 10000,
            TimeSpan? rateLimitWindow = null)
        {
            Guard.AgainstNegativeOrZero(maxConcurrentMessages, nameof(maxConcurrentMessages));
            Guard.AgainstNegativeOrZero(rateLimit, nameof(rateLimit));
            Guard.AgainstNegativeOrZero(maxQueueSize, nameof(maxQueueSize));

            _maxConcurrentMessages = maxConcurrentMessages;
            _rateLimit = rateLimit;
            _maxQueueSize = maxQueueSize;
            _rateLimitWindow = rateLimitWindow ?? TimeSpan.FromSeconds(1);
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrentMessages, maxConcurrentMessages);
            _taskChannel = Channel.CreateBounded<FlowControlTask>(new BoundedChannelOptions(maxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
            _metrics = new ConcurrentDictionary<string, FlowMetrics>();
            _rateLimiter = new RateLimiter(rateLimit, _rateLimitWindow);

            // Start metrics reset timer
            _metricsResetTimer = new Timer(
                ResetMetrics,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));

            // Start task processor
            Task.Run(ProcessTasksAsync);
        }

        /// <summary>
        /// Executes an operation with flow control.
        /// </summary>
        public async Task<T> ExecuteWithFlowControlAsync<T>(
            Func<Task<T>> operation,
            Priority priority = Priority.Normal,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(operation, nameof(operation));

            // Check if we should reject immediately due to overload
            if (ShouldRejectDueToOverload())
            {
                Interlocked.Increment(ref _totalRejected);
                throw new FlowControlException("System overloaded, rejecting new messages");
            }

            // Apply rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);

            var tcs = new TaskCompletionSource<T>();
            var task = new FlowControlTask
            {
                Id = Guid.NewGuid().ToString(),
                Priority = priority,
                EnqueuedAt = DateTimeOffset.UtcNow,
                Operation = async () =>
                {
                    try
                    {
                        var result = await operation();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                },
                CancellationToken = cancellationToken
            };

            // Try to enqueue the task
            if (!_taskChannel.Writer.TryWrite(task))
            {
                // Apply backpressure
                await ApplyBackpressureAsync(task, cancellationToken);
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Executes an operation with flow control (void return).
        /// </summary>
        public async Task ExecuteWithFlowControlAsync(
            Func<Task> operation,
            Priority priority = Priority.Normal,
            CancellationToken cancellationToken = default)
        {
            await ExecuteWithFlowControlAsync(async () =>
            {
                await operation();
                return true;
            }, priority, cancellationToken);
        }

        /// <summary>
        /// Applies throttling to the message flow.
        /// </summary>
        public async Task ThrottleAsync(TimeSpan delay)
        {
            Interlocked.Increment(ref _totalThrottled);
            await Task.Delay(delay);
        }

        /// <summary>
        /// Gets flow control statistics.
        /// </summary>
        public FlowControlStatistics GetStatistics()
        {
            return new FlowControlStatistics
            {
                CurrentLoad = Interlocked.Read(ref _currentLoad),
                MaxConcurrentMessages = _maxConcurrentMessages,
                LoadPercentage = LoadPercentage,
                QueueDepth = QueueDepth,
                MaxQueueSize = _maxQueueSize,
                TotalProcessed = Interlocked.Read(ref _totalProcessed),
                TotalRejected = Interlocked.Read(ref _totalRejected),
                TotalThrottled = Interlocked.Read(ref _totalThrottled),
                IsBackpressureActive = IsBackpressureActive,
                IsHealthy = IsHealthy,
                RateLimit = _rateLimit,
                MetricsByPriority = GetMetricsByPriority()
            };
        }

        /// <summary>
        /// Adjusts the rate limit dynamically.
        /// </summary>
        public void AdjustRateLimit(int newRateLimit)
        {
            Guard.AgainstNegativeOrZero(newRateLimit, nameof(newRateLimit));
            _rateLimiter.UpdateLimit(newRateLimit);
        }

        /// <summary>
        /// Pauses message processing.
        /// </summary>
        public async Task PauseAsync()
        {
            await _concurrencySemaphore.WaitAsync();
        }

        /// <summary>
        /// Resumes message processing.
        /// </summary>
        public void Resume()
        {
            _concurrencySemaphore.Release();
            FlowRestored?.Invoke(this, EventArgs.Empty);
        }

        private async Task ProcessTasksAsync()
        {
            await foreach (var task in _taskChannel.Reader.ReadAllAsync())
            {
                if (task.CancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                await _concurrencySemaphore.WaitAsync(task.CancellationToken);
                Interlocked.Increment(ref _currentLoad);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Update metrics
                        UpdateMetrics(task);

                        // Execute the operation
                        await task.Operation();

                        Interlocked.Increment(ref _totalProcessed);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _currentLoad);
                        _concurrencySemaphore.Release();

                        // Check if we should raise flow restored event
                        if (_currentLoad < (_maxConcurrentMessages * 0.7))
                        {
                            FlowRestored?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }, task.CancellationToken);
            }
        }

        private bool ShouldRejectDueToOverload()
        {
            // Reject if queue is full and load is high
            return QueueDepth >= _maxQueueSize * 0.95 && LoadPercentage > 95;
        }

        private async Task ApplyBackpressureAsync(FlowControlTask task, CancellationToken cancellationToken)
        {
            // Raise backpressure event
            var args = new BackpressureEventArgs
            {
                CurrentLoad = _currentLoad,
                MaxLoad = _maxConcurrentMessages,
                QueueDepth = QueueDepth,
                Priority = task.Priority
            };
            BackpressureApplied?.Invoke(this, args);

            // Apply different strategies based on priority
            switch (task.Priority)
            {
                case Priority.High:
                    // High priority messages wait longer
                    await _taskChannel.Writer.WriteAsync(task, cancellationToken);
                    break;

                case Priority.Normal:
                    // Normal priority messages wait with timeout
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(30));
                        try
                        {
                            await _taskChannel.Writer.WriteAsync(task, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Increment(ref _totalRejected);
                            throw new FlowControlException("Backpressure timeout exceeded");
                        }
                    }
                    break;

                case Priority.Low:
                    // Low priority messages are rejected immediately under backpressure
                    Interlocked.Increment(ref _totalRejected);
                    throw new FlowControlException("Low priority message rejected due to backpressure");
            }
        }

        private void UpdateMetrics(FlowControlTask task)
        {
            var key = task.Priority.ToString();
            var metrics = _metrics.GetOrAdd(key, _ => new FlowMetrics());

            Interlocked.Increment(ref metrics.Count);

            var waitTime = DateTimeOffset.UtcNow - task.EnqueuedAt;
            lock (metrics.WaitTimesLock)
            {
                metrics.WaitTimes.Add(waitTime);
                if (metrics.WaitTimes.Count > 1000)
                {
                    metrics.WaitTimes.RemoveAt(0);
                }
            }
        }

        private Dictionary<string, FlowMetricsSummary> GetMetricsByPriority()
        {
            var result = new Dictionary<string, FlowMetricsSummary>();

            foreach (var kvp in _metrics)
            {
                var metrics = kvp.Value;
                var summary = new FlowMetricsSummary
                {
                    Count = Interlocked.Read(ref metrics.Count)
                };

                lock (metrics.WaitTimesLock)
                {
                    if (metrics.WaitTimes.Any())
                    {
                        summary.AverageWaitTimeMs = metrics.WaitTimes.Average(t => t.TotalMilliseconds);
                        summary.MinWaitTimeMs = metrics.WaitTimes.Min(t => t.TotalMilliseconds);
                        summary.MaxWaitTimeMs = metrics.WaitTimes.Max(t => t.TotalMilliseconds);
                    }
                }

                result[kvp.Key] = summary;
            }

            return result;
        }

        private void ResetMetrics(object? state)
        {
            // Reset per-priority metrics periodically
            foreach (var metrics in _metrics.Values)
            {
                Interlocked.Exchange(ref metrics.Count, 0);
                lock (metrics.WaitTimesLock)
                {
                    metrics.WaitTimes.Clear();
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _taskChannel.Writer.TryComplete();
                _concurrencySemaphore?.Dispose();
                _metricsResetTimer?.Dispose();
                _disposed = true;
            }
        }

        private class FlowControlTask
        {
            public string Id { get; set; } = "";
            public Priority Priority { get; set; }
            public Func<Task> Operation { get; set; } = null!;
            public DateTimeOffset EnqueuedAt { get; set; }
            public CancellationToken CancellationToken { get; set; }
        }

        private class FlowMetrics
        {
            public long Count;
            public List<TimeSpan> WaitTimes { get; } = new();
            public object WaitTimesLock { get; } = new();
        }
    }

    /// <summary>
    /// Simple rate limiter implementation.
    /// </summary>
    public class RateLimiter
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _resetTimer;
        private int _limit;
        private int _currentCount;

        public RateLimiter(int limit, TimeSpan window)
        {
            _limit = limit;
            _semaphore = new SemaphoreSlim(limit, limit);
            _resetTimer = new Timer(Reset, null, window, window);
        }

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _currentCount);
        }

        public void UpdateLimit(int newLimit)
        {
            _limit = newLimit;
        }

        private void Reset(object? state)
        {
            var toRelease = _limit - _semaphore.CurrentCount;
            if (toRelease > 0)
            {
                _semaphore.Release(Math.Min(toRelease, _limit));
            }
            Interlocked.Exchange(ref _currentCount, 0);
        }
    }

    /// <summary>
    /// Statistics about flow control.
    /// </summary>
    public class FlowControlStatistics
    {
        public long CurrentLoad { get; set; }
        public int MaxConcurrentMessages { get; set; }
        public double LoadPercentage { get; set; }
        public int QueueDepth { get; set; }
        public int MaxQueueSize { get; set; }
        public long TotalProcessed { get; set; }
        public long TotalRejected { get; set; }
        public long TotalThrottled { get; set; }
        public bool IsBackpressureActive { get; set; }
        public bool IsHealthy { get; set; }
        public int RateLimit { get; set; }
        public Dictionary<string, FlowMetricsSummary> MetricsByPriority { get; set; } = new();
    }

    /// <summary>
    /// Summary of flow metrics.
    /// </summary>
    public class FlowMetricsSummary
    {
        public long Count { get; set; }
        public double AverageWaitTimeMs { get; set; }
        public double MinWaitTimeMs { get; set; }
        public double MaxWaitTimeMs { get; set; }
    }

    /// <summary>
    /// Event arguments for backpressure events.
    /// </summary>
    public class BackpressureEventArgs : EventArgs
    {
        public long CurrentLoad { get; set; }
        public int MaxLoad { get; set; }
        public int QueueDepth { get; set; }
        public Priority Priority { get; set; }
        public double LoadPercentage => (double)CurrentLoad / MaxLoad * 100;
    }

    /// <summary>
    /// Exception thrown when flow control limits are exceeded.
    /// </summary>
    public class FlowControlException : Exception
    {
        public FlowControlException(string message) : base(message) { }
        public FlowControlException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}