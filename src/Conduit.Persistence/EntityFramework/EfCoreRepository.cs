using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Persistence.EntityFramework
{
    /// <summary>
    /// Entity Framework Core repository implementation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The identifier type</typeparam>
    public class EfCoreRepository<TEntity, TId> : IQueryableRepository<TEntity, TId>, IPagedRepository<TEntity, TId>
        where TEntity : class, IEntity<TId>
        where TId : notnull
    {
        protected readonly DbContext Context;
        protected readonly DbSet<TEntity> DbSet;

        /// <summary>
        /// Initializes a new instance of the EfCoreRepository class.
        /// </summary>
        public EfCoreRepository(DbContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            DbSet = context.Set<TEntity>();
        }

        // Query operations

        public virtual IQueryable<TEntity> Query()
        {
            return DbSet.AsQueryable();
        }

        public virtual IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> predicate)
        {
            return DbSet.Where(predicate);
        }

        public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            return await DbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet.ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await DbSet.Where(predicate).ToListAsync(cancellationToken);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(predicate, cancellationToken);
        }

        public virtual async Task<long> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null)
            {
                return await DbSet.LongCountAsync(cancellationToken);
            }

            return await DbSet.LongCountAsync(predicate, cancellationToken);
        }

        // Write operations

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            await DbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            await DbSet.AddRangeAsync(entities, cancellationToken);
        }

        public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            DbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            DbSet.UpdateRange(entities);
            return Task.CompletedTask;
        }

        public virtual async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return false;

            await DeleteAsync(entity, cancellationToken);
            return true;
        }

        public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Check if soft deletable
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = DateTime.UtcNow;
                DbSet.Update(entity);
            }
            else
            {
                DbSet.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            foreach (var entity in entities)
            {
                if (entity is ISoftDeletable softDeletable)
                {
                    softDeletable.IsDeleted = true;
                    softDeletable.DeletedAt = DateTime.UtcNow;
                }
            }

            var hardDeleteEntities = entities.Where(e => e is not ISoftDeletable).ToList();
            var softDeleteEntities = entities.Where(e => e is ISoftDeletable).ToList();

            if (hardDeleteEntities.Any())
                DbSet.RemoveRange(hardDeleteEntities);

            if (softDeleteEntities.Any())
                DbSet.UpdateRange(softDeleteEntities);

            return Task.CompletedTask;
        }

        public virtual async Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            var entities = await DbSet.Where(predicate).ToListAsync(cancellationToken);
            await DeleteRangeAsync(entities, cancellationToken);
            return entities.Count;
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

            var query = DbSet.AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.LongCountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

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

            var query = DbSet.AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.LongCountAsync(cancellationToken);

            query = ascending
                ? query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

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
