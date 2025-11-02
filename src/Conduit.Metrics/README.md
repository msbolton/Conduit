# Conduit.Metrics

Comprehensive metrics collection, monitoring, and health checking for the Conduit framework with support for Prometheus and OpenTelemetry.

## Features

- **Multiple Metrics Providers**
  - Prometheus integration with scrape endpoint
  - OpenTelemetry support with OTLP export
  - Console exporter for debugging
  - Custom provider support

- **Metric Types**
  - **Counter** - Monotonically increasing values
  - **Gauge** - Values that can go up and down
  - **Histogram** - Distribution of values with buckets
  - **Summary** - Percentile calculations
  - **Timer** - Duration measurements

- **Health Checks**
  - Built-in health checks (memory, thread pool, components)
  - Custom health check support
  - Startup/liveness/readiness probes
  - Kubernetes-compatible endpoints
  - Detailed health reports with timing

- **Runtime Metrics**
  - GC statistics
  - Memory usage (heap, working set)
  - Thread pool utilization
  - CPU usage
  - Component health

- **Integration**
  - ASP.NET Core middleware support
  - Microsoft.Extensions.Diagnostics.HealthChecks integration
  - Automatic instrumentation for Conduit components
  - Global labels and tagging

## Installation

```bash
dotnet add package Conduit.Metrics
```

## Quick Start

### Basic Setup with Prometheus

```csharp
using Conduit.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    // Add metrics with Prometheus
    services.AddConduitMetrics(configure: config =>
    {
        config.Enabled = true;
        config.Provider = MetricsProvider.Prometheus;
        config.Prefix = "myapp";
        config.EnablePrometheus = true;
        config.PrometheusEndpoint = "/metrics";
        config.EnableHealthChecks = true;
        config.HealthCheckEndpoint = "/health";
    });
});

var host = builder.Build();
await host.RunAsync();
```

### Using Metrics

```csharp
public class OrderService
{
    private readonly IMetricsCollector _metrics;

    public OrderService(IMetricsCollector metrics)
    {
        _metrics = metrics;
    }

    public async Task<Order> ProcessOrderAsync(Order order)
    {
        // Increment counter
        _metrics.Increment("orders_processed_total",
            labelNames: ("status", "success"));

        // Measure duration
        using (_metrics.Measure("order_processing_duration_seconds"))
        {
            await ProcessOrder(order);
        }

        // Set gauge
        _metrics.Set("orders_pending", GetPendingOrderCount());

        return order;
    }
}
```

## Configuration

### appsettings.json

```json
{
  "Conduit": {
    "Metrics": {
      "Enabled": true,
      "Prefix": "conduit",
      "Provider": "Prometheus",
      "EnablePrometheus": true,
      "PrometheusEndpoint": "/metrics",
      "PrometheusPort": 0,
      "EnableOpenTelemetry": false,
      "OpenTelemetryEndpoint": null,
      "EnableConsoleExporter": false,
      "EnableRuntimeMetrics": true,
      "EnableHealthChecks": true,
      "HealthCheckEndpoint": "/health",
      "DetailedHealthCheckEndpoint": "/health/detailed",
      "DefaultHistogramBuckets": [
        0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1,
        0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0
      ],
      "CollectionIntervalSeconds": 15,
      "GlobalLabels": {
        "environment": "production",
        "service": "order-service"
      },
      "EnableAutomaticInstrumentation": true,
      "HealthCheckTimeoutSeconds": 5
    }
  }
}
```

### Code Configuration

```csharp
services.AddConduitMetrics(configure: config =>
{
    config.Enabled = true;
    config.Prefix = "myapp";
    config.Provider = MetricsProvider.Prometheus;
    config.EnablePrometheus = true;
    config.EnableHealthChecks = true;
    config.DefaultHistogramBuckets = new[]
    {
        0.001, 0.01, 0.1, 1.0, 10.0
    };
    config.GlobalLabels = new Dictionary<string, string>
    {
        ["environment"] = "production",
        ["datacenter"] = "us-east-1"
    };
});
```

