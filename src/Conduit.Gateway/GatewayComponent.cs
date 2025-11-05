using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Gateway;
using Conduit.Messaging;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway;

/// <summary>
/// Gateway component for Conduit framework integration.
/// Manages network gateway, connection routing, and transport management.
/// </summary>
public class GatewayComponent : AbstractPluggableComponent
{
    private readonly Gateway _gateway;
    private readonly GatewayConfiguration _configuration;
    private readonly ILogger<GatewayComponent> _logger;

    public GatewayComponent(
        Gateway gateway,
        GatewayConfiguration configuration,
        ILogger<GatewayComponent> logger) : base(logger)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.gateway",
            Name = "Conduit.Gateway",
            Version = "1.0.0-alpha",
            Description = "Network gateway with connection routing, transport management, and low-level socket handling for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "gateway", "networking", "routing", "transports", "connections", "sockets" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Gateway component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Gateway component '{Name}' starting", Name);

        // Start the gateway
        await _gateway.StartAsync(cancellationToken);

        var stats = _gateway.GetStatistics();
        Logger.LogInformation("Gateway component '{Name}' started with {ServerBindings} server bindings, {ClientEndpoints} client endpoints, {RegisteredTransports} transports, and {RouteCount} routes",
            Name, stats.ServerBindings, stats.ClientEndpoints, stats.RegisteredTransports, stats.RoutingStatistics?.TotalRoutes ?? 0);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Gateway component '{Name}' stopping", Name);

        // Stop the gateway
        try
        {
            await _gateway.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error stopping gateway");
        }

        Logger.LogInformation("Gateway component '{Name}' stopped", Name);
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Gateway component '{Name}' disposing", Name);

        // Dispose the gateway
        try
        {
            _gateway?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing gateway");
        }

        Logger.LogInformation("Gateway component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "NetworkGateway",
                Name = "Network Gateway",
                Description = "Low-level network gateway with socket management and connection routing",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "ConnectionManagement",
                Name = "Connection Management",
                Description = "Connection tracking, pooling, and lifecycle management",
                Version = Version,
                IsEnabledByDefault = _configuration.EnableConnectionTracking
            },
            new ComponentFeature
            {
                Id = "RoutingTable",
                Name = "Routing Table",
                Description = "Bidirectional routing with priority-based rule evaluation",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "TransportRegistry",
                Name = "Transport Registry",
                Description = "Transport registration and lifecycle management",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SocketManager",
                Name = "Socket Manager",
                Description = "Low-level socket operations and port binding",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "GatewayMetrics",
                Name = "Gateway Metrics",
                Description = "Connection metrics, routing statistics, and transport health",
                Version = Version,
                IsEnabledByDefault = _configuration.EnableMetrics
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(Gateway),
                ImplementationType = _gateway.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _gateway
            },
            new ServiceContract
            {
                ServiceType = typeof(RoutingTable),
                ImplementationType = _gateway.RoutingTable.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _gateway.RoutingTable
            },
            new ServiceContract
            {
                ServiceType = typeof(ConnectionTable),
                ImplementationType = _gateway.ConnectionTable.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _gateway.ConnectionTable
            },
            new ServiceContract
            {
                ServiceType = typeof(TransportRegistry),
                ImplementationType = _gateway.TransportRegistry.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _gateway.TransportRegistry
            },
            new ServiceContract
            {
                ServiceType = typeof(GatewayConfiguration),
                ImplementationType = _configuration.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _configuration
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var gatewayHealthy = _gateway != null;
        var isRunning = _gateway?.IsRunning ?? false;
        var stats = _gateway?.GetStatistics();

        var isHealthy = gatewayHealthy && isRunning;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["Gateway"] = gatewayHealthy ? "Available" : "Unavailable",
            ["IsRunning"] = isRunning,
            ["ServerBindings"] = stats?.ServerBindings ?? 0,
            ["ClientEndpoints"] = stats?.ClientEndpoints ?? 0,
            ["RegisteredTransports"] = stats?.RegisteredTransports ?? 0,
            ["ActiveConnections"] = stats?.ConnectionStatistics?.TotalConnections ?? 0,
            ["RouteCount"] = stats?.RoutingStatistics?.TotalRoutes ?? 0,
            ["BoundPorts"] = stats?.BoundPorts ?? Array.Empty<int>(),
            ["EnableConnectionTracking"] = _configuration?.EnableConnectionTracking ?? false,
            ["EnableMetrics"] = _configuration?.EnableMetrics ?? false
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "Gateway is not available or not running", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var gatewayHealthy = _gateway != null;
        var isRunning = _gateway?.IsRunning ?? false;
        var stats = _gateway?.GetStatistics();

        var isHealthy = gatewayHealthy && isRunning;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["Gateway"] = gatewayHealthy ? "Available" : "Unavailable",
            ["IsRunning"] = isRunning,
            ["ServerBindings"] = stats?.ServerBindings ?? 0,
            ["ClientEndpoints"] = stats?.ClientEndpoints ?? 0,
            ["RegisteredTransports"] = stats?.RegisteredTransports ?? 0,
            ["ActiveConnections"] = stats?.ConnectionStatistics?.TotalConnections ?? 0,
            ["RouteCount"] = stats?.RoutingStatistics?.TotalRoutes ?? 0
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "Gateway is not available or not running", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        var stats = _gateway?.GetStatistics();

        metrics.SetCounter("gateway_available", _gateway != null ? 1 : 0);
        metrics.SetCounter("gateway_running", _gateway?.IsRunning == true ? 1 : 0);
        metrics.SetCounter("server_bindings", stats?.ServerBindings ?? 0);
        metrics.SetCounter("client_endpoints", stats?.ClientEndpoints ?? 0);
        metrics.SetCounter("registered_transports", stats?.RegisteredTransports ?? 0);
        metrics.SetCounter("connection_tracking_enabled", _configuration?.EnableConnectionTracking == true ? 1 : 0);
        metrics.SetCounter("metrics_enabled", _configuration?.EnableMetrics == true ? 1 : 0);
        metrics.SetGauge("component_state", (int)GetState());

        // Collect gateway statistics if available
        if (stats != null && _configuration?.EnableMetrics == true)
        {
            // Connection metrics
            if (stats.ConnectionStatistics != null)
            {
                metrics.SetCounter("total_connections", stats.ConnectionStatistics.TotalConnections);
                metrics.SetCounter("total_bytes_transferred", stats.ConnectionStatistics.TotalBytesTransferred);
                metrics.SetCounter("total_messages_transferred", stats.ConnectionStatistics.TotalMessagesTransferred);
                metrics.SetGauge("average_connection_duration_ms", stats.ConnectionStatistics.AverageConnectionDuration.TotalMilliseconds);

                foreach (var statusGroup in stats.ConnectionStatistics.ConnectionsByStatus)
                {
                    metrics.SetCounter($"connections_{statusGroup.Key.ToString().ToLower()}", statusGroup.Value);
                }
            }

            // Routing metrics
            if (stats.RoutingStatistics != null)
            {
                metrics.SetCounter("total_routes", stats.RoutingStatistics.TotalRoutes);
                metrics.SetCounter("inbound_routes", stats.RoutingStatistics.InboundRoutes);
                metrics.SetCounter("outbound_routes", stats.RoutingStatistics.OutboundRoutes);
                metrics.SetCounter("enabled_routes", stats.RoutingStatistics.EnabledRoutes);
                metrics.SetCounter("disabled_routes", stats.RoutingStatistics.DisabledRoutes);
                metrics.SetCounter("total_route_matches", stats.RoutingStatistics.TotalMatches);
            }

            // Transport health metrics
            if (stats.TransportHealth != null)
            {
                var connectedTransports = stats.TransportHealth.Values.Count(t => t.IsConnected);
                var disconnectedTransports = stats.TransportHealth.Values.Count(t => !t.IsConnected);

                metrics.SetCounter("connected_transports", connectedTransports);
                metrics.SetCounter("disconnected_transports", disconnectedTransports);
            }

            // Bound ports
            metrics.SetCounter("bound_ports", stats.BoundPorts.Length);
        }
    }

    /// <summary>
    /// Gets the gateway.
    /// </summary>
    public Gateway GetGateway() => _gateway;

    /// <summary>
    /// Gets the routing table.
    /// </summary>
    public RoutingTable GetRoutingTable() => _gateway.RoutingTable;

    /// <summary>
    /// Gets the connection table.
    /// </summary>
    public ConnectionTable GetConnectionTable() => _gateway.ConnectionTable;

    /// <summary>
    /// Gets the transport registry.
    /// </summary>
    public TransportRegistry GetTransportRegistry() => _gateway.TransportRegistry;

    /// <summary>
    /// Gets the gateway configuration.
    /// </summary>
    public new GatewayConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Adds a new route to the gateway.
    /// </summary>
    public void AddRoute(RouteEntry route)
    {
        ArgumentNullException.ThrowIfNull(route);
        _gateway.RoutingTable.AddRoute(route);
        Logger.LogInformation("Added route '{RouteId}' to gateway", route.Id);
    }

    /// <summary>
    /// Removes a route from the gateway.
    /// </summary>
    public bool RemoveRoute(string routeId)
    {
        var result = _gateway.RoutingTable.RemoveRoute(routeId);
        if (result)
        {
            Logger.LogInformation("Removed route '{RouteId}' from gateway", routeId);
        }
        return result;
    }

    /// <summary>
    /// Gets gateway statistics.
    /// </summary>
    public GatewayStatistics GetGatewayStatistics()
    {
        return _gateway.GetStatistics();
    }

    /// <summary>
    /// Gets connection statistics.
    /// </summary>
    public ConnectionTableStatistics GetConnectionStatistics()
    {
        return _gateway.ConnectionTable.GetStatistics();
    }

    /// <summary>
    /// Gets routing statistics.
    /// </summary>
    public RoutingTableStatistics GetRoutingStatistics()
    {
        return _gateway.RoutingTable.GetStatistics();
    }

    /// <summary>
    /// Gets transport health information.
    /// </summary>
    public Dictionary<Transports.Core.TransportType, TransportHealth> GetTransportHealth()
    {
        return _gateway.TransportRegistry.GetTransportHealth();
    }
}