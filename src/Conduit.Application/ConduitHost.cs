using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Conduit.Api;
using Conduit.Core;
using Conduit.Messaging;

namespace Conduit.Application
{
    /// <summary>
    /// Conduit application host.
    /// </summary>
    public class ConduitHost : IHostedService, IDisposable
    {
        private readonly ILogger<ConduitHost> _logger;
        private readonly ConduitConfiguration _configuration;
        private readonly IComponentRegistry? _componentRegistry;
        private readonly IMessageBus? _messageBus;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ConduitHost class.
        /// </summary>
        public ConduitHost(
            ILogger<ConduitHost> logger,
            IOptions<ConduitConfiguration> configuration,
            IComponentRegistry? componentRegistry = null,
            IMessageBus? messageBus = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            _componentRegistry = componentRegistry;
            _messageBus = messageBus;
        }

        /// <summary>
        /// Starts the application host.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Conduit application: {Name} v{Version}",
                _configuration.ApplicationName,
                _configuration.Version);

            _logger.LogInformation("Environment: {Environment}", _configuration.Environment);

            try
            {
                // Initialize component registry
                if (_componentRegistry != null && _configuration.ComponentDiscovery.Enabled)
                {
                    _logger.LogInformation("Initializing component registry");
                    await InitializeComponentsAsync(cancellationToken);
                }

                // Initialize message bus
                if (_messageBus != null && _configuration.Messaging.Enabled)
                {
                    _logger.LogInformation("Message bus is ready");
                }

                // Log enabled features
                if (_configuration.Features.Count > 0)
                {
                    _logger.LogInformation("Enabled features:");
                    foreach (var feature in _configuration.Features)
                    {
                        if (feature.Value)
                        {
                            _logger.LogInformation("  - {Feature}", feature.Key);
                        }
                    }
                }

                _logger.LogInformation("Conduit application started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Conduit application");
                throw;
            }
        }

        /// <summary>
        /// Stops the application host.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Conduit application");

            try
            {
                // Stop components
                if (_componentRegistry != null)
                {
                    _logger.LogInformation("Stopping components");
                    await StopComponentsAsync(cancellationToken);
                }

                _logger.LogInformation("Conduit application stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping Conduit application");
                throw;
            }
        }

        /// <summary>
        /// Initializes discovered components.
        /// </summary>
        private async Task InitializeComponentsAsync(CancellationToken cancellationToken)
        {
            if (_componentRegistry == null)
                return;

            var components = _componentRegistry.GetAllComponents();
            var componentCount = 0;

            foreach (var descriptor in components)
            {
                componentCount++;
                _logger.LogDebug("Component registered: {ComponentId} ({ComponentName})",
                    descriptor.Id,
                    descriptor.Name);
            }

            _logger.LogInformation("Total components registered: {Count}", componentCount);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops all components.
        /// </summary>
        private async Task StopComponentsAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the host.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _logger.LogDebug("Conduit host disposed");
            GC.SuppressFinalize(this);
        }
    }
}