## Metric Types

### Counter

Monotonically increasing value (e.g., requests processed, errors).

```csharp
var counter = _metrics.Counter(
    "requests_total",
    "Total number of requests",
    "method", "status");

counter.Increment(1.0, "GET", "200");

// Shorthand
_metrics.Increment("requests_total", 1.0,
    ("method", "GET"),
    ("status", "200"));
```

### Gauge

Value that can increase or decrease (e.g., temperature, queue size).

```csharp
var gauge = _metrics.Gauge(
    "queue_size",
    "Current queue size");

gauge.Set(42);
gauge.Increment(1);
gauge.Decrement(5);
gauge.SetToCurrentTime();

// Shorthand
_metrics.Set("queue_size", 42);
```

### Histogram

Distribution of values in buckets (e.g., request duration, response size).

```csharp
var histogram = _metrics.Histogram(
    "request_duration_seconds",
    "Request duration in seconds",
    buckets: new[] { 0.1, 0.5, 1.0, 2.5, 5.0 },
    "route");

histogram.Observe(1.234, "/api/orders");

// Shorthand
_metrics.Record("request_duration_seconds", 1.234,
    MetricType.Histogram,
    ("route", "/api/orders"));
```

### Summary

Percentile calculations (p50, p90, p99).

```csharp
var summary = _metrics.Summary(
    "response_size_bytes",
    "Response size in bytes");

summary.Observe(1024);
```

### Timer

Measure duration of operations.

```csharp
var timer = _metrics.Timer(
    "operation_duration_seconds",
    "Operation duration");

// Using block
using (timer.Time("operation_name", "create_order"))
{
    await PerformOperation();
}

// Manual recording
timer.Record(TimeSpan.FromSeconds(1.5), "operation_name", "update_order");
timer.Record(150.0, "operation_name", "delete_order"); // milliseconds

// Shorthand
using (_metrics.Measure("operation_duration_seconds",
    ("operation", "create_order")))
{
    await PerformOperation();
}
```

## Health Checks

### Built-in Health Checks

```csharp
services.AddConduitMetrics();

// Built-in checks are automatically registered:
// - component_registry: Conduit component registry status
// - message_bus: Message bus connectivity
// - memory: Memory usage monitoring
// - thread_pool: Thread pool utilization
// - startup: Startup readiness probe
// - liveness: Liveness probe
```

### Custom Health Checks

```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnection _connection;

    public DatabaseHealthCheck(IDbConnection connection)
    {
        _connection = connection;
    }

    public string Name => "database";

    public async Task<HealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _connection.OpenAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["connection_state"] = _connection.State.ToString()
            };
            return HealthCheckResult.Healthy("Database is reachable", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database is unreachable",
                ex);
        }
    }
}

// Register
services.AddConduitHealthCheck<DatabaseHealthCheck>();
```

### Checking Health

```csharp
public class HealthController
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public async Task<IActionResult> GetHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        return report.Status == HealthStatus.Healthy
            ? Ok(report)
            : StatusCode(503, report);
    }

    public async Task<bool> IsHealthyAsync()
    {
        return await _healthCheckService.IsHealthyAsync();
    }
}
```

### Health Check Response

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Database is reachable",
      "duration": "00:00:00.0234567",
      "data": {
        "connection_state": "Open"
      }
    },
    "memory": {
      "status": "Healthy",
      "description": "Memory usage is normal: 245.67 MB",
      "duration": "00:00:00.0001234",
      "data": {
        "gc_memory_bytes": 257485824,
        "working_set_bytes": 268435456,
        "gc_memory_mb": 245.67,
        "working_set_mb": 256.0
      }
    }
  }
}
```

## Prometheus Integration

### Setup

```csharp
services.AddConduitMetrics(configure: config =>
{
    config.Provider = MetricsProvider.Prometheus;
    config.EnablePrometheus = true;
    config.PrometheusEndpoint = "/metrics";
});
```

### ASP.NET Core Middleware

```csharp
// In Program.cs or Startup.cs
app.UseMetricServer(); // Prometheus scrape endpoint

