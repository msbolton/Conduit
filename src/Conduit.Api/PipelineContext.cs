using System.Collections.Generic;
using System.Threading;

namespace Conduit.Api;

/// <summary>
/// Represents the execution context for a pipeline.
/// </summary>
public class PipelineContext
{
    private readonly Dictionary<string, object?> _properties = new();
    private readonly Dictionary<string, object?> _items = new();

    /// <summary>
    /// Gets or sets the correlation ID for this execution.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the start time of the pipeline execution.
    /// </summary>
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the end time of the pipeline execution.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the result of the pipeline execution.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets any exception that occurred during pipeline execution.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token for the pipeline execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets a dictionary of items that can be used to share data between pipeline stages.
    /// </summary>
    public IDictionary<string, object?> Items => _items;

    /// <summary>
    /// Sets a property in the context.
    /// </summary>
    /// <param name="key">The property key</param>
    /// <param name="value">The property value</param>
    public void SetProperty(string key, object? value)
    {
        _properties[key] = value;
    }

    /// <summary>
    /// Gets a property from the context.
    /// </summary>
    /// <param name="key">The property key</param>
    /// <returns>The property value or null if not found</returns>
    public object? GetProperty(string key)
    {
        return _properties.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a typed property from the context.
    /// </summary>
    /// <typeparam name="T">The property type</typeparam>
    /// <param name="key">The property key</param>
    /// <returns>The property value or default if not found</returns>
    public T? GetProperty<T>(string key)
    {
        var value = GetProperty(key);
        return value is T typed ? typed : default;
    }

    /// <summary>
    /// Checks if a property exists in the context.
    /// </summary>
    /// <param name="key">The property key</param>
    /// <returns>True if the property exists, false otherwise</returns>
    public bool HasProperty(string key)
    {
        return _properties.ContainsKey(key);
    }

    /// <summary>
    /// Creates a copy of this context.
    /// </summary>
    /// <returns>A new context with copied properties</returns>
    public PipelineContext Copy()
    {
        var copy = new PipelineContext
        {
            CorrelationId = CorrelationId,
            ExecutionId = Guid.NewGuid().ToString()
        };

        foreach (var kvp in _properties)
        {
            copy._properties[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in _items)
        {
            copy._items[kvp.Key] = kvp.Value;
        }

        return copy;
    }
}