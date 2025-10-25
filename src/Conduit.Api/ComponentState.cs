namespace Conduit.Api;

/// <summary>
/// Represents the lifecycle state of a component.
/// </summary>
public enum ComponentState
{
    /// <summary>
    /// Component has been created but not initialized.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// Component has been registered but not initialized.
    /// </summary>
    Registered,

    /// <summary>
    /// Component is being initialized.
    /// </summary>
    Initializing,

    /// <summary>
    /// Component has been initialized and is ready to start.
    /// </summary>
    Initialized,

    /// <summary>
    /// Component is starting up.
    /// </summary>
    Starting,

    /// <summary>
    /// Component is running and active.
    /// </summary>
    Running,

    /// <summary>
    /// Component is stopping.
    /// </summary>
    Stopping,

    /// <summary>
    /// Component has been stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Component has failed and is not operational.
    /// </summary>
    Failed,

    /// <summary>
    /// Component is attempting to recover from a failure.
    /// </summary>
    Recovering,

    /// <summary>
    /// Component has recovered from a failure.
    /// </summary>
    Recovered,

    /// <summary>
    /// Component is being disposed.
    /// </summary>
    Disposing,

    /// <summary>
    /// Component has been disposed and cannot be used.
    /// </summary>
    Disposed
}

/// <summary>
/// Extension methods for ComponentState.
/// </summary>
public static class ComponentStateExtensions
{
    /// <summary>
    /// Checks if this state represents an active/operational component.
    /// </summary>
    public static bool IsActive(this ComponentState state)
    {
        return state == ComponentState.Running;
    }

    /// <summary>
    /// Checks if this state represents a terminal state.
    /// </summary>
    public static bool IsTerminal(this ComponentState state)
    {
        return state == ComponentState.Disposed || state == ComponentState.Failed;
    }

    /// <summary>
    /// Checks if the component can be started from this state.
    /// </summary>
    public static bool CanStart(this ComponentState state)
    {
        return state == ComponentState.Initialized ||
               state == ComponentState.Stopped ||
               state == ComponentState.Recovered;
    }

    /// <summary>
    /// Checks if the component can be stopped from this state.
    /// </summary>
    public static bool CanStop(this ComponentState state)
    {
        return state == ComponentState.Running;
    }

    /// <summary>
    /// Checks if the component can transition to the specified state.
    /// </summary>
    public static bool CanTransitionTo(this ComponentState current, ComponentState target)
    {
        return (current, target) switch
        {
            (ComponentState.Uninitialized, ComponentState.Registered) => true,
            (ComponentState.Uninitialized, ComponentState.Initializing) => true,
            (ComponentState.Registered, ComponentState.Initializing) => true,
            (ComponentState.Initializing, ComponentState.Initialized) => true,
            (ComponentState.Initializing, ComponentState.Failed) => true,
            (ComponentState.Initialized, ComponentState.Starting) => true,
            (ComponentState.Starting, ComponentState.Running) => true,
            (ComponentState.Starting, ComponentState.Failed) => true,
            (ComponentState.Running, ComponentState.Stopping) => true,
            (ComponentState.Stopping, ComponentState.Stopped) => true,
            (ComponentState.Stopping, ComponentState.Failed) => true,
            (ComponentState.Stopped, ComponentState.Starting) => true,
            (ComponentState.Stopped, ComponentState.Disposing) => true,
            (ComponentState.Failed, ComponentState.Recovering) => true,
            (ComponentState.Recovering, ComponentState.Recovered) => true,
            (ComponentState.Recovering, ComponentState.Failed) => true,
            (ComponentState.Recovered, ComponentState.Starting) => true,
            (_, ComponentState.Disposing) when !current.IsTerminal() => true,
            (ComponentState.Disposing, ComponentState.Disposed) => true,
            _ => false
        };
    }
}