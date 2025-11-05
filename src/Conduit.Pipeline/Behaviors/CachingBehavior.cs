using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Conduit.Core.Behaviors;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Pipeline behavior that caches query results based on configurable cache keys
/// </summary>
public class CachingBehavior : IPipelineBehavior
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingBehavior> _logger;
    private readonly CachingBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the CachingBehavior class
    /// </summary>
    public CachingBehavior(IMemoryCache cache, ILogger<CachingBehavior> logger, CachingBehaviorOptions? options = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new CachingBehaviorOptions();
    }

    /// <summary>
    /// Executes caching logic around the pipeline execution
    /// </summary>
    public async Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        // Check if caching is disabled for this context
        if (context.GetValueProperty<bool>("CachingDisabled"))
        {
            return await next.ProceedAsync(context);
        }

        // Only cache if this is a cacheable message type
        if (!ShouldCache(context))
        {
            return await next.ProceedAsync(context);
        }

        var cacheKey = GenerateCacheKey(context);

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            context.SetProperty("CacheHit", true);
            context.SetProperty("CacheKey", cacheKey);
            return cachedResult;
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        context.SetProperty("CacheHit", false);
        context.SetProperty("CacheKey", cacheKey);

        // Execute the pipeline
        var result = await next.ProceedAsync(context);

        // Cache the result if it's cacheable
        if (ShouldCacheResult(result, context))
        {
            var cacheOptions = CreateCacheEntryOptions(context);
            _cache.Set(cacheKey, result, cacheOptions);

            _logger.LogDebug("Cached result for key: {CacheKey} with expiration: {Expiration}",
                cacheKey, cacheOptions.AbsoluteExpiration?.ToString() ?? "None");

            context.SetProperty("ResultCached", true);
        }

        return result;
    }

    private bool ShouldCache(PipelineContext context)
    {
        if (context.Input == null) return false;

        var messageType = context.Input.GetType();

        // Check if message type is in the cacheable types
        if (_options.CacheableMessageTypes.Count > 0)
        {
            return _options.CacheableMessageTypes.Contains(messageType);
        }

        // Check if message type is in the excluded types
        if (_options.ExcludedMessageTypes.Contains(messageType))
        {
            return false;
        }

        // Check custom predicate
        if (_options.ShouldCachePredicate != null)
        {
            return _options.ShouldCachePredicate(context);
        }

        // Default: cache if not explicitly excluded
        return true;
    }

    private bool ShouldCacheResult(object? result, PipelineContext context)
    {
        // Don't cache null results if configured not to
        if (result == null && !_options.CacheNullResults)
        {
            return false;
        }

        // Don't cache if there was an exception
        if (context.Exception != null)
        {
            return false;
        }

        // Check custom result predicate
        if (_options.ShouldCacheResultPredicate != null)
        {
            return _options.ShouldCacheResultPredicate(result, context);
        }

        return true;
    }

    private string GenerateCacheKey(PipelineContext context)
    {
        if (_options.CacheKeyGenerator != null)
        {
            return _options.CacheKeyGenerator(context);
        }

        // Default cache key generation
        var messageType = context.Input?.GetType().Name ?? "Unknown";
        var messageContent = SerializeMessage(context.Input);
        var userContext = context.GetProperty<string>("UserId") ?? "anonymous";

        var keyData = $"{messageType}:{userContext}:{messageContent}";

        // Hash the key to ensure it's not too long and is consistent
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyData));
        var hash = Convert.ToBase64String(hashBytes);

        return $"{_options.CacheKeyPrefix}{messageType}:{hash[..16]}"; // Take first 16 chars of hash
    }

    private string SerializeMessage(object? message)
    {
        if (message == null) return "null";

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(message, options);
        }
        catch
        {
            return message.GetType().Name;
        }
    }

    private MemoryCacheEntryOptions CreateCacheEntryOptions(PipelineContext context)
    {
        var options = new MemoryCacheEntryOptions();

        // Set expiration
        if (_options.DefaultAbsoluteExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(_options.DefaultAbsoluteExpiration.Value);
        }

        if (_options.DefaultSlidingExpiration.HasValue)
        {
            options.SetSlidingExpiration(_options.DefaultSlidingExpiration.Value);
        }

        // Set priority
        options.SetPriority(_options.CachePriority);

        // Set size if specified
        if (_options.DefaultCacheSize.HasValue)
        {
            options.SetSize(_options.DefaultCacheSize.Value);
        }

        // Apply custom options if provided
        _options.CacheEntryOptionsConfigurator?.Invoke(options, context);

        return options;
    }
}

/// <summary>
/// Configuration options for the caching behavior
/// </summary>
public class CachingBehaviorOptions
{
    /// <summary>
    /// Prefix for all cache keys
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "conduit:cache:";

    /// <summary>
    /// Default absolute expiration for cache entries
    /// </summary>
    public TimeSpan? DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Default sliding expiration for cache entries
    /// </summary>
    public TimeSpan? DefaultSlidingExpiration { get; set; }

    /// <summary>
    /// Default cache priority
    /// </summary>
    public CacheItemPriority CachePriority { get; set; } = CacheItemPriority.Normal;

    /// <summary>
    /// Default size for cache entries
    /// </summary>
    public long? DefaultCacheSize { get; set; }

    /// <summary>
    /// Whether to cache null results
    /// </summary>
    public bool CacheNullResults { get; set; } = false;

    /// <summary>
    /// List of message types that should be cached (empty means cache all)
    /// </summary>
    public HashSet<Type> CacheableMessageTypes { get; set; } = new();

    /// <summary>
    /// List of message types that should never be cached
    /// </summary>
    public HashSet<Type> ExcludedMessageTypes { get; set; } = new();

    /// <summary>
    /// Custom predicate to determine if a message should be cached
    /// </summary>
    public Func<PipelineContext, bool>? ShouldCachePredicate { get; set; }

    /// <summary>
    /// Custom predicate to determine if a result should be cached
    /// </summary>
    public Func<object?, PipelineContext, bool>? ShouldCacheResultPredicate { get; set; }

    /// <summary>
    /// Custom cache key generator
    /// </summary>
    public Func<PipelineContext, string>? CacheKeyGenerator { get; set; }

    /// <summary>
    /// Custom configurator for cache entry options
    /// </summary>
    public Action<MemoryCacheEntryOptions, PipelineContext>? CacheEntryOptionsConfigurator { get; set; }

    /// <summary>
    /// Adds a message type to the cacheable types list
    /// </summary>
    public CachingBehaviorOptions AddCacheableType<T>()
    {
        CacheableMessageTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Adds a message type to the excluded types list
    /// </summary>
    public CachingBehaviorOptions ExcludeType<T>()
    {
        ExcludedMessageTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Sets a custom cache key generator
    /// </summary>
    public CachingBehaviorOptions WithKeyGenerator(Func<PipelineContext, string> keyGenerator)
    {
        CacheKeyGenerator = keyGenerator;
        return this;
    }

    /// <summary>
    /// Sets a custom predicate for determining what to cache
    /// </summary>
    public CachingBehaviorOptions WithCachePredicate(Func<PipelineContext, bool> predicate)
    {
        ShouldCachePredicate = predicate;
        return this;
    }
}