using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience.ErrorHandling;

/// <summary>
/// Policy that provides fallback actions when operations fail
/// </summary>
public class FallbackPolicy : IResiliencePolicy
{
    private readonly ILogger<FallbackPolicy>? _logger;
    private readonly FallbackConfiguration _config;
    private readonly PolicyMetricsTracker _metrics;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ResiliencePattern Pattern => ResiliencePattern.Fallback;

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the FallbackPolicy class
    /// </summary>
    public FallbackPolicy(string name, FallbackConfiguration config, ILogger<FallbackPolicy>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _metrics = new PolicyMetricsTracker(name, Pattern);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            await action(cancellationToken);
            return;
        }

        var startTime = DateTimeOffset.UtcNow;
        _metrics.IncrementExecutions();

        try
        {
            await action(cancellationToken);
            _metrics.IncrementSuccesses();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            _metrics.IncrementFailures();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            var errorContext = new ErrorContext(ex)
                .WithOperation("FallbackPolicy")
                .WithComponent(Name);

            if (ShouldApplyFallback(errorContext))
            {
                _logger?.LogWarning(ex, "Executing fallback for policy '{PolicyName}' due to error: {ErrorMessage}",
                    Name, ex.Message);

                try
                {
                    if (_config.FallbackAction != null)
                    {
                        await _config.FallbackAction(errorContext, cancellationToken);
                        _metrics.IncrementFallbacks();
                        return;
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger?.LogError(fallbackEx, "Fallback action failed for policy '{PolicyName}'", Name);
                    _metrics.IncrementFallbackFailures();

                    // If fallback fails and we're configured to throw on fallback failure, throw aggregate
                    if (_config.ThrowOnFallbackFailure)
                    {
                        throw new AggregateException("Both primary action and fallback failed", ex, fallbackEx);
                    }
                }
            }

            // If no fallback or fallback failed, re-throw original exception
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return await func(cancellationToken);
        }

        var startTime = DateTimeOffset.UtcNow;
        _metrics.IncrementExecutions();

        try
        {
            var result = await func(cancellationToken);
            _metrics.IncrementSuccesses();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);
            return result;
        }
        catch (Exception ex)
        {
            _metrics.IncrementFailures();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            var errorContext = new ErrorContext(ex)
                .WithOperation("FallbackPolicy")
                .WithComponent(Name);

            if (ShouldApplyFallback(errorContext))
            {
                _logger?.LogWarning(ex, "Executing fallback for policy '{PolicyName}' due to error: {ErrorMessage}",
                    Name, ex.Message);

                try
                {
                    if (_config.FallbackFunc != null)
                    {
                        var fallbackResult = await _config.FallbackFunc(errorContext, cancellationToken);
                        _metrics.IncrementFallbacks();
                        if (fallbackResult is TResult typedResult)
                        {
                            return typedResult;
                        }
                        else if (fallbackResult != null)
                        {
                            try
                            {
                                return (TResult)fallbackResult;
                            }
                            catch (InvalidCastException)
                            {
                                _logger?.LogWarning("Cannot convert fallback result of type {FallbackResultType} to {ResultType}",
                                    fallbackResult.GetType().Name, typeof(TResult).Name);
                            }
                        }
                    }
                    else if (_config.DefaultValue != null)
                    {
                        if (_config.DefaultValue is TResult defaultResult)
                        {
                            _metrics.IncrementFallbacks();
                            return defaultResult;
                        }
                        else
                        {
                            try
                            {
                                var convertedResult = (TResult)_config.DefaultValue;
                                _metrics.IncrementFallbacks();
                                return convertedResult;
                            }
                            catch (InvalidCastException)
                            {
                                _logger?.LogWarning("Cannot convert default value of type {DefaultValueType} to {ResultType}",
                                    _config.DefaultValue.GetType().Name, typeof(TResult).Name);
                            }
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger?.LogError(fallbackEx, "Fallback function failed for policy '{PolicyName}'", Name);
                    _metrics.IncrementFallbackFailures();

                    // If fallback fails and we're configured to throw on fallback failure, throw aggregate
                    if (_config.ThrowOnFallbackFailure)
                    {
                        throw new AggregateException("Both primary function and fallback failed", ex, fallbackEx);
                    }
                }
            }

            // If no fallback or fallback failed, re-throw original exception
            throw;
        }
    }

    /// <inheritdoc />
    public PolicyMetrics GetMetrics()
    {
        return _metrics.ToMetrics();
    }

    /// <inheritdoc />
    public void Reset()
    {
        _metrics.Reset();
    }

    private bool ShouldApplyFallback(ErrorContext errorContext)
    {
        // Check if error type is in the fallback exception types
        if (_config.FallbackExceptionTypes.Count > 0)
        {
            var exceptionType = errorContext.Exception.GetType();
            return _config.FallbackExceptionTypes.Exists(type => type.IsAssignableFrom(exceptionType));
        }

        // Check custom predicate
        if (_config.ShouldFallbackPredicate != null)
        {
            return _config.ShouldFallbackPredicate(errorContext);
        }

        // Default: apply fallback for transient errors
        return errorContext.IsTransient;
    }
}

/// <summary>
/// Configuration for fallback policy
/// </summary>
public class FallbackConfiguration
{
    /// <summary>
    /// Whether the fallback policy is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Action to execute when fallback is triggered
    /// </summary>
    public Func<ErrorContext, CancellationToken, Task>? FallbackAction { get; set; }

    /// <summary>
    /// Function to execute when fallback is triggered (for functions with return values)
    /// </summary>
    public Func<ErrorContext, CancellationToken, Task<object>>? FallbackFunc { get; set; }

    /// <summary>
    /// Default value to return when fallback is triggered
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// List of exception types that should trigger fallback
    /// </summary>
    public List<Type> FallbackExceptionTypes { get; set; } = new();

    /// <summary>
    /// Custom predicate to determine if fallback should be applied
    /// </summary>
    public Func<ErrorContext, bool>? ShouldFallbackPredicate { get; set; }

    /// <summary>
    /// Whether to throw an exception if the fallback itself fails
    /// </summary>
    public bool ThrowOnFallbackFailure { get; set; } = true;

    /// <summary>
    /// Adds an exception type that should trigger fallback
    /// </summary>
    public FallbackConfiguration AddFallbackException<T>() where T : Exception
    {
        FallbackExceptionTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Sets the fallback action for void operations
    /// </summary>
    public FallbackConfiguration WithFallbackAction(Func<ErrorContext, CancellationToken, Task> fallbackAction)
    {
        FallbackAction = fallbackAction;
        return this;
    }

    /// <summary>
    /// Sets the fallback function for operations with return values
    /// </summary>
    public FallbackConfiguration WithFallbackFunc(Func<ErrorContext, CancellationToken, Task<object>> fallbackFunc)
    {
        FallbackFunc = fallbackFunc;
        return this;
    }

    /// <summary>
    /// Sets a default value to return when fallback is triggered
    /// </summary>
    public FallbackConfiguration WithDefaultValue(object defaultValue)
    {
        DefaultValue = defaultValue;
        return this;
    }

    /// <summary>
    /// Sets a custom predicate for determining when to apply fallback
    /// </summary>
    public FallbackConfiguration WithFallbackPredicate(Func<ErrorContext, bool> predicate)
    {
        ShouldFallbackPredicate = predicate;
        return this;
    }
}