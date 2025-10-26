using Prometheus;

namespace Conduit.Metrics.Prometheus;

/// <summary>
/// Prometheus counter wrapper.
/// </summary>
internal class PrometheusCounter : ICounter
{
    private readonly Counter _counter;

    public PrometheusCounter(string name, string? help, string[] labelNames, Counter counter)
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
        if (labelValues.Length > 0)
            _counter.WithLabels(labelValues).Inc(value);
        else
            _counter.Inc(value);
    }

    public ICounter WithLabels(params string[] labelValues)
    {
        return new PrometheusCounter(Name, Help, LabelNames.ToArray(), _counter);
    }

    public double GetValue(params string[] labelValues)
    {
        return labelValues.Length > 0
            ? _counter.WithLabels(labelValues).Value
            : _counter.Value;
    }
}

/// <summary>
/// Prometheus gauge wrapper.
/// </summary>
internal class PrometheusGauge : IGauge
{
    private readonly Gauge _gauge;

    public PrometheusGauge(string name, string? help, string[] labelNames, Gauge gauge)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        _gauge = gauge;
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Gauge;
    public IReadOnlyList<string> LabelNames { get; }

    public void Set(double value, params string[] labelValues)
    {
        if (labelValues.Length > 0)
            _gauge.WithLabels(labelValues).Set(value);
        else
            _gauge.Set(value);
    }

    public void Increment(double value = 1.0, params string[] labelValues)
    {
        if (labelValues.Length > 0)
            _gauge.WithLabels(labelValues).Inc(value);
        else
            _gauge.Inc(value);
    }

    public void Decrement(double value = 1.0, params string[] labelValues)
    {
        if (labelValues.Length > 0)
            _gauge.WithLabels(labelValues).Dec(value);
        else
            _gauge.Dec(value);
    }

    public void SetToCurrentTime(params string[] labelValues)
    {
        if (labelValues.Length > 0)
            _gauge.WithLabels(labelValues).SetToCurrentTimeUtc();
        else
            _gauge.SetToCurrentTimeUtc();
    }

    public IGauge WithLabels(params string[] labelValues)
    {
        return new PrometheusGauge(Name, Help, LabelNames.ToArray(), _gauge);
    }

    public double GetValue(params string[] labelValues)
    {
        return labelValues.Length > 0
            ? _gauge.WithLabels(labelValues).Value
            : _gauge.Value;
    }
}

/// <summary>
/// Prometheus histogram wrapper.
/// </summary>
internal class PrometheusHistogram : IHistogram
{
    private readonly Histogram _histogram;

    public PrometheusHistogram(string name, string? help, string[] labelNames, double[] buckets, Histogram histogram)
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
        if (labelValues.Length > 0)
            _histogram.WithLabels(labelValues).Observe(value);
        else
            _histogram.Observe(value);
    }

    public IHistogram WithLabels(params string[] labelValues)
    {
        return new PrometheusHistogram(Name, Help, LabelNames.ToArray(), Buckets, _histogram);
    }

    public double GetValue(params string[] labelValues)
    {
        return labelValues.Length > 0
            ? _histogram.WithLabels(labelValues).Sum
            : _histogram.Sum;
    }
}

/// <summary>
/// Prometheus summary wrapper.
/// </summary>
internal class PrometheusSummary : ISummary
{
    private readonly Summary _summary;

    public PrometheusSummary(string name, string? help, string[] labelNames, Summary summary)
    {
        Name = name;
        Help = help;
        LabelNames = labelNames.ToList().AsReadOnly();
        _summary = summary;
    }

    public string Name { get; }
    public string? Help { get; }
    public MetricType Type => MetricType.Summary;
    public IReadOnlyList<string> LabelNames { get; }

    public void Observe(double value, params string[] labelValues)
    {
        if (labelValues.Length > 0)
            _summary.WithLabels(labelValues).Observe(value);
        else
            _summary.Observe(value);
    }

    public ISummary WithLabels(params string[] labelValues)
    {
        return new PrometheusSummary(Name, Help, LabelNames.ToArray(), _summary);
    }

    public double GetValue(params string[] labelValues)
    {
        return labelValues.Length > 0
            ? _summary.WithLabels(labelValues).Sum
            : _summary.Sum;
    }
}

/// <summary>
/// Prometheus timer wrapper using histogram.
/// </summary>
internal class PrometheusTimer : ITimer
{
    private readonly IHistogram _histogram;

    public PrometheusTimer(string name, string? help, string[] labelNames, IHistogram histogram)
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

    public double GetValue(params string[] labelValues)
    {
        return _histogram.GetValue(labelValues);
    }

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