// Or with custom path
app.UseMetricServer("/custom/metrics");
```

### Prometheus Configuration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'conduit-app'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

### Example Metrics Output

```
# HELP conduit_requests_total Total number of requests
# TYPE conduit_requests_total counter
conduit_requests_total{method="GET",status="200"} 1234

# HELP conduit_request_duration_seconds Request duration in seconds
# TYPE conduit_request_duration_seconds histogram
conduit_request_duration_seconds_bucket{route="/api/orders",le="0.1"} 100
conduit_request_duration_seconds_bucket{route="/api/orders",le="0.5"} 450
conduit_request_duration_seconds_bucket{route="/api/orders",le="1.0"} 890
conduit_request_duration_seconds_sum{route="/api/orders"} 234.56
conduit_request_duration_seconds_count{route="/api/orders"} 1000
```

## OpenTelemetry Integration

### Setup

```csharp
services.AddConduitMetrics(configure: config =>
{
    config.EnableOpenTelemetry = true;
    config.OpenTelemetryEndpoint = "http://localhost:4317";
});

services.AddOpenTelemetryMetrics(builder =>
{
    builder
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        });
});
```

### With Jaeger

```csharp
services.AddOpenTelemetryMetrics(builder =>
{
    builder
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
        });
});
```

## Metrics Reporting

### Get Metrics Snapshot

```csharp
public class MetricsController
{
    private readonly MetricsReporter _reporter;

    public MetricsController(MetricsReporter reporter)
    {
        _reporter = reporter;
    }

    public IActionResult GetMetrics()
    {
        var snapshot = _reporter.GetSnapshot();
        return Ok(snapshot);
    }

    public IActionResult ExportMetrics()
    {
        var json = _reporter.ExportAsJson();
        return Content(json, "application/json");
    }
}
```

### Console Reporting

```csharp
services.AddConduitMetrics(configure: config =>
{
    config.EnableConsoleExporter = true;
});

// Metrics will be logged to console periodically
// Or manually:
_reporter.ReportToConsole();
```

## Advanced Scenarios

### Custom Metric Provider

```csharp
public class CustomMetricsCollector : IMetricsCollector
{
    public ICounter Counter(string name, string? help = null, params string[] labelNames)
    {
        // Your implementation
    }

    // Implement other methods...
}

services.AddSingleton<IMetricsCollector, CustomMetricsCollector>();
```

### Automatic Instrumentation

```csharp
// Conduit components automatically emit metrics:
// - component_initializations_total
// - component_lifecycle_state
// - message_bus_messages_sent_total
// - message_bus_messages_received_total
// - pipeline_executions_total
// - pipeline_execution_duration_seconds

services.AddConduitMetrics(configure: config =>
{
    config.EnableAutomaticInstrumentation = true;
});
```

### Global Labels

```csharp
services.AddConduitMetrics(configure: config =>
{
    config.GlobalLabels = new Dictionary<string, string>
    {
        ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        ["version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
        ["host"] = Environment.MachineName,
        ["region"] = "us-east-1"
    };
});
```

### Custom Histogram Buckets

```csharp
// For API response times
var apiHistogram = _metrics.Histogram(
    "api_response_time_seconds",
    "API response time",
    buckets: new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0 },
    "endpoint", "method");

// For database query times
var dbHistogram = _metrics.Histogram(
    "db_query_duration_seconds",
    "Database query duration",
    buckets: new[] { 0.001, 0.01, 0.05, 0.1, 0.5, 1.0, 5.0 },
    "query_type");

// For message sizes
var sizeHistogram = _metrics.Histogram(
    "message_size_bytes",
    "Message size in bytes",
    buckets: new[] { 100, 1000, 10000, 100000, 1000000 },
    "message_type");
