namespace Conduit.Api;

/// <summary>
/// Configuration settings for a pipeline.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets whether the pipeline is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the pipeline should execute asynchronously.
    /// </summary>
    public bool AsyncExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum degree of concurrency for parallel operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the timeout for pipeline operations.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to preserve order in parallel operations.
    /// </summary>
    public bool PreserveOrder { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fail fast on errors.
    /// </summary>
    public bool FailFast { get; set; } = false;

    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool CacheEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the concurrency semaphore for limiting parallel execution.
    /// </summary>
    public SemaphoreSlim? ConcurrencySemaphore { get; set; }

    /// <summary>
    /// Gets or sets the error handling strategy.
    /// </summary>
    public ErrorHandlingStrategy ErrorStrategy { get; set; } = ErrorHandlingStrategy.FailFast;

    /// <summary>
    /// Gets or sets the default timeout for operations.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default cache duration.
    /// </summary>
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether tracing is enabled.
    /// </summary>
    public bool TracingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum cache size.
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether validation is enabled.
    /// </summary>
    public bool ValidationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether dead letter handling is enabled.
    /// </summary>
    public bool DeadLetterEnabled { get; set; } = false;

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public PipelineConfiguration Clone()
    {
        return new PipelineConfiguration
        {
            IsEnabled = IsEnabled,
            AsyncExecution = AsyncExecution,
            MaxConcurrency = MaxConcurrency,
            Timeout = Timeout,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay,
            PreserveOrder = PreserveOrder,
            FailFast = FailFast,
            CacheEnabled = CacheEnabled,
            ConcurrencySemaphore = ConcurrencySemaphore,
            ErrorStrategy = ErrorStrategy,
            DefaultTimeout = DefaultTimeout,
            DefaultCacheDuration = DefaultCacheDuration,
            MetricsEnabled = MetricsEnabled,
            TracingEnabled = TracingEnabled,
            MaxCacheSize = MaxCacheSize,
            ValidationEnabled = ValidationEnabled,
            DeadLetterEnabled = DeadLetterEnabled
        };
    }

    /// <summary>
    /// Gets a default configuration instance.
    /// </summary>
    public static PipelineConfiguration Default => new PipelineConfiguration();

    /// <summary>
    /// Gets a reliable configuration instance with retries enabled.
    /// </summary>
    public static PipelineConfiguration Reliable => new PipelineConfiguration
    {
        MaxRetries = 3,
        RetryDelay = TimeSpan.FromSeconds(2),
        FailFast = false,
        Timeout = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// Gets a high throughput configuration optimized for performance.
    /// </summary>
    public static PipelineConfiguration HighThroughput => new PipelineConfiguration
    {
        MaxConcurrency = Environment.ProcessorCount * 2,
        AsyncExecution = true,
        PreserveOrder = false,
        FailFast = true,
        MaxRetries = 0
    };
}