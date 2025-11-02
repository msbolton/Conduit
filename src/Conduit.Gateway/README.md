# Conduit.Gateway

A feature-rich API Gateway implementation for the Conduit framework, providing request routing, load balancing, rate limiting, and metrics collection for distributed microservices architectures.

## Features

- **Flexible Routing**: Pattern-based route matching with parameter extraction
- **Load Balancing**: Multiple strategies (Round-robin, Least Connections, Random, IP Hash)
- **Rate Limiting**: Token bucket algorithm for per-client request throttling
- **Health Tracking**: Automatic upstream health monitoring and failover
- **Metrics Collection**: Request counts, response times, and success rates
- **Concurrency Control**: Configurable request concurrency limits
- **HTTP Proxying**: Transparent request/response forwarding
- **Custom Headers**: Add headers to upstream requests and downstream responses

## Quick Start

### Basic Gateway Setup

```csharp
using Conduit.Gateway;
using Microsoft.Extensions.Logging;

// Create configuration
var config = new GatewayConfiguration
{
    Host = "localhost",
    Port = 8080,
    MaxConcurrentRequests = 100,
    EnableRateLimiting = true,
    DefaultRateLimit = 100 // requests per second
};

// Add routes
config.Routes.Add(new RouteConfiguration
{
    Id = "users-api",
    Path = "/api/users/{id}",
    Methods = new List<string> { "GET", "POST", "PUT", "DELETE" },
    Upstreams = new List<string>
    {
        "http://localhost:5001",
        "http://localhost:5002",
        "http://localhost:5003"
    },
    LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin,
    RateLimit = 50 // per-route rate limit
});

// Create gateway
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ApiGateway>();

var gateway = new ApiGateway(config, logger);

// Start gateway
await gateway.StartAsync();

// Process requests
var response = await gateway.ProcessRequestAsync(
    path: "/api/users/123",
    method: "GET",
    clientId: "client-1"
);

Console.WriteLine($"Status: {response.StatusCode}");
Console.WriteLine($"Content: {response.Content}");

// Stop gateway
await gateway.StopAsync();
```

## Configuration

### Gateway Configuration

```csharp
var config = new GatewayConfiguration
{
    // Basic settings
    Name = "My API Gateway",
    Host = "localhost",
    Port = 8080,

    // Timeouts
    RequestTimeout = 30000, // 30 seconds

    // Concurrency
    MaxConcurrentRequests = 100,

    // Features
    EnableRateLimiting = true,
    DefaultRateLimit = 100, // requests/second
    EnableMetrics = true,
    EnableCircuitBreaker = true,
    EnableHealthChecks = true,

    // Load balancing
    DefaultLoadBalancingStrategy = LoadBalancingStrategy.RoundRobin,

    // Circuit breaker
    CircuitBreakerFailureThreshold = 5,
    CircuitBreakerTimeout = 60000, // 60 seconds

    // Health checks
    HealthCheckInterval = 30000, // 30 seconds

    // CORS
    EnableCors = true,
    CorsOrigins = new List<string> { "*" }
};
```

### Route Configuration

```csharp
var route = new RouteConfiguration
{
    // Identification
    Id = "my-service",

    // Path pattern (supports parameters)
    Path = "/api/resources/{id}/items/{itemId}",

    // HTTP methods
    Methods = new List<string> { "GET", "POST", "PUT", "DELETE" },

    // Upstream servers
    Upstreams = new List<string>
    {
        "http://service1:5000",
        "http://service2:5000",
        "http://service3:5000"
    },

    // Load balancing
    LoadBalancingStrategy = LoadBalancingStrategy.LeastConnections,

    // Rate limiting
    RateLimit = 50, // requests/second for this route

    // Timeout
    Timeout = 15000, // 15 seconds

    // Authentication
    RequireAuthentication = true,
    RequiredRoles = new List<string> { "Admin", "User" },

    // Custom headers
    UpstreamHeaders = new Dictionary<string, string>
    {
        { "X-Gateway-Version", "1.0" },
        { "X-Forwarded-By", "Conduit-Gateway" }
    },

    DownstreamHeaders = new Dictionary<string, string>
    {
        { "X-Gateway-Time", "timestamp" }
    },

    // Enable/disable
    Enabled = true
};
```

## Load Balancing Strategies

