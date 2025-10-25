using System.Collections.Concurrent;
using System.Diagnostics;
using Conduit.Api;
using Microsoft.Extensions.Logging;

namespace Conduit.Core;

/// <summary>
/// Default implementation of IMetricsCollector for the Conduit framework.
/// </summary>
public class DefaultMetricsCollector : IMetricsCollector
{
    private readonly ILogger<DefaultMetricsCollector>? _logger;
    private readonly ConcurrentDictionary<string, MetricValue> _counters = new();
    private readonly ConcurrentDictionary<string, MetricValue> _gauges = new();
    private readonly ConcurrentDictionary<string, List<double>> _histograms = new();
    private readonly ConcurrentDictionary<string, List<TimeSpan>> _timers = new();
    private readonly List<(string Key, string Value)> _defaultTags;

    /// <summary>
    /// Initializes a new instance of the DefaultMetricsCollector class.
    /// </summary>
    public DefaultMetricsCollector(ILogger<DefaultMetricsCollector>? logger = null)
    {
        _logger = logger;
        _defaultTags = new List<(string Key, string Value)>();
    }

    /// <summary>
    /// Initializes a new instance with default tags.
    /// </summary>
    protected DefaultMetricsCollector(
        ILogger<DefaultMetricsCollector>? logger,
        List<(string Key, string Value)> defaultTags)
    {
        _logger = logger;
        _defaultTags = defaultTags;
    }

    /// <inheritdoc />
    public void RecordCounter(string name, long value = 1, params (string Key, string Value)[] tags)
    {
        var key = GetMetricKey(name, tags);
        _counters.AddOrUpdate(key,
            new MetricValue { Value = value, Tags = CombineTags(tags) },
            (_, existing) =>
            {
                existing.Value += value;
                return existing;
            });

        _logger?.LogTrace("Counter {Name} incremented by {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordGauge(string name, double value, params (string Key, string Value)[] tags)
    {
        var key = GetMetricKey(name, tags);
        _gauges.AddOrUpdate(key,
            new MetricValue { Value = value, Tags = CombineTags(tags), Timestamp = DateTimeOffset.UtcNow },
            (_, existing) =>
            {
                existing.Value = value;
                existing.Timestamp = DateTimeOffset.UtcNow;
                return existing;
            });

        _logger?.LogTrace("Gauge {Name} set to {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordHistogram(string name, double value, params (string Key, string Value)[] tags)
    {
        var key = GetMetricKey(name, tags);
        _histograms.AddOrUpdate(key,
            new List<double> { value },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(value);
                    // Keep only last 1000 values to prevent unbounded growth
                    if (existing.Count > 1000)
                    {
                        existing.RemoveAt(0);
                    }
                }
                return existing;
            });

        _logger?.LogTrace("Histogram {Name} recorded value {Value}", name, value);
    }

    /// <inheritdoc />
    public void RecordTimer(string name, TimeSpan duration, params (string Key, string Value)[] tags)
    {
        var key = GetMetricKey(name, tags);
        _timers.AddOrUpdate(key,
            new List<TimeSpan> { duration },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(duration);
                    // Keep only last 1000 values
                    if (existing.Count > 1000)
                    {
                        existing.RemoveAt(0);
                    }
                }
                return existing;
            });

        RecordHistogram($"{name}.ms", duration.TotalMilliseconds, tags);
        _logger?.LogTrace("Timer {Name} recorded duration {Duration}ms", name, duration.TotalMilliseconds);
    }

    /// <inheritdoc />
    public ITimer StartTimer(string name, params (string Key, string Value)[] tags)
    {
        return new MetricTimer(this, name, CombineTags(tags));
    }

    /// <inheritdoc />
    public void RecordCommandExecution(string commandType, bool success, TimeSpan duration)
    {
        RecordCounter("commands.executed", 1,
            ("type", commandType),
            ("success", success.ToString().ToLowerInvariant()));

        RecordTimer("commands.duration", duration,
            ("type", commandType),
            ("success", success.ToString().ToLowerInvariant()));

        if (!success)
        {
            RecordCounter("commands.failed", 1, ("type", commandType));
        }
    }

