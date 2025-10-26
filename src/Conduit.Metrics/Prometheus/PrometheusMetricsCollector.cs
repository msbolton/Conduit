using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Conduit.Metrics.Prometheus;

/// <summary>
/// Prometheus-based metrics collector implementation.
/// </summary>
public class PrometheusMetricsCollector : IMetricsCollector
{
    private readonly MetricsConfiguration _configuration;
    private readonly ILogger<PrometheusMetricsCollector> _logger;
    private readonly Dictionary<string, IMetric> _metrics = new();
    private readonly object _lock = new();

    public PrometheusMetricsCollector(
        IOptions<MetricsConfiguration> configuration,
        ILogger<PrometheusMetricsCollector> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;
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

            var prometheusCounter = global::Prometheus.Metrics.CreateCounter(
                metricName,
                help ?? $"Counter for {name}",
                new CounterConfiguration { LabelNames = labelNames });

            var wrapper = new PrometheusCounter(metricName, help, labelNames, prometheusCounter);
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

            var prometheusGauge = global::Prometheus.Metrics.CreateGauge(
                metricName,
                help ?? $"Gauge for {name}",
                new GaugeConfiguration { LabelNames = labelNames });

            var wrapper = new PrometheusGauge(metricName, help, labelNames, prometheusGauge);
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
            var prometheusHistogram = global::Prometheus.Metrics.CreateHistogram(
                metricName,
                help ?? $"Histogram for {name}",
                new HistogramConfiguration
                {
                    LabelNames = labelNames,
                    Buckets = buckets
                });

            var wrapper = new PrometheusHistogram(metricName, help, labelNames, buckets, prometheusHistogram);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
    }

    public ISummary Summary(string name, string? help = null, params string[] labelNames)
    {
        var metricName = FormatMetricName(name);
        lock (_lock)
        {
            if (_metrics.TryGetValue(metricName, out var existing) && existing is ISummary summary)
            {
                return summary;
            }

            var prometheusSummary = global::Prometheus.Metrics.CreateSummary(
                metricName,
                help ?? $"Summary for {name}",
                new SummaryConfiguration { LabelNames = labelNames });

            var wrapper = new PrometheusSummary(metricName, help, labelNames, prometheusSummary);
            _metrics[metricName] = wrapper;
            return wrapper;
        }
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
            var wrapper = new PrometheusTimer(metricName, help, labelNames, histogram);
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
        var formatted = name.ToLowerInvariant().Replace('.', '_').Replace('-', '_');
        return string.IsNullOrEmpty(_configuration.Prefix)
            ? formatted
            : $"{_configuration.Prefix}_{formatted}";
    }
}