### Round-Robin

Distributes requests evenly across all healthy upstreams in rotation.

```csharp
route.LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin;
```

**Best for:**
- Uniform server capacity
- Stateless services
- Simple deployment scenarios

### Least Connections

Routes requests to the upstream with the fewest active connections.

```csharp
route.LoadBalancingStrategy = LoadBalancingStrategy.LeastConnections;
```

**Best for:**
- Variable request processing times
- Mixed workloads
- Optimal resource utilization

### Random

Selects a random upstream for each request.

```csharp
route.LoadBalancingStrategy = LoadBalancingStrategy.Random;
```

**Best for:**
- Simple stateless services
- Testing and development
- When predictable patterns should be avoided

### IP Hash (Sticky Sessions)

Routes requests from the same client IP to the same upstream server.

```csharp
route.LoadBalancingStrategy = LoadBalancingStrategy.IpHash;
```

**Best for:**
- Session affinity requirements
- Stateful applications
- Caching at upstream servers

## Rate Limiting

The gateway uses a token bucket algorithm for rate limiting, providing smooth request throttling.

### Per-Route Rate Limiting

```csharp
var route = new RouteConfiguration
{
    Path = "/api/expensive-operation",
    RateLimit = 10 // 10 requests per second
};
```

### Default Rate Limiting

```csharp
var config = new GatewayConfiguration
{
    EnableRateLimiting = true,
    DefaultRateLimit = 100 // applies to routes without specific limits
};
```

### Rate Limit Response

When rate limit is exceeded, the gateway returns:
```json
{
    "StatusCode": 429,
    "Message": "Rate limit exceeded",
    "Success": false
}
```

### Checking Rate Limit State

```csharp
var state = rateLimiter.GetState("client-123");
Console.WriteLine($"Tokens available: {state.TokensAvailable}");
Console.WriteLine($"Capacity: {state.Capacity}");
Console.WriteLine($"Percentage remaining: {state.PercentageRemaining:P}");
```

## Routing and Path Matching

### Static Routes

```csharp
// Exact match
Path = "/api/users"

// Request: GET /api/users
// Match: ✓
// Request: GET /api/users/123
// Match: ✗
```

### Parameterized Routes

```csharp
// Single parameter
Path = "/api/users/{id}"

// Request: GET /api/users/123
// Match: ✓
// Parameters: { "id": "123" }
```

### Multi-Parameter Routes

```csharp
// Multiple parameters
Path = "/api/users/{userId}/orders/{orderId}"

// Request: GET /api/users/42/orders/999
// Match: ✓
// Parameters: { "userId": "42", "orderId": "999" }
```

### Route Specificity

Routes are matched by specificity (most specific first):

```csharp
// Order of matching (most to least specific)
1. /api/users/admin        // Static: specificity 20
2. /api/users/{id}/orders  // Mixed: specificity 21
3. /api/users/{id}         // Parameter: specificity 11
4. /api/{resource}         // Parameter: specificity 11
```

## Metrics Collection

### Enabling Metrics

```csharp
var config = new GatewayConfiguration
{
    EnableMetrics = true
};
```

### Accessing Metrics

```csharp
// Get metrics for a specific route
var metrics = gateway.GetMetrics("route-id");
if (metrics != null)
{
    Console.WriteLine($"Total requests: {metrics.TotalRequests}");
    Console.WriteLine($"Successful: {metrics.SuccessfulRequests}");
    Console.WriteLine($"Failed: {metrics.FailedRequests}");
    Console.WriteLine($"Success rate: {metrics.SuccessRate:P}");
    Console.WriteLine($"Avg response time: {metrics.AverageResponseTimeMs}ms");
}

// Get all metrics
var allMetrics = gateway.GetAllMetrics();
foreach (var kvp in allMetrics)
{
    Console.WriteLine($"Route {kvp.Key}: {kvp.Value.TotalRequests} requests");
}
```

### Metrics Structure

```csharp
public class GatewayMetrics
{
    public long TotalRequests { get; }
    public long SuccessfulRequests { get; }
    public long FailedRequests { get; }
    public double AverageResponseTimeMs { get; }
    public double SuccessRate { get; } // 0.0 to 1.0
}
```

## Health Tracking and Failover

### Upstream Health State

