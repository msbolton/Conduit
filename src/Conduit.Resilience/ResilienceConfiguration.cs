using System;
using System.Collections.Generic;

namespace Conduit.Resilience
{
    /// <summary>
    /// Configuration for resilience patterns including circuit breaker, retry, bulkhead, and timeout.
    /// </summary>
    public class ResilienceConfiguration
    {
        /// <summary>
        /// Gets or sets circuit breaker configuration.
        /// </summary>
        public CircuitBreakerConfig CircuitBreaker { get; set; } = new();

        /// <summary>
        /// Gets or sets retry configuration.
        /// </summary>
        public RetryConfig Retry { get; set; } = new();

        /// <summary>
        /// Gets or sets bulkhead configuration.
        /// </summary>
        public BulkheadConfig Bulkhead { get; set; } = new();

        /// <summary>
        /// Gets or sets timeout configuration.
        /// </summary>
        public TimeoutConfig Timeout { get; set; } = new();

        /// <summary>
        /// Gets or sets rate limiter configuration.
        /// </summary>
        public RateLimiterConfig RateLimiter { get; set} = new();

        /// <summary>
        /// Circuit breaker configuration.
        /// </summary>
        public class CircuitBreakerConfig
        {
            /// <summary>
            /// Gets or sets whether circuit breaker is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the failure threshold before opening the circuit.
            /// </summary>
            public int FailureThreshold { get; set; } = 5;

            /// <summary>
            /// Gets or sets the success threshold before closing the circuit.
            /// </summary>
            public int SuccessThreshold { get; set; } = 3;

            /// <summary>
            /// Gets or sets the wait duration in open state before attempting recovery.
            /// </summary>
            public TimeSpan WaitDurationInOpenState { get; set; } = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Gets or sets the duration threshold for considering a call as slow.
            /// </summary>
            public TimeSpan SlowCallDurationThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

            /// <summary>
            /// Gets or sets the minimum number of calls before calculating failure rate.
            /// </summary>
            public int MinimumThroughput { get; set; } = 10;

            /// <summary>
            /// Gets or sets the failure rate threshold (0.0 to 1.0).
            /// </summary>
            public double FailureRateThreshold { get; set; } = 0.5; // 50%
        }

        /// <summary>
        /// Retry policy configuration.
        /// </summary>
        public class RetryConfig
        {
            /// <summary>
            /// Gets or sets whether retry is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the maximum number of retry attempts.
            /// </summary>
            public int MaxAttempts { get; set; } = 3;

            /// <summary>
            /// Gets or sets the initial wait duration between retries.
            /// </summary>
            public TimeSpan WaitDuration { get; set; } = TimeSpan.FromMilliseconds(100);

            /// <summary>
            /// Gets or sets the backoff strategy.
            /// </summary>
            public BackoffStrategy Strategy { get; set; } = BackoffStrategy.Exponential;

            /// <summary>
            /// Gets or sets the maximum wait duration for exponential backoff.
            /// </summary>
            public TimeSpan MaxWaitDuration { get; set; } = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Gets or sets the backoff multiplier for exponential strategy.
            /// </summary>
            public double BackoffMultiplier { get; set; } = 2.0;

            /// <summary>
            /// Gets or sets whether to add jitter to retry delays.
            /// </summary>
            public bool UseJitter { get; set; } = true;
        }

        /// <summary>
        /// Bulkhead configuration for resource isolation.
        /// </summary>
        public class BulkheadConfig
        {
            /// <summary>
            /// Gets or sets whether bulkhead is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the maximum number of concurrent calls.
            /// </summary>
            public int MaxConcurrentCalls { get; set; } = 10;

            /// <summary>
            /// Gets or sets the maximum queue length.
            /// </summary>
            public int MaxQueuedCalls { get; set} = 20;

            /// <summary>
            /// Gets or sets the maximum wait duration for queued calls.
            /// </summary>
            public TimeSpan MaxWaitDuration { get; set; } = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Timeout configuration.
        /// </summary>
        public class TimeoutConfig
        {
            /// <summary>
            /// Gets or sets whether timeout is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the timeout duration.
            /// </summary>
            public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);

            /// <summary>
            /// Gets or sets the timeout strategy.
            /// </summary>
            public TimeoutStrategy Strategy { get; set; } = TimeoutStrategy.Optimistic;
        }

        /// <summary>
        /// Rate limiter configuration.
        /// </summary>
        public class RateLimiterConfig
        {
            /// <summary>
            /// Gets or sets whether rate limiter is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the maximum number of permits.
            /// </summary>
            public int MaxPermits { get; set; } = 100;

            /// <summary>
            /// Gets or sets the time window for rate limiting.
            /// </summary>
            public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

            /// <summary>
            /// Gets or sets the queue limit for waiting requests.
            /// </summary>
            public int QueueLimit { get; set; } = 10;
        }
    }

    /// <summary>
    /// Backoff strategies for retry policies.
    /// </summary>
    public enum BackoffStrategy
    {
        /// <summary>Fixed delay between retries</summary>
        Fixed,

        /// <summary>Linear increase in delay</summary>
        Linear,

        /// <summary>Exponential increase in delay</summary>
        Exponential
    }

    /// <summary>
    /// Timeout strategies.
    /// </summary>
    public enum TimeoutStrategy
    {
        /// <summary>Timeout assumes delegates don't support cancellation</summary>
        Optimistic,

        /// <summary>Timeout assumes delegates support cancellation</summary>
        Pessimistic
    }

    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitBreakerState
    {
        /// <summary>Circuit is closed, normal operation</summary>
        Closed,

        /// <summary>Circuit is open, calls fail immediately</summary>
        Open,

        /// <summary>Circuit is half-open, testing if service recovered</summary>
        HalfOpen,

        /// <summary>Circuit breaker is isolated (manual override)</summary>
        Isolated
    }

    /// <summary>
    /// Available resilience patterns.
    /// </summary>
    public enum ResiliencePattern
    {
        CircuitBreaker,
        Retry,
        Bulkhead,
        Timeout,
        RateLimiter
    }
}
