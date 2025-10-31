using Conduit.Api;

namespace Conduit.Messaging;

/// <summary>
/// Interface for managing dead letter messages - messages that failed processing.
/// </summary>
public interface IDeadLetterQueue : IDisposable
{
    /// <summary>
    /// Event fired when a message is added to the dead letter queue.
    /// </summary>
    event EventHandler<DeadLetterEventArgs>? MessageAdded;

    /// <summary>
    /// Event fired when the queue reaches capacity.
    /// </summary>
    event EventHandler<QueueCapacityEventArgs>? CapacityReached;

    /// <summary>
    /// Gets the current number of messages in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets whether the queue is at capacity.
    /// </summary>
    bool IsAtCapacity { get; }

    /// <summary>
    /// Gets the total number of messages ever enqueued.
    /// </summary>
    long TotalEnqueued { get; }

    /// <summary>
    /// Gets the total number of messages ever dequeued.
    /// </summary>
    long TotalDequeued { get; }

    /// <summary>
    /// Gets the total number of messages ever reprocessed.
    /// </summary>
    long TotalReprocessed { get; }

    /// <summary>
    /// Gets the total number of messages that expired.
    /// </summary>
    long TotalExpired { get; }

    /// <summary>
    /// Adds a failed message to the dead letter queue.
    /// </summary>
    Task<DeadLetterEntry> AddAsync(
        IMessage message,
        Exception exception,
        MessageContext? context = null);

    /// <summary>
    /// Gets a message by ID.
    /// </summary>
    DeadLetterEntry? GetById(string id);

    /// <summary>
    /// Gets messages based on filters.
    /// </summary>
    IEnumerable<DeadLetterEntry> GetMessages(
        int? limit = null,
        Func<DeadLetterEntry, bool>? filter = null);

    /// <summary>
    /// Gets messages by correlation ID.
    /// </summary>
    IEnumerable<DeadLetterEntry> GetByCorrelationId(string correlationId);

    /// <summary>
    /// Gets messages by message type.
    /// </summary>
    IEnumerable<DeadLetterEntry> GetByMessageType(string messageType);
}