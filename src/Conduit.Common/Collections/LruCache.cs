using System.Collections.Concurrent;

namespace Conduit.Common.Collections;

/// <summary>
/// Thread-safe Least Recently Used (LRU) cache implementation.
/// </summary>
/// <typeparam name="TKey">The type of cache keys</typeparam>
/// <typeparam name="TValue">The type of cache values</typeparam>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly ReaderWriterLockSlim _lock;
    private readonly TimeSpan? _defaultExpiration;

    /// <summary>
    /// Initializes a new instance of the LruCache class.
    /// </summary>
    /// <param name="capacity">The maximum number of items in the cache</param>
    /// <param name="defaultExpiration">Default expiration time for cache items</param>
    public LruCache(int capacity, TimeSpan? defaultExpiration = null)
    {
        Guard.InRange(capacity, 1, int.MaxValue);

        _capacity = capacity;
        _defaultExpiration = defaultExpiration;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    /// <summary>
    /// Gets the number of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _cache.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the cache.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets a value indicating whether the cache is full.
    /// </summary>
    public bool IsFull => Count >= _capacity;

    /// <summary>
    /// Gets or sets a cache item.
    /// </summary>
    public TValue? this[TKey key]
    {
        get => TryGet(key, out var value) ? value : default;
        set
        {
            if (value != null)
                Set(key, value);
        }
    }

    /// <summary>
    /// Tries to get a value from the cache.
    /// </summary>
    public bool TryGet(TKey key, out TValue? value)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Check expiration
                if (node.Value.ExpiresAt.HasValue && node.Value.ExpiresAt.Value < DateTimeOffset.UtcNow)
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        RemoveNode(node);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                    value = default;
                    return false;
                }

                value = node.Value.Value;

                // Move to front (most recently used)
                _lock.EnterWriteLock();
                try
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    node.Value.LastAccessed = DateTimeOffset.UtcNow;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                return true;
            }

            value = default;
            return false;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Gets a value from the cache, or adds it if not present.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan? expiration = null)
    {
        Guard.NotNull(valueFactory);

        if (TryGet(key, out var existingValue) && existingValue != null)
            return existingValue;

        var value = valueFactory(key);
        Set(key, value, expiration);
        return value;
    }

    /// <summary>
    /// Gets a value from the cache asynchronously, or adds it if not present.
    /// </summary>
    public async Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, Task<TValue>> valueFactory,
        TimeSpan? expiration = null)
    {
        Guard.NotNull(valueFactory);

        if (TryGet(key, out var existingValue) && existingValue != null)
            return existingValue;

        var value = await valueFactory(key).ConfigureAwait(false);
        Set(key, value, expiration);
        return value;
    }

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    public void Set(TKey key, TValue value, TimeSpan? expiration = null)
    {
        var expiresAt = expiration.HasValue || _defaultExpiration.HasValue
            ? DateTimeOffset.UtcNow + (expiration ?? _defaultExpiration!.Value)
            : (DateTimeOffset?)null;

        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing item
                _lruList.Remove(existingNode);
                existingNode.Value.Value = value;
                existingNode.Value.LastAccessed = DateTimeOffset.UtcNow;
                existingNode.Value.ExpiresAt = expiresAt;
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // Add new item
                var cacheItem = new CacheItem(key, value, expiresAt);
                var node = _lruList.AddFirst(cacheItem);
                _cache[key] = node;

                // Remove least recently used if at capacity
                if (_cache.Count > _capacity)
                {
                    var lru = _lruList.Last;
                    if (lru != null)
                    {
                        RemoveNode(lru);
                    }
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryRemove(key, out var node))
            {
                _lruList.Remove(node);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Determines whether the cache contains a key.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        _lock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Check expiration
                if (node.Value.ExpiresAt.HasValue && node.Value.ExpiresAt.Value < DateTimeOffset.UtcNow)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all keys in the cache.
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _cache.Keys.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets all values in the cache.
    /// </summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _lruList
                    .Where(item => !item.IsExpired)
                    .Select(item => item.Value)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Removes expired items from the cache.
    /// </summary>
    public int RemoveExpired()
    {
        _lock.EnterWriteLock();
        try
        {
            var expiredNodes = _lruList
                .Where(item => item.IsExpired)
                .Select(item => _cache[item.Key])
                .ToList();

            foreach (var node in expiredNodes)
            {
                RemoveNode(node);
            }

            return expiredNodes.Count;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        _lock.EnterReadLock();
        try
        {
            return new CacheStatistics
            {
                Count = _cache.Count,
                Capacity = _capacity,
                OldestAccess = _lruList.Last?.Value.LastAccessed,
                NewestAccess = _lruList.First?.Value.LastAccessed
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void RemoveNode(LinkedListNode<CacheItem> node)
    {
        _cache.TryRemove(node.Value.Key, out _);
        _lruList.Remove(node);
    }

    private sealed class CacheItem
    {
        public CacheItem(TKey key, TValue value, DateTimeOffset? expiresAt)
        {
            Key = key;
            Value = value;
            LastAccessed = DateTimeOffset.UtcNow;
            ExpiresAt = expiresAt;
        }

        public TKey Key { get; }
        public TValue Value { get; set; }
        public DateTimeOffset LastAccessed { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Represents cache statistics.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Gets or sets the number of items in the cache.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the cache capacity.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Gets or sets the oldest access time.
        /// </summary>
        public DateTimeOffset? OldestAccess { get; set; }

        /// <summary>
        /// Gets or sets the newest access time.
        /// </summary>
        public DateTimeOffset? NewestAccess { get; set; }

        /// <summary>
        /// Gets the cache utilization percentage.
        /// </summary>
        public double UtilizationPercentage => Capacity > 0 ? (double)Count / Capacity * 100 : 0;
    }
}