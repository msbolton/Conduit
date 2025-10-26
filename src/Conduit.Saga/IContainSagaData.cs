namespace Conduit.Saga;

/// <summary>
/// Interface for saga data persistence, inspired by NServiceBus IContainSagaData.
/// All saga data classes must implement this interface.
/// </summary>
public interface IContainSagaData
{
    /// <summary>
    /// Unique identifier for this saga instance.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// The endpoint that started this saga.
    /// </summary>
    string? Originator { get; set; }

    /// <summary>
    /// The original message ID that started this saga.
    /// </summary>
    string? OriginalMessageId { get; set; }

    /// <summary>
    /// When this saga was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this saga was last updated.
    /// </summary>
    DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Current state of the saga workflow.
    /// </summary>
    string State { get; set; }

    /// <summary>
    /// Correlation ID for tracking related messages.
    /// </summary>
    string CorrelationId { get; set; }
}

/// <summary>
/// Base class for saga data with common properties.
/// </summary>
public abstract class SagaData : IContainSagaData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Originator { get; set; }
    public string? OriginalMessageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdated { get; set; }
    public string State { get; set; } = "STARTED";
    public string CorrelationId { get; set; } = string.Empty;
}
