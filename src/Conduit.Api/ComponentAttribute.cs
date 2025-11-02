namespace Conduit.Api;

/// <summary>
/// Marks a class as a pluggable component that can be discovered and loaded by the framework.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ComponentAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the ComponentAttribute class.
    /// </summary>
    /// <param name="id">The unique component ID</param>
    /// <param name="name">The component name</param>
    /// <param name="version">The component version</param>
    public ComponentAttribute(string id, string name, string version = "1.0.0")
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the component name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the component version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets or sets the component description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the component author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the component category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets tags for the component.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the component is enabled by default.
    /// </summary>
    public bool IsEnabledByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the component should auto-start.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the component priority for loading order.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the minimum framework version required.
    /// </summary>
    public string? MinFrameworkVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum framework version supported.
    /// </summary>
    public string? MaxFrameworkVersion { get; set; }

    /// <summary>
    /// Gets or sets dependencies on other components.
    /// </summary>
    public string[]? Dependencies { get; set; }

    /// <summary>
    /// Gets or sets the isolation level required.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.Standard;
}