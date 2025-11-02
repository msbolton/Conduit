using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core
{
    /// <summary>
    /// Provides an isolated AssemblyLoadContext for loading plugin assemblies.
    /// Implements parent-last delegation model to prevent version conflicts.
    /// </summary>
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string _pluginName;
        private readonly string _assemblyPath;
        private readonly IsolationRequirements _isolationRequirements;
        private readonly AssemblyLoadContext _parentContext;
        private readonly AssemblyDependencyResolver _resolver;
        private readonly ILogger<PluginLoadContext>? _logger;
        private readonly HashSet<string> _systemAssemblies;
        private readonly bool _isParentLast;

        /// <summary>
        /// Gets the name of this plugin context.
        /// </summary>
        public new string Name => _pluginName;

        /// <summary>
        /// Gets the path to the main plugin assembly.
        /// </summary>
        public string AssemblyPath => _assemblyPath;

        /// <summary>
        /// Gets the isolation requirements for this context.
        /// </summary>
        public IsolationRequirements IsolationRequirements => _isolationRequirements;

        /// <summary>
        /// Gets a value indicating whether parent-last loading is enabled.
        /// </summary>
        public bool IsParentLast => _isParentLast;

        /// <summary>
        /// Initializes a new instance of the PluginLoadContext class.
        /// </summary>
        /// <param name="pluginName">Name of the plugin</param>
        /// <param name="assemblyPath">Path to the plugin assembly</param>
        /// <param name="isolationRequirements">Isolation requirements</param>
        /// <param name="parentContext">Parent context for delegation</param>
        /// <param name="logger">Optional logger</param>
        public PluginLoadContext(
            string pluginName,
            string assemblyPath,
            IsolationRequirements isolationRequirements,
            AssemblyLoadContext parentContext,
            ILogger<PluginLoadContext>? logger = null)
            : base(pluginName, isCollectible: true)
        {
            Guard.NotNullOrEmpty(pluginName, nameof(pluginName));
            Guard.NotNullOrEmpty(assemblyPath, nameof(assemblyPath));
            Guard.NotNull(isolationRequirements, nameof(isolationRequirements));
            Guard.NotNull(parentContext, nameof(parentContext));

            _pluginName = pluginName;
            _assemblyPath = Path.GetFullPath(assemblyPath);
            _isolationRequirements = isolationRequirements;
            _parentContext = parentContext;
            _logger = logger;
            _isParentLast = isolationRequirements.Level == IsolationLevel.Standard;

            // Create resolver for the plugin directory
            _resolver = new AssemblyDependencyResolver(_assemblyPath);

            // Define system assemblies that should always be loaded from the parent
            _systemAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System",
                "System.Core",
                "System.Runtime",
                "System.Collections",
                "System.Linq",
                "System.Threading",
                "System.Threading.Tasks",
                "System.Memory",
                "System.Numerics",
                "System.Text",
                "System.IO",
                "System.Reflection",
                "System.ComponentModel",
                "System.Diagnostics",
                "mscorlib",
                "netstandard",
                "Microsoft.CSharp",
                "Conduit.Api" // Always use shared API from parent
            };
        }

        /// <summary>
        /// Creates a secure plugin load context with appropriate isolation settings.
        /// </summary>
        public static PluginLoadContext CreateSecure(
            string pluginName,
            string assemblyPath,
            IsolationRequirements isolationRequirements,
            AssemblyLoadContext parentContext,
            ILogger<PluginLoadContext>? logger = null)
        {
            // Apply default security settings if not specified
            if (isolationRequirements.Level == IsolationLevel.None)
            {
                isolationRequirements = new IsolationRequirements
                {
                    Level = IsolationLevel.Standard
                };
            }

            return new PluginLoadContext(pluginName, assemblyPath, isolationRequirements, parentContext, logger);
        }

        /// <summary>
        /// Loads an assembly with the specified name.
        /// </summary>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            _logger?.LogTrace("Loading assembly: {Assembly} for plugin: {Plugin}", assemblyName.Name, _pluginName);

            // Check if this is a system assembly that should be loaded from parent
            if (IsSystemAssembly(assemblyName.Name))
            {
                _logger?.LogTrace("Loading system assembly from parent: {Assembly}", assemblyName.Name);
                return null; // Let the default context handle it
            }

            // Check if the assembly is restricted
            if (!IsAssemblyAllowed(assemblyName.Name))
            {
                _logger?.LogWarning("Assembly {Assembly} is restricted for plugin {Plugin}",
                    assemblyName.Name, _pluginName);
                throw new InvalidOperationException(
                    $"Assembly '{assemblyName.Name}' is not allowed in plugin context '{_pluginName}'");
            }

            // Parent-last loading: try to load from plugin directory first
            if (_isParentLast)
            {
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null && File.Exists(assemblyPath))
                {
                    _logger?.LogTrace("Loading assembly from plugin directory: {Path}", assemblyPath);
                    return LoadFromAssemblyPath(assemblyPath);
                }
            }

            // Fallback to parent context
            _logger?.LogTrace("Delegating assembly load to parent: {Assembly}", assemblyName.Name);
            return null;
        }

        /// <summary>
        /// Loads an unmanaged library with the specified name.
        /// </summary>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            _logger?.LogTrace("Loading unmanaged DLL: {DLL} for plugin: {Plugin}", unmanagedDllName, _pluginName);

            // Check if the DLL is allowed
            if (!IsAssemblyAllowed(unmanagedDllName))
            {
                _logger?.LogWarning("Unmanaged DLL {DLL} is restricted for plugin {Plugin}",
                    unmanagedDllName, _pluginName);
                throw new InvalidOperationException(
                    $"Unmanaged DLL '{unmanagedDllName}' is not allowed in plugin context '{_pluginName}'");
            }

            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null && File.Exists(libraryPath))
            {
                _logger?.LogTrace("Loading unmanaged DLL from plugin directory: {Path}", libraryPath);
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            // Fallback to default behavior
            return IntPtr.Zero;
        }

        /// <summary>
        /// Checks if an assembly name represents a system assembly.
        /// </summary>
        private bool IsSystemAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            // Check exact matches
            if (_systemAssemblies.Contains(assemblyName))
            {
                return true;
            }

            // Check prefixes for system assemblies
            return _systemAssemblies.Any(systemAssembly =>
                assemblyName.StartsWith(systemAssembly + ".", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if an assembly is allowed based on isolation requirements.
        /// </summary>
        private bool IsAssemblyAllowed(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            // If no restrictions, everything is allowed
            if (_isolationRequirements.Level == IsolationLevel.None)
            {
                return true;
            }

            // Check blocked assemblies
            if (_isolationRequirements.BlockedAssemblies.Contains(assemblyName))
            {
                return false;
            }

            // Check allowed assemblies (if specified)
            if (_isolationRequirements.AllowedAssemblies.Count > 0 &&
                !_isolationRequirements.AllowedAssemblies.Contains(assemblyName))
            {
                _logger?.LogTrace("Assembly {Assembly} not in allowed assemblies for plugin {Plugin}",
                    assemblyName, _pluginName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads a specific type from the plugin assembly.
        /// </summary>
        public Type? LoadType(string typeName)
        {
            Guard.NotNullOrEmpty(typeName, nameof(typeName));

            try
            {
                var assembly = LoadFromAssemblyPath(_assemblyPath);
                return assembly.GetType(typeName, throwOnError: false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load type {Type} from plugin {Plugin}",
                    typeName, _pluginName);
                return null;
            }
        }

        /// <summary>
        /// Gets all types from the plugin assembly.
        /// </summary>
        public IEnumerable<Type> GetPluginTypes()
        {
            try
            {
                var assembly = LoadFromAssemblyPath(_assemblyPath);
                return assembly.GetExportedTypes();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get types from plugin {Plugin}", _pluginName);
                return Enumerable.Empty<Type>();
            }
        }

        /// <summary>
        /// Validates that the plugin assembly can be loaded.
        /// </summary>
        public bool Validate()
        {
            try
            {
                var assembly = LoadFromAssemblyPath(_assemblyPath);
                return assembly != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Plugin validation failed for {Plugin}", _pluginName);
                return false;
            }
        }

        /// <summary>
        /// Gets information about the plugin.
        /// </summary>
        public PluginInfo GetPluginInfo()
        {
            try
            {
                var assembly = LoadFromAssemblyPath(_assemblyPath);
                var assemblyName = assembly.GetName();

                return new PluginInfo
                {
                    Name = _pluginName,
                    AssemblyPath = _assemblyPath,
                    Version = assemblyName.Version?.ToString() ?? "0.0.0",
                    AssemblyName = assemblyName.Name ?? _pluginName,
                    IsolationLevel = _isolationRequirements.Level,
                    IsParentLast = _isParentLast
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get plugin info for {Plugin}", _pluginName);
                return new PluginInfo
                {
                    Name = _pluginName,
                    AssemblyPath = _assemblyPath,
                    Version = "0.0.0",
                    AssemblyName = _pluginName,
                    IsolationLevel = _isolationRequirements.Level,
                    IsParentLast = _isParentLast
                };
            }
        }

        /// <summary>
        /// Information about a loaded plugin.
        /// </summary>
        public class PluginInfo
        {
            public string Name { get; set; } = string.Empty;
            public string AssemblyPath { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string AssemblyName { get; set; } = string.Empty;
            public IsolationLevel IsolationLevel { get; set; }
            public bool IsParentLast { get; set; }
        }
    }
}