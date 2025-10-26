namespace Conduit.Metrics;

/// <summary>
/// Configuration for metrics collection and export.
/// </summary>
public class MetricsConfiguration
{
    /// <summary>
    /// Enable metrics collection.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Prefix for all metric names.
    /// </summary>
    public string Prefix { get; set; } = "conduit";

    /// <summary>
    /// Metrics provider type.
    /// </summary>
    public MetricsProvider Provider { get; set; } = MetricsProvider.Prometheus;

    /// <summary>
    /// Enable Prometheus exporter.
    /// </summary>
    public bool EnablePrometheus { get; set; } = true;

    /// <summary>
    /// Prometheus scrape endpoint path.
    /// </summary>
    public string PrometheusEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Prometheus scrape port (0 = use application port).
    /// </summary>
    public int PrometheusPort { get; set; } = 0;

    /// <summary>
    /// Enable OpenTelemetry exporter.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; } = false;

    /// <summary>
    /// OpenTelemetry endpoint URL.
    /// </summary>
    public string? OpenTelemetryEndpoint { get; set; }

    /// <summary>
    /// Enable console exporter for debugging.
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Enable runtime metrics (GC, CPU, memory).
    /// </summary>
    public bool EnableRuntimeMetrics { get; set; } = true;

    /// <summary>
    /// Enable health checks.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Health check endpoint path.
    /// </summary>
    public string HealthCheckEndpoint { get; set; } = "/health";

    /// <summary>
    /// Detailed health check endpoint path.
    /// </summary>
    public string DetailedHealthCheckEndpoint { get; set; } = "/health/detailed";

    /// <summary>
    /// Default histogram buckets.
    /// </summary>
    public double[] DefaultHistogramBuckets { get; set; } = new[]
    {
        0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0
    };

    /// <summary>
    /// Metric collection interval in seconds.
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Custom labels to add to all metrics.
    /// </summary>
    public Dictionary<string, string> GlobalLabels { get; set; } = new();

    /// <summary>
    /// Enable automatic metric collection for framework components.
    /// </summary>
    public bool EnableAutomaticInstrumentation { get; set; } = true;

    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Metrics provider types.
/// </summary>
public enum MetricsProvider
{
    Prometheus,
    OpenTelemetry,
    Both,
    Custom
}

/// <summary>
/// Health check configuration.
/// </summary>
public class HealthCheckOptions
{
    /// <summary>
    /// Health check name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tags for categorizing health checks.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Timeout for this health check.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Failure status (Degraded or Unhealthy).
    /// </summary>
    public HealthStatus FailureStatus { get; set; } = HealthStatus.Unhealthy;
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}
