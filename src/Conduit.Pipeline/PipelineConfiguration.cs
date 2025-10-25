using System;
using System.Threading;

namespace Conduit.Pipeline;

/// <summary>
/// Configuration options for pipeline execution.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets the default timeout for each stage.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to execute the pipeline asynchronously.
    /// </summary>
    public bool AsyncExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether distributed tracing is enabled.
    /// </summary>
    public bool TracingEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum concurrency for parallel execution.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Gets or sets the error handling strategy.
    /// </summary>
    public ErrorHandlingStrategy ErrorStrategy { get; set; } = ErrorHandlingStrategy.FailFast;

    /// <summary>
    /// Gets or sets whether to enable caching.
    /// </summary>
    public bool CacheEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the default cache duration.
    /// </summary>
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum cache size in entries.
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to validate inputs and outputs.
    /// </summary>
    public bool ValidationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable dead letter queue for failed messages.
    /// </summary>
    public bool DeadLetterEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the semaphore for concurrency control.
    /// </summary>
    internal SemaphoreSlim? ConcurrencySemaphore { get; private set; }

    /// <summary>
    /// Initializes concurrency control if needed.
    /// </summary>
    public void InitializeConcurrencyControl()
    {
        if (MaxConcurrency > 0 && ConcurrencySemaphore == null)
        {
            ConcurrencySemaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        }
    }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static PipelineConfiguration Default()
    {
        return new PipelineConfiguration();
    }

    /// <summary>
    /// Creates a high-throughput configuration optimized for performance.
    /// </summary>
    public static PipelineConfiguration HighThroughput()
    {
        return new PipelineConfiguration
        {
            MaxConcurrency = 50,
            DefaultTimeout = TimeSpan.FromSeconds(10),
            ErrorStrategy = ErrorHandlingStrategy.Continue,
            AsyncExecution = true,
            MetricsEnabled = false,
            TracingEnabled = false,
            ValidationEnabled = false,
            CacheEnabled = true,
            DefaultCacheDuration = TimeSpan.FromMinutes(10)
        };
    }

    /// <summary>
    /// Creates a reliable configuration optimized for reliability.
    /// </summary>
    public static PipelineConfiguration Reliable()
    {
        return new PipelineConfiguration
        {
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            ErrorStrategy = ErrorHandlingStrategy.Retry,
            AsyncExecution = true,
            MetricsEnabled = true,
            TracingEnabled = true,
            ValidationEnabled = true,
            DeadLetterEnabled = true,
            DefaultTimeout = TimeSpan.FromMinutes(1),
            MaxConcurrency = 5
        };
    }

    /// <summary>
    /// Creates a configuration for development/debugging.
    /// </summary>
    public static PipelineConfiguration Development()
    {
        return new PipelineConfiguration
        {
            AsyncExecution = false, // Synchronous for easier debugging
            MetricsEnabled = true,
            TracingEnabled = true,
            ValidationEnabled = true,
            ErrorStrategy = ErrorHandlingStrategy.FailFast,
            DefaultTimeout = TimeSpan.FromMinutes(5), // Longer timeout for debugging
            MaxConcurrency = 1
        };
    }

    /// <summary>
    /// Creates a minimal configuration with most features disabled.
    /// </summary>
    public static PipelineConfiguration Minimal()
    {
        return new PipelineConfiguration
        {
            MetricsEnabled = false,
            TracingEnabled = false,
            ValidationEnabled = false,
            CacheEnabled = false,
            DeadLetterEnabled = false,
            ErrorStrategy = ErrorHandlingStrategy.FailFast,
            MaxRetries = 0
        };
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    public PipelineConfiguration Clone()
    {
        return new PipelineConfiguration
        {
            DefaultTimeout = DefaultTimeout,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay,
            AsyncExecution = AsyncExecution,
            MetricsEnabled = MetricsEnabled,
            TracingEnabled = TracingEnabled,
            MaxConcurrency = MaxConcurrency,
            ErrorStrategy = ErrorStrategy,
            CacheEnabled = CacheEnabled,
            DefaultCacheDuration = DefaultCacheDuration,
            MaxCacheSize = MaxCacheSize,
            ValidationEnabled = ValidationEnabled,
            DeadLetterEnabled = DeadLetterEnabled
        };
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        ConcurrencySemaphore?.Dispose();
    }
}

/// <summary>
/// Error handling strategies for pipeline execution.
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>
    /// Stop execution on the first error.
    /// </summary>
    FailFast,

    /// <summary>
    /// Continue execution despite errors.
    /// </summary>
    Continue,

    /// <summary>
    /// Automatically retry on errors.
    /// </summary>
    Retry,

    /// <summary>
    /// Send failed messages to dead letter queue.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Use custom error handler.
    /// </summary>
    Custom
}

/// <summary>
/// Builder for creating pipeline configurations.
/// </summary>
public class PipelineConfigurationBuilder
{
    private readonly PipelineConfiguration _configuration = new();

    /// <summary>
    /// Sets the default timeout.
    /// </summary>
    public PipelineConfigurationBuilder WithTimeout(TimeSpan timeout)
    {
        _configuration.DefaultTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the retry policy.
    /// </summary>
    public PipelineConfigurationBuilder WithRetry(int maxRetries, TimeSpan retryDelay)
    {
        _configuration.MaxRetries = maxRetries;
        _configuration.RetryDelay = retryDelay;
        return this;
    }

    /// <summary>
    /// Sets the error handling strategy.
    /// </summary>
    public PipelineConfigurationBuilder WithErrorStrategy(ErrorHandlingStrategy strategy)
    {
        _configuration.ErrorStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Enables or disables async execution.
    /// </summary>
    public PipelineConfigurationBuilder WithAsyncExecution(bool enabled = true)
    {
        _configuration.AsyncExecution = enabled;
        return this;
    }

    /// <summary>
    /// Enables or disables metrics.
    /// </summary>
    public PipelineConfigurationBuilder WithMetrics(bool enabled = true)
    {
        _configuration.MetricsEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Enables or disables tracing.
    /// </summary>
    public PipelineConfigurationBuilder WithTracing(bool enabled = true)
    {
        _configuration.TracingEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets the maximum concurrency.
    /// </summary>
    public PipelineConfigurationBuilder WithMaxConcurrency(int maxConcurrency)
    {
        _configuration.MaxConcurrency = maxConcurrency;
        return this;
    }

    /// <summary>
    /// Enables caching with the specified duration.
    /// </summary>
    public PipelineConfigurationBuilder WithCache(TimeSpan cacheDuration, int maxSize = 1000)
    {
        _configuration.CacheEnabled = true;
        _configuration.DefaultCacheDuration = cacheDuration;
        _configuration.MaxCacheSize = maxSize;
        return this;
    }

    /// <summary>
    /// Enables or disables validation.
    /// </summary>
    public PipelineConfigurationBuilder WithValidation(bool enabled = true)
    {
        _configuration.ValidationEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Enables or disables dead letter queue.
    /// </summary>
    public PipelineConfigurationBuilder WithDeadLetter(bool enabled = true)
    {
        _configuration.DeadLetterEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Builds the configuration.
    /// </summary>
    public PipelineConfiguration Build()
    {
        _configuration.InitializeConcurrencyControl();
        return _configuration;
    }
}