using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conduit.Metrics.OpenTelemetry;

/// <summary>
/// OpenTelemetry-based metrics collector implementation.
/// </summary>
public class OpenTelemetryMetricsCollector : IMetricsCollector
{
    private readonly MetricsConfiguration _configuration;
    private readonly ILogger<OpenTelemetryMetricsCollector> _logger;
    private readonly Meter _meter;
    private readonly Dictionary<string, IMetric> _metrics = new();
    private readonly object _lock = new();

    public OpenTelemetryMetricsCollector(
        IOptions<MetricsConfiguration> configuration,
        ILogger<OpenTelemetryMetricsCollector> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _meter = new Meter(_configuration.Prefix, "1.0.0");
    }

    public ICounter Counter(string name, string? help = null, params string[] labelNames)
    {
        var metricName = FormatMetricName(name);
        lock (_lock)
        {
            if (_metrics.TryGetValue(metricName, out var existing) && existing is ICounter counter)
            {
                return counter;
            }

            var otelCounter = _meter.CreateCounter<double>(metricName, unit: null, description: help);
            var wrapper = new OpenTelemetryCounter(metricName, help, labelNames, otelCounter);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
    }

    public IGauge Gauge(string name, string? help = null, params string[] labelNames)
    {
        var metricName = FormatMetricName(name);
        lock (_lock)
        {
            if (_metrics.TryGetValue(metricName, out var existing) && existing is IGauge gauge)
            {
                return gauge;
            }

            var wrapper = new OpenTelemetryGauge(metricName, help, labelNames, _meter);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
    }

    public IHistogram Histogram(string name, string? help = null, double[]? buckets = null, params string[] labelNames)
    {
        var metricName = FormatMetricName(name);
        lock (_lock)
        {
            if (_metrics.TryGetValue(metricName, out var existing) && existing is IHistogram histogram)
            {
                return histogram;
            }

            buckets ??= _configuration.DefaultHistogramBuckets;
            var otelHistogram = _meter.CreateHistogram<double>(metricName, unit: null, description: help);
            var wrapper = new OpenTelemetryHistogram(metricName, help, labelNames, buckets, otelHistogram);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
    }

    public ISummary Summary(string name, string? help = null, params string[] labelNames)
    {
        // OpenTelemetry doesn't have native summary support, use histogram instead
        var histogram = Histogram(name, help, null, labelNames);
        return new OpenTelemetrySummary(histogram);
    }

    public ITimer Timer(string name, string? help = null, params string[] labelNames)
    {
        var metricName = FormatMetricName(name);
        lock (_lock)
        {
            if (_metrics.TryGetValue(metricName, out var existing) && existing is ITimer timer)
            {
                return timer;
            }

            var histogram = Histogram(name, help ?? $"Timer for {name}", null, labelNames);
            var wrapper = new OpenTelemetryTimer(metricName, help, labelNames, histogram);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
    }

    public void Record(string name, double value, MetricType type = MetricType.Gauge, params (string Name, string Value)[] labels)
    {
        var labelNames = labels.Select(l => l.Name).ToArray();
        var labelValues = labels.Select(l => l.Value).ToArray();

        switch (type)
        {
            case MetricType.Counter:
                Counter(name, null, labelNames).Increment(value, labelValues);
                break;
            case MetricType.Gauge:
                Gauge(name, null, labelNames).Set(value, labelValues);
                break;
            case MetricType.Histogram:
                Histogram(name, null, null, labelNames).Observe(value, labelValues);
                break;
            case MetricType.Summary:
                Summary(name, null, labelNames).Observe(value, labelValues);
                break;
            default:
                _logger.LogWarning("Unknown metric type {Type} for {Name}", type, name);
                break;
        }
    }

    public void Increment(string name, double value = 1.0, params (string Name, string Value)[] labels)
    {
        Record(name, value, MetricType.Counter, labels);
    }

    public void Set(string name, double value, params (string Name, string Value)[] labels)
    {
        Record(name, value, MetricType.Gauge, labels);
    }

    public IDisposable Measure(string name, params (string Name, string Value)[] labels)
    {
        var labelNames = labels.Select(l => l.Name).ToArray();
        var labelValues = labels.Select(l => l.Value).ToArray();
        return Timer(name, null, labelNames).Time(labelValues);
    }

    public IEnumerable<IMetric> GetAllMetrics()
    {
        lock (_lock)
        {
            return _metrics.Values.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }

    private string FormatMetricName(string name)
    {
        return name.ToLowerInvariant().Replace('.', '_').Replace('-', '_');
    }
}

/// <summary>
/// OpenTelemetry counter wrapper.
/// </summary>
internal class OpenTelemetryCounter : ICounter
{
    private readonly Counter<double> _counter;

    public OpenTelemetryCounter(string name, string? help, string[] labelNames, Counter<double> counter)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        _counter = counter;
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Counter;
    public IReadOnlyList<string> LabelNames { get; }

    public void Increment(double value = 1.0, params string[] labelValues)
    {
        var tags = CreateTags(labelValues);
        _counter.Add(value, tags);
    }

    public ICounter WithLabels(params string[] labelValues) => this;

    public double GetValue(params string[] labelValues) => 0; // OTel doesn't expose current values

    private KeyValuePair<string, object?>[] CreateTags(string[] labelValues)
    {
        var tags = new List<KeyValuePair<string, object?>>();
        for (int i = 0; i < Math.Min(LabelNames.Count, labelValues.Length); i++)
        {
            tags.Add(new KeyValuePair<string, object?>(LabelNames[i], labelValues[i]));
        }
        return tags.ToArray();
    }
}

/// <summary>
/// OpenTelemetry gauge wrapper.
/// </summary>
internal class OpenTelemetryGauge : IGauge
{
    private readonly ObservableGauge<double> _gauge;
    private double _currentValue;

    public OpenTelemetryGauge(string name, string? help, string[] labelNames, Meter meter)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        _gauge = meter.CreateObservableGauge(name, () => _currentValue, unit: null, description: help);
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Gauge;
    public IReadOnlyList<string> LabelNames { get; }

    public void Set(double value, params string[] labelValues)
    {
        _currentValue = value;
    }

    public void Increment(double value = 1.0, params string[] labelValues)
    {
        _currentValue += value;
    }

    public void Decrement(double value = 1.0, params string[] labelValues)
    {
        _currentValue -= value;
    }

    public void SetToCurrentTime(params string[] labelValues)
    {
        _currentValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public IGauge WithLabels(params string[] labelValues) => this;

    public double GetValue(params string[] labelValues) => _currentValue;
}

/// <summary>
/// OpenTelemetry histogram wrapper.
/// </summary>
internal class OpenTelemetryHistogram : IHistogram
{
    private readonly Histogram<double> _histogram;

    public OpenTelemetryHistogram(string name, string? help, string[] labelNames, double[] buckets, Histogram<double> histogram)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        Buckets = buckets;
        _histogram = histogram;
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Histogram;
    public IReadOnlyList<string> LabelNames { get; }
    public double[] Buckets { get; }

    public void Observe(double value, params string[] labelValues)
    {
        var tags = CreateTags(labelValues);
        _histogram.Record(value, tags);
    }

    public IHistogram WithLabels(params string[] labelValues) => this;

    public double GetValue(params string[] labelValues) => 0; // OTel doesn't expose current values

    private KeyValuePair<string, object?>[] CreateTags(string[] labelValues)
    {
        var tags = new List<KeyValuePair<string, object?>>();
        for (int i = 0; i < Math.Min(LabelNames.Count, labelValues.Length); i++)
        {
            tags.Add(new KeyValuePair<string, object?>(LabelNames[i], labelValues[i]));
        }
        return tags.ToArray();
    }
}

/// <summary>
/// OpenTelemetry summary wrapper (uses histogram).
/// </summary>
internal class OpenTelemetrySummary : ISummary
{
    private readonly IHistogram _histogram;

    public OpenTelemetrySummary(IHistogram histogram)
    {
        _histogram = histogram;
    }

    public string Name => _histogram.Name;
    public string? Help => _histogram.Help;
    public MetricType Type => MetricType.Summary;
    public IReadOnlyList<string> LabelNames => _histogram.LabelNames;

    public void Observe(double value, params string[] labelValues)
    {
        _histogram.Observe(value, labelValues);
    }

    public ISummary WithLabels(params string[] labelValues) => this;

    public double GetValue(params string[] labelValues) => _histogram.GetValue(labelValues);
}

/// <summary>
/// OpenTelemetry timer wrapper.
/// </summary>
internal class OpenTelemetryTimer : ITimer
{
    private readonly IHistogram _histogram;

    public OpenTelemetryTimer(string name, string? help, string[] labelNames, IHistogram histogram)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        _histogram = histogram;
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Timer;
    public IReadOnlyList<string> LabelNames { get; }

    public IDisposable Time(params string[] labelValues)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        return new TimerScope(() =>
        {
            stopwatch.Stop();
            _histogram.Observe(stopwatch.Elapsed.TotalSeconds, labelValues);
        });
    }

    public void Record(TimeSpan duration, params string[] labelValues)
    {
        _histogram.Observe(duration.TotalSeconds, labelValues);
    }

    public void Record(double milliseconds, params string[] labelValues)
    {
        _histogram.Observe(milliseconds / 1000.0, labelValues);
    }

    public double GetValue(params string[] labelValues) => _histogram.GetValue(labelValues);

    private class TimerScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public TimerScope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _onDispose();
                _disposed = true;
            }
        }
    }
}
