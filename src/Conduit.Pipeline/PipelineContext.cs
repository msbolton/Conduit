using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Conduit.Pipeline;

/// <summary>
/// Represents the context for pipeline execution, carrying metadata and state
/// through the pipeline stages.
/// </summary>
public class PipelineContext
{
    private readonly ConcurrentDictionary<string, object> _properties = new();
    private readonly Stopwatch _stopwatch;
    private volatile bool _cancelled;
    private int _lastStageIndex = -1;

    /// <summary>
    /// Initializes a new instance of the PipelineContext class.
    /// </summary>
    public PipelineContext() : this(Guid.NewGuid().ToString())
    {
    }

    /// <summary>
    /// Initializes a new instance of the PipelineContext class with a specific ID.
    /// </summary>
    public PipelineContext(string contextId)
    {
        ContextId = contextId ?? throw new ArgumentNullException(nameof(contextId));
        CreatedAt = DateTimeOffset.UtcNow;
        _stopwatch = Stopwatch.StartNew();
        CancellationToken = CancellationToken.None;
    }

    /// <summary>
    /// Gets the unique context identifier.
    /// </summary>
    public string ContextId { get; }

    /// <summary>
    /// Gets the timestamp when this context was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets the pipeline ID this context belongs to.
    /// </summary>
    public string? PipelineId { get; set; }

    /// <summary>
    /// Gets or sets the pipeline name.
    /// </summary>
    public string? PipelineName { get; set; }

    /// <summary>
    /// Gets or sets the input to the pipeline.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets or sets the result of the pipeline.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the current stage name being executed.
    /// </summary>
    public string? CurrentStage { get; set; }

    /// <summary>
    /// Gets the last completed stage index.
    /// </summary>
    public int LastStageIndex => _lastStageIndex;

    /// <summary>
    /// Gets or sets the start time of pipeline execution.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of pipeline execution.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token for the pipeline execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets whether the pipeline execution has been cancelled.
    /// </summary>
    public bool IsCancelled => _cancelled || CancellationToken.IsCancellationRequested;

    /// <summary>
    /// Gets or sets the exception that occurred during execution, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets whether the pipeline execution resulted in an error.
    /// </summary>
    public bool HasError => Exception != null;

    /// <summary>
    /// Gets the properties dictionary for storing custom data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties => _properties;

    /// <summary>
    /// Sets a property value.
    /// </summary>
    public void SetProperty(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or empty", nameof(key));

        _properties[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a property value.
    /// </summary>
    public object? GetProperty(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or empty", nameof(key));

        return _properties.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a typed property value.
    /// </summary>
    public T? GetProperty<T>(string key) where T : class
    {
        var value = GetProperty(key);
        return value as T;
    }

    /// <summary>
    /// Gets a value property with a specific type.
    /// </summary>
    public T GetValueProperty<T>(string key, T defaultValue = default) where T : struct
    {
        var value = GetProperty(key);
        return value is T typedValue ? typedValue : defaultValue;
    }

    /// <summary>
    /// Checks if a property exists.
    /// </summary>
    public bool HasProperty(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _properties.ContainsKey(key);
    }

    /// <summary>
    /// Removes a property.
    /// </summary>
    public bool RemoveProperty(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _properties.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all properties.
    /// </summary>
    public void ClearProperties()
    {
        _properties.Clear();
    }

    /// <summary>
    /// Cancels the pipeline execution.
    /// </summary>
    public void Cancel()
    {
        _cancelled = true;
    }

    /// <summary>
    /// Gets the elapsed time since context creation.
    /// </summary>
    public TimeSpan GetElapsedTime()
    {
        return _stopwatch.Elapsed;
    }

    /// <summary>
    /// Gets the execution duration if both start and end times are set.
    /// </summary>
    public TimeSpan? GetExecutionDuration()
    {
        if (StartTime.HasValue && EndTime.HasValue)
        {
            return EndTime.Value - StartTime.Value;
        }
        else if (StartTime.HasValue)
        {
            return DateTimeOffset.UtcNow - StartTime.Value;
        }
        return null;
    }

    /// <summary>
    /// Marks a stage as completed.
    /// </summary>
    public void MarkStageCompleted(int stageIndex)
    {
        _lastStageIndex = stageIndex;
    }

    /// <summary>
    /// Creates a shallow copy of this context for parallel execution.
    /// </summary>
    public PipelineContext Copy()
    {
        var copy = new PipelineContext(Guid.NewGuid().ToString())
        {
            PipelineId = PipelineId,
            PipelineName = PipelineName,
            Input = Input,
            CancellationToken = CancellationToken
        };

        // Copy properties
        foreach (var kvp in _properties)
        {
            copy._properties[kvp.Key] = kvp.Value;
        }

        return copy;
    }

    /// <summary>
    /// Creates a child context for nested pipeline execution.
    /// </summary>
    public PipelineContext CreateChildContext()
    {
        var child = new PipelineContext(Guid.NewGuid().ToString())
        {
            CancellationToken = CancellationToken
        };

        // Set parent reference
        child.SetProperty("ParentContextId", ContextId);
        child.SetProperty("ParentPipelineId", PipelineId ?? string.Empty);

        // Copy selected properties that should be inherited
        if (HasProperty("CorrelationId"))
        {
            child.SetProperty("CorrelationId", GetProperty("CorrelationId")!);
        }

        if (HasProperty("UserId"))
        {
            child.SetProperty("UserId", GetProperty("UserId")!);
        }

        if (HasProperty("TenantId"))
        {
            child.SetProperty("TenantId", GetProperty("TenantId")!);
        }

        return child;
    }

    /// <summary>
    /// Merges properties from another context into this one.
    /// </summary>
    public void MergeFrom(PipelineContext other, bool overwrite = false)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        foreach (var kvp in other._properties)
        {
            if (overwrite || !_properties.ContainsKey(kvp.Key))
            {
                _properties[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Creates a pipeline context with common properties.
    /// </summary>
    public static PipelineContext CreateWithCorrelation(string correlationId)
    {
        var context = new PipelineContext();
        context.SetProperty("CorrelationId", correlationId);
        context.SetProperty("Timestamp", DateTimeOffset.UtcNow);
        return context;
    }

    /// <summary>
    /// Creates a pipeline context for a specific user.
    /// </summary>
    public static PipelineContext CreateForUser(string userId, string? tenantId = null)
    {
        var context = new PipelineContext();
        context.SetProperty("UserId", userId);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            context.SetProperty("TenantId", tenantId);
        }

        context.SetProperty("Timestamp", DateTimeOffset.UtcNow);
        return context;
    }

    /// <summary>
    /// Returns a string representation of the context.
    /// </summary>
    public override string ToString()
    {
        return $"PipelineContext[Id={ContextId}, Pipeline={PipelineName ?? "Unknown"}, Stage={CurrentStage ?? "None"}, Elapsed={GetElapsedTime().TotalMilliseconds}ms]";
    }
}