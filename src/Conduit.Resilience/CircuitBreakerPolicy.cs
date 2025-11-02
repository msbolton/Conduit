using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Conduit.Resilience
{
    /// <summary>
    /// Circuit breaker policy implementation using Polly.
    /// Implements the circuit breaker pattern to prevent cascading failures.
    /// </summary>
    public class CircuitBreakerPolicy : IResiliencePolicy
    {
        private readonly ResilienceConfiguration.CircuitBreakerConfig _config;
        private readonly ILogger? _logger;
        private readonly AsyncCircuitBreakerPolicy _policy;
        private readonly CircuitBreakerMetrics _metrics;
        private readonly object _metricsLock = new();

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ResiliencePattern Pattern => ResiliencePattern.CircuitBreaker;

        /// <inheritdoc/>
        public bool IsEnabled => _config.Enabled;

        /// <summary>
        /// Gets the current circuit breaker state.
        /// </summary>
        public CircuitBreakerState State => MapPollyState(_policy.CircuitState);

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerPolicy class.
        /// </summary>
        public CircuitBreakerPolicy(
            string name,
            ResilienceConfiguration.CircuitBreakerConfig config,
            ILogger<CircuitBreakerPolicy>? logger = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(config, nameof(config));

            Name = name;
            _config = config;
            _logger = logger;
            _metrics = new CircuitBreakerMetrics();

            _policy = Policy
                .Handle<Exception>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: _config.FailureRateThreshold,
                    samplingDuration: _config.WaitDurationInOpenState,
                    minimumThroughput: _config.MinimumThroughput,
                    durationOfBreak: _config.WaitDurationInOpenState,
                    onBreak: OnCircuitBreak,
                    onReset: OnCircuitReset,
                    onHalfOpen: OnCircuitHalfOpen
                );

            _logger?.LogInformation("Circuit breaker '{Name}' initialized with failure threshold {Threshold}",
                name, _config.FailureRateThreshold);
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
            catch (BrokenCircuitException ex)
            {
                RecordRejection();
                _logger?.LogWarning(ex, "Circuit breaker '{Name}' is open, call rejected", Name);
                throw new ResilienceException($"Circuit breaker '{Name}' is open", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in circuit breaker '{Name}'", Name);
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
            catch (BrokenCircuitException ex)
            {
                RecordRejection();
                _logger?.LogWarning(ex, "Circuit breaker '{Name}' is open, call rejected", Name);
                throw new ResilienceException($"Circuit breaker '{Name}' is open", ex);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds);
                _logger?.LogError(ex, "Execution failed in circuit breaker '{Name}'", Name);
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
                    RejectedExecutions = _metrics.RejectedCalls,
                    AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                    AdditionalMetrics = new CircuitBreakerMetrics
                    {
                        State = State,
                        TotalCalls = _metrics.TotalCalls,
                        SuccessfulCalls = _metrics.SuccessfulCalls,
                        FailedCalls = _metrics.FailedCalls,
                        RejectedCalls = _metrics.RejectedCalls,
                        CircuitOpenedCount = _metrics.CircuitOpenedCount,
                        CircuitClosedCount = _metrics.CircuitClosedCount,
                        AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs
                    }
                };
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _policy.Reset();
            lock (_metricsLock)
            {
                _metrics.Reset();
            }
            _logger?.LogInformation("Circuit breaker '{Name}' reset", Name);
        }

        /// <summary>
        /// Manually opens the circuit breaker.
        /// </summary>
        public void Isolate()
        {
            _policy.Isolate();
            _logger?.LogWarning("Circuit breaker '{Name}' manually isolated", Name);
        }

        private void OnCircuitBreak(Exception exception, TimeSpan duration)
        {
            lock (_metricsLock)
            {
                _metrics.CircuitOpenedCount++;
            }

            _logger?.LogWarning(exception,
                "Circuit breaker '{Name}' opened for {Duration}s due to failures",
                Name, duration.TotalSeconds);
        }

        private void OnCircuitReset()
        {
            lock (_metricsLock)
            {
                _metrics.CircuitClosedCount++;
            }

            _logger?.LogInformation("Circuit breaker '{Name}' reset to closed state", Name);
        }

        private void OnCircuitHalfOpen()
        {
            _logger?.LogInformation("Circuit breaker '{Name}' entered half-open state", Name);
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

        private static CircuitBreakerState MapPollyState(CircuitState pollyState)
        {
            return pollyState switch
            {
                CircuitState.Closed => CircuitBreakerState.Closed,
                CircuitState.Open => CircuitBreakerState.Open,
                CircuitState.HalfOpen => CircuitBreakerState.HalfOpen,
                CircuitState.Isolated => CircuitBreakerState.Isolated,
                _ => CircuitBreakerState.Closed
            };
        }

        private class CircuitBreakerMetrics
        {
            public CircuitBreakerState State { get; set; }
            public long TotalCalls { get; set; }
            public long SuccessfulCalls { get; set; }
            public long FailedCalls { get; set; }
            public long RejectedCalls { get; set; }
            public long CircuitOpenedCount { get; set; }
            public long CircuitClosedCount { get; set; }
            public double AverageExecutionTimeMs { get; set; }

            public void Reset()
            {
                TotalCalls = 0;
                SuccessfulCalls = 0;
                FailedCalls = 0;
                RejectedCalls = 0;
                CircuitOpenedCount = 0;
                CircuitClosedCount = 0;
                AverageExecutionTimeMs = 0;
            }
        }
    }

    /// <summary>
    /// Exception thrown by resilience policies.
    /// </summary>
    public class ResilienceException : Exception
    {
        public ResilienceException(string message) : base(message) { }
        public ResilienceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
