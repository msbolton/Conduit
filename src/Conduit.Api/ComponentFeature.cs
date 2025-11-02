namespace Conduit.Api;

/// <summary>
/// Represents a feature exposed by a component.
/// </summary>
public class ComponentFeature
{
    /// <summary>
    /// Gets or sets the feature ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the feature description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the feature version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a value indicating whether the feature is enabled by default.
    /// </summary>
    public bool IsEnabledByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets the feature category.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Gets or sets the required permissions to use this feature.
    /// </summary>
    public HashSet<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the feature dependencies.
    /// </summary>
    public HashSet<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the feature configuration schema.
    /// </summary>
    public string? ConfigurationSchema { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a new feature with basic properties.
    /// </summary>
    public static ComponentFeature Create(string id, string name, string description)
    {
        return new ComponentFeature
        {
            Id = id,
            Name = name,
            Description = description
        };
    }
}