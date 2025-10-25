using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core.Discovery.Strategies
{
    /// <summary>
    /// Discovery strategy that scans directories for plugin assemblies.
    /// Creates isolated AssemblyLoadContexts for each assembly to prevent version conflicts.
    /// </summary>
    public class DirectoryDiscoveryStrategy : IComponentDiscoveryStrategy, IDisposable
    {
        private readonly ILogger<DirectoryDiscoveryStrategy>? _logger;
        private readonly List<string> _pluginDirectories;
        private readonly Dictionary<string, PluginLoadContext> _loadContexts;
        private readonly ComponentValidator _validator;
        private ComponentDiscoveryConfiguration _configuration;
        private bool _disposed;

        /// <summary>
        /// Gets the name of this discovery strategy.
        /// </summary>
        public string Name => "Directory Discovery";

        /// <summary>
        /// Gets the priority of this strategy.
        /// </summary>
        public int Priority => 500; // Medium priority - after classpath but before service loader

        /// <summary>
        /// Gets a value indicating whether this strategy is enabled.
        /// </summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Gets the plugin directories being monitored.
        /// </summary>
        public IReadOnlyList<string> PluginDirectories => _pluginDirectories.AsReadOnly();

        /// <summary>
        /// Gets the loaded plugin contexts.
        /// </summary>
        protected IReadOnlyDictionary<string, PluginLoadContext> LoadContexts => _loadContexts;

        /// <summary>
        /// Initializes a new instance of the DirectoryDiscoveryStrategy class.
        /// </summary>
        /// <param name="logger">Optional logger</param>
        public DirectoryDiscoveryStrategy(ILogger<DirectoryDiscoveryStrategy>? logger = null)
        {
            _logger = logger;
            _pluginDirectories = new List<string>();
            _loadContexts = new Dictionary<string, PluginLoadContext>();
            _validator = new ComponentValidator();
            _configuration = new ComponentDiscoveryConfiguration();
        }

        /// <summary>
        /// Initializes the discovery strategy with configuration.
        /// </summary>
        public virtual void Initialize(ComponentDiscoveryConfiguration configuration)
        {
            Guard.AgainstNull(configuration, nameof(configuration));
            _configuration = configuration;

            // Clear and update plugin directories
            _pluginDirectories.Clear();
            _pluginDirectories.AddRange(configuration.PluginDirectories);

            // Create directories if they don't exist
            foreach (var directory in _pluginDirectories)
            {
                var fullPath = Path.GetFullPath(directory);
                if (!Directory.Exists(fullPath))
                {
                    try
                    {
                        Directory.CreateDirectory(fullPath);
                        _logger?.LogInformation("Created plugin directory: {Directory}", fullPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to create plugin directory: {Directory}", fullPath);
                        if (!_configuration.IgnoreErrors)
                        {
                            throw;
                        }
                    }
                }
            }

            IsEnabled = _pluginDirectories.Count > 0;
        }

        /// <summary>
        /// Discovers components from plugin directories.
        /// </summary>
        public virtual async Task<IEnumerable<DiscoveredComponent>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            var components = new List<DiscoveredComponent>();

            foreach (var directory in _pluginDirectories)
            {
                var fullPath = Path.GetFullPath(directory);
                if (!Directory.Exists(fullPath))
                {
                    _logger?.LogWarning("Plugin directory does not exist: {Directory}", fullPath);
                    continue;
                }

                _logger?.LogInformation("Scanning plugin directory: {Directory}", fullPath);

                // Find all DLL files matching the patterns
                var dllFiles = GetPluginFiles(fullPath);

                foreach (var dllFile in dllFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var discoveredComponents = await LoadComponentsFromAssemblyAsync(dllFile, cancellationToken);
                        components.AddRange(discoveredComponents);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to load components from assembly: {Assembly}", dllFile);
                        if (!_configuration.IgnoreErrors)
                        {
                            throw;
                        }
                    }
                }
            }

            _logger?.LogInformation("Directory discovery found {Count} components", components.Count);
            return components;
        }

        /// <summary>
        /// Gets the assembly load context for components discovered by this strategy.
        /// </summary>
        public AssemblyLoadContext? GetLoadContext()
        {
            // Return null as we create separate contexts per assembly
            return null;
        }

        /// <summary>
        /// Gets the default isolation requirements for components discovered by this strategy.
        /// </summary>
        public IsolationRequirements GetDefaultIsolation()
        {
            return new IsolationRequirements
            {
                IsolationLevel = IsolationLevel.Plugin,
                UseParentLastLoading = true,
                AllowedPackages = new List<string> { "System", "Microsoft", "Conduit.Api" },
                RestrictedClasses = new List<string>()
            };
        }

        /// <summary>
        /// Validates that a discovered component type is valid.
        /// </summary>
        public bool ValidateComponentType(Type componentType)
        {
            Guard.AgainstNull(componentType, nameof(componentType));

            // Must be a class
            if (!componentType.IsClass || componentType.IsAbstract)
            {
                return false;
            }

            // Must have Component attribute
            var componentAttr = componentType.GetCustomAttribute<ComponentAttribute>();
            if (componentAttr == null)
            {
                return false;
            }

            // Must implement IPluggableComponent
            if (!typeof(IPluggableComponent).IsAssignableFrom(componentType))
            {
                return false;
            }

            // Must have a parameterless constructor or DI constructor
            var hasParameterlessConstructor = componentType.GetConstructor(Type.EmptyTypes) != null;
            var hasDIConstructor = componentType.GetConstructors()
                .Any(c => c.GetParameters().All(p => p.ParameterType.IsInterface));

            return hasParameterlessConstructor || hasDIConstructor;
        }

        /// <summary>
        /// Reloads a specific plugin assembly.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly to reload</param>
        /// <returns>Discovered components from the reloaded assembly</returns>
        public virtual async Task<IEnumerable<DiscoveredComponent>> ReloadAssemblyAsync(string assemblyPath)
        {
            Guard.AgainstNullOrEmpty(assemblyPath, nameof(assemblyPath));

            var fullPath = Path.GetFullPath(assemblyPath);
            var assemblyName = Path.GetFileNameWithoutExtension(fullPath);

            // Unload existing context if present
            if (_loadContexts.TryGetValue(assemblyName, out var existingContext))
            {
                _logger?.LogInformation("Unloading existing context for assembly: {Assembly}", assemblyName);
                _loadContexts.Remove(assemblyName);
                existingContext.Unload();
            }

            // Load the assembly again
            return await LoadComponentsFromAssemblyAsync(fullPath);
        }

        /// <summary>
        /// Gets plugin files from a directory based on configuration patterns.
        /// </summary>
        protected virtual IEnumerable<string> GetPluginFiles(string directory)
        {
            var files = new List<string>();

            foreach (var pattern in _configuration.IncludePatterns)
            {
                files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories));
            }

            // Filter out excluded patterns
            return files.Where(file =>
            {
                var fileName = Path.GetFileName(file);
                return !_configuration.ExcludePatterns.Any(pattern =>
                    FileMatchesPattern(fileName, pattern));
            });
        }

        /// <summary>
        /// Loads components from an assembly file.
        /// </summary>
        protected virtual async Task<IEnumerable<DiscoveredComponent>> LoadComponentsFromAssemblyAsync(
            string assemblyPath,
            CancellationToken cancellationToken = default)
        {
            var components = new List<DiscoveredComponent>();
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

            _logger?.LogDebug("Loading assembly: {Assembly}", assemblyPath);

            // Validate it's a valid .NET assembly
            if (!IsValidAssembly(assemblyPath))
            {
                _logger?.LogWarning("File is not a valid .NET assembly: {Assembly}", assemblyPath);
                return components;
            }

            // Create isolated load context for the plugin
            var loadContext = CreatePluginLoadContext(assemblyName, assemblyPath);
            _loadContexts[assemblyName] = loadContext;

            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                // Scan for component types
                var componentTypes = await Task.Run(() =>
                    ScanAssemblyForComponents(assembly), cancellationToken);

                foreach (var componentType in componentTypes)
                {
                    var componentAttr = componentType.GetCustomAttribute<ComponentAttribute>();

                    components.Add(new DiscoveredComponent
                    {
                        ComponentType = componentType,
                        Attribute = componentAttr,
                        DiscoverySource = Name,
                        AssemblyPath = assemblyPath,
                        LoadContext = loadContext,
                        Metadata = new Dictionary<string, object>
                        {
                            ["AssemblyName"] = assemblyName,
                            ["AssemblyVersion"] = assembly.GetName().Version?.ToString() ?? "0.0.0"
                        }
                    });

                    _logger?.LogDebug("Discovered component: {Component} from {Assembly}",
                        componentType.Name, assemblyName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to scan assembly: {Assembly}", assemblyPath);
                _loadContexts.Remove(assemblyName);
                loadContext.Unload();

                if (!_configuration.IgnoreErrors)
                {
                    throw;
                }
            }

            return components;
        }

        /// <summary>
        /// Creates an isolated plugin load context.
        /// </summary>
        protected virtual PluginLoadContext CreatePluginLoadContext(string pluginName, string assemblyPath)
        {
            var isolation = GetDefaultIsolation();
            return new PluginLoadContext(
                pluginName,
                assemblyPath,
                isolation,
                AssemblyLoadContext.Default);
        }

        /// <summary>
        /// Scans an assembly for component types.
        /// </summary>
        protected virtual IEnumerable<Type> ScanAssemblyForComponents(Assembly assembly)
        {
            var components = new List<Type>();

            try
            {
                var types = assembly.GetExportedTypes();

                foreach (var type in types)
                {
                    if (ValidateComponentType(type))
                    {
                        var validationResult = _validator.ValidateClass(type);
                        if (validationResult.IsValid)
                        {
                            components.Add(type);
                        }
                        else
                        {
                            _logger?.LogWarning("Component validation failed for {Type}: {Errors}",
                                type.Name, string.Join(", ", validationResult.Errors));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to scan assembly types: {Assembly}", assembly.FullName);
            }

            return components;
        }

        /// <summary>
        /// Checks if a file is a valid .NET assembly.
        /// </summary>
        protected virtual bool IsValidAssembly(string filePath)
        {
            try
            {
                // Try to get assembly name - this will fail if not a valid assembly
                AssemblyName.GetAssemblyName(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a filename matches a pattern (supports wildcards).
        /// </summary>
        protected bool FileMatchesPattern(string fileName, string pattern)
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + pattern
                .Replace(".", "\\.")
                .Replace("*", ".*")
                .Replace("?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(
                fileName,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Disposes of the discovery strategy and unloads all plugin contexts.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the discovery strategy.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var context in _loadContexts.Values)
                    {
                        try
                        {
                            context.Unload();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to unload plugin context: {Plugin}",
                                context.Name);
                        }
                    }

                    _loadContexts.Clear();
                }

                _disposed = true;
            }
        }
    }
}