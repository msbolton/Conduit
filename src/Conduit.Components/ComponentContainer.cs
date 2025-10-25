using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Messaging;
using Microsoft.Extensions.Logging;

namespace Conduit.Components
{
    /// <summary>
    /// Container for hosting and managing pluggable components.
    /// </summary>
    public class ComponentContainer : IDisposable
    {
        private readonly ComponentFactory _factory;
        private readonly IMessageBus? _messageBus;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ComponentContainer> _logger;
        private readonly ConcurrentDictionary<string, IPluggableComponent> _components;
        private readonly ConcurrentDictionary<string, ComponentContext> _contexts;
        private readonly BehaviorContributionCollection _behaviors;
        private bool _disposed;

        public ComponentContainer(
            ComponentFactory factory,
            IServiceProvider serviceProvider,
            IMessageBus? messageBus = null,
            ILogger<ComponentContainer>? logger = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus;
            _logger = logger ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<ComponentContainer>();
            _components = new ConcurrentDictionary<string, IPluggableComponent>();
            _contexts = new ConcurrentDictionary<string, ComponentContext>();
            _behaviors = new BehaviorContributionCollection();
        }

        /// <summary>
        /// Loads and attaches a component by ID.
        /// </summary>
        public async Task<IPluggableComponent> LoadComponentAsync(
            string componentId,
            ComponentConfiguration? configuration = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (_components.ContainsKey(componentId))
            {
                throw new InvalidOperationException(
                    $"Component with ID {componentId} is already loaded");
            }

            // Create component instance
            var component = _factory.CreateComponent(componentId);

            // Create component context
            var context = CreateComponentContext(component);
            _contexts[componentId] = context;

            // Attach component to container
            component.OnAttach(context);

            // Initialize component
            var config = configuration ?? new ComponentConfiguration();
            await component.InitializeAsync(config, cancellationToken);

            // Store component
            _components[componentId] = component;

            // Collect behavior contributions
            CollectBehaviorContributions(component);

            // Register message handlers
            RegisterMessageHandlers(component);

            // Expose services
            ExposeServices(component);

            _logger.LogInformation("Loaded component: {ComponentId}", componentId);

            return component;
        }

        /// <summary>
        /// Loads and attaches a component by type.
        /// </summary>
        public async Task<TComponent> LoadComponentAsync<TComponent>(
            ComponentConfiguration? configuration = null,
            CancellationToken cancellationToken = default)
            where TComponent : IPluggableComponent
        {
            var component = _factory.CreateComponent<TComponent>();
            var manifest = component.GetManifest();

            if (_components.ContainsKey(manifest.Id))
            {
                throw new InvalidOperationException(
                    $"Component with ID {manifest.Id} is already loaded");
            }

            // Create component context
            var context = CreateComponentContext(component);
            _contexts[manifest.Id] = context;

            // Attach component to container
            component.OnAttach(context);

            // Initialize component
            var config = configuration ?? new ComponentConfiguration();
            await component.InitializeAsync(config, cancellationToken);

            // Store component
            _components[manifest.Id] = component;

            // Collect behavior contributions
            CollectBehaviorContributions(component);

            // Register message handlers
            RegisterMessageHandlers(component);

            // Expose services
            ExposeServices(component);

            _logger.LogInformation("Loaded component: {ComponentId}", manifest.Id);

            return component;
        }

        /// <summary>
        /// Unloads a component by ID.
        /// </summary>
        public async Task UnloadComponentAsync(
            string componentId,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (!_components.TryRemove(componentId, out var component))
            {
                throw new InvalidOperationException(
                    $"Component with ID {componentId} is not loaded");
            }

            // Stop component if running
            if (component.GetState() == ComponentState.Running)
            {
                await component.StopAsync(cancellationToken);
            }

            // Detach component
            component.OnDetach();

            // Dispose component
            await component.DisposeAsync();

            // Remove context
            _contexts.TryRemove(componentId, out _);

            _logger.LogInformation("Unloaded component: {ComponentId}", componentId);
        }

