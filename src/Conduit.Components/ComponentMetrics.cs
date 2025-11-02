using System;
using System.Collections.Generic;

namespace Conduit.Components
{
    /// <summary>
    /// Represents metrics collected from a component.
    /// </summary>
    public class ComponentMetrics
    {
        /// <summary>
        /// Gets the component ID.
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// Gets the collection timestamp.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the metric values.
        /// </summary>
        public Dictionary<string, object> Values { get; }

        /// <summary>
        /// Gets the counters.
        /// </summary>
        public Dictionary<string, long> Counters { get; }

        /// <summary>
        /// Gets the gauges.
        /// </summary>
        public Dictionary<string, double> Gauges { get; }

        /// <summary>
        /// Initializes a new instance of ComponentMetrics.
        /// </summary>
        public ComponentMetrics(string componentId)
        {
            ComponentId = componentId;
            Timestamp = DateTimeOffset.UtcNow;
            Values = new Dictionary<string, object>();
            Counters = new Dictionary<string, long>();
            Gauges = new Dictionary<string, double>();
        }

        /// <summary>
        /// Sets a metric value.
        /// </summary>
        public ComponentMetrics SetValue(string name, object value)
        {
            Values[name] = value;
            return this;
        }

        /// <summary>
        /// Sets a counter value.
        /// </summary>
        public ComponentMetrics SetCounter(string name, long value)
        {
            Counters[name] = value;
            return this;
        }

        /// <summary>
        /// Increments a counter.
        /// </summary>
        public ComponentMetrics IncrementCounter(string name, long increment = 1)
        {
            Counters[name] = Counters.GetValueOrDefault(name, 0) + increment;
            return this;
        }

        /// <summary>
        /// Sets a gauge value.
        /// </summary>
        public ComponentMetrics SetGauge(string name, double value)
        {
            Gauges[name] = value;
            return this;
        }

        /// <summary>
        /// Gets a metric value.
        /// </summary>
        public T? GetValue<T>(string name)
        {
            return Values.TryGetValue(name, out var value) && value is T typed ? typed : default;
        }

        /// <summary>
        /// Gets a counter value.
        /// </summary>
        public long GetCounter(string name)
        {
            return Counters.GetValueOrDefault(name, 0);
        }

        /// <summary>
        /// Gets a gauge value.
        /// </summary>
        public double GetGauge(string name)
        {
            return Gauges.GetValueOrDefault(name, 0.0);
        }
    }
}