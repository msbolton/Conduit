using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Persistence.Caching
{
    /// <summary>
    /// Repository decorator that adds caching capabilities.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public class CachedRepository<TEntity, TId> : IRepository<TEntity, TId>
        where TEntity : class, IEntity<TId>
        where TId : notnull
    {
        private readonly IRepository<TEntity, TId> _innerRepository;
        private readonly ICacheProvider _cacheProvider;
        private readonly CacheOptions _options;

        /// <summary>
        /// Initializes a new instance of the CachedRepository class.
        /// </summary>
        public CachedRepository(
            IRepository<TEntity, TId> innerRepository,
            ICacheProvider cacheProvider,
            CacheOptions? options = null)
        {
            _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _options = options ?? new CacheOptions();
        }

        private string GetCacheKey(TId id) => $"{_options.KeyPrefix}:{typeof(TEntity).Name}:{id}";

        private string GetListCacheKey() => $"{_options.KeyPrefix}:{typeof(TEntity).Name}:all";

        // Query operations with caching

        public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCaching)
                return await _innerRepository.GetByIdAsync(id, cancellationToken);

            var cacheKey = GetCacheKey(id);

            return await _cacheProvider.GetOrSetAsync(
                cacheKey,
                async ct => await _innerRepository.GetByIdAsync(id, ct),
                _options.Expiration,
                cancellationToken);
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCaching || !_options.CacheListQueries)
                return await _innerRepository.GetAllAsync(cancellationToken);

            var cacheKey = GetListCacheKey();

            var cached = await _cacheProvider.GetOrSetAsync(
                cacheKey,
                async ct => await _innerRepository.GetAllAsync(ct),
                _options.ListExpiration ?? _options.Expiration,
                cancellationToken);

            return cached ?? Array.Empty<TEntity>();
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            // Complex queries are not cached by default
            return await _innerRepository.FindAsync(predicate, cancellationToken);
        }

        public virtual Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return _innerRepository.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return _innerRepository.AnyAsync(predicate, cancellationToken);
        }

        public virtual Task<long> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            return _innerRepository.CountAsync(predicate, cancellationToken);
        }

        // Write operations with cache invalidation

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var result = await _innerRepository.AddAsync(entity, cancellationToken);

            if (_options.EnableCaching)
            {
                // Invalidate list cache
                await InvalidateListCacheAsync(cancellationToken);

                // Optionally cache the new entity
                if (_options.CacheOnWrite)
                {
                    var cacheKey = GetCacheKey(result.Id);
                    await _cacheProvider.SetAsync(cacheKey, result, _options.Expiration, cancellationToken);
                }
            }

            return result;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await _innerRepository.AddRangeAsync(entities, cancellationToken);

            if (_options.EnableCaching)
            {
                await InvalidateListCacheAsync(cancellationToken);
            }
        }

        public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _innerRepository.UpdateAsync(entity, cancellationToken);

            if (_options.EnableCaching)
            {
                var cacheKey = GetCacheKey(entity.Id);
                await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
                await InvalidateListCacheAsync(cancellationToken);

                if (_options.CacheOnWrite)
                {
                    await _cacheProvider.SetAsync(cacheKey, entity, _options.Expiration, cancellationToken);
                }
            }
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await _innerRepository.UpdateRangeAsync(entities, cancellationToken);

            if (_options.EnableCaching)
            {
                foreach (var entity in entities)
                {
                    var cacheKey = GetCacheKey(entity.Id);
                    await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
                }

                await InvalidateListCacheAsync(cancellationToken);
            }
        }

        public virtual async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
        {
            var result = await _innerRepository.DeleteAsync(id, cancellationToken);

            if (result && _options.EnableCaching)
            {
                var cacheKey = GetCacheKey(id);
                await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
                await InvalidateListCacheAsync(cancellationToken);
            }

            return result;
        }

        public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _innerRepository.DeleteAsync(entity, cancellationToken);

            if (_options.EnableCaching)
            {
                var cacheKey = GetCacheKey(entity.Id);
                await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
                await InvalidateListCacheAsync(cancellationToken);
            }
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await _innerRepository.DeleteRangeAsync(entities, cancellationToken);

            if (_options.EnableCaching)
            {
                foreach (var entity in entities)
                {
                    var cacheKey = GetCacheKey(entity.Id);
                    await _cacheProvider.RemoveAsync(cacheKey, cancellationToken);
                }

                await InvalidateListCacheAsync(cancellationToken);
            }
        }

        public virtual async Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var count = await _innerRepository.DeleteWhereAsync(predicate, cancellationToken);

            if (count > 0 && _options.EnableCaching)
            {
                // Clear all entity caches since we don't know which ones were deleted
                await InvalidateAllCacheAsync(cancellationToken);
            }

            return count;
        }

        private async Task InvalidateListCacheAsync(CancellationToken cancellationToken = default)
        {
            var listCacheKey = GetListCacheKey();
            await _cacheProvider.RemoveAsync(listCacheKey, cancellationToken);
        }

        private async Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default)
        {
            var pattern = $"{_options.KeyPrefix}:{typeof(TEntity).Name}:*";
            await _cacheProvider.RemoveByPatternAsync(pattern, cancellationToken);
        }
    }

    /// <summary>
    /// Cache options for cached repository.
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether caching is enabled.
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache key prefix.
        /// </summary>
        public string KeyPrefix { get; set; } = "conduit";

        /// <summary>
        /// Gets or sets the default cache expiration time.
        /// </summary>
        public TimeSpan? Expiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets a value indicating whether to cache list queries.
        /// </summary>
        public bool CacheListQueries { get; set; } = true;

        /// <summary>
        /// Gets or sets the expiration time for list queries.
        /// </summary>
        public TimeSpan? ListExpiration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets a value indicating whether to cache entities on write operations.
        /// </summary>
        public bool CacheOnWrite { get; set; } = true;
    }
}
