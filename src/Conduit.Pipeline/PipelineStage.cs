using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;

namespace Conduit.Pipeline
{
    /// <summary>
    /// Advanced pipeline stage implementations with retry, timeout, and validation capabilities.
    /// </summary>
    public static class PipelineStage
    {
        /// <summary>
        /// Creates a stage from a function.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> Create<TInput, TOutput>(
            string name,
            Func<TInput, TOutput> processor)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(processor, nameof(processor));

            return DelegateStage<TInput, TOutput>.Create(processor, name);
        }

        /// <summary>
        /// Creates a stage from an async function.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> CreateAsync<TInput, TOutput>(
            string name,
            Func<TInput, Task<TOutput>> processor)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(processor, nameof(processor));

            return DelegateStage<TInput, TOutput>.Create(processor, name);
        }

        /// <summary>
        /// Creates a stage with context access.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> CreateWithContext<TInput, TOutput>(
            string name,
            Func<TInput, PipelineContext, Task<TOutput>> processor)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(processor, nameof(processor));

            return new DelegateStage<TInput, TOutput>(processor, name);
        }

        /// <summary>
        /// Creates a no-op pass-through stage.
        /// </summary>
        public static IPipelineStage<T, T> PassThrough<T>(string? name = null)
        {
            return Create(name ?? "PassThrough", (T input) => input);
        }

        /// <summary>
        /// Creates a stage that validates input.
        /// </summary>
        public static IPipelineStage<TInput, TInput> Validate<TInput>(
            string name,
            Func<TInput, bool> validator,
            string? errorMessage = null)
        {
            return new ValidationStage<TInput>(name, validator, errorMessage);
        }

        /// <summary>
        /// Creates a stage that logs input/output.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> WithLogging<TInput, TOutput>(
            IPipelineStage<TInput, TOutput> innerStage,
            Action<string>? logger = null)
        {
            return new LoggingStage<TInput, TOutput>(innerStage, logger);
        }

        /// <summary>
        /// Creates a stage with retry logic.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> WithRetry<TInput, TOutput>(
            IPipelineStage<TInput, TOutput> innerStage,
            int maxRetries = 3,
            TimeSpan? retryDelay = null)
        {
            return new RetryStage<TInput, TOutput>(innerStage, maxRetries, retryDelay);
        }

        /// <summary>
        /// Creates a stage with timeout.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> WithTimeout<TInput, TOutput>(
            IPipelineStage<TInput, TOutput> innerStage,
            TimeSpan timeout)
        {
            return new TimeoutStage<TInput, TOutput>(innerStage, timeout);
        }

        /// <summary>
        /// Creates a stage with circuit breaker.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> WithCircuitBreaker<TInput, TOutput>(
            IPipelineStage<TInput, TOutput> innerStage,
            int failureThreshold = 5,
            TimeSpan breakDuration = default)
        {
            return new CircuitBreakerStage<TInput, TOutput>(innerStage, failureThreshold,
                breakDuration == default ? TimeSpan.FromSeconds(30) : breakDuration);
        }

        /// <summary>
        /// Creates a stage that measures execution time.
        /// </summary>
        public static IPipelineStage<TInput, TOutput> WithMetrics<TInput, TOutput>(
            IPipelineStage<TInput, TOutput> innerStage)
        {
            return new MetricsStage<TInput, TOutput>(innerStage);
        }
    }

    /// <summary>
    /// A stage that validates input before processing.
    /// </summary>
    public class ValidationStage<TInput> : PipelineStage<TInput, TInput>
    {
        private readonly Func<TInput, bool> _validator;
        private readonly string _errorMessage;
        private readonly string _name;

        public ValidationStage(string name, Func<TInput, bool> validator, string? errorMessage = null)
        {
            _name = name;
            _validator = validator;
            _errorMessage = errorMessage ?? "Validation failed";
        }

        public override string Name => _name;

        public override Task<TInput> ProcessAsync(TInput input, PipelineContext context)
        {
            if (!_validator(input))
            {
                throw new ValidationException($"{_errorMessage} at stage {Name}");
            }

            context.SetProperty($"{Name}.Validated", true);
            return Task.FromResult(input);
        }
    }

    /// <summary>
    /// A stage that adds logging.
    /// </summary>
    public class LoggingStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
    {
        private readonly IPipelineStage<TInput, TOutput> _innerStage;
        private readonly Action<string> _logger;

        public LoggingStage(IPipelineStage<TInput, TOutput> innerStage, Action<string>? logger = null)
        {
            _innerStage = innerStage;
            _logger = logger ?? Console.WriteLine;
        }

        public override string Name => $"{_innerStage.Name} (Logged)";

        public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
        {
            _logger($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Stage '{_innerStage.Name}' starting with input: {input}");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await _innerStage.ProcessAsync(input, context);
                stopwatch.Stop();

                _logger($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Stage '{_innerStage.Name}' completed in {stopwatch.ElapsedMilliseconds}ms with output: {result}");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Stage '{_innerStage.Name}' failed after {stopwatch.ElapsedMilliseconds}ms with error: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// A stage that implements retry logic.
    /// </summary>
    public class RetryStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
    {
        private readonly IPipelineStage<TInput, TOutput> _innerStage;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;

        public RetryStage(IPipelineStage<TInput, TOutput> innerStage, int maxRetries, TimeSpan? retryDelay)
        {
            _innerStage = innerStage;
            _maxRetries = maxRetries;
            _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        }

        public override string Name => $"{_innerStage.Name} (Retry x{_maxRetries})";

        public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= _maxRetries + 1; attempt++)
            {
                try
                {
                    context.SetProperty($"{Name}.Attempt", attempt);
                    return await _innerStage.ProcessAsync(input, context);
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt <= _maxRetries)
                    {
                        context.SetProperty($"{Name}.RetryingAfterError", ex.Message);
                        await Task.Delay(_retryDelay);
                    }
                }
            }

            throw new RetryExhaustedException($"Stage '{_innerStage.Name}' failed after {_maxRetries} retries", lastException!);
        }
    }

    /// <summary>
    /// A stage that enforces a timeout.
    /// </summary>
    public class TimeoutStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
    {
        private readonly IPipelineStage<TInput, TOutput> _innerStage;
        private readonly TimeSpan _timeout;

        public TimeoutStage(IPipelineStage<TInput, TOutput> innerStage, TimeSpan timeout)
        {
            _innerStage = innerStage;
            _timeout = timeout;
        }

        public override string Name => $"{_innerStage.Name} (Timeout: {_timeout.TotalSeconds}s)";

        public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
        {
            using var cts = new CancellationTokenSource(_timeout);

            try
            {
                var task = _innerStage.ProcessAsync(input, context);
                var completedTask = await Task.WhenAny(task, Task.Delay(_timeout, cts.Token));

                if (completedTask == task)
                {
                    cts.Cancel();
                    return await task;
                }

                throw new TimeoutException($"Stage '{_innerStage.Name}' timed out after {_timeout.TotalSeconds} seconds");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Stage '{_innerStage.Name}' timed out after {_timeout.TotalSeconds} seconds");
            }
        }
    }

    /// <summary>
    /// A stage that implements circuit breaker pattern.
    /// </summary>
    public class CircuitBreakerStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
    {
        private readonly IPipelineStage<TInput, TOutput> _innerStage;
        private readonly int _failureThreshold;
        private readonly TimeSpan _breakDuration;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitState _state;
        private readonly object _stateLock = new();

        private enum CircuitState
        {
            Closed,
            Open,
            HalfOpen
        }

        public CircuitBreakerStage(IPipelineStage<TInput, TOutput> innerStage, int failureThreshold, TimeSpan breakDuration)
        {
            _innerStage = innerStage;
            _failureThreshold = failureThreshold;
            _breakDuration = breakDuration;
            _state = CircuitState.Closed;
        }

        public override string Name => $"{_innerStage.Name} (Circuit Breaker)";

        public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
        {
            lock (_stateLock)
            {
                if (_state == CircuitState.Open)
                {
                    if (DateTime.UtcNow - _lastFailureTime > _breakDuration)
                    {
                        _state = CircuitState.HalfOpen;
                    }
                    else
                    {
                        throw new CircuitBreakerOpenException($"Circuit breaker is open for stage '{_innerStage.Name}'");
                    }
                }
            }

            try
            {
                var result = await _innerStage.ProcessAsync(input, context);

                lock (_stateLock)
                {
                    if (_state == CircuitState.HalfOpen)
                    {
                        _state = CircuitState.Closed;
                        _failureCount = 0;
                    }
                }

                return result;
            }
            catch (Exception)
            {
                lock (_stateLock)
                {
                    _failureCount++;
                    _lastFailureTime = DateTime.UtcNow;

                    if (_failureCount >= _failureThreshold)
                    {
                        _state = CircuitState.Open;
                        context.SetProperty($"{Name}.CircuitOpen", true);
                    }
                }

                throw;
            }
        }
    }

    /// <summary>
    /// A stage that collects metrics.
    /// </summary>
    public class MetricsStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
    {
        private readonly IPipelineStage<TInput, TOutput> _innerStage;
        private readonly List<long> _executionTimes = new();
        private long _totalExecutions;
        private long _successfulExecutions;
        private long _failedExecutions;
        private readonly object _metricsLock = new();

        public MetricsStage(IPipelineStage<TInput, TOutput> innerStage)
        {
            _innerStage = innerStage;
        }

        public override string Name => $"{_innerStage.Name} (Metrics)";

        public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _innerStage.ProcessAsync(input, context);
                stopwatch.Stop();

                RecordSuccess(stopwatch.ElapsedMilliseconds, context);

                return result;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                RecordFailure(stopwatch.ElapsedMilliseconds, context);
                throw;
            }
        }

        private void RecordSuccess(long elapsedMs, PipelineContext context)
        {
            lock (_metricsLock)
            {
                _totalExecutions++;
                _successfulExecutions++;
                _executionTimes.Add(elapsedMs);

                UpdateContextMetrics(context);
            }
        }

        private void RecordFailure(long elapsedMs, PipelineContext context)
        {
            lock (_metricsLock)
            {
                _totalExecutions++;
                _failedExecutions++;
                _executionTimes.Add(elapsedMs);

                UpdateContextMetrics(context);
            }
        }

        private void UpdateContextMetrics(PipelineContext context)
        {
            context.SetProperty($"{Name}.TotalExecutions", _totalExecutions);
            context.SetProperty($"{Name}.SuccessfulExecutions", _successfulExecutions);
            context.SetProperty($"{Name}.FailedExecutions", _failedExecutions);
            context.SetProperty($"{Name}.SuccessRate", _totalExecutions > 0 ? (double)_successfulExecutions / _totalExecutions : 0);

            if (_executionTimes.Count > 0)
            {
                context.SetProperty($"{Name}.AverageExecutionTime", _executionTimes.Average());
                context.SetProperty($"{Name}.MinExecutionTime", _executionTimes.Min());
                context.SetProperty($"{Name}.MaxExecutionTime", _executionTimes.Max());
            }
        }

        public StageMetrics GetMetrics()
        {
            lock (_metricsLock)
            {
                return new StageMetrics
                {
                    TotalExecutions = _totalExecutions,
                    SuccessfulExecutions = _successfulExecutions,
                    FailedExecutions = _failedExecutions,
                    SuccessRate = _totalExecutions > 0 ? (double)_successfulExecutions / _totalExecutions : 0,
                    AverageExecutionTime = _executionTimes.Count > 0 ? _executionTimes.Average() : 0,
                    MinExecutionTime = _executionTimes.Count > 0 ? _executionTimes.Min() : 0,
                    MaxExecutionTime = _executionTimes.Count > 0 ? _executionTimes.Max() : 0
                };
            }
        }
    }

    /// <summary>
    /// Stage execution metrics.
    /// </summary>
    public class StageMetrics
    {
        public long TotalExecutions { get; set; }
        public long SuccessfulExecutions { get; set; }
        public long FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public double AverageExecutionTime { get; set; }
        public long MinExecutionTime { get; set; }
        public long MaxExecutionTime { get; set; }
    }

    /// <summary>
    /// Exception thrown when validation fails.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when retries are exhausted.
    /// </summary>
    public class RetryExhaustedException : Exception
    {
        public RetryExhaustedException(string message) : base(message) { }
        public RetryExhaustedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when circuit breaker is open.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
    }
}