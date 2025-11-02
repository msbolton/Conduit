using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conduit.Metrics;

/// <summary>
/// Service for aggregating and reporting metrics.
/// </summary>
public class MetricsReporter
{
    private readonly IMetricsCollector _collector;
    private readonly MetricsConfiguration _configuration;
    private readonly ILogger<MetricsReporter> _logger;

    public MetricsReporter(
        IMetricsCollector collector,
        IOptions<MetricsConfiguration> configuration,
        ILogger<MetricsReporter> logger)
    {
        _collector = collector;
        _configuration = configuration.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets a snapshot of all current metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        var metrics = _collector.GetAllMetrics();
        return new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Metrics = metrics.Select(m => new MetricInfo
            {
                Name = m.Name,
                Type = m.Type.ToString(),
                Help = m.Help,
                Value = m.GetValue(),
                Labels = m.LabelNames.ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Reports metrics to console (for debugging).
    /// </summary>
    public void ReportToConsole()
    {
        var snapshot = GetSnapshot();
        _logger.LogInformation("=== Metrics Report at {Timestamp} ===", snapshot.Timestamp);

        foreach (var metric in snapshot.Metrics)
        {
            _logger.LogInformation("{Name} ({Type}): {Value} - {Help}",
                metric.Name, metric.Type, metric.Value, metric.Help ?? "No description");
        }

        _logger.LogInformation("=== End Metrics Report ===");
    }

    /// <summary>
    /// Exports metrics in JSON format.
    /// </summary>
    public string ExportAsJson()
    {
        var snapshot = GetSnapshot();
        return System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Snapshot of metrics at a point in time.
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<MetricInfo> Metrics { get; set; } = new();
}

/// <summary>
/// Information about a single metric.
/// </summary>
public class MetricInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Help { get; set; }
    public double Value { get; set; }
    public List<string> Labels { get; set; } = new();
}
