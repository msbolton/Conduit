using Conduit.Api;

namespace Conduit.Core;

/// <summary>
/// Describes metadata and runtime information about a component.
/// </summary>
public class ComponentDescriptor
{
    /// <summary>
    /// Gets or sets the component ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the component description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component type.
    /// </summary>
    public Type ComponentType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the component instance.
    /// </summary>
    public IPluggableComponent? Instance { get; set; }

    /// <summary>
    /// Gets or sets the component state.
    /// </summary>
    public ComponentState State { get; set; } = ComponentState.Uninitialized;

    /// <summary>
    /// Gets or sets the component configuration.
    /// </summary>
    public ComponentConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the component dependencies.
    /// </summary>
    public HashSet<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the components that depend on this component.
    /// </summary>
    public HashSet<string> Dependents { get; set; } = new();

    /// <summary>
    /// Gets or sets the isolation requirements.
    /// </summary>
    public IsolationRequirements IsolationRequirements { get; set; } = IsolationRequirements.Standard();

    /// <summary>
    /// Gets or sets the load priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether the component is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the component should auto-start.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the component tags for categorization.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the component metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the time when the component was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the time when the component was last activated.
    /// </summary>
    public DateTimeOffset? LastActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets the error information if the component failed.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the assembly containing the component.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Gets or sets the discovery source.
    /// </summary>
    public string? DiscoverySource { get; set; }

    /// <summary>
    /// Creates a descriptor from a component instance.
    /// </summary>
    public static ComponentDescriptor FromComponent(IPluggableComponent component)
    {
        return new ComponentDescriptor
        {
            Id = component.Id,
            Name = component.Name,
            Version = component.Version,
            Description = component.Description,
            ComponentType = component.GetType(),
            Instance = component,
            IsolationRequirements = component.IsolationRequirements,
            AssemblyName = component.GetType().Assembly.FullName
        };
    }

    /// <summary>
    /// Creates a descriptor from a component attribute.
    /// </summary>
    public static ComponentDescriptor FromAttribute(Type type, ComponentAttribute attribute)
    {
        return new ComponentDescriptor
        {
            Id = attribute.Id,
            Name = attribute.Name,
            Version = attribute.Version,
            Description = attribute.Description ?? string.Empty,
            ComponentType = type,
            Priority = attribute.Priority,
            IsEnabled = attribute.IsEnabledByDefault,
            AutoStart = attribute.AutoStart,
            IsolationRequirements = new IsolationRequirements { Level = attribute.IsolationLevel },
            Tags = new HashSet<string>(attribute.Tags ?? Array.Empty<string>()),
            Dependencies = new HashSet<string>(attribute.Dependencies ?? Array.Empty<string>()),
            AssemblyName = type.Assembly.FullName
        };
    }
}