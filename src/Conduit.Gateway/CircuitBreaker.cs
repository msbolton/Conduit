using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Circuit breaker implementation for protecting against failing services.
    /// </summary>
    public class CircuitBreaker : IDisposable
    {
        private readonly ILogger<CircuitBreaker>? _logger;
        private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStates;
        private readonly Timer _recoveryTimer;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the CircuitBreaker class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        /// <param name="recoveryCheckInterval">Recovery check interval (default: 30 seconds)</param>
        public CircuitBreaker(ILogger<CircuitBreaker>? logger = null, TimeSpan? recoveryCheckInterval = null)
        {
            _logger = logger;
            _circuitStates = new ConcurrentDictionary<string, CircuitBreakerState>();

            var interval = recoveryCheckInterval ?? TimeSpan.FromSeconds(30);
            _recoveryTimer = new Timer(CheckRecovery, null, interval, interval);
        }

        /// <summary>
        /// Executes an operation with circuit breaker protection.
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="key">Circuit breaker key (e.g., transport name, endpoint)</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="timeout">Timeout before transitioning to half-open state</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the operation</returns>
        /// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open</exception>
        public async Task<T> ExecuteAsync<T>(
            string key,
            Func<Task<T>> operation,
            int failureThreshold = 5,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            ThrowIfDisposed();

            var circuitTimeout = timeout ?? TimeSpan.FromMinutes(1);
            var state = _circuitStates.GetOrAdd(key, _ => new CircuitBreakerState(failureThreshold, circuitTimeout));

            // Update configuration if it has changed
            state.UpdateConfiguration(failureThreshold, circuitTimeout);

            // Check circuit state
            switch (state.State)
            {
                case CircuitState.Open:
                    _logger?.LogWarning("Circuit breaker {Key} is open, rejecting request", key);
                    throw new CircuitBreakerOpenException($"Circuit breaker for {key} is open");

                case CircuitState.HalfOpen:
                    // Allow limited requests in half-open state
                    if (state.HalfOpenAttempts >= 3)
                    {
                        _logger?.LogDebug("Circuit breaker {Key} half-open request limit reached", key);
                        throw new CircuitBreakerOpenException($"Circuit breaker for {key} is half-open with too many attempts");
                    }
                    break;

                case CircuitState.Closed:
                default:
                    // Normal operation
                    break;
            }

            try
            {
                var result = await operation();
                state.RecordSuccess();

                if (state.State == CircuitState.HalfOpen)
                {
                    _logger?.LogInformation("Circuit breaker {Key} recovered, transitioning to closed", key);
                }

                return result;
            }
            catch (Exception ex)
            {
                state.RecordFailure();

                if (state.State == CircuitState.Open)
                {
                    _logger?.LogWarning(ex, "Circuit breaker {Key} opened due to failures", key);
                }
                else if (state.State == CircuitState.HalfOpen)
                {
                    _logger?.LogWarning(ex, "Circuit breaker {Key} failed during half-open state, returning to open", key);
                }

                throw;
            }
        }

        /// <summary>
        /// Gets the state of a circuit breaker.
        /// </summary>
        /// <param name="key">Circuit breaker key</param>
        /// <returns>Circuit breaker information, or null if not found</returns>
        public CircuitBreakerInfo? GetCircuitInfo(string key)
        {
            if (_circuitStates.TryGetValue(key, out var state))
            {
                return new CircuitBreakerInfo
                {
                    Key = key,
                    State = state.State,
                    FailureCount = state.FailureCount,
                    FailureThreshold = state.FailureThreshold,
                    SuccessCount = state.SuccessCount,
                    TotalRequests = state.TotalRequests,
                    LastFailureTime = state.LastFailureTime,
                    NextRetryTime = state.NextRetryTime,
                    HalfOpenAttempts = state.HalfOpenAttempts
                };
            }

            return null;
        }

        /// <summary>
        /// Gets statistics for all circuit breakers.
        /// </summary>
        /// <returns>Circuit breaker statistics</returns>
        public CircuitBreakerStatistics GetStatistics()
        {
            var stats = new CircuitBreakerStatistics();

            foreach (var kvp in _circuitStates)
            {
                var state = kvp.Value;
                stats.TotalCircuits++;
                stats.TotalRequests += state.TotalRequests;
                stats.TotalFailures += state.FailureCount;
                stats.TotalSuccesses += state.SuccessCount;

                switch (state.State)
                {
                    case CircuitState.Open:
                        stats.OpenCircuits++;
                        break;
                    case CircuitState.HalfOpen:
                        stats.HalfOpenCircuits++;
                        break;
                    case CircuitState.Closed:
                        stats.ClosedCircuits++;
                        break;
                }
            }

            stats.FailureRate = stats.TotalRequests > 0 ? (double)stats.TotalFailures / stats.TotalRequests : 0.0;

            return stats;
        }

        /// <summary>
        /// Manually opens a circuit breaker.
        /// </summary>
        /// <param name="key">Circuit breaker key</param>
        public void OpenCircuit(string key)
        {
            if (_circuitStates.TryGetValue(key, out var state))
            {
                state.ForceOpen();
                _logger?.LogWarning("Circuit breaker {Key} manually opened", key);
            }
        }

        /// <summary>
        /// Manually closes a circuit breaker.
        /// </summary>
        /// <param name="key">Circuit breaker key</param>
        public void CloseCircuit(string key)
        {
            if (_circuitStates.TryGetValue(key, out var state))
            {
                state.ForceClose();
                _logger?.LogInformation("Circuit breaker {Key} manually closed", key);
            }
        }

        /// <summary>
        /// Removes a circuit breaker.
        /// </summary>
        /// <param name="key">Circuit breaker key</param>
        /// <returns>True if removed, false if not found</returns>
        public bool RemoveCircuit(string key)
        {
            var removed = _circuitStates.TryRemove(key, out _);
            if (removed)
            {
                _logger?.LogDebug("Removed circuit breaker {Key}", key);
            }
            return removed;
        }

        /// <summary>
        /// Checks for circuits that can transition from open to half-open.
        /// </summary>
        private void CheckRecovery(object? state)
        {
            if (_disposed)
                return;

            try
            {
                var now = DateTime.UtcNow;
                foreach (var kvp in _circuitStates)
                {
                    var circuitState = kvp.Value;
                    if (circuitState.State == CircuitState.Open && now >= circuitState.NextRetryTime)
                    {
                        circuitState.TransitionToHalfOpen();
                        _logger?.LogInformation("Circuit breaker {Key} transitioned to half-open", kvp.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during circuit breaker recovery check");
            }
        }

        /// <summary>
        /// Throws an exception if the circuit breaker has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CircuitBreaker));
        }

        /// <summary>
        /// Disposes the circuit breaker.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _recoveryTimer?.Dispose();
            _logger?.LogInformation("Circuit breaker disposed");
        }
    }

    /// <summary>
    /// Internal state for a circuit breaker.
    /// </summary>
    internal class CircuitBreakerState
    {
        private readonly object _lock = new object();
        private CircuitState _state = CircuitState.Closed;
        private int _failureCount;
        private int _successCount;
        private int _totalRequests;
        private int _failureThreshold;
        private TimeSpan _timeout;
        private DateTime _lastFailureTime;
        private DateTime _nextRetryTime;
        private int _halfOpenAttempts;

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerState class.
        /// </summary>
        /// <param name="failureThreshold">Failure threshold</param>
        /// <param name="timeout">Timeout before retry</param>
        public CircuitBreakerState(int failureThreshold, TimeSpan timeout)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout;
        }

        /// <summary>
        /// Gets the current circuit state.
        /// </summary>
        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// Gets the failure count.
        /// </summary>
        public int FailureCount => _failureCount;

        /// <summary>
        /// Gets the failure threshold.
        /// </summary>
        public int FailureThreshold => _failureThreshold;

        /// <summary>
        /// Gets the success count.
        /// </summary>
        public int SuccessCount => _successCount;

        /// <summary>
        /// Gets the total request count.
        /// </summary>
        public int TotalRequests => _totalRequests;

        /// <summary>
        /// Gets the last failure time.
        /// </summary>
        public DateTime LastFailureTime
        {
            get
            {
                lock (_lock)
                {
                    return _lastFailureTime;
                }
            }
        }

        /// <summary>
        /// Gets the next retry time.
        /// </summary>
        public DateTime NextRetryTime
        {
            get
            {
                lock (_lock)
                {
                    return _nextRetryTime;
                }
            }
        }

        /// <summary>
        /// Gets the number of half-open attempts.
        /// </summary>
        public int HalfOpenAttempts => _halfOpenAttempts;

        /// <summary>
        /// Updates the circuit breaker configuration.
        /// </summary>
        /// <param name="failureThreshold">New failure threshold</param>
        /// <param name="timeout">New timeout</param>
        public void UpdateConfiguration(int failureThreshold, TimeSpan timeout)
        {
            lock (_lock)
            {
                _failureThreshold = failureThreshold;
                _timeout = timeout;
            }
        }

        /// <summary>
        /// Records a successful operation.
        /// </summary>
        public void RecordSuccess()
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _successCount);
                Interlocked.Increment(ref _totalRequests);

                if (_state == CircuitState.HalfOpen)
                {
                    // Successful request in half-open state, transition to closed
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _halfOpenAttempts = 0;
                }
            }
        }

        /// <summary>
        /// Records a failed operation.
        /// </summary>
        public void RecordFailure()
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _failureCount);
                Interlocked.Increment(ref _totalRequests);
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen)
                {
                    // Failed request in half-open state, transition back to open
                    _state = CircuitState.Open;
                    _nextRetryTime = DateTime.UtcNow.Add(_timeout);
                    _halfOpenAttempts = 0;
                }
                else if (_state == CircuitState.Closed && _failureCount >= _failureThreshold)
                {
                    // Too many failures, open the circuit
                    _state = CircuitState.Open;
                    _nextRetryTime = DateTime.UtcNow.Add(_timeout);
                }

                if (_state == CircuitState.HalfOpen)
                {
                    Interlocked.Increment(ref _halfOpenAttempts);
                }
            }
        }

        /// <summary>
        /// Transitions to half-open state.
        /// </summary>
        public void TransitionToHalfOpen()
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open)
                {
                    _state = CircuitState.HalfOpen;
                    _halfOpenAttempts = 0;
                }
            }
        }

        /// <summary>
        /// Manually forces the circuit to open.
        /// </summary>
        public void ForceOpen()
        {
            lock (_lock)
            {
                _state = CircuitState.Open;
                _nextRetryTime = DateTime.UtcNow.Add(_timeout);
                _halfOpenAttempts = 0;
            }
        }

        /// <summary>
        /// Manually forces the circuit to close.
        /// </summary>
        public void ForceClose()
        {
            lock (_lock)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _halfOpenAttempts = 0;
            }
        }
    }

    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Normal operation, requests are allowed.
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, requests are blocked.
        /// </summary>
        Open,

        /// <summary>
        /// Testing if the service has recovered, limited requests allowed.
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// Circuit breaker information.
    /// </summary>
    public class CircuitBreakerInfo
    {
        /// <summary>
        /// Gets or sets the circuit breaker key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current state.
        /// </summary>
        public CircuitState State { get; set; }

        /// <summary>
        /// Gets or sets the failure count.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the failure threshold.
        /// </summary>
        public int FailureThreshold { get; set; }

        /// <summary>
        /// Gets or sets the success count.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the total request count.
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the last failure time.
        /// </summary>
        public DateTime LastFailureTime { get; set; }

        /// <summary>
        /// Gets or sets the next retry time.
        /// </summary>
        public DateTime NextRetryTime { get; set; }

        /// <summary>
        /// Gets or sets the number of half-open attempts.
        /// </summary>
        public int HalfOpenAttempts { get; set; }
    }

    /// <summary>
    /// Circuit breaker statistics.
    /// </summary>
    public class CircuitBreakerStatistics
    {
        /// <summary>
        /// Gets or sets the total number of circuits.
        /// </summary>
        public int TotalCircuits { get; set; }

        /// <summary>
        /// Gets or sets the number of open circuits.
        /// </summary>
        public int OpenCircuits { get; set; }

        /// <summary>
        /// Gets or sets the number of half-open circuits.
        /// </summary>
        public int HalfOpenCircuits { get; set; }

        /// <summary>
        /// Gets or sets the number of closed circuits.
        /// </summary>
        public int ClosedCircuits { get; set; }

        /// <summary>
        /// Gets or sets the total number of requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the total number of failures.
        /// </summary>
        public long TotalFailures { get; set; }

        /// <summary>
        /// Gets or sets the total number of successes.
        /// </summary>
        public long TotalSuccesses { get; set; }

        /// <summary>
        /// Gets or sets the failure rate (0.0 to 1.0).
        /// </summary>
        public double FailureRate { get; set; }
    }

    /// <summary>
    /// Exception thrown when a circuit breaker is open.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class.
        /// </summary>
        /// <param name="message">Exception message</param>
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CircuitBreakerOpenException class.
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}