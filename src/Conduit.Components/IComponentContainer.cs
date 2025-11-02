using Conduit.Api;

namespace Conduit.Components;

/// <summary>
/// Interface for hosting and managing pluggable components.
/// </summary>
public interface IComponentContainer : IDisposable
{
    /// <summary>
    /// Loads and attaches a component by ID.
    /// </summary>
    Task<IPluggableComponent> LoadComponentAsync(
        string componentId,
        ComponentConfiguration? configuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and attaches a component by type.
    /// </summary>
    Task<TComponent> LoadComponentAsync<TComponent>(
        ComponentConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
        where TComponent : IPluggableComponent;

    /// <summary>
    /// Unloads a component by ID.
    /// </summary>
    Task UnloadComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all loaded components.
    /// </summary>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all loaded components.
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a component by ID.
    /// </summary>
    IPluggableComponent? GetComponent(string componentId);

    /// <summary>
    /// Gets a component by type.
    /// </summary>
    TComponent? GetComponent<TComponent>()
        where TComponent : IPluggableComponent;

    /// <summary>
    /// Gets all loaded components.
    /// </summary>
    IEnumerable<IPluggableComponent> GetAllComponents();

    // /// <summary>
    // /// Gets all behavior contributions.
    // /// </summary>
    // IEnumerable<BehaviorContribution> GetBehaviorContributions();

    /// <summary>
    /// Gets the health status of all components.
    /// </summary>
    Task<ComponentContainerHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a component is loaded.
    /// </summary>
    bool IsLoaded(string componentId);
}