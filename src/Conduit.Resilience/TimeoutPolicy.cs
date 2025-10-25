using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

namespace Conduit.Resilience
{
    /// <summary>
    /// Timeout policy implementation using Polly.
    /// Enforces timeout limits on operation execution.
    /// </summary>
    public class TimeoutPolicy : IResiliencePolicy
    {
        private readonly ResilienceConfiguration.TimeoutConfig _config;
        private readonly ILogger? _logger;
        private readonly AsyncTimeoutPolicy _policy;
        private readonly TimeoutMetrics _metrics;
        private readonly object _metricsLock = new();

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ResiliencePattern Pattern => ResiliencePattern.Timeout;

        /// <inheritdoc/>
        public bool IsEnabled => _config.Enabled;

        /// <summary>
        /// Gets the configured timeout duration.
        /// </summary>
        public TimeSpan Duration => _config.Duration;

        /// <summary>
        /// Gets the timeout strategy (Optimistic or Pessimistic).
        /// </summary>
        public TimeoutStrategy Strategy => _config.Strategy;

        /// <summary>
        /// Initializes a new instance of the TimeoutPolicy class.
        /// </summary>
        public TimeoutPolicy(
            string name,
            ResilienceConfiguration.TimeoutConfig config,
            ILogger<TimeoutPolicy>? logger = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(config, nameof(config));

            Name = name;
            _config = config;
            _logger = logger;
            _metrics = new TimeoutMetrics();

            var pollyStrategy = _config.Strategy == TimeoutStrategy.Optimistic
                ? Polly.Timeout.TimeoutStrategy.Optimistic
                : Polly.Timeout.TimeoutStrategy.Pessimistic;

            _policy = Policy
                .TimeoutAsync(
                    timeout: _config.Duration,
                    timeoutStrategy: pollyStrategy,
                    onTimeoutAsync: OnTimeout
                );

            _logger?.LogInformation("Timeout policy '{Name}' initialized with {Duration}ms {Strategy} timeout",
                name, _config.Duration.TotalMilliseconds, _config.Strategy);
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

            try
            {
                await _policy.ExecuteAsync(async ct =>
                {
                    await action(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds);
            }
            catch (TimeoutRejectedException ex)
            {
                RecordTimeout(sw.ElapsedMilliseconds);
                _logger?.LogWarning(ex, "Timeout policy '{Name}' timed out after {Duration}ms", Name, _config.Duration.TotalMilliseconds);
                throw new ResilienceException($"Operation timed out after {_config.Duration.TotalMilliseconds}ms in policy '{Name}'", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in timeout policy '{Name}'", Name);
                throw;
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

            try
            {
                var result = await _policy.ExecuteAsync(async ct =>
                {
                    return await func(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds);
                return result;
            }
            catch (TimeoutRejectedException ex)
            {
                RecordTimeout(sw.ElapsedMilliseconds);
                _logger?.LogWarning(ex, "Timeout policy '{Name}' timed out after {Duration}ms", Name, _config.Duration.TotalMilliseconds);
                throw new ResilienceException($"Operation timed out after {_config.Duration.TotalMilliseconds}ms in policy '{Name}'", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in timeout policy '{Name}'", Name);
                throw;
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
                    TimeoutExecutions = _metrics.TimeoutCalls,
                    AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                    AdditionalMetrics = new TimeoutMetrics
                    {
                        TotalCalls = _metrics.TotalCalls,
                        SuccessfulCalls = _metrics.SuccessfulCalls,
                        FailedCalls = _metrics.FailedCalls,
                        TimeoutCalls = _metrics.TimeoutCalls,
                        AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                        TimeoutThresholdMs = _config.Duration.TotalMilliseconds
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
            _logger?.LogInformation("Timeout policy '{Name}' metrics reset", Name);
        }

        private Task OnTimeout(Polly.Context context, TimeSpan timeout, Task task)
        {
            _logger?.LogWarning("Timeout policy '{Name}' triggered after {Timeout}ms", Name, timeout.TotalMilliseconds);
            return Task.CompletedTask;
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

        private void RecordTimeout(long executionTimeMs)
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.TimeoutCalls++;
                UpdateAverageExecutionTime(executionTimeMs);
            }
        }

        private void UpdateAverageExecutionTime(long executionTimeMs)
        {
            var totalTime = _metrics.AverageExecutionTimeMs * (_metrics.TotalCalls - 1);
            _metrics.AverageExecutionTimeMs = (totalTime + executionTimeMs) / _metrics.TotalCalls;
        }

        private class TimeoutMetrics
        {
            public long TotalCalls { get; set; }
            public long SuccessfulCalls { get; set; }
            public long FailedCalls { get; set; }
            public long TimeoutCalls { get; set; }
            public double AverageExecutionTimeMs { get; set; }
            public double TimeoutThresholdMs { get; set; }

            public void Reset()
            {
                TotalCalls = 0;
                SuccessfulCalls = 0;
                FailedCalls = 0;
                TimeoutCalls = 0;
                AverageExecutionTimeMs = 0;
            }
        }
    }
}