```csharp
var state = loadBalancer.GetUpstreamState("http://service1:5000");

Console.WriteLine($"Active connections: {state.ActiveConnections}");
Console.WriteLine($"Total requests: {state.TotalRequests}");
Console.WriteLine($"Success rate: {state.SuccessRate:P}");
Console.WriteLine($"Is healthy: {state.IsHealthy}");
```

### Manual Health Management

```csharp
// Mark upstream as unhealthy
loadBalancer.MarkUnhealthy("http://service1:5000");

// Mark upstream as healthy
loadBalancer.MarkHealthy("http://service1:5000");
```

### Automatic Failover

When an upstream fails, it's automatically marked as unhealthy and excluded from load balancing:

```csharp
// Upstreams: [service1, service2, service3]
// service1 fails → automatically excluded
// Future requests only go to service2 and service3
```

## Advanced Usage

### Multi-Service Gateway

```csharp
var config = new GatewayConfiguration
{
    Port = 8080,
    Routes = new List<RouteConfiguration>
    {
        // User service
        new RouteConfiguration
        {
            Id = "users",
            Path = "/api/users/{id}",
            Upstreams = new List<string>
            {
                "http://user-service-1:5000",
                "http://user-service-2:5000"
            },
            LoadBalancingStrategy = LoadBalancingStrategy.RoundRobin
        },

        // Order service
        new RouteConfiguration
        {
            Id = "orders",
            Path = "/api/orders/{id}",
            Upstreams = new List<string>
            {
                "http://order-service-1:6000",
                "http://order-service-2:6000"
            },
            LoadBalancingStrategy = LoadBalancingStrategy.LeastConnections
        },

        // Payment service (strict rate limit)
        new RouteConfiguration
        {
            Id = "payments",
            Path = "/api/payments/{id}",
            Upstreams = new List<string> { "http://payment-service:7000" },
            RateLimit = 10, // Only 10 requests/second
            RequireAuthentication = true
        }
    }
};
```

### Custom Headers

```csharp
// Add headers to upstream requests
var route = new RouteConfiguration
{
    Path = "/api/data",
    UpstreamHeaders = new Dictionary<string, string>
    {
        { "X-Gateway-Id", "gateway-1" },
        { "X-Request-Source", "external" },
        { "Authorization", "Bearer token" }
    },

    // Add headers to downstream responses
    DownstreamHeaders = new Dictionary<string, string>
    {
        { "X-Cache-Status", "MISS" },
        { "X-Response-Time", "123ms" }
    }
};
```

### Integration with Message Bus

```csharp
using Conduit.Messaging;

// Create message bus
var messageBus = new InMemoryMessageBus(logger);

// Create gateway with message bus support
var gateway = new ApiGateway(config, logger, messageBus);

// Message-based routing (future feature)
// Gateway can publish metrics and events to the message bus
```

## Performance Tuning

### Concurrency Settings

```csharp
var config = new GatewayConfiguration
{
    // Limit concurrent requests
    MaxConcurrentRequests = 500,

    // Request timeout
    RequestTimeout = 30000, // 30 seconds

    // Buffer size for request/response bodies
    BufferSize = 81920 // 80 KB
};
```

### Load Balancing Optimization

```csharp
// For high-throughput scenarios
LoadBalancingStrategy = LoadBalancingStrategy.Random; // Lowest overhead

// For optimal resource usage
LoadBalancingStrategy = LoadBalancingStrategy.LeastConnections;

// For session affinity
LoadBalancingStrategy = LoadBalancingStrategy.IpHash;
```

### Rate Limiting Configuration

```csharp
// Conservative rate limiting
DefaultRateLimit = 50; // 50 req/sec

// Aggressive rate limiting for expensive operations
route.RateLimit = 5; // 5 req/sec

// Disable for trusted internal services
EnableRateLimiting = false;
```

## Error Handling

### Response Codes

| Code | Meaning | Cause |
|------|---------|-------|
| 200-299 | Success | Upstream returned success |
| 404 | Not Found | No matching route |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Error | Gateway error |
| 502 | Bad Gateway | Upstream HTTP error |
| 503 | Service Unavailable | No healthy upstreams |
| 504 | Gateway Timeout | Upstream timeout |

### Error Response Structure

