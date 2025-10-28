using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Pipeline.Composition
{
    /// <summary>
    /// A pipeline that caches results to avoid recomputation.
    /// Implements the Cache pattern from Enterprise Integration Patterns.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    public class CachingPipeline<TInput, TOutput> : IPipeline<TInput, TOutput>
    {
        private readonly IPipeline<TInput, TOutput> _innerPipeline;
        private readonly Func<TInput, string> _cacheKeyExtractor;
        private readonly ICache<string, TOutput> _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly CacheEvictionPolicy _evictionPolicy;
        private readonly bool _refreshOnAccess;
        private readonly SemaphoreSlim _cacheLock;

        /// <summary>
        /// Initializes a new instance of the CachingPipeline class.
        /// </summary>
        /// <param name="innerPipeline">The inner pipeline to wrap</param>
        /// <param name="cacheKeyExtractor">Function to extract cache key from input</param>
        /// <param name="cacheDuration">How long to cache results</param>
        /// <param name="cache">Optional custom cache implementation</param>
        /// <param name="evictionPolicy">Cache eviction policy</param>
        /// <param name="refreshOnAccess">Whether to refresh TTL on cache hits</param>
        public CachingPipeline(
            IPipeline<TInput, TOutput> innerPipeline,
            Func<TInput, string> cacheKeyExtractor,
            TimeSpan cacheDuration,
            ICache<string, TOutput>? cache = null,
            CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.LRU,
            bool refreshOnAccess = false)
        {
            _ = Guard.NotNull(innerPipeline, nameof(innerPipeline));
            _ = Guard.NotNull(cacheKeyExtractor, nameof(cacheKeyExtractor));
            Guard.Against(cacheDuration.TotalSeconds < 0, nameof(cacheDuration), "Duration cannot be negative");

            _innerPipeline = innerPipeline;
            _cacheKeyExtractor = cacheKeyExtractor;
            _cacheDuration = cacheDuration;
            _cache = cache ?? new InMemoryCache<string, TOutput>(evictionPolicy);
            _evictionPolicy = evictionPolicy;
            _refreshOnAccess = refreshOnAccess;
            _cacheLock = new SemaphoreSlim(1, 1);
        }

        /// <inheritdoc />
        public PipelineMetadata Metadata
        {
            get
            {
                var innerMetadata = _innerPipeline.Metadata;
                return new PipelineMetadata
                {
                    PipelineId = Guid.NewGuid().ToString(),
                    Name = $"{innerMetadata.Name} -> Cached",
                    Description = $"Cached execution of {innerMetadata.Name} with TTL {_cacheDuration}",
                    Type = innerMetadata.Type,
                    Version = innerMetadata.Version,
                    Stages = new List<string> { "Cache Check" }.Concat(innerMetadata.Stages).ToList()
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration
        {
            get
            {
                var config = _innerPipeline.Configuration.Clone();
                config.CacheEnabled = true;
                return config;
            }
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheKeyExtractor(input);

            // Try to get from cache
            if (_cache.TryGet(cacheKey, out var cachedResult))
            {
                if (_refreshOnAccess)
                {
                    _cache.Set(cacheKey, cachedResult, _cacheDuration);
                }
                return cachedResult;
            }

            // Use double-checked locking pattern for cache population
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                // Check again after acquiring lock
                if (_cache.TryGet(cacheKey, out cachedResult))
                {
                    return cachedResult;
                }

                // Execute the inner pipeline
                var result = await _innerPipeline.ExecuteAsync(input, cancellationToken);

                // Store in cache
                _cache.Set(cacheKey, result, _cacheDuration);

                return result;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheKeyExtractor(input);
            context.SetProperty("CachingPipeline.CacheKey", cacheKey);

            // Try to get from cache
            if (_cache.TryGet(cacheKey, out var cachedResult))
            {
                context.SetProperty("CachingPipeline.CacheHit", true);
                context.SetProperty("CachingPipeline.Stage", "CacheHit");

                if (_refreshOnAccess)
                {
                    _cache.Set(cacheKey, cachedResult, _cacheDuration);
                    context.SetProperty("CachingPipeline.RefreshedTTL", true);
                }

                return cachedResult;
            }

            context.SetProperty("CachingPipeline.CacheHit", false);
            context.SetProperty("CachingPipeline.Stage", "CacheMiss");

            // Use double-checked locking pattern for cache population
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                // Check again after acquiring lock
                if (_cache.TryGet(cacheKey, out cachedResult))
                {
                    context.SetProperty("CachingPipeline.CacheHit", true);
                    context.SetProperty("CachingPipeline.Stage", "CacheHitAfterLock");
                    return cachedResult;
                }

                context.SetProperty("CachingPipeline.Stage", "Executing");

                // Execute the inner pipeline
                var result = await _innerPipeline.ExecuteAsync(input, context, cancellationToken);

                // Store in cache
                _cache.Set(cacheKey, result, _cacheDuration);
                context.SetProperty("CachingPipeline.Stage", "Cached");
                context.SetProperty("CachingPipeline.CachedAt", DateTimeOffset.UtcNow);
                context.SetProperty("CachingPipeline.CacheExpiry", DateTimeOffset.UtcNow.Add(_cacheDuration));

                return result;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Invalidates a specific cache entry.
        /// </summary>
        /// <param name="input">The input to generate the cache key from</param>
        public void Invalidate(TInput input)
        {
            var cacheKey = _cacheKeyExtractor(input);
            _cache.Remove(cacheKey);
        }

        /// <summary>
        /// Invalidates a specific cache entry by key.
        /// </summary>
        /// <param name="cacheKey">The cache key</param>
        public void InvalidateByKey(string cacheKey)
        {
            _cache.Remove(cacheKey);
        }

        /// <summary>
        /// Clears all cached entries.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return _cache.GetStatistics();
        }

        // Interface implementation methods
        public IPipeline<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
            return this;
        }

        public void AddBehavior(IBehaviorContribution behavior)
        {
            _innerPipeline.AddBehavior(behavior);
        }

        public void AddStage(IPipelineStage<object, object> stage)
        {
            _innerPipeline.AddStage(stage);
        }

        public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
        {
            _innerPipeline.SetErrorHandler(errorHandler);
        }

        public void SetCompletionHandler(Action<TOutput> completionHandler)
        {
            _innerPipeline.SetCompletionHandler(completionHandler);
        }

        public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            // This pipeline is already configured for caching
            // Could potentially update the configuration if needed
        }


        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            // Return self if cache configuration matches, otherwise create new instance
            if (_cacheKeyExtractor.Equals(cacheKeySelector) && _cacheDuration == cacheDuration)
            {
                return this;
            }
            return new CachingPipeline<TInput, TOutput>(_innerPipeline, cacheKeySelector, cacheDuration);
        }

        // Missing IPipeline interface methods

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not yet implemented for CachingPipeline. Apply transformations to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not yet implemented for CachingPipeline. Apply transformations to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not yet implemented for CachingPipeline. Apply chaining to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not yet implemented for CachingPipeline. Apply transformations to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not yet implemented for CachingPipeline. Apply transformations to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
        {
            throw new NotImplementedException("Filter operation is not yet implemented for CachingPipeline. Apply filtering to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not yet implemented for CachingPipeline. Apply filtering to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> Branch(
            Predicate<TOutput> condition,
            IPipeline<TOutput, TOutput> trueBranch,
            IPipeline<TOutput, TOutput> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not yet implemented for CachingPipeline. Apply branching to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not yet implemented for CachingPipeline. Apply error handling to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not yet implemented for CachingPipeline. Apply error handling to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not yet implemented for CachingPipeline. Apply retry logic to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not yet implemented for CachingPipeline. Apply retry logic to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not yet implemented for CachingPipeline. Apply timeout to the inner pipeline before caching.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration)
        {
            if (_cacheDuration == cacheDuration)
            {
                return this;
            }
            return new CachingPipeline<TInput, TOutput>(_innerPipeline, _cacheKeyExtractor, cacheDuration);
        }

        /// <inheritdoc />
        public IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, TInput> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not directly supported on CachingPipeline. Apply caching to the parallel pipeline instead.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
            where TStageOutput : TOutput
        {
            return new CachingPipeline<TInput, TOutput>(
                _innerPipeline.AddStage(stage),
                _cacheKeyExtractor,
                _cacheDuration);
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineInterceptor> GetInterceptors()
        {
            return _innerPipeline.GetInterceptors();
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineStage<object, object>> GetStages()
        {
            return _innerPipeline.GetStages();
        }
    }

    /// <summary>
    /// Cache eviction policies.
    /// </summary>
    public enum CacheEvictionPolicy
    {
        /// <summary>Least Recently Used</summary>
        LRU,
        /// <summary>Least Frequently Used</summary>
        LFU,
        /// <summary>First In First Out</summary>
        FIFO,
        /// <summary>Time To Live only</summary>
        TTL
    }

    /// <summary>
    /// Interface for cache implementations.
    /// </summary>
    /// <typeparam name="TKey">Cache key type</typeparam>
    /// <typeparam name="TValue">Cached value type</typeparam>
    public interface ICache<TKey, TValue>
    {
        bool TryGet(TKey key, out TValue value);
        void Set(TKey key, TValue value, TimeSpan expiration);
        void Remove(TKey key);
        void Clear();
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// In-memory cache implementation.
    /// </summary>
    public class InMemoryCache<TKey, TValue> : ICache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _cache;
        private readonly CacheEvictionPolicy _evictionPolicy;
        private readonly int _maxSize;
        private long _hits;
        private long _misses;
        private long _evictions;

        public InMemoryCache(CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.LRU, int maxSize = 1000)
        {
            _cache = new ConcurrentDictionary<TKey, CacheEntry<TValue>>();
            _evictionPolicy = evictionPolicy;
            _maxSize = maxSize;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    entry.LastAccessed = DateTimeOffset.UtcNow;
                    entry.AccessCount++;
                    Interlocked.Increment(ref _hits);
                    value = entry.Value;
                    return true;
                }
                else
                {
                    // Entry expired, remove it
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _evictions);
                }
            }

            Interlocked.Increment(ref _misses);
            value = default!;
            return false;
        }

        public void Set(TKey key, TValue value, TimeSpan expiration)
        {
            var entry = new CacheEntry<TValue>
            {
                Value = value,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessed = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(expiration),
                AccessCount = 0
            };

            // Check if we need to evict
            if (_cache.Count >= _maxSize && !_cache.ContainsKey(key))
            {
                EvictEntry();
            }

            _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);
        }

        public void Remove(TKey key)
        {
            if (_cache.TryRemove(key, out _))
            {
                Interlocked.Increment(ref _evictions);
            }
        }

        public void Clear()
        {
            var count = _cache.Count;
            _cache.Clear();
            Interlocked.Add(ref _evictions, count);
        }

        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                Hits = _hits,
                Misses = _misses,
                Evictions = _evictions,
                Size = _cache.Count,
                HitRatio = _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0
            };
        }

        private void EvictEntry()
        {
            if (_cache.IsEmpty) return;

            KeyValuePair<TKey, CacheEntry<TValue>> toEvict;

            switch (_evictionPolicy)
            {
                case CacheEvictionPolicy.LRU:
                    toEvict = _cache.OrderBy(kvp => kvp.Value.LastAccessed).First();
                    break;
                case CacheEvictionPolicy.LFU:
                    toEvict = _cache.OrderBy(kvp => kvp.Value.AccessCount).First();
                    break;
                case CacheEvictionPolicy.FIFO:
                    toEvict = _cache.OrderBy(kvp => kvp.Value.CreatedAt).First();
                    break;
                case CacheEvictionPolicy.TTL:
                    toEvict = _cache.OrderBy(kvp => kvp.Value.ExpiresAt).First();
                    break;
                default:
                    toEvict = _cache.First();
                    break;
            }

            if (_cache.TryRemove(toEvict.Key, out _))
            {
                Interlocked.Increment(ref _evictions);
            }
        }

        private class CacheEntry<T>
        {
            public T Value { get; set; } = default!;
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset LastAccessed { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
            public long AccessCount { get; set; }
        }
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public class CacheStatistics
    {
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long Evictions { get; set; }
        public int Size { get; set; }
        public double HitRatio { get; set; }

        public override string ToString()
        {
            return $"Cache Stats: Hits={Hits}, Misses={Misses}, HitRatio={HitRatio:P2}, Size={Size}, Evictions={Evictions}";
        }
    }

    /// <summary>
    /// Extension methods for creating caching pipelines.
    /// </summary>
    public static class CachingPipelineExtensions
    {
        /// <summary>
        /// Creates a pipeline with caching.
        /// </summary>
        public static IPipeline<TInput, TOutput> WithCache<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TInput, string> cacheKeyExtractor,
            TimeSpan duration)
        {
            return new CachingPipeline<TInput, TOutput>(pipeline, cacheKeyExtractor, duration);
        }

        /// <summary>
        /// Creates a pipeline with advanced caching options.
        /// </summary>
        public static IPipeline<TInput, TOutput> WithAdvancedCache<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TInput, string> cacheKeyExtractor,
            TimeSpan duration,
            CacheEvictionPolicy evictionPolicy,
            bool refreshOnAccess = false)
        {
            return new CachingPipeline<TInput, TOutput>(
                pipeline,
                cacheKeyExtractor,
                duration,
                evictionPolicy: evictionPolicy,
                refreshOnAccess: refreshOnAccess);
        }

        /// <summary>
        /// Creates a pipeline with a custom cache implementation.
        /// </summary>
        public static IPipeline<TInput, TOutput> WithCustomCache<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TInput, string> cacheKeyExtractor,
            TimeSpan duration,
            ICache<string, TOutput> cache)
        {
            return new CachingPipeline<TInput, TOutput>(pipeline, cacheKeyExtractor, duration, cache);
        }

        /// <summary>
        /// Creates a pipeline that caches based on all input properties.
        /// </summary>
        public static IPipeline<TInput, TOutput> WithAutoCache<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            TimeSpan duration)
        {
            return pipeline.WithCache(
                input => System.Text.Json.JsonSerializer.Serialize(input),
                duration);
        }
    }
}