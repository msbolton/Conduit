using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.Loader;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core.Discovery.Strategies;

/// <summary>
/// Discovery strategy that watches directories for component assemblies with hot reload support.
/// This strategy monitors specified directories for changes to assemblies and can dynamically
/// load/unload components at runtime.
/// </summary>
public class FileSystemWatchingStrategy : IComponentDiscoveryStrategy, IDisposable
{
    private readonly ILogger<FileSystemWatchingStrategy>? _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Subject<ComponentChangeEvent> _componentChanges = new();
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new();
    private ComponentDiscoveryConfiguration _configuration = new();
    private IDisposable? _changeSubscription;

    /// <summary>
    /// Initializes a new instance of the FileSystemWatchingStrategy class.
    /// </summary>
    public FileSystemWatchingStrategy(ILogger<FileSystemWatchingStrategy>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "FileSystemWatching";

    /// <inheritdoc />
    public int Priority => 500; // Medium priority

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the observable stream of component changes.
    /// </summary>
    public IObservable<ComponentChangeEvent> ComponentChanges => _componentChanges.AsObservable();

    /// <inheritdoc />
    public async Task<IEnumerable<DiscoveredComponent>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting file system discovery in directories: {Directories}",
            string.Join(", ", _configuration.PluginDirectories));

        var discoveredComponents = new List<DiscoveredComponent>();

        foreach (var directory in _configuration.PluginDirectories)
        {
            if (!Directory.Exists(directory))
            {
                _logger?.LogWarning("Plugin directory does not exist: {Directory}", directory);
                continue;
            }

            // Set up file watcher if hot reload is enabled
            if (_configuration.EnableHotReload)
            {
                SetupFileWatcher(directory);
            }

            // Scan directory for assemblies
            var components = await ScanDirectoryAsync(directory, cancellationToken);
            discoveredComponents.AddRange(components);
        }

        _logger?.LogInformation("File system discovery completed. Found {Count} components",
            discoveredComponents.Count);

