using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Pipeline;
using Microsoft.Extensions.Logging;

namespace Conduit.Pipeline;

/// <summary>
/// Pipeline component for Conduit framework integration.
/// Manages pipeline processing stages, behaviors, and composition.
/// </summary>
public class PipelineComponent : AbstractPluggableComponent
{
    private readonly PipelineFactory _pipelineFactory;
    private readonly ILogger<PipelineComponent> _logger;
    private readonly Dictionary<string, object> _activePipelines;
    private readonly object _pipelineLock = new();

    public PipelineComponent(
        PipelineFactory pipelineFactory,
        ILogger<PipelineComponent> logger) : base(logger)
    {
        _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
        _logger = logger;
        _activePipelines = new Dictionary<string, object>();

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.pipeline",
            Name = "Conduit.Pipeline",
            Version = "0.9.0-alpha",
            Description = "Pipeline processing and composition for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "pipeline", "processing", "composition", "behaviors", "stages" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Pipeline component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Pipeline component '{Name}' started", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Pipeline component '{Name}' stopping", Name);

        // Stop all active pipelines gracefully
        lock (_pipelineLock)
        {
            foreach (var (pipelineId, pipeline) in _activePipelines)
            {
                try
                {
                    if (pipeline is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    Logger.LogDebug("Stopped pipeline: {PipelineId}", pipelineId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error stopping pipeline {PipelineId}", pipelineId);
                }
            }
            _activePipelines.Clear();
        }

        Logger.LogInformation("Pipeline component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Pipeline component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "PipelineFactory",
                Name = "Pipeline Factory",
                Description = "Factory for creating configured pipeline instances",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "PipelineStages",
                Name = "Pipeline Stages",
                Description = "Built-in stages including validation, logging, retry, timeout, circuit breaker",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "PipelineInterceptors",
                Name = "Pipeline Interceptors",
                Description = "Cross-cutting concerns including logging, metrics, validation",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "PipelineComposition",
                Name = "Pipeline Composition",
                Description = "Fluent API for composing complex pipeline configurations",
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
                ServiceType = typeof(PipelineFactory),
                ImplementationType = _pipelineFactory.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _pipelineFactory
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var activePipelineCount = _activePipelines.Count;
        var isHealthy = true; // Pipeline component is healthy if it can create pipelines

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ActivePipelines"] = activePipelineCount,
            ["FactoryStatus"] = _pipelineFactory != null ? "Available" : "Unavailable"
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "Pipeline factory unavailable", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var activePipelineCount = _activePipelines.Count;
        var isHealthy = _pipelineFactory != null;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ActivePipelines"] = activePipelineCount,
            ["FactoryStatus"] = _pipelineFactory != null ? "Available" : "Unavailable"
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "Pipeline factory unavailable", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("active_pipelines", _activePipelines.Count);
        metrics.SetGauge("component_state", (int)GetState());
        metrics.SetCounter("factory_available", _pipelineFactory != null ? 1 : 0);
    }

    /// <summary>
    /// Gets the pipeline factory.
    /// </summary>
    public PipelineFactory GetPipelineFactory() => _pipelineFactory;

    /// <summary>
    /// Registers an active pipeline for tracking and management.
    /// </summary>
    public void RegisterActivePipeline(string pipelineId, object pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipelineId);
        ArgumentNullException.ThrowIfNull(pipeline);

        lock (_pipelineLock)
        {
            _activePipelines[pipelineId] = pipeline;
            Logger.LogDebug("Registered active pipeline: {PipelineId}", pipelineId);
        }
    }

    /// <summary>
    /// Unregisters an active pipeline.
    /// </summary>
    public bool UnregisterActivePipeline(string pipelineId)
    {
        ArgumentNullException.ThrowIfNull(pipelineId);

        lock (_pipelineLock)
        {
            var removed = _activePipelines.Remove(pipelineId);
            if (removed)
            {
                Logger.LogDebug("Unregistered active pipeline: {PipelineId}", pipelineId);
            }
            return removed;
        }
    }

    /// <summary>
    /// Gets all active pipeline IDs.
    /// </summary>
    public IEnumerable<string> GetActivePipelineIds()
    {
        lock (_pipelineLock)
        {
            return _activePipelines.Keys.ToList();
        }
    }
}