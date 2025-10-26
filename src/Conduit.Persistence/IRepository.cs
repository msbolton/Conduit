using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Persistence
{
    /// <summary>
    /// Generic repository interface for data access operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public interface IRepository<TEntity, TId> where TEntity : IEntity<TId> where TId : notnull
    {
        // Query operations

        /// <summary>
        /// Gets an entity by its identifier.
        /// </summary>
        Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all entities.
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds entities matching a predicate.
        /// </summary>
        Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the first entity matching a predicate, or null.
        /// </summary>
        Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity matches a predicate.
        /// </summary>
        Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entities matching a predicate.
        /// </summary>
        Task<long> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        // Write operations

        /// <summary>
        /// Adds a new entity.
        /// </summary>
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple entities.
        /// </summary>
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity.
        /// </summary>
        Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities.
        /// </summary>
        Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by its identifier.
        /// </summary>
        Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity.
        /// </summary>
        Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple entities.
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes entities matching a predicate.
        /// </summary>
        Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Queryable repository interface with LINQ support.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public interface IQueryableRepository<TEntity, TId> : IRepository<TEntity, TId>
        where TEntity : IEntity<TId>
        where TId : notnull
    {
        /// <summary>
        /// Gets a queryable for the entity type.
        /// </summary>
        IQueryable<TEntity> Query();

        /// <summary>
        /// Gets a queryable with a predicate filter.
        /// </summary>
        IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> predicate);
    }

    /// <summary>
    /// Repository with paging support.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public interface IPagedRepository<TEntity, TId> : IRepository<TEntity, TId>
        where TEntity : IEntity<TId>
        where TId : notnull
    {
        /// <summary>
        /// Gets a page of entities.
        /// </summary>
        Task<PagedResult<TEntity>> GetPageAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a page of entities with ordering.
        /// </summary>
        Task<PagedResult<TEntity>> GetPageAsync<TKey>(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, TKey>> orderBy,
            bool ascending = true,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Paged result container.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// Gets or sets the items in the current page.
        /// </summary>
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();

        /// <summary>
        /// Gets or sets the current page number (1-based).
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets the total number of items.
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// Gets the total number of pages.
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

        /// <summary>
        /// Gets a value indicating whether there is a previous page.
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Gets a value indicating whether there is a next page.
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
