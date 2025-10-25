using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;

namespace Conduit.Resilience
{
    /// <summary>
    /// Bulkhead policy implementation using Polly.
    /// Limits concurrent executions to prevent resource exhaustion.
    /// </summary>
    public class BulkheadPolicy : IResiliencePolicy
    {
        private readonly ResilienceConfiguration.BulkheadConfig _config;
        private readonly ILogger? _logger;
        private readonly AsyncBulkheadPolicy _policy;
        private readonly BulkheadMetrics _metrics;
        private readonly object _metricsLock = new();

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ResiliencePattern Pattern => ResiliencePattern.Bulkhead;

        /// <inheritdoc/>
        public bool IsEnabled => _config.Enabled;

        /// <summary>
        /// Gets the number of currently executing actions.
        /// </summary>
        public int CurrentExecutions => _policy.BulkheadAvailableCount;

        /// <summary>
        /// Gets the number of queued actions.
        /// </summary>
        public int QueuedActions => _policy.QueueAvailableCount;

        /// <summary>
        /// Initializes a new instance of the BulkheadPolicy class.
        /// </summary>
        public BulkheadPolicy(
            string name,
            ResilienceConfiguration.BulkheadConfig config,
            ILogger<BulkheadPolicy>? logger = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(config, nameof(config));

            Name = name;
            _config = config;
            _logger = logger;
            _metrics = new BulkheadMetrics();

            _policy = Policy
                .BulkheadAsync(
                    maxParallelization: _config.MaxConcurrentCalls,
                    maxQueuingActions: _config.MaxQueuedCalls,
                    onBulkheadRejectedAsync: OnBulkheadRejected
                );

            _logger?.LogInformation(
                "Bulkhead policy '{Name}' initialized with {MaxConcurrent} max concurrent calls and {MaxQueued} max queued calls",
                name, _config.MaxConcurrentCalls, _config.MaxQueuedCalls);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(action, nameof(action));

            if (!IsEnabled)
            {
                await action(cancellationToken);
                return;
            }

            var sw = Stopwatch.StartNew();
            var wasQueued = false;

            try
            {
                // Check if this call will be queued
                if (_policy.BulkheadAvailableCount == 0)
                {
                    wasQueued = true;
                    RecordQueued();
                }

                await _policy.ExecuteAsync(async ct =>
                {
                    RecordExecutionStarted();
                    await action(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds, wasQueued);
            }
            catch (BulkheadRejectedException ex)
            {
                RecordRejection();
                _logger?.LogWarning(ex, "Bulkhead '{Name}' rejected call - max concurrent ({MaxConcurrent}) and queue ({MaxQueued}) limits reached",
                    Name, _config.MaxConcurrentCalls, _config.MaxQueuedCalls);
                throw new ResilienceException($"Bulkhead '{Name}' is at capacity", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in bulkhead '{Name}'", Name);
                throw;
            }
            finally
            {
                RecordExecutionCompleted();
            }
        }

        /// <inheritdoc/>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> func,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(func, nameof(func));

            if (!IsEnabled)
            {
                return await func(cancellationToken);
            }

            var sw = Stopwatch.StartNew();
            var wasQueued = false;

            try
            {
                // Check if this call will be queued
                if (_policy.BulkheadAvailableCount == 0)
                {
                    wasQueued = true;
                    RecordQueued();
                }

                var result = await _policy.ExecuteAsync(async ct =>
                {
                    RecordExecutionStarted();
                    return await func(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds, wasQueued);
                return result;
            }
            catch (BulkheadRejectedException ex)
            {
                RecordRejection();
                _logger?.LogWarning(ex, "Bulkhead '{Name}' rejected call - max concurrent ({MaxConcurrent}) and queue ({MaxQueued}) limits reached",
                    Name, _config.MaxConcurrentCalls, _config.MaxQueuedCalls);
                throw new ResilienceException($"Bulkhead '{Name}' is at capacity", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in bulkhead '{Name}'", Name);
                throw;
            }
            finally
            {
                RecordExecutionCompleted();
            }
        }

        /// <inheritdoc/>
        public PolicyMetrics GetMetrics()
        {
            lock (_metricsLock)
            {
                return new PolicyMetrics
                {
                    Name = Name,
                    Pattern = Pattern,
                    TotalExecutions = _metrics.TotalCalls,
                    SuccessfulExecutions = _metrics.SuccessfulCalls,
                    FailedExecutions = _metrics.FailedCalls,
                    RejectedExecutions = _metrics.RejectedCalls,
                    AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                    AdditionalMetrics = new BulkheadMetrics
                    {
                        TotalCalls = _metrics.TotalCalls,
                        SuccessfulCalls = _metrics.SuccessfulCalls,
                        FailedCalls = _metrics.FailedCalls,
                        RejectedCalls = _metrics.RejectedCalls,
                        QueuedCalls = _metrics.QueuedCalls,
                        CurrentExecutions = CurrentExecutions,
                        CurrentQueued = QueuedActions,
                        MaxConcurrentExecutions = _metrics.MaxConcurrentExecutions,
                        AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs
                    }
                };
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            lock (_metricsLock)
            {
                _metrics.Reset();
            }
            _logger?.LogInformation("Bulkhead policy '{Name}' metrics reset", Name);
        }

        private Task OnBulkheadRejected(Polly.Context context)
        {
            _logger?.LogWarning("Bulkhead '{Name}' rejected a call due to capacity limits", Name);
            return Task.CompletedTask;
        }

        private void RecordQueued()
        {
            lock (_metricsLock)
            {
                _metrics.QueuedCalls++;
            }
        }

        private void RecordExecutionStarted()
        {
            lock (_metricsLock)
            {
                _metrics.CurrentExecutingCount++;
                if (_metrics.CurrentExecutingCount > _metrics.MaxConcurrentExecutions)
                {
                    _metrics.MaxConcurrentExecutions = _metrics.CurrentExecutingCount;
                }
            }
        }

        private void RecordExecutionCompleted()
        {
            lock (_metricsLock)
            {
                _metrics.CurrentExecutingCount--;
            }
        }

        private void RecordSuccess(long executionTimeMs, bool wasQueued)
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.SuccessfulCalls++;
                UpdateAverageExecutionTime(executionTimeMs);
            }
        }

        private void RecordFailure(long executionTimeMs)
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.FailedCalls++;
                UpdateAverageExecutionTime(executionTimeMs);
            }
        }

        private void RecordRejection()
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.RejectedCalls++;
            }
        }

        private void UpdateAverageExecutionTime(long executionTimeMs)
        {
            var totalTime = _metrics.AverageExecutionTimeMs * (_metrics.TotalCalls - 1);
            _metrics.AverageExecutionTimeMs = (totalTime + executionTimeMs) / _metrics.TotalCalls;
        }

        private class BulkheadMetrics
        {
            public long TotalCalls { get; set; }
            public long SuccessfulCalls { get; set; }
            public long FailedCalls { get; set; }
            public long RejectedCalls { get; set; }
            public long QueuedCalls { get; set; }
            public int CurrentExecutions { get; set; }
            public int CurrentQueued { get; set; }
            public int MaxConcurrentExecutions { get; set; }
            public int CurrentExecutingCount { get; set; }
            public double AverageExecutionTimeMs { get; set; }

            public void Reset()
            {
                TotalCalls = 0;
                SuccessfulCalls = 0;
                FailedCalls = 0;
                RejectedCalls = 0;
                QueuedCalls = 0;
                MaxConcurrentExecutions = 0;
                CurrentExecutingCount = 0;
                AverageExecutionTimeMs = 0;
            }
        }
    }
}
