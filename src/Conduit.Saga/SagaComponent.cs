using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Saga;
using Microsoft.Extensions.Logging;

namespace Conduit.Saga;

/// <summary>
/// Saga component for Conduit framework integration.
/// Manages long-running process coordination and saga orchestration.
/// </summary>
public class SagaComponent : AbstractPluggableComponent
{
    private readonly ISagaOrchestrator _sagaOrchestrator;
    private readonly ISagaPersister _sagaPersister;
    private readonly ILogger<SagaComponent> _logger;
    private readonly List<Type> _registeredSagas;
    private readonly object _sagaLock = new();

    public SagaComponent(
        ISagaOrchestrator sagaOrchestrator,
        ISagaPersister sagaPersister,
        ILogger<SagaComponent> logger) : base(logger)
    {
        _sagaOrchestrator = sagaOrchestrator ?? throw new ArgumentNullException(nameof(sagaOrchestrator));
        _sagaPersister = sagaPersister ?? throw new ArgumentNullException(nameof(sagaPersister));
        _logger = logger;
        _registeredSagas = new List<Type>();

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.saga",
            Name = "Conduit.Saga",
            Version = "0.9.0-alpha",
            Description = "Long-running process coordination and saga orchestration for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "saga", "orchestration", "workflow", "coordination", "long-running", "stateful" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Saga component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Saga component '{Name}' started with {SagaCount} registered sagas",
            Name, _registeredSagas.Count);

        // Log registered sagas
        foreach (var sagaType in _registeredSagas)
        {
            Logger.LogDebug("Registered saga: {SagaType}", sagaType.Name);
        }

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Saga component '{Name}' stopping", Name);

        // Clean up any active saga operations if needed
        // Note: Individual sagas should handle their own cleanup during normal completion

        Logger.LogInformation("Saga component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Saga component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "SagaOrchestration",
                Name = "Saga Orchestration",
                Description = "Centralized coordination of distributed saga workflows",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SagaPersistence",
                Name = "Saga Persistence",
                Description = "Durable storage and retrieval of saga state",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MessageCorrelation",
                Name = "Message Correlation",
                Description = "Correlation of messages to specific saga instances",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SagaLifecycle",
                Name = "Saga Lifecycle",
                Description = "Creation, execution, completion, and cleanup of saga instances",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "CompensationActions",
                Name = "Compensation Actions",
                Description = "Rollback and compensation logic for failed saga steps",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SagaTimeout",
                Name = "Saga Timeout",
                Description = "Timeout handling and automatic saga completion",
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
                ServiceType = typeof(ISagaOrchestrator),
                ImplementationType = _sagaOrchestrator.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _sagaOrchestrator
            },
            new ServiceContract
            {
                ServiceType = typeof(ISagaPersister),
                ImplementationType = _sagaPersister.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _sagaPersister
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var orchestratorHealthy = _sagaOrchestrator != null;
        var persisterHealthy = _sagaPersister != null;
        var sagaCount = _registeredSagas.Count;

        var isHealthy = orchestratorHealthy && persisterHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["SagaOrchestrator"] = orchestratorHealthy ? "Available" : "Unavailable",
            ["SagaPersister"] = persisterHealthy ? "Available" : "Unavailable",
            ["RegisteredSagaCount"] = sagaCount,
            ["RegisteredSagas"] = _registeredSagas.Select(t => t.Name).ToArray()
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "One or more saga services unavailable", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var orchestratorHealthy = _sagaOrchestrator != null;
        var persisterHealthy = _sagaPersister != null;
        var sagaCount = _registeredSagas.Count;

        var isHealthy = orchestratorHealthy && persisterHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["SagaOrchestrator"] = orchestratorHealthy ? "Available" : "Unavailable",
            ["SagaPersister"] = persisterHealthy ? "Available" : "Unavailable",
            ["RegisteredSagaCount"] = sagaCount
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "One or more saga services unavailable", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("saga_orchestrator_available", _sagaOrchestrator != null ? 1 : 0);
        metrics.SetCounter("saga_persister_available", _sagaPersister != null ? 1 : 0);
        metrics.SetCounter("registered_saga_count", _registeredSagas.Count);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the saga orchestrator.
    /// </summary>
    public ISagaOrchestrator GetSagaOrchestrator() => _sagaOrchestrator;

    /// <summary>
    /// Gets the saga persister.
    /// </summary>
    public ISagaPersister GetSagaPersister() => _sagaPersister;

    /// <summary>
    /// Registers a saga type with the orchestrator.
    /// </summary>
    public void RegisterSaga<TSaga>() where TSaga : Saga
    {
        RegisterSaga(typeof(TSaga));
    }

    /// <summary>
    /// Registers a saga type with the orchestrator.
    /// </summary>
    public void RegisterSaga(Type sagaType)
    {
        ArgumentNullException.ThrowIfNull(sagaType);

        if (!typeof(Saga).IsAssignableFrom(sagaType))
        {
            throw new ArgumentException($"Type {sagaType.Name} must inherit from Saga", nameof(sagaType));
        }

        lock (_sagaLock)
        {
            if (!_registeredSagas.Contains(sagaType))
            {
                _registeredSagas.Add(sagaType);
                _sagaOrchestrator.RegisterSaga(sagaType);
                Logger.LogInformation("Registered saga type: {SagaType}", sagaType.Name);
            }
            else
            {
                Logger.LogWarning("Saga type {SagaType} is already registered", sagaType.Name);
            }
        }
    }

    /// <summary>
    /// Handles a message for a specific saga type.
    /// </summary>
    public async Task<object?> HandleSagaMessageAsync<TSaga>(
        object message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken = default) where TSaga : Saga
    {
        return await HandleSagaMessageAsync(typeof(TSaga), message, context, cancellationToken);
    }

    /// <summary>
    /// Handles a message for a specific saga type.
    /// </summary>
    public async Task<object?> HandleSagaMessageAsync(
        Type sagaType,
        object message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sagaType);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        if (!_registeredSagas.Contains(sagaType))
        {
            throw new InvalidOperationException($"Saga type {sagaType.Name} is not registered");
        }

        return await _sagaOrchestrator.HandleMessageAsync(sagaType, message, context, cancellationToken);
    }

