using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Pipeline behavior that implements rate limiting to control message processing throughput
/// </summary>
public class RateLimitingBehavior : IPipelineBehavior, IDisposable
{
    private readonly ILogger<RateLimitingBehavior> _logger;
    private readonly RateLimitingBehaviorOptions _options;
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the RateLimitingBehavior class
    /// </summary>
    public RateLimitingBehavior(ILogger<RateLimitingBehavior> logger, RateLimitingBehaviorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new RateLimitingBehaviorOptions();
        _limiters = new ConcurrentDictionary<string, RateLimiter>();
    }

    /// <summary>
    /// Executes rate limiting logic before proceeding with the pipeline
    /// </summary>
    public async Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RateLimitingBehavior));
        }

        // Check if rate limiting is disabled for this context
        if (context.GetProperty<bool>("RateLimitingDisabled"))
        {
            return await next.ProceedAsync(context);
        }

        // Get the rate limiter for this request
        var rateLimiterKey = GetRateLimiterKey(context);
        var rateLimiter = GetOrCreateRateLimiter(rateLimiterKey, context);

        var startTime = DateTimeOffset.UtcNow;

        // Attempt to acquire a permit
        using var lease = await rateLimiter.AcquireAsync(permitCount: 1, context.CancellationToken);

        if (lease.IsAcquired)
        {
            var waitTime = DateTimeOffset.UtcNow - startTime;
            if (waitTime > TimeSpan.FromMilliseconds(100))
            {
                _logger.LogDebug("Acquired rate limit permit after waiting {WaitTime}ms for {MessageType} with key {RateLimiterKey}",
                    waitTime.TotalMilliseconds, context.Message?.GetType().Name ?? "Unknown", rateLimiterKey);
            }

            context.SetProperty("RateLimitAcquired", true);
            context.SetProperty("RateLimitWaitTime", waitTime);
            context.SetProperty("RateLimiterKey", rateLimiterKey);

            return await next.ProceedAsync(context);
        }
        else
        {
            var waitTime = DateTimeOffset.UtcNow - startTime;
            _logger.LogWarning("Rate limit exceeded for {MessageType} with key {RateLimiterKey} after waiting {WaitTime}ms",
                context.Message?.GetType().Name ?? "Unknown", rateLimiterKey, waitTime.TotalMilliseconds);

            context.SetProperty("RateLimitExceeded", true);
            context.SetProperty("RateLimitWaitTime", waitTime);
            context.SetProperty("RateLimiterKey", rateLimiterKey);

            if (_options.ThrowOnRateLimitExceeded)
            {
                throw new RateLimitExceededException($"Rate limit exceeded for {context.Message?.GetType().Name} with key {rateLimiterKey}");
            }

            if (_options.ReturnErrorOnRateLimitExceeded)
            {
                return new RateLimitErrorResult
                {
                    IsRateLimited = true,
                    RateLimiterKey = rateLimiterKey,
                    MessageType = context.Message?.GetType().Name ?? "Unknown",
                    RetryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterMetadata) ? retryAfterMetadata : null
                };
            }

            // If configured to wait, this would have already happened in AcquireAsync
            // So we return null to indicate the request was not processed
            return null;
        }
    }

    private string GetRateLimiterKey(PipelineContext context)
    {
        if (_options.RateLimiterKeyGenerator != null)
        {
            return _options.RateLimiterKeyGenerator(context);
        }

        // Default key generation strategy
        var messageType = context.Message?.GetType().Name ?? "Unknown";
        var userId = context.GetProperty<string>("UserId") ?? "anonymous";
        var tenantId = context.GetProperty<string>("TenantId") ?? "default";

        return _options.RateLimitScope switch
        {
            RateLimitScope.Global => "global",
            RateLimitScope.PerUser => $"user:{userId}",
            RateLimitScope.PerMessageType => $"messageType:{messageType}",
            RateLimitScope.PerUserAndMessageType => $"user:{userId}:messageType:{messageType}",
            RateLimitScope.PerTenant => $"tenant:{tenantId}",
            RateLimitScope.PerTenantAndUser => $"tenant:{tenantId}:user:{userId}",
            _ => "global"
        };
    }

    private RateLimiter GetOrCreateRateLimiter(string key, PipelineContext context)
    {
        return _limiters.GetOrAdd(key, _ => CreateRateLimiter(context));
    }

    private RateLimiter CreateRateLimiter(PipelineContext context)
    {
        return _options.RateLimiterType switch
        {
            RateLimiterType.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = _options.TokenLimit,
                QueueProcessingOrder = _options.QueueProcessingOrder,
                QueueLimit = _options.QueueLimit,
                ReplenishmentPeriod = _options.ReplenishmentPeriod,
                TokensPerPeriod = _options.TokensPerPeriod,
                AutoReplenishment = _options.AutoReplenishment
            }),

            RateLimiterType.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                QueueProcessingOrder = _options.QueueProcessingOrder,
                QueueLimit = _options.QueueLimit,
                Window = _options.Window,
                AutoReplenishment = _options.AutoReplenishment
            }),

            RateLimiterType.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                QueueProcessingOrder = _options.QueueProcessingOrder,
                QueueLimit = _options.QueueLimit,
                Window = _options.Window,
                SegmentsPerWindow = _options.SegmentsPerWindow,
                AutoReplenishment = _options.AutoReplenishment
            }),

            RateLimiterType.Concurrency => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                QueueProcessingOrder = _options.QueueProcessingOrder,
                QueueLimit = _options.QueueLimit
            }),

            _ => throw new ArgumentException($"Unsupported rate limiter type: {_options.RateLimiterType}")
        };
    }

    /// <summary>
    /// Disposes all rate limiters
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var limiter in _limiters.Values)
            {
                limiter.Dispose();
            }
            _limiters.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for the rate limiting behavior
