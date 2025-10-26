using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Conduit.Metrics.HealthChecks;

/// <summary>
/// Service for managing and executing health checks.
/// </summary>
public class HealthCheckService
{
    private readonly IEnumerable<IHealthCheck> _healthChecks;
    private readonly MetricsConfiguration _configuration;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IMetricsCollector _metricsCollector;

    public HealthCheckService(
        IEnumerable<IHealthCheck> healthChecks,
        IOptions<MetricsConfiguration> configuration,
        ILogger<HealthCheckService> logger,
        IMetricsCollector metricsCollector)
    {
        _healthChecks = healthChecks;
        _configuration = configuration.Value;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    /// <summary>
    /// Executes all registered health checks.
    /// </summary>
    public async Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new HealthReport();

        var healthCheckTasks = _healthChecks.Select(async healthCheck =>
        {
            var checkStopwatch = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_configuration.HealthCheckTimeoutSeconds));

                var result = await healthCheck.CheckHealthAsync(cts.Token);
                result.Duration = checkStopwatch.Elapsed;

                _metricsCollector.Set(
                    $"health_check_status_{healthCheck.Name}",
                    result.Status == HealthStatus.Healthy ? 1 : 0);

                _metricsCollector.Record(
                    $"health_check_duration_{healthCheck.Name}",
                    checkStopwatch.Elapsed.TotalSeconds,
                    MetricType.Histogram);

                return (healthCheck.Name, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check {Name} failed", healthCheck.Name);

                _metricsCollector.Set($"health_check_status_{healthCheck.Name}", 0);

                return (healthCheck.Name, HealthCheckResult.Unhealthy(
                    $"Health check failed: {ex.Message}",
                    ex));
            }
        });

        var results = await Task.WhenAll(healthCheckTasks);

        foreach (var (name, result) in results)
        {
            report.Entries[name] = result;
        }

        report.Status = HealthReport.GetAggregateStatus(report.Entries.Values);
        report.TotalDuration = stopwatch.Elapsed;

        // Record aggregate health status
        _metricsCollector.Set("health_check_status_aggregate", report.Status == HealthStatus.Healthy ? 1 : 0);

        return report;
    }

    /// <summary>
    /// Checks if the system is healthy.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        var report = await CheckHealthAsync(cancellationToken);
        return report.Status == HealthStatus.Healthy;
    }
}
