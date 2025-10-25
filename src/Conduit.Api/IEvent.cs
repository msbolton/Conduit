namespace Conduit.Api;

/// <summary>
/// Marker interface for event messages.
/// Events represent something that has happened in the system.
/// They are immutable facts that can have multiple handlers.
/// </summary>
public interface IEvent : IMessage
{
    /// <summary>
    /// Gets the event identifier.
    /// </summary>
    string EventId => MessageId;

    /// <summary>
    /// Gets the event name for logging and error reporting.
    /// </summary>
    string EventName => GetType().Name;

    /// <summary>
    /// Gets the aggregate ID this event relates to, if applicable.
    /// </summary>
    string? AggregateId { get; }

    /// <summary>
    /// Gets the version of the aggregate when this event was created, if applicable.
    /// </summary>
    long? AggregateVersion { get; }

    /// <summary>
    /// Gets the sequence number of this event within its stream, if applicable.
    /// </summary>
    long? SequenceNumber { get; }
}