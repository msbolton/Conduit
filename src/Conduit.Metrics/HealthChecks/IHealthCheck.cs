using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Metrics.HealthChecks;

/// <summary>
/// Interface for health check implementations.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Health check name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Performs the health check.
    /// </summary>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a health check.
/// </summary>
public class HealthCheckResult
{
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public Exception? Exception { get; set; }

    public static HealthCheckResult Healthy(string? description = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Description = description,
            Data = data ?? new()
        };
    }

    public static HealthCheckResult Degraded(string? description = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Degraded,
            Description = description,
            Data = data ?? new()
        };
    }

    public static HealthCheckResult Unhealthy(string? description = null, Exception? exception = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Unhealthy,
            Description = description,
            Exception = exception,
            Data = data ?? new()
        };
    }
}

/// <summary>
/// Health check report containing all health check results.
/// </summary>
public class HealthReport
{
    public HealthStatus Status { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, HealthCheckResult> Entries { get; set; } = new();

    public static HealthStatus GetAggregateStatus(IEnumerable<HealthCheckResult> results)
    {
        var hasUnhealthy = results.Any(r => r.Status == HealthStatus.Unhealthy);
        if (hasUnhealthy) return HealthStatus.Unhealthy;

        var hasDegraded = results.Any(r => r.Status == HealthStatus.Degraded);
        if (hasDegraded) return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }
}