    /// <inheritdoc />
    public void RecordEventPublication(string eventType, int handlerCount)
    {
        RecordCounter("events.published", 1, ("type", eventType));
        RecordGauge("events.handlers", handlerCount, ("type", eventType));
    }

    /// <inheritdoc />
    public void RecordQueryExecution(string queryType, bool success, TimeSpan duration, bool cacheHit = false)
    {
        RecordCounter("queries.executed", 1,
            ("type", queryType),
            ("success", success.ToString().ToLowerInvariant()),
            ("cache_hit", cacheHit.ToString().ToLowerInvariant()));

        RecordTimer("queries.duration", duration,
            ("type", queryType),
            ("success", success.ToString().ToLowerInvariant()));

        if (cacheHit)
        {
            RecordCounter("queries.cache_hits", 1, ("type", queryType));
        }
    }

    /// <inheritdoc />
    public void RecordComponentLifecycle(string componentId, string action, bool success)
    {
        RecordCounter("components.lifecycle", 1,
            ("component", componentId),
            ("action", action),
            ("success", success.ToString().ToLowerInvariant()));

        if (!success)
        {
            RecordCounter("components.lifecycle.failures", 1,
                ("component", componentId),
                ("action", action));
        }
    }

    /// <inheritdoc />
    public void RecordBehaviorExecution(string behaviorId, TimeSpan duration, bool success)
    {
        RecordCounter("behaviors.executed", 1,
            ("behavior", behaviorId),
            ("success", success.ToString().ToLowerInvariant()));

        RecordTimer("behaviors.duration", duration,
            ("behavior", behaviorId),
            ("success", success.ToString().ToLowerInvariant()));
    }

    /// <inheritdoc />
    public void RecordError(string messageType, string errorType)
    {
        RecordCounter("errors", 1,
            ("message_type", messageType),
            ("error_type", errorType));
    }

    /// <inheritdoc />
    public void RecordDeadLetter(string messageType, string reason)
    {
        RecordCounter("deadletter", 1,
            ("message_type", messageType),
            ("reason", reason));
    }

    /// <inheritdoc />
    public void RecordRetry(string messageType, int attemptNumber, bool success)
    {
        RecordCounter("retries", 1,
            ("message_type", messageType),
            ("attempt", attemptNumber.ToString()),
            ("success", success.ToString().ToLowerInvariant()));
    }

