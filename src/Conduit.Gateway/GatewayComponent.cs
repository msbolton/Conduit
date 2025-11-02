using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Gateway;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway;

/// <summary>
/// Gateway component for Conduit framework integration.
/// Manages API gateway, routing, load balancing, and rate limiting.
/// </summary>
public class GatewayComponent : AbstractPluggableComponent
{
    private readonly ApiGateway _apiGateway;
    private readonly RouteManager _routeManager;
    private readonly LoadBalancer _loadBalancer;
    private readonly RateLimiter _rateLimiter;
    private readonly GatewayConfiguration _configuration;
    private readonly ILogger<GatewayComponent> _logger;

    public GatewayComponent(
        ApiGateway apiGateway,
        RouteManager routeManager,
        LoadBalancer loadBalancer,
        RateLimiter rateLimiter,
        GatewayConfiguration configuration,
        ILogger<GatewayComponent> logger) : base(logger)
    {
        _apiGateway = apiGateway ?? throw new ArgumentNullException(nameof(apiGateway));
        _routeManager = routeManager ?? throw new ArgumentNullException(nameof(routeManager));
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.gateway",
            Name = "Conduit.Gateway",
            Version = "0.8.2",
            Description = "API gateway with routing, load balancing, and rate limiting for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "gateway", "routing", "load-balancing", "rate-limiting", "proxy", "api-gateway" }
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

        // Start the API gateway
        await _apiGateway.StartAsync(cancellationToken);

        Logger.LogInformation("Gateway component '{Name}' started on {Host}:{Port} with {RouteCount} routes",
            Name, _configuration.Host, _configuration.Port, _routeManager.GetAllRoutes().Count());
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Gateway component '{Name}' stopping", Name);

        // Stop the API gateway
        try
        {
            await _apiGateway.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error stopping API gateway");
        }

        Logger.LogInformation("Gateway component '{Name}' stopped", Name);
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Gateway component '{Name}' disposing", Name);

        // Dispose the API gateway
        try
        {
            _apiGateway?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing API gateway");
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
                Id = "ApiGateway",
                Name = "API Gateway",
                Description = "HTTP API gateway with request forwarding and response handling",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "RouteManagement",
                Name = "Route Management",
                Description = "Dynamic route configuration and path matching with parameters",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "LoadBalancing",
                Name = "Load Balancing",
                Description = "Multiple load balancing strategies (round-robin, weighted, least-connections)",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "RateLimiting",
                Name = "Rate Limiting",
                Description = "Per-client rate limiting with configurable limits and windows",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "GatewayMetrics",
                Name = "Gateway Metrics",
                Description = "Request metrics, response times, and success rates",
                Version = Version,
                IsEnabledByDefault = _configuration.EnableMetrics
            },
            new ComponentFeature
            {
                Id = "UpstreamHealthChecks",
                Name = "Upstream Health Checks",
                Description = "Health monitoring and circuit breaker for upstream services",
                Version = Version,
                IsEnabledByDefault = true
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(ApiGateway),
                ImplementationType = _apiGateway.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _apiGateway
            },
            new ServiceContract
            {
                ServiceType = typeof(RouteManager),
                ImplementationType = _routeManager.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _routeManager
            },
            new ServiceContract
            {
                ServiceType = typeof(LoadBalancer),
                ImplementationType = _loadBalancer.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _loadBalancer
            },
            new ServiceContract
            {
                ServiceType = typeof(RateLimiter),
                ImplementationType = _rateLimiter.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _rateLimiter
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
        var gatewayHealthy = _apiGateway != null;
        var routeManagerHealthy = _routeManager != null;
        var loadBalancerHealthy = _loadBalancer != null;
        var rateLimiterHealthy = _rateLimiter != null;

        var routeCount = _routeManager?.GetAllRoutes().Count() ?? 0;
        var isHealthy = gatewayHealthy && routeManagerHealthy && loadBalancerHealthy && rateLimiterHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ApiGateway"] = gatewayHealthy ? "Available" : "Unavailable",
            ["RouteManager"] = routeManagerHealthy ? "Available" : "Unavailable",
            ["LoadBalancer"] = loadBalancerHealthy ? "Available" : "Unavailable",
            ["RateLimiter"] = rateLimiterHealthy ? "Available" : "Unavailable",
            ["RouteCount"] = routeCount,
            ["Host"] = _configuration?.Host ?? "Unknown",
            ["Port"] = _configuration?.Port ?? 0,
            ["EnableRateLimiting"] = _configuration?.EnableRateLimiting ?? false,
            ["EnableMetrics"] = _configuration?.EnableMetrics ?? false
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "One or more gateway services unavailable", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var gatewayHealthy = _apiGateway != null;
        var routeManagerHealthy = _routeManager != null;
        var loadBalancerHealthy = _loadBalancer != null;
        var rateLimiterHealthy = _rateLimiter != null;

        var routeCount = _routeManager?.GetAllRoutes().Count() ?? 0;
        var isHealthy = gatewayHealthy && routeManagerHealthy && loadBalancerHealthy && rateLimiterHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ApiGateway"] = gatewayHealthy ? "Available" : "Unavailable",
            ["RouteManager"] = routeManagerHealthy ? "Available" : "Unavailable",
            ["LoadBalancer"] = loadBalancerHealthy ? "Available" : "Unavailable",
            ["RateLimiter"] = rateLimiterHealthy ? "Available" : "Unavailable",
            ["RouteCount"] = routeCount
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "One or more gateway services unavailable", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("api_gateway_available", _apiGateway != null ? 1 : 0);
        metrics.SetCounter("route_manager_available", _routeManager != null ? 1 : 0);
        metrics.SetCounter("load_balancer_available", _loadBalancer != null ? 1 : 0);
        metrics.SetCounter("rate_limiter_available", _rateLimiter != null ? 1 : 0);
        metrics.SetCounter("route_count", _routeManager?.GetAllRoutes().Count() ?? 0);
        metrics.SetCounter("rate_limiting_enabled", _configuration?.EnableRateLimiting == true ? 1 : 0);
        metrics.SetCounter("metrics_enabled", _configuration?.EnableMetrics == true ? 1 : 0);
        metrics.SetGauge("component_state", (int)GetState());

        // Collect gateway metrics if available
        if (_apiGateway != null && _configuration?.EnableMetrics == true)
        {
            var allMetrics = _apiGateway.GetAllMetrics();
            var totalRequests = allMetrics.Values.Sum(m => m.TotalRequests);
            var successfulRequests = allMetrics.Values.Sum(m => m.SuccessfulRequests);
            var failedRequests = allMetrics.Values.Sum(m => m.FailedRequests);
            var avgResponseTime = allMetrics.Values.Count > 0
                ? allMetrics.Values.Average(m => m.AverageResponseTimeMs)
                : 0;

            metrics.SetCounter("total_requests", totalRequests);
            metrics.SetCounter("successful_requests", successfulRequests);
            metrics.SetCounter("failed_requests", failedRequests);
            metrics.SetGauge("average_response_time_ms", avgResponseTime);
        }
    }

    /// <summary>
    /// Gets the API gateway.
    /// </summary>
    public ApiGateway GetApiGateway() => _apiGateway;

    /// <summary>
    /// Gets the route manager.
    /// </summary>
    public RouteManager GetRouteManager() => _routeManager;

    /// <summary>
    /// Gets the load balancer.
    /// </summary>
    public LoadBalancer GetLoadBalancer() => _loadBalancer;

    /// <summary>
    /// Gets the rate limiter.
    /// </summary>
    public RateLimiter GetRateLimiter() => _rateLimiter;

    /// <summary>
    /// Gets the gateway configuration.
    /// </summary>
    public new GatewayConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Adds a new route to the gateway.
    /// </summary>
    public void AddRoute(RouteConfiguration route)
    {
        ArgumentNullException.ThrowIfNull(route);
        _routeManager.AddRoute(route);
        Logger.LogInformation("Added route '{RouteId}' to gateway", route.Id);
    }

    /// <summary>
    /// Removes a route from the gateway.
    /// </summary>
    public bool RemoveRoute(string routeId)
    {
        var result = _routeManager.RemoveRoute(routeId);
        if (result)
        {
            Logger.LogInformation("Removed route '{RouteId}' from gateway", routeId);
        }
        return result;
    }

    /// <summary>
    /// Gets metrics for a specific route.
    /// </summary>
    public GatewayMetrics? GetRouteMetrics(string routeId)
    {
        return _apiGateway.GetMetrics(routeId);
    }

    /// <summary>
    /// Gets all gateway metrics.
    /// </summary>
    public IDictionary<string, GatewayMetrics> GetAllMetrics()
    {
        return _apiGateway.GetAllMetrics();
    }
}