/// </summary>
public class RateLimitingBehaviorOptions
{
    /// <summary>
    /// Type of rate limiter to use
    /// </summary>
    public RateLimiterType RateLimiterType { get; set; } = RateLimiterType.TokenBucket;

    /// <summary>
    /// Scope of rate limiting
    /// </summary>
    public RateLimitScope RateLimitScope { get; set; } = RateLimitScope.PerUser;

    /// <summary>
    /// Whether to throw an exception when rate limit is exceeded
    /// </summary>
    public bool ThrowOnRateLimitExceeded { get; set; } = true;

    /// <summary>
    /// Whether to return an error result when rate limit is exceeded
    /// </summary>
    public bool ReturnErrorOnRateLimitExceeded { get; set; } = false;

    /// <summary>
    /// Custom rate limiter key generator
    /// </summary>
    public Func<PipelineContext, string>? RateLimiterKeyGenerator { get; set; }

    // Token Bucket options
    /// <summary>
    /// Maximum number of tokens in the bucket
    /// </summary>
    public int TokenLimit { get; set; } = 100;

    /// <summary>
    /// Number of tokens added per replenishment period
    /// </summary>
    public int TokensPerPeriod { get; set; } = 10;

    /// <summary>
    /// Period for token replenishment
    /// </summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);

    // Fixed/Sliding Window options
    /// <summary>
    /// Maximum number of permits per window
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Duration of the rate limiting window
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Number of segments per window (for sliding window)
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 8;

    // Queue options
    /// <summary>
    /// Maximum number of queued requests
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Order for processing queued requests
    /// </summary>
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;

    /// <summary>
    /// Whether to automatically replenish permits/tokens
    /// </summary>
    public bool AutoReplenishment { get; set; } = true;
}

/// <summary>
/// Types of rate limiters available
/// </summary>
public enum RateLimiterType
{
    /// <summary>
    /// Token bucket rate limiter
    /// </summary>
    TokenBucket,

    /// <summary>
    /// Fixed window rate limiter
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Sliding window rate limiter
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// Concurrency limiter
    /// </summary>
    Concurrency
}

/// <summary>
/// Scopes for rate limiting
/// </summary>
public enum RateLimitScope
{
    /// <summary>
    /// Global rate limiting across all requests
    /// </summary>
    Global,

    /// <summary>
    /// Rate limiting per user
    /// </summary>
    PerUser,

    /// <summary>
    /// Rate limiting per message type
    /// </summary>
    PerMessageType,

    /// <summary>
    /// Rate limiting per user and message type combination
    /// </summary>
    PerUserAndMessageType,

    /// <summary>
    /// Rate limiting per tenant
    /// </summary>
    PerTenant,

    /// <summary>
    /// Rate limiting per tenant and user combination
    /// </summary>
    PerTenantAndUser
}

/// <summary>
/// Exception thrown when rate limit is exceeded
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class
    /// </summary>
    public RateLimitExceededException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class
    /// </summary>
    public RateLimitExceededException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Result returned when rate limit is exceeded and ReturnErrorOnRateLimitExceeded is true
/// </summary>
public class RateLimitErrorResult
{
    /// <summary>
    /// Whether this result indicates rate limiting occurred
    /// </summary>
    public bool IsRateLimited { get; set; }

    /// <summary>
    /// The rate limiter key that was exceeded
    /// </summary>
    public string RateLimiterKey { get; set; } = string.Empty;

    /// <summary>
    /// Type of the message that was rate limited
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Time to wait before retrying (if available)
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}