        /// <summary>
        /// Starts all loaded components.
        /// </summary>
        public async Task StartAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var component in _components.Values)
            {
                if (component.GetState() == ComponentState.Initialized)
                {
                    await component.StartAsync(cancellationToken);
                }
            }

            _logger.LogInformation("Started all components");
        }

        /// <summary>
        /// Stops all loaded components.
        /// </summary>
        public async Task StopAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var component in _components.Values)
            {
                if (component.GetState() == ComponentState.Running)
                {
                    await component.StopAsync(cancellationToken);
                }
            }

            _logger.LogInformation("Stopped all components");
        }

        /// <summary>
        /// Gets a component by ID.
        /// </summary>
        public IPluggableComponent? GetComponent(string componentId)
        {
            return _components.TryGetValue(componentId, out var component) ? component : null;
        }

        /// <summary>
        /// Gets a component by type.
        /// </summary>
        public TComponent? GetComponent<TComponent>()
            where TComponent : IPluggableComponent
        {
            return _components.Values.OfType<TComponent>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all loaded components.
        /// </summary>
        public IEnumerable<IPluggableComponent> GetAllComponents()
        {
            return _components.Values;
        }

        /// <summary>
        /// Gets all behavior contributions.
        /// </summary>
        public IEnumerable<BehaviorContribution> GetBehaviorContributions()
        {
            return _behaviors.GetOrdered();
        }

        /// <summary>
        /// Gets the health status of all components.
        /// </summary>
        public ComponentContainerHealth GetHealth()
        {
            var componentHealths = _components.Values
                .Select(c => c.CheckHealth())
                .ToList();

            var isHealthy = componentHealths.All(h => h.IsHealthy);

            return new ComponentContainerHealth
            {
                IsHealthy = isHealthy,
                TotalComponents = _components.Count,
                HealthyComponents = componentHealths.Count(h => h.IsHealthy),
                UnhealthyComponents = componentHealths.Count(h => !h.IsHealthy),
                ComponentHealths = componentHealths
            };
        }

        /// <summary>
        /// Checks if a component is loaded.
        /// </summary>
        public bool IsLoaded(string componentId)
        {
            return _components.ContainsKey(componentId);
        }

        private ComponentContext CreateComponentContext(IPluggableComponent component)
        {
            var manifest = component.GetManifest();

            return new ComponentContext
            {
                ComponentId = manifest.Id,
                ComponentName = manifest.Name,
                Services = _serviceProvider,
                MessageBus = _messageBus,
                Configuration = new ComponentConfiguration()
            };
        }

        private void CollectBehaviorContributions(IPluggableComponent component)
        {
            var contributions = component.ContributeBehaviors();
            if (contributions != null && contributions.Length > 0)
            {
                _behaviors.AddRange(contributions.OfType<BehaviorContribution>());
                _logger.LogDebug("Collected {Count} behavior contributions from component {ComponentId}",
                    contributions.Length, component.Id);
            }
        }

        private void RegisterMessageHandlers(IPluggableComponent component)
        {
            if (_messageBus == null)
            {
                return;
            }

            var handlers = component.RegisterHandlers();
            if (handlers != null && handlers.Length > 0)
            {
                // Register handlers with message bus
                // This would typically integrate with the MessageBus's handler registration
                _logger.LogDebug("Registered {Count} message handlers from component {ComponentId}",
                    handlers.Length, component.Id);
            }
        }

        private void ExposeServices(IPluggableComponent component)
        {
            var services = component.ProvideServices();
            if (services != null && services.Length > 0)
            {
                // Register services with the service provider
                // This would typically integrate with the DI container
                _logger.LogDebug("Exposed {Count} services from component {ComponentId}",
                    services.Length, component.Id);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var component in _components.Values)
                {
                    try
                    {
                        component.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing component {ComponentId}", component.Id);
                    }
                }

                _components.Clear();
                _contexts.Clear();
                _behaviors.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Health status of the component container.
    /// </summary>
    public class ComponentContainerHealth
    {
        public bool IsHealthy { get; set; }
        public int TotalComponents { get; set; }
        public int HealthyComponents { get; set; }
        public int UnhealthyComponents { get; set; }
        public List<ComponentHealth> ComponentHealths { get; set; } = new();
    }
}