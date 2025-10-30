using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Conduit.Common;

namespace Conduit.Core.Behaviors
{
    /// <summary>
    /// Context for pipeline processing that carries data and metadata through the behavior chain.
    /// </summary>
    public class PipelineContext
    {
        private readonly ConcurrentDictionary<string, object?> _properties;
        private readonly Stopwatch _stopwatch;
        private volatile bool _cancelled;

        /// <summary>
        /// Gets the unique identifier for this context.
        /// </summary>
        public Guid ContextId { get; }

        /// <summary>
        /// Gets the creation timestamp of this context.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets or sets the pipeline identifier.
        /// </summary>
        public string? PipelineId { get; set; }

        /// <summary>
        /// Gets or sets the input data for the pipeline.
        /// </summary>
        public object? Input { get; set; }

        /// <summary>
        /// Gets or sets the result of the pipeline execution.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Gets the start time of pipeline execution.
        /// </summary>
        public DateTimeOffset? StartTime { get; private set; }

        /// <summary>
        /// Gets the end time of pipeline execution.
        /// </summary>
        public DateTimeOffset? EndTime { get; private set; }

        /// <summary>
        /// Gets or sets the last stage index in the pipeline.
        /// </summary>
        public int LastStageIndex { get; set; }

        /// <summary>
        /// Gets a value indicating whether the pipeline has been cancelled.
        /// </summary>
        public bool IsCancelled => _cancelled;

        /// <summary>
        /// Gets the properties collection for storing custom data.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Properties => _properties;

        /// <summary>
        /// Initializes a new instance of the PipelineContext class.
        /// </summary>
        public PipelineContext()
        {
            ContextId = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
            _properties = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Initializes a new instance of the PipelineContext class with input data.
        /// </summary>
        /// <param name="input">The input data</param>
        public PipelineContext(object? input) : this()
        {
            Input = input;
        }

        /// <summary>
        /// Sets a property value.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <param name="value">The property value</param>
        public void SetProperty(string key, object? value)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            _properties[key] = value;
        }

        /// <summary>
        /// Gets a property value.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <returns>The property value, or null if not found</returns>
        public object? GetProperty(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            return _properties.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Gets a typed property value.
        /// </summary>
        /// <typeparam name="T">The property type</typeparam>
        /// <param name="key">The property key</param>
        /// <returns>The property value if found and of correct type</returns>
        public T? GetProperty<T>(string key) where T : class
        {
            var value = GetProperty(key);
            return value as T;
        }

        /// <summary>
        /// Gets a typed property value or default.
        /// </summary>
        /// <typeparam name="T">The property type</typeparam>
        /// <param name="key">The property key</param>
        /// <param name="defaultValue">The default value if not found</param>
        /// <returns>The property value or default</returns>
        public T GetPropertyOrDefault<T>(string key, T defaultValue = default!)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            if (_properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Checks if a property exists.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <returns>True if the property exists</returns>
        public bool HasProperty(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            return _properties.ContainsKey(key);
        }

        /// <summary>
        /// Removes a property.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <returns>True if the property was removed</returns>
        public bool RemoveProperty(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));
            return _properties.TryRemove(key, out _);
        }

        /// <summary>
        /// Sets an attribute (alias for SetProperty).
        /// </summary>
        /// <param name="key">The attribute key</param>
        /// <param name="value">The attribute value</param>
        public void SetAttribute(string key, object? value) => SetProperty(key, value);

        /// <summary>
        /// Gets an attribute (alias for GetProperty).
        /// </summary>
        /// <param name="key">The attribute key</param>
        /// <returns>The attribute value</returns>
        public object? GetAttribute(string key) => GetProperty(key);

        /// <summary>
        /// Checks if an attribute exists (alias for HasProperty).
        /// </summary>
        /// <param name="key">The attribute key</param>
        /// <returns>True if the attribute exists</returns>
        public bool HasAttribute(string key) => HasProperty(key);

        /// <summary>
        /// Cancels the pipeline execution.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
            SetProperty("CancelledAt", DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Marks the start of pipeline execution.
        /// </summary>
        public void MarkStart()
        {
            StartTime = DateTimeOffset.UtcNow;
            _stopwatch.Start();
        }

        /// <summary>
        /// Marks the end of pipeline execution.
        /// </summary>
        public void MarkEnd()
        {
            EndTime = DateTimeOffset.UtcNow;
            _stopwatch.Stop();
        }

        /// <summary>
        /// Gets the elapsed time of pipeline execution.
        /// </summary>
        /// <returns>The elapsed time, or null if not started</returns>
        public TimeSpan? GetElapsedTime()
        {
            if (!_stopwatch.IsRunning && _stopwatch.Elapsed == TimeSpan.Zero)
            {
                return null;
            }

            return _stopwatch.Elapsed;
        }

        /// <summary>
        /// Creates a copy of this context.
        /// </summary>
        /// <returns>A new context with copied data</returns>
        public PipelineContext Copy()
        {
            var copy = new PipelineContext(Input)
            {
                PipelineId = PipelineId,
                Result = Result,
                LastStageIndex = LastStageIndex
            };

            foreach (var kvp in _properties)
            {
                copy.SetProperty(kvp.Key, kvp.Value);
            }

            if (_cancelled)
            {
                copy.Cancel();
            }

            return copy;
        }

        /// <summary>
        /// Merges properties from another context into this one.
        /// </summary>
        /// <param name="other">The context to merge from</param>
        public void MergeFrom(PipelineContext other)
        {
            Guard.NotNull(other, nameof(other));

            foreach (var kvp in other._properties)
            {
                SetProperty(kvp.Key, kvp.Value);
            }

            if (other._cancelled)
            {
                Cancel();
            }
        }

        /// <summary>
        /// Gets a summary of the context state.
        /// </summary>
        /// <returns>A string summary of the context</returns>
        public override string ToString()
        {
            var status = IsCancelled ? "Cancelled" :
                         EndTime.HasValue ? "Completed" :
                         StartTime.HasValue ? "Running" : "Pending";

            var elapsed = GetElapsedTime();
            var elapsedStr = elapsed.HasValue ? $", Elapsed: {elapsed.Value.TotalMilliseconds:F2}ms" : "";

            return $"PipelineContext[Id: {ContextId:N}, Status: {status}, Properties: {_properties.Count}{elapsedStr}]";
        }
    }
}