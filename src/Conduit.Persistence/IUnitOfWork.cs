using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Persistence
{
    /// <summary>
    /// Unit of Work pattern interface for managing transactions.
    /// </summary>
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Begins a new transaction.
        /// </summary>
        Task<ITransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves all changes made in this unit of work.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a repository for the specified entity type.
        /// </summary>
        IRepository<TEntity, TId> Repository<TEntity, TId>()
            where TEntity : class, IEntity<TId>
            where TId : notnull;
    }

    /// <summary>
    /// Transaction interface.
    /// </summary>
    public interface ITransaction : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the transaction identifier.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction is active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the transaction.
        /// </summary>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Transaction scope for managing distributed transactions.
    /// </summary>
    public class TransactionScope : IDisposable, IAsyncDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITransaction? _transaction;
        private bool _completed;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TransactionScope class.
        /// </summary>
        public TransactionScope(IUnitOfWork unitOfWork, ITransaction? transaction = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _transaction = transaction;
        }

        /// <summary>
        /// Marks the transaction scope as complete (ready to commit).
        /// </summary>
        public void Complete()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionScope));

            _completed = true;
        }

        /// <summary>
        /// Commits or rolls back the transaction based on completion status.
        /// </summary>
        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionScope));

            if (_transaction != null)
            {
                if (_completed)
                {
                    await _transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await _transaction.RollbackAsync(cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Disposes the transaction scope.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_transaction != null && !_completed && _transaction.IsActive)
            {
                _transaction.RollbackAsync().GetAwaiter().GetResult();
            }

            _transaction?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously disposes the transaction scope.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_transaction != null && !_completed && _transaction.IsActive)
            {
                await _transaction.RollbackAsync();
            }

            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Transaction options for configuring transaction behavior.
    /// </summary>
    public class TransactionOptions
    {
        /// <summary>
        /// Gets or sets the isolation level.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

        /// <summary>
        /// Gets or sets the transaction timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the transaction is read-only.
        /// </summary>
        public bool IsReadOnly { get; set; }
    }
}
