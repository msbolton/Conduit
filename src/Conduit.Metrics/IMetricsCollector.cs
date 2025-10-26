namespace Conduit.Metrics;

/// <summary>
/// Main interface for collecting and managing metrics in the Conduit framework.
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Creates or retrieves a counter metric.
    /// </summary>
    ICounter Counter(string name, string? help = null, params string[] labelNames);

    /// <summary>
    /// Creates or retrieves a gauge metric.
    /// </summary>
    IGauge Gauge(string name, string? help = null, params string[] labelNames);

    /// <summary>
    /// Creates or retrieves a histogram metric.
    /// </summary>
    IHistogram Histogram(string name, string? help = null, double[]? buckets = null, params string[] labelNames);

    /// <summary>
    /// Creates or retrieves a summary metric (for percentiles).
    /// </summary>
    ISummary Summary(string name, string? help = null, params string[] labelNames);

    /// <summary>
    /// Records a timer metric for measuring duration.
    /// </summary>
    ITimer Timer(string name, string? help = null, params string[] labelNames);

    /// <summary>
    /// Records a simple metric value.
    /// </summary>
    void Record(string name, double value, MetricType type = MetricType.Gauge, params (string Name, string Value)[] labels);

    /// <summary>
    /// Increments a counter by name.
    /// </summary>
    void Increment(string name, double value = 1.0, params (string Name, string Value)[] labels);

    /// <summary>
    /// Sets a gauge value by name.
    /// </summary>
    void Set(string name, double value, params (string Name, string Value)[] labels);

    /// <summary>
    /// Measures execution time of an operation.
    /// </summary>
    IDisposable Measure(string name, params (string Name, string Value)[] labels);

    /// <summary>
    /// Gets all registered metrics.
    /// </summary>
    IEnumerable<IMetric> GetAllMetrics();

    /// <summary>
    /// Clears all metrics.
    /// </summary>
    void Clear();
}

/// <summary>
/// Base interface for all metrics.
/// </summary>
public interface IMetric
{
    string Name { get; }
    string? Help { get; }
    MetricType Type { get; }
    IReadOnlyList<string> LabelNames { get; }
    double GetValue(params string[] labelValues);
}

/// <summary>
/// Counter metric - monotonically increasing value.
/// </summary>
public interface ICounter : IMetric
{
    void Increment(double value = 1.0, params string[] labelValues);
    ICounter WithLabels(params string[] labelValues);
}

/// <summary>
/// Gauge metric - can go up and down.
/// </summary>
public interface IGauge : IMetric
{
    void Set(double value, params string[] labelValues);
    void Increment(double value = 1.0, params string[] labelValues);
    void Decrement(double value = 1.0, params string[] labelValues);
    void SetToCurrentTime(params string[] labelValues);
    IGauge WithLabels(params string[] labelValues);
}

/// <summary>
/// Histogram metric - tracks distribution of values.
/// </summary>
public interface IHistogram : IMetric
{
    void Observe(double value, params string[] labelValues);
    IHistogram WithLabels(params string[] labelValues);
    double[] Buckets { get; }
}

/// <summary>
/// Summary metric - tracks percentiles.
/// </summary>
public interface ISummary : IMetric
{
    void Observe(double value, params string[] labelValues);
    ISummary WithLabels(params string[] labelValues);
}

/// <summary>
/// Timer metric - measures duration.
/// </summary>
public interface ITimer : IMetric
{
    IDisposable Time(params string[] labelValues);
    void Record(TimeSpan duration, params string[] labelValues);
    void Record(double milliseconds, params string[] labelValues);
}

/// <summary>
/// Metric type enumeration.
/// </summary>
public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Summary,
    Timer
}
