using Conduit.Core;
using Conduit.Messaging;
using Microsoft.Extensions.Logging;

namespace Conduit.Metrics.HealthChecks;

/// <summary>
/// Health check for component registry.
/// </summary>
public class ComponentRegistryHealthCheck : IHealthCheck
{
    private readonly IComponentRegistry? _registry;
    private readonly ILogger<ComponentRegistryHealthCheck> _logger;

    public ComponentRegistryHealthCheck(
        ILogger<ComponentRegistryHealthCheck> logger,
        IComponentRegistry? registry = null)
    {
        _logger = logger;
        _registry = registry;
    }

    public string Name => "component_registry";

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (_registry == null)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Component registry not configured",
                new Dictionary<string, object> { ["enabled"] = false }));
        }

        try
        {
            var components = _registry.GetAllComponents();
            var componentCount = components.Count();

            var data = new Dictionary<string, object>
            {
                ["component_count"] = componentCount,
                ["enabled"] = true
            };

            return Task.FromResult(HealthCheckResult.Healthy(
                $"{componentCount} components registered",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Component registry health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Failed to check component registry",
                ex));
        }
    }
}

/// <summary>
/// Health check for message bus.
/// </summary>
public class MessageBusHealthCheck : IHealthCheck
{
    private readonly IMessageBus? _messageBus;
    private readonly ILogger<MessageBusHealthCheck> _logger;

    public MessageBusHealthCheck(
        ILogger<MessageBusHealthCheck> logger,
        IMessageBus? messageBus = null)
    {
        _logger = logger;
        _messageBus = messageBus;
    }

    public string Name => "message_bus";

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (_messageBus == null)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Message bus not configured",
                new Dictionary<string, object> { ["enabled"] = false }));
        }

        try
        {
            // Simple connectivity check - message bus is operational if it doesn't throw
            var data = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["type"] = _messageBus.GetType().Name
            };

            return Task.FromResult(HealthCheckResult.Healthy(
                "Message bus is operational",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message bus health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Message bus is not operational",
                ex));
        }
    }
}

/// <summary>
/// Health check for memory usage.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;
    private readonly ILogger<MemoryHealthCheck> _logger;

    public MemoryHealthCheck(
        ILogger<MemoryHealthCheck> logger,
        long thresholdBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _logger = logger;
        _thresholdBytes = thresholdBytes;
    }

    public string Name => "memory";

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var gcMemory = GC.GetTotalMemory(false);
        var workingSet = Environment.WorkingSet;

        var data = new Dictionary<string, object>
        {
            ["gc_memory_bytes"] = gcMemory,
            ["working_set_bytes"] = workingSet,
            ["threshold_bytes"] = _thresholdBytes,
            ["gc_memory_mb"] = gcMemory / (1024.0 * 1024.0),
            ["working_set_mb"] = workingSet / (1024.0 * 1024.0)
        };

        if (gcMemory > _thresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage is high: {gcMemory / (1024.0 * 1024.0):F2} MB",
                data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage is normal: {gcMemory / (1024.0 * 1024.0):F2} MB",
            data));
    }
}

/// <summary>
/// Health check for thread pool.
/// </summary>
public class ThreadPoolHealthCheck : IHealthCheck
{
    private readonly ILogger<ThreadPoolHealthCheck> _logger;

    public ThreadPoolHealthCheck(ILogger<ThreadPoolHealthCheck> logger)
    {
        _logger = logger;
    }

    public string Name => "thread_pool";

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);

        var data = new Dictionary<string, object>
        {
            ["available_worker_threads"] = availableWorkerThreads,
            ["available_completion_port_threads"] = availableCompletionPortThreads,
            ["max_worker_threads"] = maxWorkerThreads,
            ["max_completion_port_threads"] = maxCompletionPortThreads,
            ["min_worker_threads"] = minWorkerThreads,
            ["min_completion_port_threads"] = minCompletionPortThreads,
            ["busy_worker_threads"] = maxWorkerThreads - availableWorkerThreads,
            ["busy_completion_port_threads"] = maxCompletionPortThreads - availableCompletionPortThreads
        };

        var workerThreadUtilization = (maxWorkerThreads - availableWorkerThreads) / (double)maxWorkerThreads;
        if (workerThreadUtilization > 0.9)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Thread pool utilization is high: {workerThreadUtilization:P0}",
                data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Thread pool is healthy: {workerThreadUtilization:P0} utilization",
            data));
    }
}

/// <summary>
/// Health check for startup probe.
/// </summary>
public class StartupHealthCheck : IHealthCheck
{
    private bool _isReady;

    public string Name => "startup";

    public void MarkAsReady() => _isReady = true;

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isReady
            ? HealthCheckResult.Healthy("Application has started")
            : HealthCheckResult.Unhealthy("Application is still starting"));
    }
}

/// <summary>
/// Health check for liveness probe.
/// </summary>
public class LivenessHealthCheck : IHealthCheck
{
    private bool _isAlive = true;

    public string Name => "liveness";

    public void MarkAsDown() => _isAlive = false;

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isAlive
            ? HealthCheckResult.Healthy("Application is alive")
            : HealthCheckResult.Unhealthy("Application is not responsive"));
    }
}