    /// <inheritdoc />
    public IMetricsCollector WithTags(params (string Key, string Value)[] additionalTags)
    {
        var newTags = new List<(string Key, string Value)>(_defaultTags);
        newTags.AddRange(additionalTags);
        return new DefaultMetricsCollector(_logger, newTags);
    }

    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            Counters = _counters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value),
            Gauges = _gauges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value),
            Histograms = _histograms.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    lock (kvp.Value)
                    {
                        if (!kvp.Value.Any()) return new HistogramStatistics();

                        var sorted = kvp.Value.OrderBy(v => v).ToList();
                        return new HistogramStatistics
                        {
                            Count = sorted.Count,
                            Min = sorted.First(),
                            Max = sorted.Last(),
                            Mean = sorted.Average(),
                            Median = GetPercentile(sorted, 0.5),
                            P95 = GetPercentile(sorted, 0.95),
                            P99 = GetPercentile(sorted, 0.99)
                        };
                    }
                }),
            Timers = _timers.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    lock (kvp.Value)
                    {
                        if (!kvp.Value.Any()) return new TimerStatistics();

                        var milliseconds = kvp.Value.Select(t => t.TotalMilliseconds).OrderBy(v => v).ToList();
                        return new TimerStatistics
                        {
                            Count = milliseconds.Count,
                            MinMs = milliseconds.First(),
                            MaxMs = milliseconds.Last(),
                            MeanMs = milliseconds.Average(),
                            MedianMs = GetPercentile(milliseconds, 0.5),
                            P95Ms = GetPercentile(milliseconds, 0.95),
                            P99Ms = GetPercentile(milliseconds, 0.99)
                        };
                    }
                })
        };
    }

    /// <summary>
    /// Clears all metrics.
    /// </summary>
    public void Clear()
    {
        _counters.Clear();
        _gauges.Clear();
        _histograms.Clear();
        _timers.Clear();
    }

    private string GetMetricKey(string name, (string Key, string Value)[] tags)
    {
        var allTags = CombineTags(tags);
        if (!allTags.Any())
            return name;

        var tagString = string.Join(",", allTags.Select(t => $"{t.Key}={t.Value}"));
        return $"{name}[{tagString}]";
    }

    private List<(string Key, string Value)> CombineTags((string Key, string Value)[] tags)
    {
        var combined = new List<(string Key, string Value)>(_defaultTags);
        combined.AddRange(tags);
        return combined;
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (!sortedValues.Any()) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private class MetricValue
    {
        public double Value { get; set; }
        public List<(string Key, string Value)> Tags { get; set; } = new();
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    private class MetricTimer : ITimer
    {
        private readonly DefaultMetricsCollector _collector;
        private readonly string _name;
        private readonly List<(string Key, string Value)> _tags;
        private readonly Stopwatch _stopwatch;
        private readonly Dictionary<string, TimeSpan> _checkpoints = new();
        private bool _stopped;

        public MetricTimer(DefaultMetricsCollector collector, string name, List<(string Key, string Value)> tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public void Stop()
        {
            if (!_stopped)
            {
                _stopped = true;
                _stopwatch.Stop();
                _collector.RecordTimer(_name, _stopwatch.Elapsed, _tags.ToArray());
            }
        }

        public void Checkpoint(string checkpointName)
        {
            _checkpoints[checkpointName] = _stopwatch.Elapsed;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

/// <summary>
/// Snapshot of all metrics at a point in time.
/// </summary>
public class MetricsSnapshot
{
    /// <summary>
    /// Gets or sets counter metrics.
    /// </summary>
    public Dictionary<string, double> Counters { get; set; } = new();

    /// <summary>
    /// Gets or sets gauge metrics.
    /// </summary>
    public Dictionary<string, double> Gauges { get; set; } = new();

    /// <summary>
    /// Gets or sets histogram statistics.
    /// </summary>
    public Dictionary<string, HistogramStatistics> Histograms { get; set; } = new();

    /// <summary>
    /// Gets or sets timer statistics.
    /// </summary>
    public Dictionary<string, TimerStatistics> Timers { get; set; } = new();
}

/// <summary>
/// Statistics for histogram metrics.
/// </summary>
public class HistogramStatistics
{
    /// <summary>
    /// Gets or sets the count of values.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Gets or sets the mean value.
    /// </summary>
    public double Mean { get; set; }

    /// <summary>
    /// Gets or sets the median value.
    /// </summary>
    public double Median { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile.
    /// </summary>
    public double P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile.
    /// </summary>
    public double P99 { get; set; }
}

/// <summary>
/// Statistics for timer metrics.
/// </summary>
public class TimerStatistics
{
    /// <summary>
    /// Gets or sets the count of timings.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the minimum time in milliseconds.
    /// </summary>
    public double MinMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum time in milliseconds.
    /// </summary>
    public double MaxMs { get; set; }

    /// <summary>
    /// Gets or sets the mean time in milliseconds.
    /// </summary>
    public double MeanMs { get; set; }

    /// <summary>
    /// Gets or sets the median time in milliseconds.
    /// </summary>
    public double MedianMs { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile in milliseconds.
    /// </summary>
    public double P95Ms { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile in milliseconds.
    /// </summary>
    public double P99Ms { get; set; }
}