using Conduit.Api;
using Conduit.Common;

namespace Conduit.Core;

/// <summary>
/// Interface for managing the lifecycle of components in the system.
/// This interface defines methods for initialization, startup, shutdown, and cleanup
/// of components according to their dependencies and lifecycle requirements.
/// </summary>
public interface IComponentLifecycleManager
{
    /// <summary>
    /// Initializes a component with the given configuration.
    /// </summary>
    Task<Result> InitializeComponentAsync(
        string componentId,
        ComponentConfiguration? configuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a component.
    /// </summary>
    Task<Result> StartComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a component.
    /// </summary>
    Task<Result> StopComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a component.
    /// </summary>
    Task<Result> RestartComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes a component.
    /// </summary>
    Task<Result> DisposeComponentAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes all registered components.
    /// </summary>
    Task<Result> InitializeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all initialized components with auto-start enabled.
    /// </summary>
    Task<Result> StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running components.
    /// </summary>
    Task<Result> StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a component.
    /// </summary>
    ComponentState GetComponentState(string componentId);

    /// <summary>
    /// Gets the states of all components.
    /// </summary>
    Dictionary<string, ComponentState> GetAllComponentStates();
}