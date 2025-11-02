using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Persistence;
using Microsoft.Extensions.Logging;

namespace Conduit.Persistence;

/// <summary>
/// Persistence component for Conduit framework integration.
/// Manages unit of work, repository patterns, and transaction coordination.
/// </summary>
public class PersistenceComponent : AbstractPluggableComponent
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PersistenceComponent> _logger;
    private readonly List<ITransaction> _activeTransactions;
    private readonly object _transactionLock = new();

    public PersistenceComponent(
        IUnitOfWork unitOfWork,
        ILogger<PersistenceComponent> logger) : base(logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger;
        _activeTransactions = new List<ITransaction>();

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.persistence",
            Name = "Conduit.Persistence",
            Version = "0.8.2",
            Description = "Data access, repository patterns, and transaction management for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "persistence", "repository", "unitofwork", "transactions", "data-access" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Persistence component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Persistence component '{Name}' started", Name);

        // Log persistence configuration
        Logger.LogInformation(
            "Persistence services: UnitOfWork={UnitOfWorkAvailable}",
            _unitOfWork != null);

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Persistence component '{Name}' stopping", Name);

        // Clean up active transactions
        lock (_transactionLock)
        {
            foreach (var transaction in _activeTransactions.ToArray())
            {
                try
                {
                    if (transaction.IsActive)
                    {
                        transaction.RollbackAsync(cancellationToken).GetAwaiter().GetResult();
                        Logger.LogDebug("Rolled back active transaction: {TransactionId}", transaction.Id);
                    }
                    transaction.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error cleaning up transaction {TransactionId}", transaction.Id);
                }
            }
            _activeTransactions.Clear();
        }

        // Dispose unit of work
        try
        {
            _unitOfWork?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing unit of work during shutdown");
        }

        Logger.LogInformation("Persistence component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Persistence component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "UnitOfWork",
                Name = "Unit of Work",
                Description = "Transaction coordination and change tracking",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "RepositoryPattern",
                Name = "Repository Pattern",
                Description = "Generic repository interfaces for data access",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "TransactionManagement",
                Name = "Transaction Management",
                Description = "ACID transaction support with isolation levels",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "QueryableRepository",
                Name = "Queryable Repository",
                Description = "LINQ-enabled repository with advanced querying",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "PagedRepository",
                Name = "Paged Repository",
                Description = "Pagination support for large dataset queries",
                Version = Version,
                IsEnabledByDefault = true
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(IUnitOfWork),
                ImplementationType = _unitOfWork.GetType(),
                Lifetime = ServiceLifetime.Scoped,
                Factory = _ => _unitOfWork
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var unitOfWorkHealthy = _unitOfWork != null;
        var activeTransactionCount = _activeTransactions.Count;

        var isHealthy = unitOfWorkHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["UnitOfWork"] = unitOfWorkHealthy ? "Available" : "Unavailable",
            ["ActiveTransactions"] = activeTransactionCount
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "Unit of work unavailable", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var unitOfWorkHealthy = _unitOfWork != null;
        var activeTransactionCount = _activeTransactions.Count;

        var isHealthy = unitOfWorkHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["UnitOfWork"] = unitOfWorkHealthy ? "Available" : "Unavailable",
            ["ActiveTransactions"] = activeTransactionCount
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "Unit of work unavailable", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("unit_of_work_available", _unitOfWork != null ? 1 : 0);
        metrics.SetCounter("active_transactions", _activeTransactions.Count);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the unit of work.
    /// </summary>
    public IUnitOfWork GetUnitOfWork() => _unitOfWork;

    /// <summary>
    /// Begins a new transaction and tracks it.
    /// </summary>
    public async Task<ITransaction> BeginTransactionAsync(
        System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _unitOfWork.BeginTransactionAsync(isolationLevel, cancellationToken);

        lock (_transactionLock)
        {
            _activeTransactions.Add(transaction);
            Logger.LogDebug("Started transaction: {TransactionId}", transaction.Id);
        }

        return transaction;
    }

    /// <summary>
    /// Removes a transaction from active tracking.
    /// </summary>
    public void UntrackTransaction(ITransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        lock (_transactionLock)
        {
            var removed = _activeTransactions.Remove(transaction);
            if (removed)
            {
                Logger.LogDebug("Untracked transaction: {TransactionId}", transaction.Id);
            }
        }
    }

    /// <summary>
    /// Gets a repository for the specified entity type.
    /// </summary>
    public IRepository<TEntity, TId> GetRepository<TEntity, TId>()
        where TEntity : class, IEntity<TId>
        where TId : notnull
    {
        return _unitOfWork.Repository<TEntity, TId>();
    }

    /// <summary>
    /// Creates a transaction scope for automatic transaction management.
    /// </summary>
    public async Task<TransactionScope> CreateTransactionScopeAsync(
        System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var transaction = await BeginTransactionAsync(isolationLevel, cancellationToken);
        return new TransactionScope(_unitOfWork, transaction);
    }

    /// <summary>
    /// Gets all active transaction IDs.
    /// </summary>
    public IEnumerable<Guid> GetActiveTransactionIds()
    {
        lock (_transactionLock)
        {
            return _activeTransactions.Select(t => t.Id).ToList();
        }
    }
}