using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Conduit.Resilience
{
    /// <summary>
    /// Retry policy implementation using Polly.
    /// Implements various retry strategies with exponential, linear, or fixed backoff.
    /// </summary>
    public class RetryPolicy : IResiliencePolicy
    {
        private readonly ResilienceConfiguration.RetryConfig _config;
        private readonly ILogger? _logger;
        private readonly AsyncRetryPolicy _policy;
        private readonly RetryMetrics _metrics;
        private readonly object _metricsLock = new();
        private readonly Random _jitterRandom = new();

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public ResiliencePattern Pattern => ResiliencePattern.Retry;

        /// <inheritdoc/>
        public bool IsEnabled => _config.Enabled;

        /// <summary>
        /// Initializes a new instance of the RetryPolicy class.
        /// </summary>
        public RetryPolicy(
            string name,
            ResilienceConfiguration.RetryConfig config,
            ILogger<RetryPolicy>? logger = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(config, nameof(config));

            Name = name;
            _config = config;
            _logger = logger;
            _metrics = new RetryMetrics();

            _policy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: _config.MaxAttempts,
                    sleepDurationProvider: CalculateRetryDelay,
                    onRetry: OnRetry
                );

            _logger?.LogInformation("Retry policy '{Name}' initialized with {MaxAttempts} attempts using {Strategy} strategy",
                name, _config.MaxAttempts, _config.Strategy);
        }

        /// <summary>
        /// Initializes a new instance of the RetryPolicy class with simple parameters.
        /// </summary>
        public RetryPolicy(string name, int retryCount, RetryStrategy strategy) :
            this(name, new ResilienceConfiguration.RetryConfig
            {
                Enabled = true,
                MaxAttempts = retryCount,
                Strategy = strategy switch
                {
                    RetryStrategy.Exponential => BackoffStrategy.Exponential,
                    RetryStrategy.Linear => BackoffStrategy.Linear,
                    RetryStrategy.Fixed => BackoffStrategy.Fixed,
                    RetryStrategy.Immediate => BackoffStrategy.Fixed,
                    _ => BackoffStrategy.Exponential
                },
                WaitDuration = TimeSpan.FromSeconds(1),
                MaxWaitDuration = TimeSpan.FromSeconds(30)
            })
        {
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
            var attemptNumber = 0;

            try
            {
                await _policy.ExecuteAsync(async ct =>
                {
                    attemptNumber++;
                    await action(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds, attemptNumber);
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds, attemptNumber);
                _logger?.LogError(ex, "All {MaxAttempts} retry attempts failed for '{Name}'", _config.MaxAttempts, Name);
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
            var attemptNumber = 0;

            try
            {
                var result = await _policy.ExecuteAsync(async ct =>
                {
                    attemptNumber++;
                    return await func(ct);
                }, cancellationToken);

                RecordSuccess(sw.ElapsedMilliseconds, attemptNumber);
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(sw.ElapsedMilliseconds, attemptNumber);
                _logger?.LogError(ex, "All {MaxAttempts} retry attempts failed for '{Name}'", _config.MaxAttempts, Name);
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
                    RetriedExecutions = _metrics.RetryAttempts,
                    AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                    AdditionalMetrics = new RetryMetrics
                    {
                        TotalCalls = _metrics.TotalCalls,
                        SuccessfulCalls = _metrics.SuccessfulCalls,
                        FailedCalls = _metrics.FailedCalls,
                        RetryAttempts = _metrics.RetryAttempts,
                        SuccessfulAfterRetry = _metrics.SuccessfulAfterRetry,
                        AverageExecutionTimeMs = _metrics.AverageExecutionTimeMs,
                        AverageRetryCount = _metrics.AverageRetryCount
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
            _logger?.LogInformation("Retry policy '{Name}' metrics reset", Name);
        }

        private TimeSpan CalculateRetryDelay(int retryAttempt)
        {
            TimeSpan delay = _config.Strategy switch
            {
                BackoffStrategy.Fixed => _config.WaitDuration,
                BackoffStrategy.Linear => TimeSpan.FromMilliseconds(_config.WaitDuration.TotalMilliseconds * retryAttempt),
                BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(
                    _config.WaitDuration.TotalMilliseconds * Math.Pow(_config.BackoffMultiplier, retryAttempt - 1)),
                _ => _config.WaitDuration
            };

            // Apply max wait duration cap
            if (delay > _config.MaxWaitDuration)
            {
                delay = _config.MaxWaitDuration;
            }

            // Apply jitter if enabled
            if (_config.UseJitter)
            {
                delay = ApplyJitter(delay);
            }

            return delay;
        }

        private TimeSpan ApplyJitter(TimeSpan delay)
        {
            // Add random jitter of ±25% to prevent thundering herd
            var jitterFactor = 0.75 + (_jitterRandom.NextDouble() * 0.5); // Range: 0.75 to 1.25
            var jitteredMs = delay.TotalMilliseconds * jitterFactor;
            return TimeSpan.FromMilliseconds(jitteredMs);
        }

        private void OnRetry(Exception exception, TimeSpan delay, int retryCount, Polly.Context context)
        {
            lock (_metricsLock)
            {
                _metrics.RetryAttempts++;
            }

            _logger?.LogWarning(exception,
                "Retry attempt {RetryCount}/{MaxAttempts} for '{Name}' after {Delay}ms delay",
                retryCount, _config.MaxAttempts, Name, delay.TotalMilliseconds);
        }

        private void RecordSuccess(long executionTimeMs, int attemptNumber)
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.SuccessfulCalls++;

                if (attemptNumber > 1)
                {
                    _metrics.SuccessfulAfterRetry++;
                }

                UpdateAverageExecutionTime(executionTimeMs);
                UpdateAverageRetryCount(attemptNumber - 1); // Subtract 1 because first attempt is not a retry
            }
        }

        private void RecordFailure(long executionTimeMs, int attemptNumber)
        {
            lock (_metricsLock)
            {
                _metrics.TotalCalls++;
                _metrics.FailedCalls++;
                UpdateAverageExecutionTime(executionTimeMs);
                UpdateAverageRetryCount(attemptNumber - 1);
            }
        }

        private void UpdateAverageExecutionTime(long executionTimeMs)
        {
            var totalTime = _metrics.AverageExecutionTimeMs * (_metrics.TotalCalls - 1);
            _metrics.AverageExecutionTimeMs = (totalTime + executionTimeMs) / _metrics.TotalCalls;
        }

        private void UpdateAverageRetryCount(int retryCount)
        {
            var totalRetries = _metrics.AverageRetryCount * (_metrics.TotalCalls - 1);
            _metrics.AverageRetryCount = (totalRetries + retryCount) / _metrics.TotalCalls;
        }

        private class RetryMetrics
        {
            public long TotalCalls { get; set; }
            public long SuccessfulCalls { get; set; }
            public long FailedCalls { get; set; }
            public long RetryAttempts { get; set; }
            public long SuccessfulAfterRetry { get; set; }
            public double AverageExecutionTimeMs { get; set; }
            public double AverageRetryCount { get; set; }

            public void Reset()
            {
                TotalCalls = 0;
                SuccessfulCalls = 0;
                FailedCalls = 0;
                RetryAttempts = 0;
                SuccessfulAfterRetry = 0;
                AverageExecutionTimeMs = 0;
                AverageRetryCount = 0;
            }
        }
    }
}
