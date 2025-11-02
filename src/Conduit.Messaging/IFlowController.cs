namespace Conduit.Messaging;

/// <summary>
/// Interface for controlling message flow and providing backpressure management.
/// </summary>
public interface IFlowController : IDisposable
{
    /// <summary>
    /// Event raised when backpressure is applied.
    /// </summary>
    event EventHandler<BackpressureEventArgs>? BackpressureApplied;

    /// <summary>
    /// Event raised when flow is restored.
    /// </summary>
    event EventHandler? FlowRestored;

    /// <summary>
    /// Gets whether the controller is healthy.
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    int QueueDepth { get; }

    /// <summary>
    /// Gets the current load percentage.
    /// </summary>
    double LoadPercentage { get; }

    /// <summary>
    /// Gets whether backpressure is currently active.
    /// </summary>
    bool IsBackpressureActive { get; }

    /// <summary>
    /// Executes a function with flow control.
    /// </summary>
    Task<T> ExecuteWithFlowControlAsync<T>(
        Func<Task<T>> operation,
        Priority priority = Priority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with flow control.
    /// </summary>
    Task ExecuteWithFlowControlAsync(
        Func<Task> operation,
        Priority priority = Priority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies throttling for the specified duration.
    /// </summary>
    Task ThrottleAsync(TimeSpan delay);

    /// <summary>
    /// Gets flow control statistics.
    /// </summary>
    FlowControlStatistics GetStatistics();

    /// <summary>
    /// Adjusts the rate limit.
    /// </summary>
    void AdjustRateLimit(int newRateLimit);

    /// <summary>
    /// Pauses flow control.
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes flow control.
    /// </summary>
    void Resume();
}