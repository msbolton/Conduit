namespace Conduit.Api;

/// <summary>
/// Provides metrics collection functionality for monitoring and observability.
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Records a counter metric.
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="value">The counter value to add</param>
    /// <param name="tags">Optional tags for the metric</param>
    void RecordCounter(string name, long value = 1, params (string Key, string Value)[] tags);

    /// <summary>
    /// Records a gauge metric (point-in-time value).
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="value">The gauge value</param>
    /// <param name="tags">Optional tags for the metric</param>
    void RecordGauge(string name, double value, params (string Key, string Value)[] tags);

    /// <summary>
    /// Records a histogram metric (distribution of values).
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="value">The value to record</param>
    /// <param name="tags">Optional tags for the metric</param>
    void RecordHistogram(string name, double value, params (string Key, string Value)[] tags);

    /// <summary>
    /// Records a timer metric (duration).
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="duration">The duration to record</param>
    /// <param name="tags">Optional tags for the metric</param>
    void RecordTimer(string name, TimeSpan duration, params (string Key, string Value)[] tags);

    /// <summary>
    /// Starts a timer for measuring duration.
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="tags">Optional tags for the metric</param>
    /// <returns>A timer instance that records when disposed</returns>
    ITimer StartTimer(string name, params (string Key, string Value)[] tags);

    /// <summary>
    /// Records command execution metrics.
    /// </summary>
    /// <param name="commandType">The command type</param>
    /// <param name="success">Whether execution was successful</param>
    /// <param name="duration">The execution duration</param>
    void RecordCommandExecution(string commandType, bool success, TimeSpan duration);

    /// <summary>
    /// Records event publication metrics.
    /// </summary>
    /// <param name="eventType">The event type</param>
    /// <param name="handlerCount">Number of handlers that processed the event</param>
    void RecordEventPublication(string eventType, int handlerCount);

    /// <summary>
    /// Records query execution metrics.
    /// </summary>
    /// <param name="queryType">The query type</param>
    /// <param name="success">Whether execution was successful</param>
    /// <param name="duration">The execution duration</param>
    /// <param name="cacheHit">Whether the result was from cache</param>
    void RecordQueryExecution(string queryType, bool success, TimeSpan duration, bool cacheHit = false);

    /// <summary>
    /// Records component lifecycle metrics.
    /// </summary>
    /// <param name="componentId">The component ID</param>
    /// <param name="action">The lifecycle action (attach, detach, etc.)</param>
    /// <param name="success">Whether the action was successful</param>
    void RecordComponentLifecycle(string componentId, string action, bool success);

    /// <summary>
    /// Records pipeline behavior execution metrics.
    /// </summary>
    /// <param name="behaviorId">The behavior ID</param>
    /// <param name="duration">The execution duration</param>
    /// <param name="success">Whether execution was successful</param>
    void RecordBehaviorExecution(string behaviorId, TimeSpan duration, bool success);

    /// <summary>
    /// Records message processing errors.
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <param name="errorType">The error type</param>
    void RecordError(string messageType, string errorType);

    /// <summary>
    /// Records dead letter queue metrics.
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <param name="reason">The reason for dead lettering</param>
    void RecordDeadLetter(string messageType, string reason);

    /// <summary>
    /// Records retry metrics.
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <param name="attemptNumber">The retry attempt number</param>
    /// <param name="success">Whether the retry was successful</param>
    void RecordRetry(string messageType, int attemptNumber, bool success);

    /// <summary>
    /// Creates a child metrics collector with additional tags.
    /// </summary>
    /// <param name="additionalTags">Additional tags to apply to all metrics</param>
    /// <returns>A new metrics collector with additional tags</returns>
    IMetricsCollector WithTags(params (string Key, string Value)[] additionalTags);
}

/// <summary>
/// Represents a timer for measuring duration.
/// </summary>
public interface ITimer : IDisposable
{
    /// <summary>
    /// Gets the elapsed time since the timer was started.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Stops the timer and records the metric.
    /// </summary>
    void Stop();

    /// <summary>
    /// Records a checkpoint without stopping the timer.
    /// </summary>
    /// <param name="checkpointName">The checkpoint name</param>
    void Checkpoint(string checkpointName);
}