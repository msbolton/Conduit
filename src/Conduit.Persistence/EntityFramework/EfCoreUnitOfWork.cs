using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Conduit.Persistence.EntityFramework
{
    /// <summary>
    /// Entity Framework Core Unit of Work implementation.
    /// </summary>
    public class EfCoreUnitOfWork : IUnitOfWork
    {
        private readonly DbContext _context;
        private readonly Dictionary<Type, object> _repositories;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the EfCoreUnitOfWork class.
        /// </summary>
        public EfCoreUnitOfWork(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repositories = new Dictionary<Type, object>();
        }

        /// <summary>
        /// Begins a new transaction.
        /// </summary>
        public async Task<ITransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            var dbTransaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            return new EfCoreTransaction(dbTransaction);
        }

        /// <summary>
        /// Saves all changes made in this unit of work.
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Gets a repository for the specified entity type.
        /// </summary>
        public IRepository<TEntity, TId> Repository<TEntity, TId>()
            where TEntity : class, IEntity<TId>
            where TId : notnull
        {
            var type = typeof(TEntity);

            if (_repositories.ContainsKey(type))
            {
                return (IRepository<TEntity, TId>)_repositories[type];
            }

            var repository = new EfCoreRepository<TEntity, TId>(_context);
            _repositories.Add(type, repository);

            return repository;
        }

        /// <summary>
        /// Disposes the unit of work.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously disposes the unit of work.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await _context.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Entity Framework Core transaction wrapper.
    /// </summary>
    internal class EfCoreTransaction : ITransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _disposed;

        public EfCoreTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Id = _transaction.TransactionId;
        }

        public Guid Id { get; }

        public bool IsActive => !_disposed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EfCoreTransaction));

            await _transaction.CommitAsync(cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EfCoreTransaction));

            await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _transaction.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await _transaction.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
