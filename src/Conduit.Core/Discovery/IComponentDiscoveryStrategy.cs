using Conduit.Api;
using System.Runtime.Loader;

namespace Conduit.Core.Discovery;

/// <summary>
/// Interface for component discovery strategies.
/// Strategies are responsible for finding and loading components from various sources.
/// </summary>
public interface IComponentDiscoveryStrategy
{
    /// <summary>
    /// Gets the name of this discovery strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this strategy (higher values run first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gets a value indicating whether this strategy is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Discovers components using this strategy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Discovered component types</returns>
    Task<IEnumerable<DiscoveredComponent>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the assembly load context for components discovered by this strategy.
    /// </summary>
    AssemblyLoadContext? GetLoadContext();

    /// <summary>
    /// Gets the default isolation requirements for components discovered by this strategy.
    /// </summary>
    IsolationRequirements GetDefaultIsolation();

    /// <summary>
    /// Initializes the discovery strategy with configuration.
    /// </summary>
    /// <param name="configuration">Discovery configuration</param>
    void Initialize(ComponentDiscoveryConfiguration configuration);

    /// <summary>
    /// Validates that a discovered component type is valid.
    /// </summary>
    /// <param name="componentType">Component type to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateComponentType(Type componentType);
}

/// <summary>
/// Represents a discovered component.
/// </summary>
public class DiscoveredComponent
{
    /// <summary>
    /// Gets or sets the component type.
    /// </summary>
    public Type ComponentType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the component attribute.
    /// </summary>
    public ComponentAttribute? Attribute { get; set; }

    /// <summary>
    /// Gets or sets the discovery source.
    /// </summary>
    public string DiscoverySource { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly path if loaded from file.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the load context.
    /// </summary>
    public AssemblyLoadContext? LoadContext { get; set; }

    /// <summary>
    /// Gets or sets discovery metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the discovery timestamp.
    /// </summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration for component discovery.
/// </summary>
public class ComponentDiscoveryConfiguration
{
    /// <summary>
    /// Gets or sets the packages to scan.
    /// </summary>
    public List<string> ScanPackages { get; set; } = new() { "Conduit" };

    /// <summary>
    /// Gets or sets the directories to scan for plugins.
    /// </summary>
    public List<string> PluginDirectories { get; set; } = new() { "./plugins" };

    /// <summary>
    /// Gets or sets whether to enable hot reload.
    /// </summary>
    public bool EnableHotReload { get; set; } = true;

    /// <summary>
    /// Gets or sets the hot reload debounce time.
    /// </summary>
    public TimeSpan HotReloadDebounce { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to enable component isolation.
    /// </summary>
    public bool EnableIsolation { get; set; } = true;

    /// <summary>
    /// Gets or sets file patterns to include.
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new() { "*.dll" };

    /// <summary>
    /// Gets or sets file patterns to exclude.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new() { "*Test*.dll", "*Tests.dll" };

    /// <summary>
    /// Gets or sets whether to validate component dependencies.
    /// </summary>
    public bool ValidateDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum discovery timeout.
    /// </summary>
    public TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to ignore discovery errors.
    /// </summary>
    public bool IgnoreErrors { get; set; } = false;
}