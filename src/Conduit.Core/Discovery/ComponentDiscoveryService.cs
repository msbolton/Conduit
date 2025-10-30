using System.Collections.Concurrent;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core.Discovery;

/// <summary>
/// Service that orchestrates component discovery using multiple strategies.
/// </summary>
public class ComponentDiscoveryService : IDisposable
{
    private readonly List<IComponentDiscoveryStrategy> _strategies = new();
    private readonly ComponentRegistry _registry;
    private readonly ComponentValidator _validator;
    private readonly ILogger<ComponentDiscoveryService>? _logger;
    private readonly ConcurrentDictionary<string, DiscoveredComponent> _discoveredComponents = new();
    private readonly SemaphoreSlim _discoverySemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the ComponentDiscoveryService class.
    /// </summary>
    public ComponentDiscoveryService(
        ComponentRegistry registry,
        ComponentValidator? validator = null,
        ILogger<ComponentDiscoveryService>? logger = null)
    {
        _registry = Guard.NotNull(registry);
        _validator = validator ?? new ComponentValidator();
        _logger = logger;
    }

    /// <summary>
    /// Adds a discovery strategy.
    /// </summary>
    public ComponentDiscoveryService AddStrategy(IComponentDiscoveryStrategy strategy)
    {
        Guard.NotNull(strategy);
        _strategies.Add(strategy);
        _logger?.LogInformation("Added discovery strategy: {StrategyName}", strategy.Name);
        return this;
    }

    /// <summary>
    /// Configures all strategies with the given configuration.
    /// </summary>
    public void Configure(ComponentDiscoveryConfiguration configuration)
    {
        Guard.NotNull(configuration);

        foreach (var strategy in _strategies)
        {
            strategy.Initialize(configuration);
        }
    }

    /// <summary>
    /// Discovers and registers all components.
    /// </summary>
    public async Task<DiscoveryResult> DiscoverAndRegisterAsync(
        ComponentDiscoveryConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        await _discoverySemaphore.WaitAsync(cancellationToken);
        try
        {
            if (configuration != null)
            {
                Configure(configuration);
            }

            var result = await DiscoverAsync(cancellationToken);

            if (result.IsSuccess)
            {
                await RegisterDiscoveredComponentsAsync(result, cancellationToken);
            }

            return result;
        }
        finally
        {
            _discoverySemaphore.Release();
        }
    }

    /// <summary>
    /// Discovers all components without registering them.
    /// </summary>
    public async Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting component discovery with {Count} strategies", _strategies.Count);

        var result = new DiscoveryResult();
        var discoveryTasks = new List<Task>();

        // Order strategies by priority
        var orderedStrategies = _strategies
            .Where(s => s.IsEnabled)
            .OrderByDescending(s => s.Priority)
            .ToList();

        if (!orderedStrategies.Any())
        {
            _logger?.LogWarning("No enabled discovery strategies found");
            return result;
        }

        foreach (var strategy in orderedStrategies)
        {
            var task = DiscoverWithStrategyAsync(strategy, result, cancellationToken);
            discoveryTasks.Add(task);
        }

        await Task.WhenAll(discoveryTasks);

        _logger?.LogInformation(
            "Discovery completed. Found {Total} components ({Success} valid, {Failed} invalid)",
            result.TotalDiscovered, result.SuccessfulComponents.Count, result.FailedComponents.Count);

