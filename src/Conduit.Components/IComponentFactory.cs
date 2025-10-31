using System.Reflection;
using Conduit.Api;

namespace Conduit.Components;

/// <summary>
/// Interface for creating component instances.
/// </summary>
public interface IComponentFactory
{
    /// <summary>
    /// Registers a component type.
    /// </summary>
    void RegisterComponentType(Type componentType);

    /// <summary>
    /// Registers a component type using generics.
    /// </summary>
    void RegisterComponentType<TComponent>()
        where TComponent : IPluggableComponent;

    /// <summary>
    /// Creates a component instance by ID.
    /// </summary>
    IPluggableComponent CreateComponent(string componentId);

    /// <summary>
    /// Creates a component instance by type.
    /// </summary>
    IPluggableComponent CreateComponent(Type componentType);

    /// <summary>
    /// Creates a component instance using generics.
    /// </summary>
    TComponent CreateComponent<TComponent>()
        where TComponent : IPluggableComponent;

    /// <summary>
    /// Checks if a component type is registered.
    /// </summary>
    bool IsRegistered(string componentId);

    /// <summary>
    /// Gets all registered component IDs.
    /// </summary>
    IEnumerable<string> GetRegisteredComponentIds();

    /// <summary>
    /// Gets all registered component types.
    /// </summary>
    IEnumerable<Type> GetRegisteredComponentTypes();

    /// <summary>
    /// Scans an assembly for component types and registers them.
    /// </summary>
    void ScanAssembly(Assembly assembly);

    /// <summary>
    /// Clears all singleton instances.
    /// </summary>
    void ClearSingletons();
}