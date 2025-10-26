using System;
using System.Collections.Generic;

namespace Conduit.Gateway
{
    /// <summary>
    /// Configuration for the API Gateway.
    /// </summary>
    public class GatewayConfiguration
    {
        /// <summary>
        /// Gets or sets the gateway name.
        /// </summary>
        public string Name { get; set; } = "Conduit Gateway";

        /// <summary>
        /// Gets or sets the host to listen on.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port to listen on.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Gets or sets the default request timeout in milliseconds.
        /// </summary>
        public int RequestTimeout { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Gets or sets the maximum concurrent requests.
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to enable request logging.
        /// </summary>
        public bool EnableRequestLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable metrics collection.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets the default load balancing strategy.
        /// </summary>
        public LoadBalancingStrategy DefaultLoadBalancingStrategy { get; set; } = LoadBalancingStrategy.RoundRobin;

        /// <summary>
        /// Gets or sets whether to enable rate limiting.
        /// </summary>
        public bool EnableRateLimiting { get; set; } = true;

        /// <summary>
        /// Gets or sets the default rate limit (requests per second).
        /// </summary>
        public int DefaultRateLimit { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to enable circuit breaker.
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Gets or sets the circuit breaker failure threshold.
        /// </summary>
        public int CircuitBreakerFailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the circuit breaker timeout in milliseconds.
        /// </summary>
        public int CircuitBreakerTimeout { get; set; } = 60000; // 60 seconds

        /// <summary>
        /// Gets or sets whether to enable health checks.
        /// </summary>
        public bool EnableHealthChecks { get; set; } = true;

        /// <summary>
        /// Gets or sets the health check interval in milliseconds.
        /// </summary>
        public int HealthCheckInterval { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Gets or sets the routes.
        /// </summary>
        public List<RouteConfiguration> Routes { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to enable CORS.
        /// </summary>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Gets or sets the allowed CORS origins.
        /// </summary>
        public List<string> CorsOrigins { get; set; } = new() { "*" };

        /// <summary>
        /// Gets or sets whether to enable request/response transformation.
        /// </summary>
        public bool EnableTransformation { get; set; } = false;

        /// <summary>
        /// Gets or sets the buffer size for request/response bodies.
        /// </summary>
        public int BufferSize { get; set; } = 81920; // 80 KB

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 0 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535");

            if (RequestTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(RequestTimeout), "RequestTimeout must be greater than 0");

            if (MaxConcurrentRequests <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxConcurrentRequests), "MaxConcurrentRequests must be greater than 0");

            if (DefaultRateLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(DefaultRateLimit), "DefaultRateLimit must be greater than 0");

            if (CircuitBreakerFailureThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(CircuitBreakerFailureThreshold), "CircuitBreakerFailureThreshold must be greater than 0");

            if (CircuitBreakerTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(CircuitBreakerTimeout), "CircuitBreakerTimeout must be greater than 0");

            if (HealthCheckInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(HealthCheckInterval), "HealthCheckInterval must be greater than 0");
        }
    }

    /// <summary>
    /// Route configuration.
    /// </summary>
    public class RouteConfiguration
    {
        /// <summary>
        /// Gets or sets the route ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the route path pattern.
        /// </summary>
        public string Path { get; set; } = "/";

        /// <summary>
        /// Gets or sets the HTTP methods allowed for this route.
        /// </summary>
        public List<string> Methods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

        /// <summary>
        /// Gets or sets the upstream service endpoints.
        /// </summary>
        public List<string> Upstreams { get; set; } = new();

        /// <summary>
        /// Gets or sets the load balancing strategy for this route.
        /// </summary>
        public LoadBalancingStrategy? LoadBalancingStrategy { get; set; }

        /// <summary>
        /// Gets or sets the rate limit for this route (requests per second).
        /// </summary>
        public int? RateLimit { get; set; }

        /// <summary>
        /// Gets or sets the request timeout for this route in milliseconds.
        /// </summary>
        public int? Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether this route requires authentication.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets the required roles for this route.
        /// </summary>
        public List<string> RequiredRoles { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this route is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets custom headers to add to upstream requests.
        /// </summary>
        public Dictionary<string, string> UpstreamHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets custom headers to add to downstream responses.
        /// </summary>
        public Dictionary<string, string> DownstreamHeaders { get; set; } = new();
    }

    /// <summary>
    /// Load balancing strategies.
    /// </summary>
    public enum LoadBalancingStrategy
    {
        /// <summary>
        /// Round-robin load balancing.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Least connections load balancing.
        /// </summary>
        LeastConnections = 1,

        /// <summary>
        /// Random load balancing.
        /// </summary>
        Random = 2,

        /// <summary>
        /// Weighted round-robin load balancing.
        /// </summary>
        WeightedRoundRobin = 3,

        /// <summary>
        /// IP hash load balancing (sticky sessions).
        /// </summary>
        IpHash = 4
    }
}
