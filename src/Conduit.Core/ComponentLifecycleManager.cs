using System.Collections.Concurrent;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core;

/// <summary>
/// Manages the lifecycle of components in the system.
/// This class handles the initialization, startup, shutdown, and cleanup
/// of components according to their dependencies and lifecycle requirements.
/// </summary>
public class ComponentLifecycleManager
{
    private readonly ComponentRegistry _registry;
    private readonly ILogger<ComponentLifecycleManager>? _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _componentLocks = new();
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the ComponentLifecycleManager class.
    /// </summary>
    public ComponentLifecycleManager(
        ComponentRegistry registry,
        IServiceProvider? serviceProvider = null,
        ILogger<ComponentLifecycleManager>? logger = null)
    {
        _registry = Guard.NotNull(registry);
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a component with the given configuration.
    /// </summary>
    public async Task<Result> InitializeComponentAsync(
        string componentId,
        ComponentConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrEmpty(componentId);

        var component = _registry.GetComponent(componentId);
        if (component == null)
        {
            return Result.Failure($"Component not found: {componentId}");
        }

        var descriptor = _registry.GetDescriptor(componentId);
        if (descriptor == null)
        {
            return Result.Failure($"Component descriptor not found: {componentId}");
        }

        var semaphore = _componentLocks.GetOrAdd(componentId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!descriptor.State.CanTransitionTo(ComponentState.Initializing))
            {
                return Result.Failure($"Component {componentId} cannot be initialized from state {descriptor.State}");
            }

            _logger?.LogInformation("Initializing component {ComponentId}", componentId);
            UpdateComponentState(descriptor, ComponentState.Initializing);

            try
            {
                // Set configuration
                component.Configuration = configuration ?? descriptor.Configuration;

                // Create component context
                var context = CreateComponentContext(componentId);

                // Attach component
                await component.OnAttachAsync(context, cancellationToken);

                UpdateComponentState(descriptor, ComponentState.Initialized);
                _logger?.LogInformation("Component {ComponentId} initialized successfully", componentId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize component {ComponentId}", componentId);
                UpdateComponentState(descriptor, ComponentState.Failed, ex);
                return Result.Failure($"Failed to initialize component {componentId}: {ex.Message}");
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Starts a component.
    /// </summary>
    public async Task<Result> StartComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrEmpty(componentId);

        var descriptor = _registry.GetDescriptor(componentId);
        if (descriptor == null)
        {
            return Result.Failure($"Component descriptor not found: {componentId}");
        }

        var semaphore = _componentLocks.GetOrAdd(componentId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!descriptor.State.CanStart())
            {
                return Result.Failure($"Component {componentId} cannot be started from state {descriptor.State}");
            }

            _logger?.LogInformation("Starting component {ComponentId}", componentId);
            UpdateComponentState(descriptor, ComponentState.Starting);

            try
            {
                // Initialize if needed
                if (descriptor.State == ComponentState.Registered)
                {
                    var initResult = await InitializeComponentAsync(componentId, null, cancellationToken);
                    if (initResult.IsFailure)
                    {
                        return initResult;
                    }
                }

                UpdateComponentState(descriptor, ComponentState.Running);
                descriptor.LastActivatedAt = DateTimeOffset.UtcNow;
                _logger?.LogInformation("Component {ComponentId} started successfully", componentId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start component {ComponentId}", componentId);
                UpdateComponentState(descriptor, ComponentState.Failed, ex);
                return Result.Failure($"Failed to start component {componentId}: {ex.Message}");
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Stops a component.
    /// </summary>
    public async Task<Result> StopComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrEmpty(componentId);

        var component = _registry.GetComponent(componentId);
        if (component == null)
        {
            return Result.Failure($"Component not found: {componentId}");
        }

        var descriptor = _registry.GetDescriptor(componentId);
        if (descriptor == null)
        {
            return Result.Failure($"Component descriptor not found: {componentId}");
        }

        var semaphore = _componentLocks.GetOrAdd(componentId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!descriptor.State.CanStop())
            {
                return Result.Failure($"Component {componentId} cannot be stopped from state {descriptor.State}");
            }

            _logger?.LogInformation("Stopping component {ComponentId}", componentId);
            UpdateComponentState(descriptor, ComponentState.Stopping);

            try
            {
                await component.OnDetachAsync(cancellationToken);

                UpdateComponentState(descriptor, ComponentState.Stopped);
                _logger?.LogInformation("Component {ComponentId} stopped successfully", componentId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop component {ComponentId}", componentId);
                UpdateComponentState(descriptor, ComponentState.Failed, ex);
                return Result.Failure($"Failed to stop component {componentId}: {ex.Message}");
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Restarts a component.
    /// </summary>
    public async Task<Result> RestartComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default)
    {
        var stopResult = await StopComponentAsync(componentId, cancellationToken);
        if (stopResult.IsFailure)
        {
            return stopResult;
        }

        return await StartComponentAsync(componentId, cancellationToken);
    }

    /// <summary>
    /// Disposes a component.
    /// </summary>
    public async Task<Result> DisposeComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrEmpty(componentId);

        var descriptor = _registry.GetDescriptor(componentId);
        if (descriptor == null)
        {
            return Result.Failure($"Component descriptor not found: {componentId}");
        }

        var semaphore = _componentLocks.GetOrAdd(componentId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (descriptor.State.IsTerminal())
            {
                return Result.Success(); // Already disposed or failed
            }

            _logger?.LogInformation("Disposing component {ComponentId}", componentId);
            UpdateComponentState(descriptor, ComponentState.Disposing);

            try
            {
                // Stop if running
                if (descriptor.State == ComponentState.Running)
                {
                    await StopComponentAsync(componentId, cancellationToken);
                }

                // Unregister from registry
                _registry.Unregister(componentId);

                UpdateComponentState(descriptor, ComponentState.Disposed);
                _logger?.LogInformation("Component {ComponentId} disposed successfully", componentId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to dispose component {ComponentId}", componentId);
                return Result.Failure($"Failed to dispose component {componentId}: {ex.Message}");
            }
        }
        finally
        {
            semaphore.Release();
            _componentLocks.TryRemove(componentId, out _);
        }
    }

    /// <summary>
    /// Initializes all registered components.
    /// </summary>
    public async Task<Result> InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        var components = _registry.GetAllDescriptors()
            .Where(d => d.IsEnabled && d.State == ComponentState.Registered)
            .OrderBy(d => d.Priority)
            .ToList();

        _logger?.LogInformation("Initializing {Count} components", components.Count);

        var failures = new List<string>();
        foreach (var descriptor in components)
        {
            var result = await InitializeComponentAsync(descriptor.Id, descriptor.Configuration, cancellationToken);
            if (result.IsFailure)
            {
                failures.Add($"{descriptor.Id}: {result.Error?.Message}");
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure($"Failed to initialize components: {string.Join(", ", failures)}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Starts all initialized components with auto-start enabled.
    /// </summary>
    public async Task<Result> StartAllAsync(CancellationToken cancellationToken = default)
    {
        var components = _registry.GetAllDescriptors()
            .Where(d => d.IsEnabled && d.AutoStart && d.State.CanStart())
            .OrderBy(d => d.Priority)
            .ToList();

        _logger?.LogInformation("Starting {Count} components", components.Count);

        var failures = new List<string>();
        foreach (var descriptor in components)
        {
            var result = await StartComponentAsync(descriptor.Id, cancellationToken);
            if (result.IsFailure)
            {
                failures.Add($"{descriptor.Id}: {result.Error?.Message}");
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure($"Failed to start components: {string.Join(", ", failures)}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Stops all running components.
    /// </summary>
    public async Task<Result> StopAllAsync(CancellationToken cancellationToken = default)
    {
        var components = _registry.GetAllDescriptors()
            .Where(d => d.State == ComponentState.Running)
            .OrderByDescending(d => d.Priority) // Stop in reverse order
            .ToList();

        _logger?.LogInformation("Stopping {Count} components", components.Count);

        var failures = new List<string>();
        foreach (var descriptor in components)
        {
            var result = await StopComponentAsync(descriptor.Id, cancellationToken);
            if (result.IsFailure)
            {
                failures.Add($"{descriptor.Id}: {result.Error?.Message}");
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure($"Failed to stop components: {string.Join(", ", failures)}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Gets the current state of a component.
    /// </summary>
    public ComponentState GetComponentState(string componentId)
    {
        var descriptor = _registry.GetDescriptor(componentId);
        return descriptor?.State ?? ComponentState.Uninitialized;
    }

    /// <summary>
    /// Gets the states of all components.
    /// </summary>
    public Dictionary<string, ComponentState> GetAllComponentStates()
    {
        return _registry.GetAllDescriptors()
            .ToDictionary(d => d.Id, d => d.State);
    }

    private ComponentContext CreateComponentContext(string componentId)
    {
        var messageBus = _serviceProvider?.GetService(typeof(IMessageBus)) as IMessageBus
            ?? throw new InvalidOperationException("IMessageBus service not available");

        var logger = _serviceProvider?.GetService(typeof(ILogger)) as ILogger
            ?? throw new InvalidOperationException("ILogger service not available");

        var metricsCollector = _serviceProvider?.GetService(typeof(IMetricsCollector)) as IMetricsCollector
            ?? throw new InvalidOperationException("IMetricsCollector service not available");

        return new ComponentContext(
            _serviceProvider ?? throw new InvalidOperationException("Service provider not available"),
            messageBus,
            logger,
            metricsCollector)
        {
            ComponentRegistry = _registry
        };
    }

    private void UpdateComponentState(ComponentDescriptor descriptor, ComponentState newState, Exception? error = null)
    {
        descriptor.State = newState;
        descriptor.LastError = error;

        _registry.UpdateDescriptor(descriptor.Id, d =>
        {
            d.State = newState;
            d.LastError = error;
        });
    }
}