        return discoveredComponents;
    }

    /// <inheritdoc />
    public AssemblyLoadContext? GetLoadContext()
    {
        // Return null as each plugin has its own context
        return null;
    }

    /// <inheritdoc />
    public IsolationRequirements GetDefaultIsolation()
    {
        // External components need standard isolation
        return IsolationRequirements.Standard();
    }

    /// <inheritdoc />
    public void Initialize(ComponentDiscoveryConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Setup debounced change handling
        if (_configuration.EnableHotReload)
        {
            _changeSubscription?.Dispose();
            _changeSubscription = _componentChanges
                .Throttle(_configuration.HotReloadDebounce)
                .Subscribe(OnComponentChanged);
        }
    }

    /// <inheritdoc />
    public bool ValidateComponentType(Type componentType)
    {
        if (componentType == null)
            return false;

        // Same validation as AssemblyScanningStrategy
        if (!typeof(IPluggableComponent).IsAssignableFrom(componentType))
            return false;

        if (componentType.IsAbstract || componentType.IsInterface)
            return false;

        if (!componentType.GetConstructors().Any(c => c.GetParameters().Length == 0))
            return false;

        var attribute = componentType.GetCustomAttribute<ComponentAttribute>();
        return attribute != null;
    }

    private async Task<IEnumerable<DiscoveredComponent>> ScanDirectoryAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var components = new List<DiscoveredComponent>();

        foreach (var pattern in _configuration.IncludePatterns)
        {
            var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f))
                .ToList();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var assemblyComponents = await LoadAssemblyComponentsAsync(file, cancellationToken);
                    components.AddRange(assemblyComponents);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load assembly: {File}", file);
                    if (!_configuration.IgnoreErrors)
                        throw;
                }
            }
        }

        return components;
    }

    private async Task<IEnumerable<DiscoveredComponent>> LoadAssemblyComponentsAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Loading assembly: {Path}", assemblyPath);

        var components = new List<DiscoveredComponent>();
        var loadContext = GetOrCreateLoadContext(assemblyPath);

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var types = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<ComponentAttribute>() != null)
                .Where(ValidateComponentType)
                .ToList();

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<ComponentAttribute>()!;
                var component = new DiscoveredComponent
                {
                    ComponentType = type,
                    Attribute = attribute,
                    DiscoverySource = Name,
                    AssemblyPath = assemblyPath,
                    LoadContext = loadContext,
                    Metadata = new Dictionary<string, object>
                    {
                        ["AssemblyFile"] = Path.GetFileName(assemblyPath),
                        ["FileVersion"] = File.GetLastWriteTimeUtc(assemblyPath).ToString("O"),
                        ["LoadContextName"] = loadContext.Name ?? "Unknown"
                    }
                };

                components.Add(component);
                _logger?.LogInformation("Discovered plugin component: {ComponentId} from {File}",
                    attribute.Id, Path.GetFileName(assemblyPath));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load types from assembly: {Path}", assemblyPath);
            throw;
        }

        return await Task.FromResult(components);
    }

    private PluginLoadContext GetOrCreateLoadContext(string assemblyPath)
    {
        if (!_configuration.EnableIsolation)
        {
            // Use a shared context if isolation is disabled
            if (!_loadContexts.ContainsKey("shared"))
            {
                _loadContexts["shared"] = new PluginLoadContext(assemblyPath, "shared");
            }
            return _loadContexts["shared"];
        }

        // Create isolated context per assembly
        var contextName = Path.GetFileNameWithoutExtension(assemblyPath);
        if (!_loadContexts.TryGetValue(contextName, out var context))
        {
            context = new PluginLoadContext(assemblyPath, contextName);
            _loadContexts[contextName] = context;
        }

        return context;
    }

    private void SetupFileWatcher(string directory)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            foreach (var pattern in _configuration.IncludePatterns)
            {
                watcher.Filters.Add(pattern);
            }

            watcher.Created += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;

            _watchers.Add(watcher);
            _logger?.LogInformation("Set up file watcher for directory: {Directory}", directory);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set up file watcher for directory: {Directory}", directory);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsExcluded(e.FullPath))
            return;

        _logger?.LogDebug("File change detected: {ChangeType} - {File}", e.ChangeType, e.Name);

        var changeEvent = new ComponentChangeEvent
        {
            ChangeType = e.ChangeType switch
            {
                WatcherChangeTypes.Created => ComponentChangeType.Added,
                WatcherChangeTypes.Deleted => ComponentChangeType.Removed,
                WatcherChangeTypes.Changed => ComponentChangeType.Modified,
                _ => ComponentChangeType.Modified
            },
            FilePath = e.FullPath,
            Timestamp = DateTimeOffset.UtcNow
        };

        _componentChanges.OnNext(changeEvent);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsExcluded(e.OldFullPath))
        {
            _componentChanges.OnNext(new ComponentChangeEvent
            {
                ChangeType = ComponentChangeType.Removed,
                FilePath = e.OldFullPath,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        if (!IsExcluded(e.FullPath))
        {
            _componentChanges.OnNext(new ComponentChangeEvent
            {
                ChangeType = ComponentChangeType.Added,
                FilePath = e.FullPath,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    private void OnComponentChanged(ComponentChangeEvent changeEvent)
    {
        _logger?.LogInformation("Processing component change: {ChangeType} - {File}",
            changeEvent.ChangeType, Path.GetFileName(changeEvent.FilePath));

        // This would trigger component reload in the ComponentDiscoveryService
        // The actual reload logic would be handled at a higher level
    }

    private bool IsExcluded(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return _configuration.ExcludePatterns.Any(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(fileName,
                "^" + pattern.Replace("*", ".*").Replace("?", ".") + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Disposes of the file watchers and load contexts.
    /// </summary>
    public void Dispose()
    {
        _changeSubscription?.Dispose();
        _componentChanges.Dispose();

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach (var context in _loadContexts.Values)
        {
            context.Unload();
        }
        _loadContexts.Clear();
    }
}

/// <summary>
/// Represents a component change event.
/// </summary>
public class ComponentChangeEvent
{
    /// <summary>
    /// Gets or sets the type of change.
    /// </summary>
    public ComponentChangeType ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the file path that changed.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the change.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Types of component changes.
/// </summary>
public enum ComponentChangeType
{
    /// <summary>
    /// Component was added.
    /// </summary>
    Added,

    /// <summary>
    /// Component was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// Component was removed.
    /// </summary>
    Removed
}

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the PluginLoadContext class.
    /// </summary>
    public PluginLoadContext(string pluginPath, string name) : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Loads an assembly.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    /// <summary>
    /// Loads an unmanaged library.
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}