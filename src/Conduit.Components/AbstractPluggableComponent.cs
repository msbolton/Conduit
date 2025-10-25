using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conduit.Components
{
    /// <summary>
    /// Abstract base class for pluggable components.
    /// Provides default implementations for common component functionality.
    /// </summary>
    public abstract class AbstractPluggableComponent : IPluggableComponent
    {
        protected readonly ILogger Logger;
        private ComponentState _state;
        private readonly object _stateLock = new();
        protected ComponentContext? Context;
        protected ComponentConfiguration? Configuration;
        protected ISecurityContext? SecurityContext;
        protected ComponentManifest Manifest;

        /// <summary>
        /// Creates a new pluggable component.
        /// </summary>
        protected AbstractPluggableComponent(ILogger? logger = null)
        {
            Logger = logger ?? NullLogger.Instance;
            _state = ComponentState.Uninitialized;

            // Load component data from attribute or create default
            Manifest = ComponentManifest.FromType(GetType());
        }

        // IComponent implementation

        public string Id => Manifest.Id;
        public string Name => Manifest.Name;
        public string Version => Manifest.Version;
        public string Description => Manifest.Description;

        public ComponentConfiguration? GetConfiguration() => Configuration;

        public void SetConfiguration(ComponentConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IServiceProvider? GetServices()
        {
            return Context?.Services;
        }

        public IMessageBus? GetMessageBus()
        {
            return Context?.Find<IMessageBus>();
        }

        public void SetSecurityContext(ISecurityContext context)
        {
            SecurityContext = context;
        }

        public ISecurityContext? GetSecurityContext()
        {
            return SecurityContext;
        }

        // Component lifecycle implementation

        public ComponentState GetState()
        {
            lock (_stateLock)
            {
                return _state;
            }
        }

        public async Task TransitionToAsync(ComponentState targetState, CancellationToken cancellationToken = default)
        {
            ComponentState currentState;

            lock (_stateLock)
            {
                currentState = _state;

                if (!IsValidTransition(currentState, targetState))
                {
                    throw new InvalidOperationException(
                        $"Invalid state transition from {currentState} to {targetState}");
                }

                _state = targetState;
            }

            Logger.LogInformation("Component {ComponentId} transitioned from {OldState} to {NewState}",
                Id, currentState, targetState);

            await Task.CompletedTask;
        }

        public async Task InitializeAsync(ComponentConfiguration config, CancellationToken cancellationToken = default)
        {
            ComponentState currentState;

            lock (_stateLock)
            {
                currentState = _state;
                if (currentState != ComponentState.Uninitialized)
                {
                    throw new InvalidOperationException(
                        $"Cannot initialize component from state {currentState}");
                }
                _state = ComponentState.Initializing;
            }

            try
            {
                Configuration = config;
                await OnInitializeAsync(cancellationToken);

                lock (_stateLock)
                {
                    _state = ComponentState.Initialized;
                }

                Logger.LogInformation("Component {ComponentId} initialized", Id);
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _state = ComponentState.Failed;
                }
                Logger.LogError(ex, "Component {ComponentId} initialization failed", Id);
                throw;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ComponentState currentState;

            lock (_stateLock)
            {
                currentState = _state;
                if (currentState != ComponentState.Initialized)
                {
                    throw new InvalidOperationException(
                        $"Cannot start component from state {currentState}");
                }
                _state = ComponentState.Starting;
            }

            try
            {
                await OnStartAsync(cancellationToken);

                lock (_stateLock)
                {
                    _state = ComponentState.Running;
                }

                Logger.LogInformation("Component {ComponentId} started", Id);
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _state = ComponentState.Failed;
                }
                Logger.LogError(ex, "Component {ComponentId} start failed", Id);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            ComponentState currentState;

            lock (_stateLock)
            {
                currentState = _state;
                if (currentState != ComponentState.Running)
                {
                    Logger.LogWarning("Component {ComponentId} stop requested from state {State}", Id, currentState);
                    return;
                }
                _state = ComponentState.Stopping;
            }

            try
            {
                await OnStopAsync(cancellationToken);

                lock (_stateLock)
                {
                    _state = ComponentState.Stopped;
                }

                Logger.LogInformation("Component {ComponentId} stopped", Id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error stopping component {ComponentId}", Id);
                lock (_stateLock)
                {
                    _state = ComponentState.Stopped;
                }
            }
        }

        public async Task DisposeAsync()
        {
            try
            {
                await OnDisposeAsync();

                lock (_stateLock)
                {
                    _state = ComponentState.Disposed;
                }

                Logger.LogInformation("Component {ComponentId} disposed", Id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error disposing component {ComponentId}", Id);
            }
        }

        public ComponentHealth CheckHealth()
        {
            var currentState = GetState();

            if (currentState == ComponentState.Running)
            {
                var health = PerformHealthCheck();
                return health ?? ComponentHealth.Healthy(Id);
            }
            else
            {
                return ComponentHealth.Unhealthy(Id,
                    $"Component not running. State: {currentState}");
            }
        }

        public ComponentMetrics GetMetrics()
        {
            var metrics = new ComponentMetrics(Id);
            CollectMetrics(metrics);
            return metrics;
        }

        public async Task RecoverAsync(ComponentError error, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Attempting recovery for component {ComponentId} from error: {Error}",
                Id, error.Message);

            try
            {
                await OnRecoverAsync(error, cancellationToken);

                lock (_stateLock)
                {
                    if (_state == ComponentState.Failed)
                    {
                        _state = ComponentState.Recovered;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Recovery failed for component {ComponentId}", Id);
                lock (_stateLock)
                {
                    _state = ComponentState.Failed;
                }
                throw;
            }
        }

        // IPluggableComponent implementation

        public void OnAttach(ComponentContext context)
        {
            Context = context;
            Logger.LogInformation("Component {ComponentId} attached to host", Id);
        }

        public void OnDetach()
        {
            Context = null;
            Logger.LogInformation("Component {ComponentId} detached from host", Id);
        }

        public ComponentManifest GetManifest()
        {
            return Manifest;
        }

        public virtual IBehaviorContribution[] ContributeBehaviors()
        {
            // Override in subclasses to contribute behaviors
            return Array.Empty<IBehaviorContribution>();
        }

        public virtual ComponentFeature[] ExposeFeatures()
        {
            // Override in subclasses to expose features
            return Array.Empty<ComponentFeature>();
        }

        public virtual ServiceContract[] ProvideServices()
        {
            // Override in subclasses to provide services
            return Array.Empty<ServiceContract>();
        }

        public virtual MessageHandlerRegistration[] RegisterHandlers()
        {
            // Override in subclasses to register message handlers
            return Array.Empty<MessageHandlerRegistration>();
        }

        // Protected lifecycle hooks for subclasses

        /// <summary>
        /// Called during component initialization.
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken = default)
        {
            // Override in subclasses
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the component is starting.
        /// </summary>
        protected virtual Task OnStartAsync(CancellationToken cancellationToken = default)
        {
            // Override in subclasses
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the component is stopping.
        /// </summary>
        protected virtual Task OnStopAsync(CancellationToken cancellationToken = default)
        {
            // Override in subclasses
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the component is being disposed.
        /// </summary>
        protected virtual Task OnDisposeAsync()
        {
            // Override in subclasses
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when recovery is attempted.
        /// </summary>
        protected virtual Task OnRecoverAsync(ComponentError error, CancellationToken cancellationToken = default)
        {
            // Override in subclasses
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs a health check.
        /// Override to provide custom health checking.
        /// </summary>
        protected virtual ComponentHealth? PerformHealthCheck()
        {
            return ComponentHealth.Healthy(Id);
        }

        /// <summary>
        /// Collects component metrics.
        /// Override to add custom metrics.
        /// </summary>
        protected virtual void CollectMetrics(ComponentMetrics metrics)
        {
            // Override in subclasses to collect metrics
        }

        // Helper methods

        /// <summary>
        /// Checks if a state transition is valid.
        /// </summary>
        protected virtual bool IsValidTransition(ComponentState from, ComponentState to)
        {
            // Define valid state transitions
            return (from, to) switch
            {
                (ComponentState.Uninitialized, ComponentState.Registered) => true,
                (ComponentState.Uninitialized, ComponentState.Initializing) => true,
                (ComponentState.Registered, ComponentState.Initializing) => true,
                (ComponentState.Initializing, ComponentState.Initialized) => true,
                (ComponentState.Initializing, ComponentState.Failed) => true,
                (ComponentState.Initialized, ComponentState.Starting) => true,
                (ComponentState.Initialized, ComponentState.Disposing) => true,
                (ComponentState.Starting, ComponentState.Running) => true,
                (ComponentState.Starting, ComponentState.Failed) => true,
                (ComponentState.Running, ComponentState.Stopping) => true,
                (ComponentState.Running, ComponentState.Failed) => true,
                (ComponentState.Stopping, ComponentState.Stopped) => true,
                (ComponentState.Stopped, ComponentState.Disposing) => true,
                (ComponentState.Stopped, ComponentState.Starting) => true,
                (ComponentState.Failed, ComponentState.Recovering) => true,
                (ComponentState.Failed, ComponentState.Disposing) => true,
                (ComponentState.Recovering, ComponentState.Recovered) => true,
                (ComponentState.Recovering, ComponentState.Failed) => true,
                (ComponentState.Recovered, ComponentState.Starting) => true,
                (ComponentState.Recovered, ComponentState.Disposing) => true,
                (ComponentState.Disposing, ComponentState.Disposed) => true,
                (ComponentState.Disposed, _) => false, // Terminal state
                _ => false
            };
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}