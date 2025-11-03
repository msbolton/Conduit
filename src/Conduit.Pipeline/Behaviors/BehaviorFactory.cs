using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Factory for creating and configuring pipeline behaviors
/// </summary>
public static class BehaviorFactory
{
    /// <summary>
    /// Creates a logging behavior with the specified options
    /// </summary>
    public static IPipelineBehavior CreateLoggingBehavior(
        ILogger<LoggingBehavior> logger,
        Action<LoggingBehaviorOptions>? configure = null)
    {
        var options = new LoggingBehaviorOptions();
        configure?.Invoke(options);
        return new LoggingBehavior(logger, options);
    }

    /// <summary>
    /// Creates a validation behavior with the specified options
    /// </summary>
    public static IPipelineBehavior CreateValidationBehavior(
        ILogger<ValidationBehavior> logger,
        Action<ValidationBehaviorOptions>? configure = null)
    {
        var options = new ValidationBehaviorOptions();
        configure?.Invoke(options);
        return new ValidationBehavior(logger, options);
    }

    /// <summary>
    /// Creates a caching behavior with the specified options
    /// </summary>
    public static IPipelineBehavior CreateCachingBehavior(
        IMemoryCache cache,
        ILogger<CachingBehavior> logger,
        Action<CachingBehaviorOptions>? configure = null)
    {
        var options = new CachingBehaviorOptions();
        configure?.Invoke(options);
        return new CachingBehavior(cache, logger, options);
    }

    /// <summary>
    /// Creates a retry behavior with the specified options
    /// </summary>
    public static IPipelineBehavior CreateRetryBehavior(
        ILogger<RetryBehavior> logger,
        Action<RetryBehaviorOptions>? configure = null)
    {
        var options = new RetryBehaviorOptions();
        configure?.Invoke(options);
        return new RetryBehavior(logger, options);
    }

    /// <summary>
    /// Creates a rate limiting behavior with the specified options
    /// </summary>
    public static IPipelineBehavior CreateRateLimitingBehavior(
        ILogger<RateLimitingBehavior> logger,
        Action<RateLimitingBehaviorOptions>? configure = null)
    {
        var options = new RateLimitingBehaviorOptions();
        configure?.Invoke(options);
        return new RateLimitingBehavior(logger, options);
    }

    /// <summary>
    /// Creates a behavior that executes an action before proceeding
    /// </summary>
    public static IPipelineBehavior CreateActionBehavior(Action<PipelineContext> action)
    {
        return DelegatingBehavior.Create(action);
    }

    /// <summary>
    /// Creates a behavior that executes an async action before proceeding
    /// </summary>
    public static IPipelineBehavior CreateAsyncActionBehavior(Func<PipelineContext, Task> asyncAction)
    {
        return DelegatingBehavior.Create(asyncAction);
    }

    /// <summary>
    /// Creates a behavior that conditionally executes another behavior
    /// </summary>
    public static IPipelineBehavior CreateConditionalBehavior(
        IPipelineBehavior behavior,
        Predicate<PipelineContext> condition)
    {
        return behavior.Conditional(condition);
    }

    /// <summary>
    /// Creates a behavior that measures execution time
    /// </summary>
    public static IPipelineBehavior CreateTimingBehavior(
        IPipelineBehavior behavior,
        Action<TimeSpan, PipelineContext>? onComplete = null)
    {
        return behavior.WithTiming(onComplete);
    }

    /// <summary>
    /// Creates a behavior with error handling
    /// </summary>
    public static IPipelineBehavior CreateErrorHandlingBehavior(
        IPipelineBehavior behavior,
        Func<Exception, PipelineContext, Task<object?>> errorHandler)
    {
        return behavior.WithErrorHandling(errorHandler);
    }

    /// <summary>
    /// Creates a custom behavior from a delegate
    /// </summary>
    public static IPipelineBehavior CreateCustomBehavior(PipelineBehaviorDelegate behaviorDelegate)
    {
        return DelegatingBehavior.Create(behaviorDelegate);
    }
}

/// <summary>
/// Extension methods for easy behavior configuration in dependency injection
/// </summary>
public static class BehaviorServiceCollectionExtensions
{
    /// <summary>
    /// Adds logging behavior to the service collection
    /// </summary>
    public static IServiceCollection AddLoggingBehavior(
        this IServiceCollection services,
        Action<LoggingBehaviorOptions>? configure = null)
    {
        services.AddSingleton<LoggingBehaviorOptions>(provider =>
        {
            var options = new LoggingBehaviorOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddTransient<LoggingBehavior>();
        return services;
    }

    /// <summary>
    /// Adds validation behavior to the service collection
    /// </summary>
    public static IServiceCollection AddValidationBehavior(
        this IServiceCollection services,
        Action<ValidationBehaviorOptions>? configure = null)
    {
        services.AddSingleton<ValidationBehaviorOptions>(provider =>
        {
            var options = new ValidationBehaviorOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddTransient<ValidationBehavior>();
        return services;
    }

    /// <summary>
    /// Adds caching behavior to the service collection
    /// </summary>
    public static IServiceCollection AddCachingBehavior(
        this IServiceCollection services,
        Action<CachingBehaviorOptions>? configure = null)
    {
        services.AddMemoryCache(); // Ensure memory cache is available

        services.AddSingleton<CachingBehaviorOptions>(provider =>
        {
            var options = new CachingBehaviorOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddTransient<CachingBehavior>();
        return services;
    }

    /// <summary>
    /// Adds retry behavior to the service collection
    /// </summary>
    public static IServiceCollection AddRetryBehavior(
        this IServiceCollection services,
        Action<RetryBehaviorOptions>? configure = null)
    {
        services.AddSingleton<RetryBehaviorOptions>(provider =>
        {
            var options = new RetryBehaviorOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddTransient<RetryBehavior>();
        return services;
    }

    /// <summary>
    /// Adds rate limiting behavior to the service collection
    /// </summary>
    public static IServiceCollection AddRateLimitingBehavior(
        this IServiceCollection services,
        Action<RateLimitingBehaviorOptions>? configure = null)
    {
        services.AddSingleton<RateLimitingBehaviorOptions>(provider =>
        {
            var options = new RateLimitingBehaviorOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddSingleton<RateLimitingBehavior>(); // Singleton because it manages state
        return services;
    }

    /// <summary>
    /// Adds all standard behaviors to the service collection
    /// </summary>
    public static IServiceCollection AddStandardBehaviors(this IServiceCollection services)
    {
        return services
            .AddLoggingBehavior()
            .AddValidationBehavior()
            .AddCachingBehavior()
            .AddRetryBehavior()
            .AddRateLimitingBehavior();
    }
}