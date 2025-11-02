using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Conduit.Messaging
{
    /// <summary>
    /// Defines retry policies for message processing.
    /// </summary>
    public class MessageRetryPolicy
    {
        private readonly RetryStrategy _strategy;
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;
        private readonly double _jitterFactor;
        private readonly Func<Exception, bool> _retryPredicate;
        private readonly Random _random = new();

        /// <summary>
        /// Gets the maximum number of retries.
        /// </summary>
        public int MaxRetries => _maxRetries;

        /// <summary>
        /// Gets the retry strategy.
        /// </summary>
        public RetryStrategy Strategy => _strategy;

        /// <summary>
        /// Initializes a new instance of the MessageRetryPolicy class.
        /// </summary>
        public MessageRetryPolicy(
            RetryStrategy strategy = RetryStrategy.ExponentialBackoff,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0,
            double jitterFactor = 0.1,
            Func<Exception, bool>? retryPredicate = null)
        {
            Guard.AgainstNegative(maxRetries, nameof(maxRetries));
            Guard.AgainstNegative(backoffMultiplier, nameof(backoffMultiplier));
            Guard.AgainstNegative(jitterFactor, nameof(jitterFactor));

            _strategy = strategy;
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromMinutes(5);
            _backoffMultiplier = backoffMultiplier;
            _jitterFactor = jitterFactor;
            _retryPredicate = retryPredicate ?? DefaultRetryPredicate;
        }

        /// <summary>
        /// Creates a default retry policy.
        /// </summary>
        public static MessageRetryPolicy Default()
        {
            return new MessageRetryPolicy(
                RetryStrategy.ExponentialBackoff,
                maxRetries: 3,
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Creates a no-retry policy.
        /// </summary>
        public static MessageRetryPolicy NoRetry()
        {
            return new MessageRetryPolicy(RetryStrategy.None, maxRetries: 0);
        }

        /// <summary>
        /// Creates an immediate retry policy.
        /// </summary>
        public static MessageRetryPolicy Immediate(int maxRetries = 3)
        {
            return new MessageRetryPolicy(
                RetryStrategy.Immediate,
                maxRetries: maxRetries,
                initialDelay: TimeSpan.Zero);
        }

        /// <summary>
        /// Creates a fixed delay retry policy.
        /// </summary>
        public static MessageRetryPolicy FixedDelay(int maxRetries = 3, TimeSpan? delay = null)
        {
            return new MessageRetryPolicy(
                RetryStrategy.FixedDelay,
                maxRetries: maxRetries,
                initialDelay: delay ?? TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Creates an exponential backoff retry policy.
        /// </summary>
        public static MessageRetryPolicy ExponentialBackoff(
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double multiplier = 2.0)
        {
            return new MessageRetryPolicy(
                RetryStrategy.ExponentialBackoff,
                maxRetries: maxRetries,
                initialDelay: initialDelay,
                maxDelay: maxDelay,
                backoffMultiplier: multiplier);
        }

        /// <summary>
        /// Creates a linear backoff retry policy.
        /// </summary>
        public static MessageRetryPolicy LinearBackoff(
            int maxRetries = 3,
            TimeSpan? increment = null,
            TimeSpan? maxDelay = null)
        {
            return new MessageRetryPolicy(
                RetryStrategy.LinearBackoff,
                maxRetries: maxRetries,
                initialDelay: increment ?? TimeSpan.FromSeconds(1),
                maxDelay: maxDelay);
        }

        /// <summary>
        /// Determines if an exception should trigger a retry.
        /// </summary>
        public bool ShouldRetry(Exception exception)
        {
            return _retryPredicate(exception);
        }

        /// <summary>
        /// Gets the delay for a specific retry attempt.
        /// </summary>
        public TimeSpan GetRetryDelay(int attemptNumber)
        {
            if (attemptNumber <= 0 || attemptNumber > _maxRetries)
            {
                return TimeSpan.Zero;
            }

            var delay = _strategy switch
            {
                RetryStrategy.None => TimeSpan.Zero,
                RetryStrategy.Immediate => TimeSpan.Zero,
                RetryStrategy.FixedDelay => _initialDelay,
                RetryStrategy.LinearBackoff => CalculateLinearDelay(attemptNumber),
                RetryStrategy.ExponentialBackoff => CalculateExponentialDelay(attemptNumber),
                RetryStrategy.Fibonacci => CalculateFibonacciDelay(attemptNumber),
                _ => _initialDelay
            };

            // Apply jitter if configured
            if (_jitterFactor > 0 && delay > TimeSpan.Zero)
            {
                delay = ApplyJitter(delay);
            }

            // Ensure delay doesn't exceed maximum
            if (delay > _maxDelay)
            {
                delay = _maxDelay;
            }

            return delay;
        }

        /// <summary>
        /// Creates a Polly retry policy based on this configuration.
        /// </summary>
        public IAsyncPolicy CreatePollyPolicy()
        {
            if (_strategy == RetryStrategy.None || _maxRetries == 0)
            {
                return Policy.NoOpAsync();
            }

            return Policy
                .Handle<Exception>(ex => ShouldRetry(ex))
                .WaitAndRetryAsync(
                    _maxRetries,
                    retryAttempt => GetRetryDelay(retryAttempt),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        context["RetryCount"] = retryCount;
                        context["RetryDelay"] = timespan;
                    });
        }

        /// <summary>
        /// Creates a combined Polly policy with circuit breaker and timeout.
        /// </summary>
        public IAsyncPolicy CreateAdvancedPolicy(
            CircuitBreakerOptions? circuitBreakerOptions = null,
            TimeSpan? timeout = null)
        {
            var policies = new List<IAsyncPolicy>();

            // Add timeout policy
            if (timeout.HasValue)
            {
                policies.Add(Policy.TimeoutAsync(timeout.Value, TimeoutStrategy.Pessimistic));
            }

            // Add circuit breaker policy
            if (circuitBreakerOptions != null)
            {
                policies.Add(Policy
                    .Handle<Exception>(ex => ShouldRetry(ex))
                    .CircuitBreakerAsync(
                        circuitBreakerOptions.HandledEventsAllowedBeforeBreaking,
                        circuitBreakerOptions.DurationOfBreak,
                        onBreak: (result, duration) => circuitBreakerOptions.OnBreak?.Invoke(duration),
                        onReset: () => circuitBreakerOptions.OnReset?.Invoke()));
            }

            // Add retry policy
            policies.Add(CreatePollyPolicy());

            return Policy.WrapAsync(policies.ToArray());
        }

        /// <summary>
        /// Executes an operation with retry logic.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(operation, nameof(operation));

            var attempt = 0;
            Exception? lastException = null;

            while (attempt <= _maxRetries)
            {
                try
                {
                    return await operation(cancellationToken);
                }
                catch (Exception ex) when (ShouldRetry(ex) && attempt < _maxRetries)
                {
                    attempt++;
                    lastException = ex;

                    var delay = GetRetryDelay(attempt);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            throw lastException ?? new InvalidOperationException("Retry policy failed without exception");
        }

        /// <summary>
        /// Executes an operation with retry logic and callbacks.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            Action<RetryAttempt>? onRetry = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(operation, nameof(operation));

            var attempt = 0;
            var startTime = DateTimeOffset.UtcNow;
            Exception? lastException = null;

            while (attempt <= _maxRetries)
            {
                try
                {
                    var result = await operation(cancellationToken);

                    if (attempt > 0)
                    {
                        // Operation succeeded after retries
                        onRetry?.Invoke(new RetryAttempt
                        {
                            AttemptNumber = attempt,
                            TotalAttempts = attempt,
                            Succeeded = true,
                            Duration = DateTimeOffset.UtcNow - startTime
                        });
                    }

                    return result;
                }
                catch (Exception ex) when (ShouldRetry(ex) && attempt < _maxRetries)
                {
                    attempt++;
                    lastException = ex;

                    var delay = GetRetryDelay(attempt);

                    onRetry?.Invoke(new RetryAttempt
                    {
                        AttemptNumber = attempt,
                        TotalAttempts = _maxRetries,
                        Succeeded = false,
                        Exception = ex,
                        NextDelay = delay,
                        Duration = DateTimeOffset.UtcNow - startTime
                    });

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            throw lastException ?? new InvalidOperationException("Retry policy failed without exception");
        }

        private TimeSpan CalculateLinearDelay(int attemptNumber)
        {
            return TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * attemptNumber);
        }

        private TimeSpan CalculateExponentialDelay(int attemptNumber)
        {
            var delayMs = _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attemptNumber - 1);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private TimeSpan CalculateFibonacciDelay(int attemptNumber)
        {
            var fibonacci = GetFibonacci(attemptNumber);
            return TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * fibonacci);
        }

        private int GetFibonacci(int n)
        {
            if (n <= 1) return n;

            int prev = 0, curr = 1;
            for (int i = 2; i <= n; i++)
            {
                int temp = prev + curr;
                prev = curr;
                curr = temp;
            }
            return curr;
        }

        private TimeSpan ApplyJitter(TimeSpan delay)
        {
            var jitterRange = delay.TotalMilliseconds * _jitterFactor;
            var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange; // Random between -jitterRange and +jitterRange
            var newDelayMs = delay.TotalMilliseconds + jitter;
            return TimeSpan.FromMilliseconds(Math.Max(0, newDelayMs));
        }

        private static bool DefaultRetryPredicate(Exception exception)
        {
            // Retry on transient exceptions
            return exception switch
            {
                TimeoutException _ => true,
                TaskCanceledException _ => false,
                OperationCanceledException _ => false,
                OutOfMemoryException _ => false,
                StackOverflowException _ => false,
                AccessViolationException _ => false,
                _ => !IsFatal(exception)
            };
        }

        private static bool IsFatal(Exception exception)
        {
            // Check for fatal exceptions that should never be retried
            return exception is OutOfMemoryException ||
                   exception is StackOverflowException ||
                   exception is AccessViolationException ||
                   exception is ThreadAbortException ||
                   (exception.InnerException != null && IsFatal(exception.InnerException));
        }

        /// <summary>
        /// Creates a retry policy builder for fluent configuration.
        /// </summary>
        public static RetryPolicyBuilder Builder()
        {
            return new RetryPolicyBuilder();
        }
    }

    /// <summary>
    /// Retry strategies.
    /// </summary>
    public enum RetryStrategy
    {
        None,
        Immediate,
        FixedDelay,
        LinearBackoff,
        ExponentialBackoff,
        Fibonacci
    }

    /// <summary>
    /// Represents a retry attempt.
    /// </summary>
    public class RetryAttempt
    {
        public int AttemptNumber { get; set; }
        public int TotalAttempts { get; set; }
        public bool Succeeded { get; set; }
        public Exception? Exception { get; set; }
        public TimeSpan? NextDelay { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Options for circuit breaker policy.
    /// </summary>
    public class CircuitBreakerOptions
    {
        public int HandledEventsAllowedBeforeBreaking { get; set; } = 3;
        public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
        public Action<TimeSpan>? OnBreak { get; set; }
        public Action? OnReset { get; set; }
    }

    /// <summary>
    /// Builder for creating retry policies.
    /// </summary>
    public class RetryPolicyBuilder
    {
        private RetryStrategy _strategy = RetryStrategy.ExponentialBackoff;
        private int _maxRetries = 3;
        private TimeSpan _initialDelay = TimeSpan.FromSeconds(1);
        private TimeSpan _maxDelay = TimeSpan.FromMinutes(5);
        private double _backoffMultiplier = 2.0;
        private double _jitterFactor = 0.1;
        private readonly List<Type> _retryableExceptions = new();
        private readonly List<Type> _nonRetryableExceptions = new();
        private Func<Exception, bool>? _customPredicate;

        public RetryPolicyBuilder WithStrategy(RetryStrategy strategy)
        {
            _strategy = strategy;
            return this;
        }

        public RetryPolicyBuilder WithMaxRetries(int maxRetries)
        {
            _maxRetries = maxRetries;
            return this;
        }

        public RetryPolicyBuilder WithInitialDelay(TimeSpan delay)
        {
            _initialDelay = delay;
            return this;
        }

        public RetryPolicyBuilder WithMaxDelay(TimeSpan delay)
        {
            _maxDelay = delay;
            return this;
        }

        public RetryPolicyBuilder WithBackoffMultiplier(double multiplier)
        {
            _backoffMultiplier = multiplier;
            return this;
        }

        public RetryPolicyBuilder WithJitter(double jitterFactor)
        {
            _jitterFactor = jitterFactor;
            return this;
        }

        public RetryPolicyBuilder RetryOn<TException>() where TException : Exception
        {
            _retryableExceptions.Add(typeof(TException));
            return this;
        }

        public RetryPolicyBuilder DontRetryOn<TException>() where TException : Exception
        {
            _nonRetryableExceptions.Add(typeof(TException));
            return this;
        }

        public RetryPolicyBuilder WithCustomPredicate(Func<Exception, bool> predicate)
        {
            _customPredicate = predicate;
            return this;
        }

        public MessageRetryPolicy Build()
        {
            Func<Exception, bool> retryPredicate;

            if (_customPredicate != null)
            {
                retryPredicate = _customPredicate;
            }
            else if (_retryableExceptions.Any() || _nonRetryableExceptions.Any())
            {
                retryPredicate = ex =>
                {
                    var exceptionType = ex.GetType();

                    if (_nonRetryableExceptions.Any(t => t.IsAssignableFrom(exceptionType)))
                        return false;

                    if (_retryableExceptions.Any())
                        return _retryableExceptions.Any(t => t.IsAssignableFrom(exceptionType));

                    return true; // Default to retry if not in non-retryable list
                };
            }
            else
            {
                retryPredicate = null!; // Use default predicate
            }

            return new MessageRetryPolicy(
                _strategy,
                _maxRetries,
                _initialDelay,
                _maxDelay,
                _backoffMultiplier,
                _jitterFactor,
                retryPredicate);
        }
    }
}