        return result;
    }

    private async Task DiscoverWithStrategyAsync(
        IComponentDiscoveryStrategy strategy,
        DiscoveryResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Running discovery strategy: {StrategyName}", strategy.Name);

            var components = await strategy.DiscoverAsync(cancellationToken);

            foreach (var component in components)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Validate component
                var validationResult = _validator.Validate(component);
                if (validationResult.IsValid)
                {
                    var key = $"{component.Attribute?.Id ?? component.ComponentType.FullName}";
                    if (_discoveredComponents.TryAdd(key, component))
                    {
                        result.AddSuccessful(component);
                        _logger?.LogDebug("Added component: {ComponentId}", key);
                    }
                    else
                    {
                        _logger?.LogWarning("Duplicate component ID: {ComponentId}", key);
                        result.AddFailed(component, "Duplicate component ID");
                    }
                }
                else
                {
                    result.AddFailed(component, string.Join("; ", validationResult.Errors));
                    _logger?.LogWarning("Component validation failed: {ComponentType} - {Errors}",
                        component.ComponentType.Name, string.Join("; ", validationResult.Errors));
                }
            }

            _logger?.LogDebug("Strategy {StrategyName} discovered {Count} components",
                strategy.Name, components.Count());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in discovery strategy: {StrategyName}", strategy.Name);
            result.AddError($"Strategy {strategy.Name}: {ex.Message}");
        }
    }

    private Task RegisterDiscoveredComponentsAsync(
        DiscoveryResult result,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Registering {Count} discovered components", result.SuccessfulComponents.Count);

        foreach (var discovered in result.SuccessfulComponents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Create component instance
                var instance = Activator.CreateInstance(discovered.ComponentType) as IPluggableComponent;
                if (instance == null)
                {
                    _logger?.LogError("Failed to create instance of {Type}", discovered.ComponentType.Name);
                    continue;
                }

                // Create descriptor
                var descriptor = discovered.Attribute != null
                    ? ComponentDescriptor.FromAttribute(discovered.ComponentType, discovered.Attribute)
                    : ComponentDescriptor.FromComponent(instance);

                descriptor.DiscoverySource = discovered.DiscoverySource;
                descriptor.Instance = instance;

                // Register with registry
                if (_registry.Register(instance, descriptor))
                {
                    _logger?.LogInformation("Registered component: {ComponentId}", descriptor.Id);
                }
                else
                {
                    _logger?.LogWarning("Failed to register component: {ComponentId}", descriptor.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to register component: {Type}", discovered.ComponentType.Name);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all discovered components.
    /// </summary>
    public IEnumerable<DiscoveredComponent> GetDiscoveredComponents()
    {
        return _discoveredComponents.Values.ToList();
    }

    /// <summary>
    /// Clears all discovered components.
    /// </summary>
    public void Clear()
    {
        _discoveredComponents.Clear();
    }

    /// <summary>
    /// Disposes of the discovery service.
    /// </summary>
    public void Dispose()
    {
        foreach (var strategy in _strategies.OfType<IDisposable>())
        {
            strategy.Dispose();
        }
        _strategies.Clear();
        _discoverySemaphore?.Dispose();
    }
}

/// <summary>
/// Result of component discovery.
/// </summary>
public class DiscoveryResult
{
    private readonly List<DiscoveredComponent> _successful = new();
    private readonly List<(DiscoveredComponent Component, string Reason)> _failed = new();
    private readonly List<string> _errors = new();

    /// <summary>
    /// Gets the successful components.
    /// </summary>
    public IReadOnlyList<DiscoveredComponent> SuccessfulComponents => _successful.AsReadOnly();

    /// <summary>
    /// Gets the failed components.
    /// </summary>
    public IReadOnlyList<(DiscoveredComponent Component, string Reason)> FailedComponents => _failed.AsReadOnly();

    /// <summary>
    /// Gets the discovery errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets the total number of discovered components.
    /// </summary>
    public int TotalDiscovered => _successful.Count + _failed.Count;

    /// <summary>
    /// Gets a value indicating whether discovery was successful.
    /// </summary>
    public bool IsSuccess => _errors.Count == 0;

    /// <summary>
    /// Adds a successful component.
    /// </summary>
    internal void AddSuccessful(DiscoveredComponent component)
    {
        _successful.Add(component);
    }

    /// <summary>
    /// Adds a failed component.
    /// </summary>
    internal void AddFailed(DiscoveredComponent component, string reason)
    {
        _failed.Add((component, reason));
    }

    /// <summary>
    /// Adds an error.
    /// </summary>
    internal void AddError(string error)
    {
        _errors.Add(error);
    }
}

/// <summary>
/// Validates discovered components.
/// </summary>
public class ComponentValidator
{
    private readonly ILogger<ComponentValidator>? _logger;

    /// <summary>
    /// Initializes a new instance of the ComponentValidator class.
    /// </summary>
    public ComponentValidator(ILogger<ComponentValidator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a discovered component.
    /// </summary>
    public ValidationResult Validate(DiscoveredComponent component)
    {
        var result = new ValidationResult();

        if (component == null)
        {
            result.AddError("Component is null");
            return result;
        }

        if (component.ComponentType == null)
        {
            result.AddError("Component type is null");
            return result;
        }

        // Check if type implements IPluggableComponent
        if (!typeof(IPluggableComponent).IsAssignableFrom(component.ComponentType))
        {
            result.AddError($"Type {component.ComponentType.Name} does not implement IPluggableComponent");
        }

        // Check for ComponentAttribute
        if (component.Attribute == null)
        {
            result.AddError($"Type {component.ComponentType.Name} does not have ComponentAttribute");
        }
        else
        {
            // Validate attribute properties
            if (string.IsNullOrWhiteSpace(component.Attribute.Id))
            {
                result.AddError("Component ID is required");
            }

            if (string.IsNullOrWhiteSpace(component.Attribute.Name))
            {
                result.AddError("Component name is required");
            }

            if (string.IsNullOrWhiteSpace(component.Attribute.Version))
            {
                result.AddError("Component version is required");
            }
        }

        // Check for parameterless constructor
        if (!component.ComponentType.GetConstructors().Any(c => c.GetParameters().Length == 0))
        {
            result.AddError($"Type {component.ComponentType.Name} must have a parameterless constructor");
        }

        // Check if abstract or interface
        if (component.ComponentType.IsAbstract || component.ComponentType.IsInterface)
        {
            result.AddError($"Type {component.ComponentType.Name} cannot be abstract or an interface");
        }

        return result;
    }
}

/// <summary>
/// Result of component validation.
/// </summary>
public class ValidationResult
{
    private readonly List<string> _errors = new();

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets a value indicating whether validation passed.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    internal void AddError(string error)
    {
        _errors.Add(error);
    }
}