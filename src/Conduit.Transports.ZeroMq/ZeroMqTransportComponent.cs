using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.ZeroMq
{
    /// <summary>
    /// ZeroMQ transport component that provides high-performance messaging using ZeroMQ patterns
    /// </summary>
    [Component(
        "conduit.transports.zeromq",
        "Conduit.Transports.ZeroMQ",
        "0.1.0",
        Description = "Provides ZeroMQ transport with support for PUB/SUB, REQ/REP, PUSH/PULL, PAIR, and ROUTER/DEALER patterns"
    )]
    public class ZeroMqTransportComponent : AbstractPluggableComponent
    {
        private ZeroMqTransport? _transport;

        /// <summary>
        /// Gets the ZeroMQ transport instance
        /// </summary>
        public ZeroMqTransport? Transport => _transport;

        /// <summary>
        /// Initializes a new instance of the ZeroMqTransportComponent class
        /// </summary>
        public ZeroMqTransportComponent(ILogger<ZeroMqTransportComponent>? logger = null) : base(logger)
        {
            // Override the default manifest
            Manifest = new ComponentManifest
            {
                Id = "conduit.transports.zeromq",
                Name = "Conduit.Transports.ZeroMQ",
                Version = "0.1.0",
                Description = "Provides ZeroMQ transport with support for PUB/SUB, REQ/REP, PUSH/PULL, PAIR, and ROUTER/DEALER patterns",
                Author = "Conduit Contributors",
                MinFrameworkVersion = "0.1.0",
                Dependencies = new List<ComponentDependency>(),
                Tags = new HashSet<string> { "transport", "zeromq", "messaging", "high-performance", "distributed" }
            };
        }

        /// <summary>
        /// Called during component initialization
        /// </summary>
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Use a default configuration for now - can be enhanced later to read from Configuration
                var config = new ZeroMqConfiguration
                {
                    Type = Conduit.Transports.Core.TransportType.ZeroMq,
                    Name = "ZeroMQ-Default",
                    ConnectAddress = "tcp://localhost:5555" // Default for testing
                };
                config.Validate();

                // Create transport - note that we need to cast to the specific logger type
                _transport = new ZeroMqTransport(config, Logger as ILogger<ZeroMqTransport>);

                Logger.LogInformation("ZeroMQ transport component initialized with pattern {Pattern}", config.Pattern);
                await base.OnInitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize ZeroMQ transport component");
                throw;
            }
        }

        /// <summary>
        /// Called when the component is starting
        /// </summary>
        protected override async Task OnStartAsync(CancellationToken cancellationToken = default)
        {
            if (_transport == null)
                throw new InvalidOperationException("Component must be initialized before starting");

            try
            {
                await _transport.ConnectAsync(cancellationToken);
                Logger.LogInformation("ZeroMQ transport component started");
                await base.OnStartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to start ZeroMQ transport component");
                throw;
            }
        }

        /// <summary>
        /// Called when the component is stopping
        /// </summary>
        protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
        {
            if (_transport != null)
            {
                try
                {
                    await _transport.DisconnectAsync(cancellationToken);
                    Logger.LogInformation("ZeroMQ transport component stopped");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to stop ZeroMQ transport component");
                    throw;
                }
            }
            await base.OnStopAsync(cancellationToken);
        }

        /// <summary>
        /// Called when the component is being disposed
        /// </summary>
        protected override async Task OnDisposeAsync()
        {
            _transport?.Dispose();
            _transport = null;
            await base.OnDisposeAsync();
        }

        /// <summary>
        /// Performs a health check
        /// </summary>
        protected override ComponentHealth? PerformHealthCheck()
        {
            if (_transport == null)
            {
                return ComponentHealth.Unhealthy(Id, "Transport not initialized");
            }

            if (_transport.IsConnected)
            {
                return ComponentHealth.Healthy(Id);
            }
            else
            {
                return ComponentHealth.Unhealthy(Id, "Transport not connected");
            }
        }

        /// <summary>
        /// Collects component metrics
        /// </summary>
        protected override void CollectMetrics(ComponentMetrics metrics)
        {
            // Enhanced metrics collection now that we have TransportAdapterBase statistics
            if (_transport != null)
            {
                var stats = _transport.GetStatistics();

                // Use reflection or properties to add metrics if ComponentMetrics supports it
                // For now, keep it simple - the transport statistics are available via the Transport property
                Logger.LogDebug("Transport statistics: {MessagesSent} sent, {MessagesReceived} received",
                    stats.MessagesSent, stats.MessagesReceived);
            }
            base.CollectMetrics(metrics);
        }
    }
}