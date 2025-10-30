namespace Conduit.Api;

/// <summary>
/// Represents the health status of a component.
/// </summary>
public class ComponentHealth
{
    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the component is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Gets the status message.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error message if unhealthy.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the exception if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets additional health data.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets the timestamp when health was checked.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a healthy status.
    /// </summary>
    public static ComponentHealth Healthy(string componentId, object? data = null)
    {
        return new ComponentHealth
        {
            ComponentId = componentId,
            IsHealthy = true,
            Status = "Healthy",
            Data = data
        };
    }

    /// <summary>
    /// Creates an unhealthy status.
    /// </summary>
    public static ComponentHealth Unhealthy(string componentId, string errorMessage, Exception? exception = null, object? data = null)
    {
        return new ComponentHealth
        {
            ComponentId = componentId,
            IsHealthy = false,
            Status = "Unhealthy",
            ErrorMessage = errorMessage,
            Exception = exception,
            Data = data
        };
    }

    /// <summary>
    /// Creates a degraded status.
    /// </summary>
    public static ComponentHealth Degraded(string componentId, string message, object? data = null)
    {
        return new ComponentHealth
        {
            ComponentId = componentId,
            IsHealthy = false,
            Status = "Degraded",
            ErrorMessage = message,
            Data = data
        };
    }
}