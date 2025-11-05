using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Conduit.Core.Behaviors;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Pipeline behavior that implements retry logic with configurable strategies
/// </summary>
public class RetryBehavior : IPipelineBehavior
{
    private readonly ILogger<RetryBehavior> _logger;
    private readonly RetryBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the RetryBehavior class
    /// </summary>
    public RetryBehavior(ILogger<RetryBehavior> logger, RetryBehaviorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new RetryBehaviorOptions();
    }

    /// <summary>
    /// Executes retry logic around the pipeline execution
    /// </summary>
    public async Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        // Check if retry is disabled for this context
        if (context.GetValueProperty<bool>("RetryDisabled"))
        {
            return await next.ProceedAsync(context);
        }

        // Check if this message type should be retried
        if (!ShouldRetry(context))
        {
            return await next.ProceedAsync(context);
        }

        var maxAttempts = GetMaxAttempts(context);
        var attempt = 0;
        var totalStopwatch = Stopwatch.StartNew();

        while (attempt < maxAttempts)
        {
            attempt++;
            var attemptStopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("Executing attempt {Attempt}/{MaxAttempts} for {MessageType}",
                    attempt, maxAttempts, context.Input?.GetType().Name ?? "Unknown");

                context.SetProperty("CurrentAttempt", attempt);
                context.SetProperty("MaxAttempts", maxAttempts);

                var result = await next.ProceedAsync(context);

                // Success - log and return
                attemptStopwatch.Stop();
                if (attempt > 1)
                {
                    _logger.LogInformation("Retry succeeded on attempt {Attempt}/{MaxAttempts} for {MessageType} after {TotalDuration}ms",
                        attempt, maxAttempts, context.Input?.GetType().Name ?? "Unknown", totalStopwatch.ElapsedMilliseconds);
                }

                context.SetProperty("RetrySucceeded", attempt > 1);
                context.SetProperty("TotalAttempts", attempt);
                context.SetProperty("TotalRetryDuration", totalStopwatch.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                context.Exception = ex;

                // Check if this exception should trigger a retry
                if (!ShouldRetryForException(ex, context))
                {
                    _logger.LogWarning("Exception {ExceptionType} is not retryable for {MessageType}, failing immediately",
                        ex.GetType().Name, context.Input?.GetType().Name ?? "Unknown");
                    throw;
                }

                // Check if we've exhausted our attempts
                if (attempt >= maxAttempts)
                {
                    _logger.LogError(ex, "All retry attempts ({MaxAttempts}) exhausted for {MessageType} after {TotalDuration}ms",
                        maxAttempts, context.Input?.GetType().Name ?? "Unknown", totalStopwatch.ElapsedMilliseconds);

                    context.SetProperty("RetryExhausted", true);
                    context.SetProperty("TotalAttempts", attempt);
                    context.SetProperty("TotalRetryDuration", totalStopwatch.Elapsed);
                    throw;
                }

                // Log the retry attempt
                _logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} failed for {MessageType} in {AttemptDuration}ms, retrying...",
                    attempt, maxAttempts, context.Input?.GetType().Name ?? "Unknown", attemptStopwatch.ElapsedMilliseconds);

