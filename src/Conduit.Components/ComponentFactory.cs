using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conduit.Components
{
    /// <summary>
    /// Factory for creating component instances.
    /// </summary>
    public class ComponentFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ComponentFactory> _logger;
        private readonly Dictionary<string, Type> _componentTypes;
        private readonly Dictionary<Type, object> _singletonInstances;

        public ComponentFactory(
            IServiceProvider serviceProvider,
            ILogger<ComponentFactory>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? serviceProvider.GetService<ILogger<ComponentFactory>>()
                ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<ComponentFactory>();
            _componentTypes = new Dictionary<string, Type>();
            _singletonInstances = new Dictionary<Type, object>();
        }

        /// <summary>
        /// Registers a component type.
        /// </summary>
        public void RegisterComponentType(Type componentType)
        {
            Guard.AgainstNull(componentType, nameof(componentType));

            if (!typeof(IPluggableComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException(
                    $"Type {componentType.FullName} does not implement IPluggableComponent",
                    nameof(componentType));
            }

            var manifest = ComponentManifest.FromType(componentType);
            _componentTypes[manifest.Id] = componentType;

            _logger.LogInformation("Registered component type {ComponentId}: {ComponentType}",
                manifest.Id, componentType.FullName);
        }

        /// <summary>
        /// Registers a component type using generics.
        /// </summary>
        public void RegisterComponentType<TComponent>()
            where TComponent : IPluggableComponent
        {
            RegisterComponentType(typeof(TComponent));
        }

        /// <summary>
        /// Creates a component instance by ID.
        /// </summary>
        public IPluggableComponent CreateComponent(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (!_componentTypes.TryGetValue(componentId, out var componentType))
            {
                throw new InvalidOperationException(
                    $"Component type not registered for ID: {componentId}");
            }

            return CreateComponent(componentType);
        }

        /// <summary>
        /// Creates a component instance by type.
        /// </summary>
        public IPluggableComponent CreateComponent(Type componentType)
        {
            Guard.AgainstNull(componentType, nameof(componentType));

            // Check if component should be singleton
            var manifest = ComponentManifest.FromType(componentType);
            if (manifest.Scope == ComponentScope.Singleton)
            {
                if (_singletonInstances.TryGetValue(componentType, out var existing))
                {
                    return (IPluggableComponent)existing;
                }
            }

            var instance = CreateComponentInstance(componentType);

            if (manifest.Scope == ComponentScope.Singleton)
            {
                _singletonInstances[componentType] = instance;
            }

            _logger.LogInformation("Created component instance: {ComponentId} ({ComponentType})",
                manifest.Id, componentType.FullName);

            return instance;
        }

        /// <summary>
        /// Creates a component instance using generics.
        /// </summary>
        public TComponent CreateComponent<TComponent>()
            where TComponent : IPluggableComponent
        {
            return (TComponent)CreateComponent(typeof(TComponent));
        }

        /// <summary>
        /// Checks if a component type is registered.
        /// </summary>
        public bool IsRegistered(string componentId)
        {
            return _componentTypes.ContainsKey(componentId);
        }

        /// <summary>
        /// Gets all registered component IDs.
        /// </summary>
        public IEnumerable<string> GetRegisteredComponentIds()
        {
            return _componentTypes.Keys;
        }

        /// <summary>
        /// Gets all registered component types.
        /// </summary>
        public IEnumerable<Type> GetRegisteredComponentTypes()
        {
            return _componentTypes.Values;
        }

        /// <summary>
        /// Scans an assembly for component types and registers them.
        /// </summary>
        public void ScanAssembly(Assembly assembly)
        {
            Guard.AgainstNull(assembly, nameof(assembly));

            var componentTypes = assembly.GetTypes()
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(IPluggableComponent).IsAssignableFrom(t))
                .ToList();

            foreach (var type in componentTypes)
            {
                try
                {
                    RegisterComponentType(type);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to register component type {ComponentType} from assembly {Assembly}",
                        type.FullName, assembly.FullName);
                }
            }

            _logger.LogInformation(
                "Scanned assembly {Assembly} and registered {Count} component types",
                assembly.FullName, componentTypes.Count);
        }

        /// <summary>
        /// Clears all singleton instances.
        /// </summary>
        public void ClearSingletons()
        {
            _singletonInstances.Clear();
        }

        private IPluggableComponent CreateComponentInstance(Type componentType)
        {
            // Try to use dependency injection to create the instance
            try
            {
                var instance = ActivatorUtilities.CreateInstance(_serviceProvider, componentType);
                if (instance is IPluggableComponent component)
                {
                    return component;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to create component using DI for {ComponentType}, falling back to reflection",
                    componentType.FullName);
            }

            // Fall back to reflection-based creation
            return CreateComponentViaReflection(componentType);
        }

        private IPluggableComponent CreateComponentViaReflection(Type componentType)
        {
            // Find suitable constructor
            var constructors = componentType.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Component type {componentType.FullName} has no public constructors");
            }

            // Try parameterless constructor first
            var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterlessConstructor != null)
            {
                return (IPluggableComponent)parameterlessConstructor.Invoke(null);
            }

            // Try constructor with parameters from service provider
            var constructor = constructors
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for component type {componentType.FullName}");
            }

            var parameters = constructor.GetParameters();
            var parameterValues = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var service = _serviceProvider.GetService(parameter.ParameterType);

                if (service == null && !parameter.HasDefaultValue)
                {
                    throw new InvalidOperationException(
                        $"Cannot resolve required parameter {parameter.Name} of type {parameter.ParameterType.FullName} " +
                        $"for component type {componentType.FullName}");
                }

                parameterValues[i] = service ?? parameter.DefaultValue!;
            }

            return (IPluggableComponent)constructor.Invoke(parameterValues);
        }
    }

    /// <summary>
    /// Factory options for component creation.
    /// </summary>
    public class ComponentFactoryOptions
    {
        /// <summary>
        /// Gets or sets whether to use dependency injection for component creation.
        /// </summary>
        public bool UseDependencyInjection { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to cache singleton instances.
        /// </summary>
        public bool CacheSingletons { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to scan assemblies automatically.
        /// </summary>
        public bool AutoScanAssemblies { get; set; } = true;

        /// <summary>
        /// Gets or sets the assemblies to scan for components.
        /// </summary>
        public List<Assembly> AssembliesToScan { get; set; } = new();
    }
}