    /// <summary>
    /// Creates a new saga instance.
    /// </summary>
    public Saga CreateSaga<TSaga>() where TSaga : Saga
    {
        return CreateSaga(typeof(TSaga));
    }

    /// <summary>
    /// Creates a new saga instance.
    /// </summary>
    public Saga CreateSaga(Type sagaType)
    {
        ArgumentNullException.ThrowIfNull(sagaType);

        if (!_registeredSagas.Contains(sagaType))
        {
            throw new InvalidOperationException($"Saga type {sagaType.Name} is not registered");
        }

        return _sagaOrchestrator.CreateSaga(sagaType);
    }

    /// <summary>
    /// Finds an existing saga instance.
    /// </summary>
    public async Task<TSaga?> FindSagaAsync<TSaga>(
        string correlationId,
        CancellationToken cancellationToken = default) where TSaga : Saga
    {
        var saga = await FindSagaAsync(typeof(TSaga), correlationId, cancellationToken);
        return saga as TSaga;
    }

    /// <summary>
    /// Finds an existing saga instance.
    /// </summary>
    public async Task<Saga?> FindSagaAsync(
        Type sagaType,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sagaType);
        ArgumentNullException.ThrowIfNull(correlationId);

        if (!_registeredSagas.Contains(sagaType))
        {
            throw new InvalidOperationException($"Saga type {sagaType.Name} is not registered");
        }

        return await _sagaOrchestrator.FindSagaAsync(sagaType, correlationId, cancellationToken);
    }

    /// <summary>
    /// Saves a saga instance.
    /// </summary>
    public async Task SaveSagaAsync(Saga saga, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saga);
        await _sagaOrchestrator.SaveSagaAsync(saga, cancellationToken);
    }

    /// <summary>
    /// Removes a completed saga instance.
    /// </summary>
    public async Task RemoveSagaAsync(Saga saga, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saga);
        await _sagaOrchestrator.RemoveSagaAsync(saga, cancellationToken);
    }

    /// <summary>
    /// Gets all registered saga types.
    /// </summary>
    public IEnumerable<Type> GetRegisteredSagaTypes()
    {
        lock (_sagaLock)
        {
            return _registeredSagas.ToList();
        }
    }

    /// <summary>
    /// Gets the count of registered sagas.
    /// </summary>
    public int GetRegisteredSagaCount()
    {
        return _registeredSagas.Count;
    }
}