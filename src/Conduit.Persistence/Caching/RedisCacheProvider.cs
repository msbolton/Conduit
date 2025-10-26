using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Conduit.Persistence.Caching
{
    /// <summary>
    /// Redis cache provider implementation.
    /// </summary>
    public class RedisCacheProvider : ICacheProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new instance of the RedisCacheProvider class.
        /// </summary>
        public RedisCacheProvider(IConnectionMultiplexer redis, int database = -1)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _database = _redis.GetDatabase(database);

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value!, _serializerOptions);
        }

        public async Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var serialized = JsonSerializer.Serialize(value, _serializerOptions);
            await _database.StringSetAsync(key, serialized, expiration);
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            return await _database.KeyExistsAsync(key);
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            return await _database.KeyDeleteAsync(key);
        }

        public async Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            long deletedCount = 0;
            var endpoints = _redis.GetEndPoints();

            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: pattern);

                foreach (var key in keys)
                {
                    if (await _database.KeyDeleteAsync(key))
                        deletedCount++;
                }
            }

            return deletedCount;
        }

        public async Task<T?> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            // Try to get from cache
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
                return cached;

            // Get from factory
            var value = await factory(cancellationToken);
            if (value != null)
            {
                await SetAsync(key, value, expiration, cancellationToken);
            }

            return value;
        }

        public async Task<bool> SetIfNotExistsAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var serialized = JsonSerializer.Serialize(value, _serializerOptions);
            return await _database.StringSetAsync(key, serialized, expiration, When.NotExists);
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            return await _database.KeyTimeToLiveAsync(key);
        }

        public async Task<bool> ExpireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            return await _database.KeyExpireAsync(key, expiration);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            var endpoints = _redis.GetEndPoints();

            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                await server.FlushDatabaseAsync();
            }
        }
    }

    /// <summary>
    /// Cache provider interface.
    /// </summary>
    public interface ICacheProvider
    {
        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache.
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all keys matching a pattern.
        /// </summary>
        Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value from cache or sets it using the factory.
        /// </summary>
        Task<T?> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value only if the key does not exist.
        /// </summary>
        Task<bool> SetIfNotExistsAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the time-to-live for a key.
        /// </summary>
        Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the expiration for a key.
        /// </summary>
        Task<bool> ExpireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cache entries.
        /// </summary>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
