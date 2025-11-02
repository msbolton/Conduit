using System.Reflection;
using System.Runtime.Loader;
using Conduit.Api;
using Conduit.Common.Reflection;
using Microsoft.Extensions.Logging;

namespace Conduit.Core.Discovery.Strategies;

/// <summary>
/// Discovery strategy that scans assemblies for components.
/// This is the default strategy that scans specified assemblies and namespaces
/// for types annotated with ComponentAttribute.
/// </summary>
public class AssemblyScanningStrategy : IComponentDiscoveryStrategy
{
    private readonly ILogger<AssemblyScanningStrategy>? _logger;
    private ComponentDiscoveryConfiguration _configuration = new();
    private readonly AssemblyScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the AssemblyScanningStrategy class.
    /// </summary>
    public AssemblyScanningStrategy(ILogger<AssemblyScanningStrategy>? logger = null)
    {
        _logger = logger;
        _scanner = new AssemblyScanner();
    }

    /// <inheritdoc />
    public string Name => "AssemblyScanning";

    /// <inheritdoc />
    public int Priority => 1000; // High priority to ensure assembly components are discovered first

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public async Task<IEnumerable<DiscoveredComponent>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting assembly scanning for components in packages: {Packages}",
            string.Join(", ", _configuration.ScanPackages));

        var discoveredComponents = new List<DiscoveredComponent>();

        try
        {
            // Scan current app domain
            _scanner.Clear();
            _scanner.ScanAppDomain()
                   .ExcludeSystemAssemblies();

            // Find types with ComponentAttribute
            var componentTypes = _scanner.FindTypesWithAttribute<ComponentAttribute>()
                .Where(ValidateComponentType)
                .ToList();

            _logger?.LogDebug("Found {Count} component types", componentTypes.Count);

            foreach (var type in componentTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var attribute = type.GetCustomAttribute<ComponentAttribute>();
                if (attribute == null) continue;

                // Check if type is in configured packages
                if (!IsInConfiguredPackages(type))
                {
                    _logger?.LogDebug("Skipping type {Type} - not in configured packages", type.FullName);
                    continue;
                }

                var component = new DiscoveredComponent
                {
                    ComponentType = type,
                    Attribute = attribute,
                    DiscoverySource = Name,
                    AssemblyPath = type.Assembly.Location,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Assembly"] = type.Assembly.FullName ?? string.Empty,
                        ["Namespace"] = type.Namespace ?? string.Empty,
                        ["IsAbstract"] = type.IsAbstract,
                        ["IsSealed"] = type.IsSealed
                    }
                };

                discoveredComponents.Add(component);
                _logger?.LogInformation("Discovered component: {ComponentId} ({Type})",
                    attribute.Id, type.Name);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Assembly scanning was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during assembly scanning");
            if (!_configuration.IgnoreErrors)
                throw;
        }

        _logger?.LogInformation("Assembly scanning completed. Found {Count} components",
            discoveredComponents.Count);

        return await Task.FromResult(discoveredComponents);
    }

    /// <inheritdoc />
    public AssemblyLoadContext? GetLoadContext()
    {
        // Assembly components use the default context
        return AssemblyLoadContext.Default;
    }

    /// <inheritdoc />
    public IsolationRequirements GetDefaultIsolation()
    {
        // Assembly components typically don't need isolation
        return IsolationRequirements.None();
    }

    /// <inheritdoc />
    public void Initialize(ComponentDiscoveryConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger?.LogDebug("Initialized with configuration: {Packages}",
            string.Join(", ", configuration.ScanPackages));
    }

    /// <inheritdoc />
    public bool ValidateComponentType(Type componentType)
    {
        if (componentType == null)
            return false;

        // Must implement IPluggableComponent
        if (!typeof(IPluggableComponent).IsAssignableFrom(componentType))
        {
            _logger?.LogDebug("Type {Type} does not implement IPluggableComponent", componentType.FullName);
            return false;
        }

        // Must not be abstract or interface
        if (componentType.IsAbstract || componentType.IsInterface)
        {
            _logger?.LogDebug("Type {Type} is abstract or interface", componentType.FullName);
            return false;
        }

        // Must have parameterless constructor
        if (!componentType.GetConstructors().Any(c => c.GetParameters().Length == 0))
        {
            _logger?.LogDebug("Type {Type} does not have a parameterless constructor", componentType.FullName);
            return false;
        }

        // Must have ComponentAttribute
        var attribute = componentType.GetCustomAttribute<ComponentAttribute>();
        if (attribute == null)
        {
            _logger?.LogDebug("Type {Type} does not have ComponentAttribute", componentType.FullName);
            return false;
        }

        return true;
    }

    private bool IsInConfiguredPackages(Type type)
    {
        if (_configuration.ScanPackages.Count == 0)
            return true; // No filter, include all

        var typeNamespace = type.Namespace ?? string.Empty;
        return _configuration.ScanPackages.Any(package =>
            typeNamespace.StartsWith(package, StringComparison.OrdinalIgnoreCase));
    }
}