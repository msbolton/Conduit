namespace Conduit.Api;

/// <summary>
/// Contains metadata about a component.
/// </summary>
public class ComponentManifest
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
    /// Gets or sets the component author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component license.
    /// </summary>
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component website.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the component repository URL.
    /// </summary>
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Gets or sets the component icon URL or path.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the component tags for categorization.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the component dependencies.
    /// </summary>
    public List<ComponentDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the minimum framework version required.
    /// </summary>
    public string MinFrameworkVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the maximum framework version supported.
    /// </summary>
    public string? MaxFrameworkVersion { get; set; }

    /// <summary>
    /// Gets or sets the component entry point assembly.
    /// </summary>
    public string? EntryAssembly { get; set; }

    /// <summary>
    /// Gets or sets the component entry point type.
    /// </summary>
    public string? EntryType { get; set; }

    /// <summary>
    /// Gets or sets configuration schema for validation.
    /// </summary>
    public string? ConfigurationSchema { get; set; }

    /// <summary>
    /// Gets or sets the component release date.
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the component scope/lifetime.
    /// </summary>
    public object? Scope { get; set; }

    /// <summary>
    /// Creates a basic manifest.
    /// </summary>
    public static ComponentManifest Create(string id, string name, string version, string description)
    {
        return new ComponentManifest
        {
            Id = id,
            Name = name,
            Version = version,
            Description = description
        };
    }
}

/// <summary>
/// Represents a component dependency.
/// </summary>
public class ComponentDependency
{
    /// <summary>
    /// Gets or sets the dependency ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dependency name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum version required.
    /// </summary>
    public string MinVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the maximum version supported.
    /// </summary>
    public string? MaxVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this dependency is optional.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets the dependency type.
    /// </summary>
    public DependencyType Type { get; set; } = DependencyType.Required;
}

/// <summary>
/// Defines dependency types.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Required dependency.
    /// </summary>
    Required,

    /// <summary>
    /// Optional dependency.
    /// </summary>
    Optional,

    /// <summary>
    /// Development-only dependency.
    /// </summary>
    Development,

    /// <summary>
    /// Peer dependency.
    /// </summary>
    Peer
}