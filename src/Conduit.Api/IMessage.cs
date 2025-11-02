namespace Conduit.Api;

/// <summary>
/// Base interface for all messages in the system.
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Gets the unique message ID.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Gets the correlation ID for tracking related messages.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the causation ID for tracking message causality.
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Gets the message timestamp.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Gets the message headers.
    /// </summary>
    IReadOnlyDictionary<string, object> Headers { get; }

    /// <summary>
    /// Gets a header value.
    /// </summary>
    /// <param name="key">The header key</param>
    /// <returns>The header value, or null if not found</returns>
    object? GetHeader(string key);

    /// <summary>
    /// Gets a typed header value.
    /// </summary>
    /// <typeparam name="T">The expected type</typeparam>
    /// <param name="key">The header key</param>
    /// <returns>The typed header value, or default if not found or wrong type</returns>
    T? GetHeader<T>(string key) where T : class;

    /// <summary>
    /// Gets the message source.
    /// </summary>
    string? Source { get; }

    /// <summary>
    /// Gets the message destination.
    /// </summary>
    string? Destination { get; }

    /// <summary>
    /// Gets the message priority.
    /// </summary>
    /// <value>Higher values indicate higher priority</value>
    int Priority { get; }

    /// <summary>
    /// Gets a value indicating whether this is a system message.
    /// </summary>
    bool IsSystemMessage { get; }

    /// <summary>
    /// Gets the message TTL (time to live) in milliseconds.
    /// </summary>
    /// <value>-1 for no expiration</value>
    long Ttl { get; }

    /// <summary>
    /// Gets a value indicating whether the message has expired.
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// Gets the message payload.
    /// </summary>
    object? Payload { get; }
}