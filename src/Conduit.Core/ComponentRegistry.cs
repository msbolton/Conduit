using System.Collections.Concurrent;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core;

/// <summary>
/// Registry for managing component instances and their metadata.
/// This class serves as the central registry for all pluggable components
/// in the system, providing registration, lookup, and lifecycle management.
/// </summary>
public class ComponentRegistry : IComponentRegistry
{
    private readonly ConcurrentDictionary<string, IPluggableComponent> _components = new();
    private readonly ConcurrentDictionary<string, ComponentDescriptor> _descriptors = new();
    private readonly ConcurrentDictionary<Type, HashSet<IPluggableComponent>> _componentsByType = new();
    private readonly ILogger<ComponentRegistry>? _logger;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    /// <summary>
    /// Initializes a new instance of the ComponentRegistry class.
    /// </summary>
    public ComponentRegistry(ILogger<ComponentRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a component with its descriptor.
    /// </summary>
    /// <param name="component">The component instance</param>
    /// <param name="descriptor">The component descriptor</param>
    /// <returns>True if registered successfully, false if already exists</returns>
    public bool Register(IPluggableComponent component, ComponentDescriptor descriptor)
    {
        Guard.NotNull(component);
        Guard.NotNull(descriptor);
        Guard.NotNullOrEmpty(descriptor.Id);

        _lock.EnterWriteLock();
        try
        {
            if (_components.ContainsKey(descriptor.Id))
            {
                _logger?.LogWarning("Component with ID {ComponentId} is already registered", descriptor.Id);
                return false;
            }

            _components[descriptor.Id] = component;
            _descriptors[descriptor.Id] = descriptor;

            // Index by type for faster lookups
            var componentType = component.GetType();
            _componentsByType.AddOrUpdate(
                componentType,
                _ => new HashSet<IPluggableComponent> { component },
                (_, set) =>
                {
                    set.Add(component);
                    return set;
                });

            // Also index by interfaces
            foreach (var interfaceType in componentType.GetInterfaces())
            {
                if (interfaceType != typeof(IPluggableComponent))
                {
                    _componentsByType.AddOrUpdate(
                        interfaceType,
                        _ => new HashSet<IPluggableComponent> { component },
                        (_, set) =>
                        {
                            set.Add(component);
                            return set;
                        });
                }
            }

            descriptor.State = ComponentState.Registered;
            _logger?.LogInformation("Registered component {ComponentId} ({ComponentType})",
                descriptor.Id, componentType.Name);

            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Unregisters a component by its ID.
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <returns>True if unregistered successfully, false if not found</returns>
    public bool Unregister(string componentId)
    {
        Guard.NotNullOrEmpty(componentId);

        _lock.EnterWriteLock();
        try
        {
            if (!_components.TryRemove(componentId, out var component))
            {
                return false;
            }

            _descriptors.TryRemove(componentId, out _);

            // Remove from type index
            var componentType = component.GetType();
            if (_componentsByType.TryGetValue(componentType, out var set))
            {
                set.Remove(component);
                if (set.Count == 0)
                {
                    _componentsByType.TryRemove(componentType, out _);
                }
            }

            // Remove from interface indexes
            foreach (var interfaceType in componentType.GetInterfaces())
            {
                if (_componentsByType.TryGetValue(interfaceType, out var interfaceSet))
                {
                    interfaceSet.Remove(component);
                    if (interfaceSet.Count == 0)
                    {
                        _componentsByType.TryRemove(interfaceType, out _);
                    }
                }
            }

            _logger?.LogInformation("Unregistered component {ComponentId}", componentId);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a component by its ID.
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <returns>Component instance or null if not found</returns>
    public IPluggableComponent? GetComponent(string componentId)
    {
        Guard.NotNullOrEmpty(componentId);

        _lock.EnterReadLock();
        try
        {
            return _components.GetValueOrDefault(componentId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a component by its type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <returns>Component instance or null if not found</returns>
    public T? GetComponent<T>() where T : class, IPluggableComponent
    {
        _lock.EnterReadLock();
        try
        {
            if (_componentsByType.TryGetValue(typeof(T), out var components))
            {
                return components.FirstOrDefault() as T;
            }
            return null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all components of a specific type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <returns>Collection of components of the specified type</returns>
    public IEnumerable<T> GetComponents<T>() where T : class, IPluggableComponent
    {
        _lock.EnterReadLock();
        try
        {
            if (_componentsByType.TryGetValue(typeof(T), out var components))
            {
                return components.OfType<T>().ToList();
            }
            return Enumerable.Empty<T>();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all registered components.
    /// </summary>
    /// <returns>Collection of all components</returns>
    public IEnumerable<IPluggableComponent> GetAllComponents()
    {
        _lock.EnterReadLock();
        try
        {
            return _components.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a component descriptor by ID.
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <returns>Component descriptor or null if not found</returns>
    public ComponentDescriptor? GetDescriptor(string componentId)
    {
        Guard.NotNullOrEmpty(componentId);

        _lock.EnterReadLock();
        try
        {
            return _descriptors.GetValueOrDefault(componentId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all registered descriptors.
    /// </summary>
    /// <returns>Collection of all descriptors</returns>
    public IEnumerable<ComponentDescriptor> GetAllDescriptors()
    {
        _lock.EnterReadLock();
        try
        {
            return _descriptors.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Updates a component descriptor.
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <param name="updater">Function to update the descriptor</param>
    public void UpdateDescriptor(string componentId, Action<ComponentDescriptor> updater)
    {
        Guard.NotNullOrEmpty(componentId);
        Guard.NotNull(updater);

        _lock.EnterWriteLock();
        try
        {
            if (_descriptors.TryGetValue(componentId, out var descriptor))
            {
                updater(descriptor);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Checks if a component is registered.
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <returns>True if registered, false otherwise</returns>
    public bool IsRegistered(string componentId)
    {
        Guard.NotNullOrEmpty(componentId);

        _lock.EnterReadLock();
        try
        {
            return _components.ContainsKey(componentId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets components by their state.
    /// </summary>
    /// <param name="state">Component state</param>
    /// <returns>Collection of components in the specified state</returns>
    public IEnumerable<IPluggableComponent> GetComponentsByState(ComponentState state)
    {
        _lock.EnterReadLock();
        try
        {
            return _descriptors.Values
                .Where(d => d.State == state && d.Instance != null)
                .Select(d => d.Instance!)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets components by tag.
    /// </summary>
    /// <param name="tag">Tag to search for</param>
    /// <returns>Collection of components with the specified tag</returns>
    public IEnumerable<IPluggableComponent> GetComponentsByTag(string tag)
    {
        Guard.NotNullOrEmpty(tag);

        _lock.EnterReadLock();
        try
        {
            return _descriptors.Values
                .Where(d => d.Tags.Contains(tag) && d.Instance != null)
                .Select(d => d.Instance!)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all registered components.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _components.Clear();
            _descriptors.Clear();
            _componentsByType.Clear();
            _logger?.LogInformation("Cleared all registered components");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets statistics about the registry.
    /// </summary>
    public ComponentRegistryStatistics GetStatistics()
    {
        _lock.EnterReadLock();
        try
        {
            var states = _descriptors.Values
                .GroupBy(d => d.State)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ComponentRegistryStatistics
            {
                TotalComponents = _components.Count,
                ComponentsByState = states,
                ComponentTypes = _componentsByType.Count,
                EnabledComponents = _descriptors.Values.Count(d => d.IsEnabled),
                FailedComponents = _descriptors.Values.Count(d => d.State == ComponentState.Failed)
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Disposes of the registry.
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}

/// <summary>
/// Statistics about the component registry.
/// </summary>
public class ComponentRegistryStatistics
{
    /// <summary>
    /// Gets or sets the total number of components.
    /// </summary>
    public int TotalComponents { get; set; }

    /// <summary>
    /// Gets or sets the count of components by state.
    /// </summary>
    public Dictionary<ComponentState, int> ComponentsByState { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of distinct component types.
    /// </summary>
    public int ComponentTypes { get; set; }

    /// <summary>
    /// Gets or sets the number of enabled components.
    /// </summary>
    public int EnabledComponents { get; set; }

    /// <summary>
    /// Gets or sets the number of failed components.
    /// </summary>
    public int FailedComponents { get; set; }
}