```csharp
public class GatewayResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public string? Content { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

### Handling Errors

```csharp
var response = await gateway.ProcessRequestAsync("/api/users/123", "GET", "client-1");

if (!response.Success)
{
    switch (response.StatusCode)
    {
        case 404:
            Console.WriteLine("Route not found");
            break;
        case 429:
            Console.WriteLine("Rate limit exceeded, retry later");
            break;
        case 503:
            Console.WriteLine("No healthy upstreams available");
            break;
        case 504:
            Console.WriteLine("Request timed out");
            break;
        default:
            Console.WriteLine($"Error: {response.Error}");
            break;
    }
}
```

## Monitoring and Observability

### Logging

The gateway uses Microsoft.Extensions.Logging for comprehensive logging:

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<ApiGateway>();
var gateway = new ApiGateway(config, logger);
```

**Log levels:**
- **Information**: Route matching, upstream selection, gateway lifecycle
- **Debug**: Detailed request routing decisions
- **Warning**: Rate limit exceeded, health check failures
- **Error**: Request processing errors, upstream failures

### Metrics Dashboard Example

```csharp
// Simple metrics dashboard
var allMetrics = gateway.GetAllMetrics();

Console.WriteLine("=== Gateway Metrics Dashboard ===");
foreach (var (routeId, metrics) in allMetrics)
{
    Console.WriteLine($"\nRoute: {routeId}");
    Console.WriteLine($"  Total: {metrics.TotalRequests}");
    Console.WriteLine($"  Success: {metrics.SuccessfulRequests} ({metrics.SuccessRate:P})");
    Console.WriteLine($"  Failed: {metrics.FailedRequests}");
    Console.WriteLine($"  Avg Time: {metrics.AverageResponseTimeMs:F2}ms");
}

// Upstream health
Console.WriteLine("\n=== Upstream Health ===");
foreach (var route in config.Routes)
{
    foreach (var upstream in route.Upstreams)
    {
        var state = loadBalancer.GetUpstreamState(upstream);
        Console.WriteLine($"{upstream}: {(state.IsHealthy ? "✓" : "✗")} " +
                         $"({state.ActiveConnections} active, {state.SuccessRate:P} success)");
    }
}
```

## Best Practices

### 1. Route Organization

```csharp
// Group related routes by service
config.Routes.Add(CreateUserServiceRoutes());
config.Routes.Add(CreateOrderServiceRoutes());
config.Routes.Add(CreatePaymentServiceRoutes());

private RouteConfiguration CreateUserServiceRoutes()
{
    return new RouteConfiguration
    {
        Id = "users",
        Path = "/api/users/{action?}",
        // ... configuration
    };
}
```

### 2. Rate Limiting Strategy

```csharp
// Public endpoints: strict limits
public RouteConfiguration
{
    Path = "/api/public/search",
    RateLimit = 10
}

// Authenticated endpoints: moderate limits
public RouteConfiguration
{
    Path = "/api/user/profile",
    RateLimit = 50,
    RequireAuthentication = true
}

// Internal services: relaxed or no limits
public RouteConfiguration
{
    Path = "/api/internal/metrics",
    RateLimit = 1000
}
```

### 3. Health Check Integration

```csharp
// Periodic health check task
var healthCheckTimer = new Timer(async _ =>
{
    foreach (var route in config.Routes)
    {
        foreach (var upstream in route.Upstreams)
        {
            try
            {
                var response = await httpClient.GetAsync($"{upstream}/health");
                if (response.IsSuccessStatusCode)
                    loadBalancer.MarkHealthy(upstream);
                else
                    loadBalancer.MarkUnhealthy(upstream);
            }
            catch
            {
                loadBalancer.MarkUnhealthy(upstream);
            }
        }
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
```

### 4. Graceful Shutdown

```csharp
// Handle shutdown gracefully
var cts = new CancellationTokenSource();

Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();

    Console.WriteLine("Shutting down gateway...");
    await gateway.StopAsync();

    // Wait for in-flight requests
    await Task.Delay(1000);

    gateway.Dispose();
};

await gateway.StartAsync();
```

### 5. Circuit Breaker Pattern

