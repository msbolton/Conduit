using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Conduit.Persistence.MongoDB
{
    /// <summary>
    /// MongoDB repository implementation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public class MongoRepository<TEntity, TId> : IRepository<TEntity, TId>, IPagedRepository<TEntity, TId>
        where TEntity : class, IEntity<TId>
        where TId : notnull
    {
        protected readonly IMongoCollection<TEntity> Collection;

        /// <summary>
        /// Initializes a new instance of the MongoRepository class.
        /// </summary>
        public MongoRepository(IMongoCollection<TEntity> collection)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        /// <summary>
        /// Initializes a new instance of the MongoRepository class.
        /// </summary>
        public MongoRepository(IMongoDatabase database, string collectionName)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (string.IsNullOrEmpty(collectionName))
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));

            Collection = database.GetCollection<TEntity>(collectionName);
        }

        // Query operations

        public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
            var cursor = await Collection.FindAsync(filter, cancellationToken: cancellationToken);
            return await cursor.FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var cursor = await Collection.FindAsync(FilterDefinition<TEntity>.Empty, cancellationToken: cancellationToken);
            return await cursor.ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var filter = Builders<TEntity>.Filter.Where(predicate);
            var cursor = await Collection.FindAsync(filter, cancellationToken: cancellationToken);
            return await cursor.ToListAsync(cancellationToken);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var filter = Builders<TEntity>.Filter.Where(predicate);
            var cursor = await Collection.FindAsync(filter, cancellationToken: cancellationToken);
            return await cursor.FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var filter = Builders<TEntity>.Filter.Where(predicate);
            var count = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count > 0;
        }

        public virtual async Task<long> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            var filter = predicate != null
                ? Builders<TEntity>.Filter.Where(predicate)
                : FilterDefinition<TEntity>.Empty;

            return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        // Write operations

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity is IAuditableEntity auditable)
            {
                auditable.CreatedAt = DateTime.UtcNow;
            }

            await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var entitiesList = entities.ToList();

            foreach (var entity in entitiesList.OfType<IAuditableEntity>())
            {
                entity.CreatedAt = DateTime.UtcNow;
            }

            await Collection.InsertManyAsync(entitiesList, cancellationToken: cancellationToken);
        }

        public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity is IAuditableEntity auditable)
            {
                auditable.UpdatedAt = DateTime.UtcNow;
            }

            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);
            await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var updates = new List<Task>();

            foreach (var entity in entities)
            {
                if (entity is IAuditableEntity auditable)
                {
                    auditable.UpdatedAt = DateTime.UtcNow;
                }

                var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);
                updates.Add(Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken));
            }

            await Task.WhenAll(updates);
        }

        public virtual async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return false;

            await DeleteAsync(entity, cancellationToken);
            return true;
        }

        public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Check if soft deletable
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = DateTime.UtcNow;
                await UpdateAsync(entity, cancellationToken);
            }
            else
            {
                var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);
                await Collection.DeleteOneAsync(filter, cancellationToken);
            }
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var tasks = entities.Select(e => DeleteAsync(e, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public virtual async Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var entities = await FindAsync(predicate, cancellationToken);
            var entitiesList = entities.ToList();
            await DeleteRangeAsync(entitiesList, cancellationToken);
            return entitiesList.Count;
        }

        // Paging operations

        public virtual async Task<PagedResult<TEntity>> GetPageAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0");

            var filter = predicate != null
                ? Builders<TEntity>.Filter.Where(predicate)
                : FilterDefinition<TEntity>.Empty;

            var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            var cursor = await Collection.FindAsync(
                filter,
                new FindOptions<TEntity>
                {
                    Skip = (pageNumber - 1) * pageSize,
                    Limit = pageSize
                },
                cancellationToken);

            var items = await cursor.ToListAsync(cancellationToken);

            return new PagedResult<TEntity>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public virtual async Task<PagedResult<TEntity>> GetPageAsync<TKey>(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, TKey>> orderBy,
            bool ascending = true,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0");

            if (orderBy == null)
                throw new ArgumentNullException(nameof(orderBy));

            var filter = predicate != null
                ? Builders<TEntity>.Filter.Where(predicate)
                : FilterDefinition<TEntity>.Empty;

            var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            var sort = ascending
                ? Builders<TEntity>.Sort.Ascending(new ExpressionFieldDefinition<TEntity>(orderBy))
                : Builders<TEntity>.Sort.Descending(new ExpressionFieldDefinition<TEntity>(orderBy));

            var cursor = await Collection.FindAsync(
                filter,
                new FindOptions<TEntity>
                {
                    Sort = sort,
                    Skip = (pageNumber - 1) * pageSize,
                    Limit = pageSize
                },
                cancellationToken);

            var items = await cursor.ToListAsync(cancellationToken);

            return new PagedResult<TEntity>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