```

## Kubernetes Integration

### Health Endpoints

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: conduit-app
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
        startupProbe:
          httpGet:
            path: /health
            port: 8080
          failureThreshold: 30
          periodSeconds: 10
```

### Prometheus ServiceMonitor

```yaml
# servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: conduit-app
spec:
  selector:
    matchLabels:
      app: conduit-app
  endpoints:
  - port: metrics
    path: /metrics
    interval: 15s
```

## Best Practices

### Naming Conventions

```csharp
// Use snake_case for metric names
_metrics.Counter("http_requests_total", ...);      // Good
_metrics.Counter("HttpRequestsTotal", ...);        // Bad

// Use base units (seconds, bytes)
_metrics.Histogram("request_duration_seconds", ...); // Good
_metrics.Histogram("request_duration_ms", ...);      // Bad

// Use _total suffix for counters
_metrics.Counter("messages_processed_total", ...);   // Good
_metrics.Counter("messages_processed", ...);         // Acceptable

// Be specific and descriptive
_metrics.Counter("order_service_orders_created_total", ...); // Good
_metrics.Counter("count", ...);                              // Bad
```

### Label Cardinality

```csharp
// Good - Low cardinality labels
_metrics.Increment("requests_total",
    ("method", "GET"),
    ("status", "200"),
    ("route", "/api/orders"));

// Bad - High cardinality labels (avoid user IDs, timestamps, UUIDs)
_metrics.Increment("requests_total",
    ("user_id", userId),           // Bad: millions of users
    ("timestamp", timestamp),      // Bad: unique per request
    ("request_id", requestId));    // Bad: unique per request
```

### Performance

```csharp
// Cache metric instances
private readonly ICounter _requestCounter;

public MyService(IMetricsCollector metrics)
{
    _requestCounter = metrics.Counter(
        "requests_total",
        "Total requests",
        "method", "status");
}

public void ProcessRequest(string method)
{
    _requestCounter.Increment(1.0, method, "200"); // Fast
}
```

### Error Handling

```csharp
public async Task ProcessAsync()
{
    try
    {
        using (_metrics.Measure("process_duration_seconds"))
        {
            await DoWork();
        }
        _metrics.Increment("process_success_total");
    }
    catch (Exception ex)
    {
        _metrics.Increment("process_errors_total",
            ("error_type", ex.GetType().Name));
        throw;
    }
}
```

## Troubleshooting

### Metrics Not Appearing

1. **Check configuration**:
   ```csharp
   config.Enabled = true;
   config.Provider = MetricsProvider.Prometheus;
   ```

2. **Verify endpoint**:
   ```bash
   curl http://localhost:5000/metrics
   ```

3. **Check logs**:
   ```csharp
   config.EnableConsoleExporter = true;
   ```

### High Memory Usage

```csharp
// Reduce label cardinality
// Limit histogram buckets
config.DefaultHistogramBuckets = new[] { 0.1, 1.0, 10.0 };

// Clear metrics periodically (if needed)
_metricsCollector.Clear();
```

### Health Checks Timing Out

```csharp
config.HealthCheckTimeoutSeconds = 10; // Increase timeout

// Or per health check
public class SlowHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // ...
    }
}
```

## Dependencies

- `prometheus-net` (>= 8.2.1)
- `OpenTelemetry` (>= 1.7.0)
- `Microsoft.Extensions.Diagnostics.HealthChecks` (>= 8.0.0)
- `Microsoft.Extensions.DependencyInjection` (>= 8.0.0)
- `Microsoft.Extensions.Logging` (>= 8.0.0)

## Examples

See the [examples directory](../../examples/) for complete working examples:
- Basic metrics collection
- Prometheus integration
- OpenTelemetry with Jaeger
- Custom health checks
- ASP.NET Core integration

## License

Part of the Conduit Framework - see LICENSE file for details.