```csharp
var config = new GatewayConfiguration
{
    EnableCircuitBreaker = true,
    CircuitBreakerFailureThreshold = 5,  // Open after 5 failures
    CircuitBreakerTimeout = 60000         // Wait 60s before retry
};

// When an upstream fails consistently:
// 1. Circuit opens (requests fail fast)
// 2. After timeout, circuit enters half-open
// 3. Test request sent
// 4. If successful, circuit closes
// 5. If failed, circuit reopens
```

## Troubleshooting

### Issue: 503 Service Unavailable

**Cause**: No healthy upstreams available

**Solutions:**
1. Check upstream server health
2. Verify upstream URLs are correct
3. Check network connectivity
4. Review upstream health state: `loadBalancer.GetUpstreamState(upstream)`
5. Manually mark healthy if needed: `loadBalancer.MarkHealthy(upstream)`

### Issue: 429 Rate Limit Exceeded

**Cause**: Client exceeded rate limit

**Solutions:**
1. Increase rate limit: `route.RateLimit = 200`
2. Check current state: `rateLimiter.GetState(clientId)`
3. Reset rate limit: `rateLimiter.Reset(clientId)`
4. Implement client-side throttling

### Issue: 504 Gateway Timeout

**Cause**: Upstream request took too long

**Solutions:**
1. Increase timeout: `config.RequestTimeout = 60000`
2. Optimize upstream service
3. Check network latency
4. Implement caching for slow operations

### Issue: High Memory Usage

**Cause**: Too many concurrent connections

**Solutions:**
1. Reduce `MaxConcurrentRequests`
2. Implement request queuing
3. Scale horizontally (multiple gateway instances)
4. Optimize buffer size: `config.BufferSize = 40960`

### Issue: Uneven Load Distribution

**Cause**: Wrong load balancing strategy

**Solutions:**
1. Use `LeastConnections` for variable workloads
2. Use `RoundRobin` for uniform workloads
3. Check upstream health and connection counts
4. Verify all upstreams are healthy

## Examples

### Example 1: Simple API Gateway

```csharp
var config = new GatewayConfiguration
{
    Port = 8080,
    Routes = new List<RouteConfiguration>
    {
        new RouteConfiguration
        {
            Id = "api",
            Path = "/api/{resource}/{id?}",
            Upstreams = new List<string> { "http://localhost:5000" }
        }
    }
};

var gateway = new ApiGateway(config, logger);
await gateway.StartAsync();

// Handles: /api/users, /api/users/123, /api/orders, etc.
```

### Example 2: Multi-Region Gateway

```csharp
var route = new RouteConfiguration
{
    Id = "global-api",
    Path = "/api/data",
    Upstreams = new List<string>
    {
        "http://us-east-1.api.com",
        "http://us-west-2.api.com",
        "http://eu-west-1.api.com"
    },
    LoadBalancingStrategy = LoadBalancingStrategy.LeastConnections
};

// Automatically routes to the region with lowest load
```

### Example 3: Rate-Limited Public API

```csharp
var config = new GatewayConfiguration
{
    EnableRateLimiting = true,
    DefaultRateLimit = 100,
    Routes = new List<RouteConfiguration>
    {
        new RouteConfiguration
        {
            Path = "/api/search",
            RateLimit = 10,  // Strict limit for expensive operation
            Upstreams = new List<string> { "http://search:5000" }
        },
        new RouteConfiguration
        {
            Path = "/api/status",
            RateLimit = 1000,  // Relaxed limit for lightweight operation
            Upstreams = new List<string> { "http://status:5000" }
        }
    }
};
```

## Thread Safety

All gateway components are thread-safe:

- **ApiGateway**: Concurrent request processing with semaphore control
- **RouteManager**: Lock-protected route compilation and matching
- **LoadBalancer**: ConcurrentDictionary for upstream state
- **RateLimiter**: Lock-protected token bucket operations
- **Metrics**: Interlocked operations for counters

## Version History

- **0.3.0** (Current)
  - Initial release
  - Route matching with parameter extraction
  - Load balancing (5 strategies)
  - Rate limiting (token bucket)
  - Health tracking and failover
  - Metrics collection
  - Concurrency control

## License

Part of the Conduit framework. See main repository for license information.

## Related Modules

- **Conduit.Messaging**: Message bus integration
- **Conduit.Api**: RESTful API patterns
- **Conduit.Common**: Shared utilities
- **Conduit.Serialization**: Message serialization

## Support

For issues, questions, or contributions, please refer to the main Conduit repository.
