using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conduit.Persistence.EntityFramework
{
    /// <summary>
    /// Base DbContext for Conduit applications.
    /// </summary>
    public abstract class ConduitDbContext : DbContext
    {
        private readonly ILogger? _logger;
        private string? _currentUser;

        /// <summary>
        /// Initializes a new instance of the ConduitDbContext class.
        /// </summary>
        protected ConduitDbContext(DbContextOptions options) : base(options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConduitDbContext class with logging.
        /// </summary>
        protected ConduitDbContext(DbContextOptions options, ILogger logger) : base(options)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sets the current user for auditing.
        /// </summary>
        public void SetCurrentUser(string? userId)
        {
            _currentUser = userId;
        }

        /// <summary>
        /// Saves changes with automatic auditing.
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ApplyAuditInfo();
                ApplySoftDelete();

                var result = await base.SaveChangesAsync(cancellationToken);

                _logger?.LogDebug("Saved {Count} changes to database", result);

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger?.LogError(ex, "Concurrency error while saving changes");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogError(ex, "Database update error while saving changes");
                throw;
            }
        }

        /// <summary>
        /// Saves changes with automatic auditing.
        /// </summary>
        public override int SaveChanges()
        {
            ApplyAuditInfo();
            ApplySoftDelete();
            return base.SaveChanges();
        }

        /// <summary>
        /// Applies audit information to entities.
        /// </summary>
        private void ApplyAuditInfo()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is IAuditableEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var auditableEntity = (IAuditableEntity)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    auditableEntity.CreatedAt = DateTime.UtcNow;
                    auditableEntity.CreatedBy = _currentUser;
                }

                if (entry.State == EntityState.Modified)
                {
                    auditableEntity.UpdatedAt = DateTime.UtcNow;
                    auditableEntity.UpdatedBy = _currentUser;
                }
            }
        }

        /// <summary>
        /// Applies soft delete information.
        /// </summary>
        private void ApplySoftDelete()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is ISoftDeletable && e.State == EntityState.Deleted);

            foreach (var entry in entries)
            {
                var softDeletable = (ISoftDeletable)entry.Entity;
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = DateTime.UtcNow;
                softDeletable.DeletedBy = _currentUser;
            }
        }

        /// <summary>
        /// Configures the model with global query filters.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply global query filter for soft delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                    var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                    var filter = System.Linq.Expressions.Expression.Lambda(
                        System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(false)),
                        parameter);

                    entityType.SetQueryFilter(filter);
                }
            }
        }
    }
}
