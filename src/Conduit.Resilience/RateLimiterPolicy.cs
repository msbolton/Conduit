using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience
{
    /// <summary>
    /// Rate limiter policy implementation using System.Threading.RateLimiting.
    /// Controls the rate of executions using a sliding window rate limiter.
    /// </summary>
    public class RateLimiterPolicy : IResiliencePolicy, IDisposable
    {
        private readonly ResilienceConfiguration.RateLimiterConfig _config;
        private readonly ILogger? _logger;
        private readonly SlidingWindowRateLimiter _rateLimiter;
        private readonly RateLimiterMetrics _metrics;
        private readonly object _metricsLock = new();
        private bool _disposed;

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ResiliencePattern Pattern => ResiliencePattern.RateLimiter;

        /// <inheritdoc/>
        public bool IsEnabled => _config.Enabled;

        /// <summary>
        /// Gets the maximum number of permits.
        /// </summary>
        public int MaxPermits => _config.MaxPermits;

        /// <summary>
        /// Gets the time window for rate limiting.
        /// </summary>
        public TimeSpan Window => _config.Window;

        /// <summary>
        /// Initializes a new instance of the RateLimiterPolicy class.
        /// </summary>
        public RateLimiterPolicy(
            string name,
            ResilienceConfiguration.RateLimiterConfig config,
            ILogger<RateLimiterPolicy>? logger = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(config, nameof(config));

            Name = name;
            _config = config;
            _logger = logger;
            _metrics = new RateLimiterMetrics();

            _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _config.MaxPermits,
                Window = _config.Window,
                SegmentsPerWindow = 10, // Divide window into 10 segments for smoother sliding
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = _config.QueueLimit
            });

            _logger?.LogInformation(
                "Rate limiter policy '{Name}' initialized with {MaxPermits} permits per {Window}ms window",
                name, _config.MaxPermits, _config.Window.TotalMilliseconds);
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
            RateLimitLease? lease = null;

            try
            {
                lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

                if (!lease.IsAcquired)
                {
                    RecordRejection();
                    _logger?.LogWarning("Rate limiter '{Name}' rejected call - permit limit ({MaxPermits}) reached", Name, _config.MaxPermits);
                    throw new ResilienceException($"Rate limiter '{Name}' rejected call - too many requests");
                }

                await action(cancellationToken);
                RecordSuccess(sw.ElapsedMilliseconds);
            }
            catch (ResilienceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in rate limiter '{Name}'", Name);
                throw;
            }
            finally
            {
                lease?.Dispose();
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
            RateLimitLease? lease = null;

            try
            {
                lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

                if (!lease.IsAcquired)
                {
                    RecordRejection();
                    _logger?.LogWarning("Rate limiter '{Name}' rejected call - permit limit ({MaxPermits}) reached", Name, _config.MaxPermits);
                    throw new ResilienceException($"Rate limiter '{Name}' rejected call - too many requests");
                }

                var result = await func(cancellationToken);
                RecordSuccess(sw.ElapsedMilliseconds);
                return result;
            }
            catch (ResilienceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in rate limiter '{Name}'", Name);
                throw;
            }
            finally
            {
                lease?.Dispose();
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
                    AdditionalMetrics = new RateLimiterMetrics
                    {
                        TotalCalls = _metrics.TotalCalls,
                        SuccessfulCalls = _metrics.SuccessfulCalls,
                        FailedCalls = _metrics.FailedCalls,
                        RejectedCalls = _metrics.RejectedCalls,
                        AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                        PermitLimit = _config.MaxPermits,
                        WindowMs = _config.Window.TotalMilliseconds
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
            _logger?.LogInformation("Rate limiter policy '{Name}' metrics reset", Name);
        }

        private void RecordSuccess(long executionTimeMs)
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

        /// <summary>
        /// Releases all resources used by the RateLimiterPolicy.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _rateLimiter?.Dispose();
                _disposed = true;
            }
        }

        private class RateLimiterMetrics
        {
            public long TotalCalls { get; set; }
            public long SuccessfulCalls { get; set; }
            public long FailedCalls { get; set; }
            public long RejectedCalls { get; set; }
            public double AverageExecutionTimeMs { get; set; }
            public int PermitLimit { get; set; }
            public double WindowMs { get; set; }

            public void Reset()
            {
                TotalCalls = 0;
                SuccessfulCalls = 0;
                FailedCalls = 0;
                RejectedCalls = 0;
                AverageExecutionTimeMs = 0;
            }
        }
    }
}