                // Wait before retry
                var delay = CalculateDelay(attempt, context);
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug("Waiting {DelayMs}ms before retry attempt {NextAttempt}",
                        delay.TotalMilliseconds, attempt + 1);
                    await Task.Delay(delay);
                }

                // Reset the context exception for the next attempt
                context.Exception = null;
            }
        }

        // This should never be reached, but just in case
        throw new InvalidOperationException("Retry logic error: exceeded maximum attempts without throwing");
    }

    private bool ShouldRetry(PipelineContext context)
    {
        if (context.Input == null) return false;

        var messageType = context.Input.GetType();

        // Check if message type is explicitly included
        if (_options.RetryableMessageTypes.Count > 0)
        {
            return _options.RetryableMessageTypes.Contains(messageType);
        }

        // Check if message type is explicitly excluded
        if (_options.ExcludedMessageTypes.Contains(messageType))
        {
            return false;
        }

        // Check custom predicate
        if (_options.ShouldRetryPredicate != null)
        {
            return _options.ShouldRetryPredicate(context);
        }

        // Default: retry if not explicitly excluded
        return true;
    }

    private bool ShouldRetryForException(Exception exception, PipelineContext context)
    {
        // Check if exception type is in the retryable list
        if (_options.RetryableExceptionTypes.Count > 0)
        {
            var exceptionType = exception.GetType();
            return _options.RetryableExceptionTypes.Any(type => type.IsAssignableFrom(exceptionType));
        }

        // Check if exception type is in the non-retryable list
        var nonRetryableType = _options.NonRetryableExceptionTypes.FirstOrDefault(type => type.IsAssignableFrom(exception.GetType()));
        if (nonRetryableType != null)
        {
            return false;
        }

        // Check custom predicate
        if (_options.ShouldRetryExceptionPredicate != null)
        {
            return _options.ShouldRetryExceptionPredicate(exception, context);
        }

        // Default: retry on most exceptions except ArgumentException, ArgumentNullException, etc.
        return !(exception is ArgumentException or ArgumentNullException or InvalidOperationException);
    }

    private int GetMaxAttempts(PipelineContext context)
    {
        // Check if context has a specific max attempts override
        var contextMaxAttempts = context.GetProperty("MaxRetryAttempts") as int?;
        if (contextMaxAttempts.HasValue)
        {
            return contextMaxAttempts.Value;
        }

        return _options.MaxAttempts;
    }

    private TimeSpan CalculateDelay(int attempt, PipelineContext context)
    {
        // Check if context has a specific delay override
        var contextDelay = context.GetProperty("RetryDelay") as TimeSpan?;
        if (contextDelay.HasValue)
        {
            return contextDelay.Value;
        }

        return _options.DelayStrategy switch
        {
            RetryDelayStrategy.Fixed => _options.BaseDelay,
            RetryDelayStrategy.Linear => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * attempt),
            RetryDelayStrategy.Exponential => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
            RetryDelayStrategy.ExponentialWithJitter => AddJitter(TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1))),
            _ => _options.BaseDelay
        };
    }

    private TimeSpan AddJitter(TimeSpan delay)
    {
        var jitterMs = Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)); // 10% jitter
        return delay.Add(TimeSpan.FromMilliseconds(jitterMs));
    }
}

/// <summary>
/// Configuration options for the retry behavior
/// </summary>
public class RetryBehaviorOptions
{
    /// <summary>
    /// Maximum number of retry attempts (including the initial attempt)
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Strategy for calculating delays between retry attempts
    /// </summary>
    public RetryDelayStrategy DelayStrategy { get; set; } = RetryDelayStrategy.Exponential;

    /// <summary>
    /// List of message types that should be retried (empty means retry all)
    /// </summary>
    public HashSet<Type> RetryableMessageTypes { get; set; } = new();

    /// <summary>
    /// List of message types that should never be retried
    /// </summary>
    public HashSet<Type> ExcludedMessageTypes { get; set; } = new();

    /// <summary>
    /// List of exception types that should trigger a retry
    /// </summary>
    public HashSet<Type> RetryableExceptionTypes { get; set; } = new();

    /// <summary>
    /// List of exception types that should never trigger a retry
    /// </summary>
    public HashSet<Type> NonRetryableExceptionTypes { get; set; } = new()
    {
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(InvalidOperationException),
        typeof(NotSupportedException),
        typeof(NotImplementedException)
    };

    /// <summary>
    /// Custom predicate to determine if a message should be retried
    /// </summary>
    public Func<PipelineContext, bool>? ShouldRetryPredicate { get; set; }

    /// <summary>
    /// Custom predicate to determine if an exception should trigger a retry
    /// </summary>
    public Func<Exception, PipelineContext, bool>? ShouldRetryExceptionPredicate { get; set; }

    /// <summary>
    /// Adds a message type to the retryable types list
    /// </summary>
    public RetryBehaviorOptions AddRetryableType<T>()
    {
        RetryableMessageTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds a message type to the excluded types list
    /// </summary>
    public RetryBehaviorOptions ExcludeType<T>()
    {
        ExcludedMessageTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds an exception type to the retryable exceptions list
    /// </summary>
    public RetryBehaviorOptions AddRetryableException<T>() where T : Exception
    {
        RetryableExceptionTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds an exception type to the non-retryable exceptions list
    /// </summary>
    public RetryBehaviorOptions AddNonRetryableException<T>() where T : Exception
    {
        NonRetryableExceptionTypes.Add(typeof(T));
        return this;
    }
}

/// <summary>
/// Strategies for calculating delays between retry attempts
/// </summary>
public enum RetryDelayStrategy
{
    /// <summary>
    /// Fixed delay between attempts
    /// </summary>
    Fixed,

    /// <summary>
    /// Linear increase in delay (baseDelay * attempt)
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff (baseDelay * 2^(attempt-1))
    /// </summary>
    Exponential,

    /// <summary>
    /// Exponential backoff with random jitter to prevent thundering herd
    /// </summary>
    ExponentialWithJitter
}