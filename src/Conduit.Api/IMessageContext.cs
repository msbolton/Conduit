namespace Conduit.Api;

/// <summary>
/// Provides context for message processing in the pipeline.
/// </summary>
public interface IMessageContext
{
    /// <summary>
    /// Gets the message being processed.
    /// </summary>
    IMessage Message { get; }

    /// <summary>
    /// Gets the message envelope containing metadata.
    /// </summary>
    MessageEnvelope Envelope { get; }

    /// <summary>
    /// Gets the security context for this message.
    /// </summary>
    ISecurityContext? SecurityContext { get; }

    /// <summary>
    /// Gets the cancellation token for this operation.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets or sets custom items for the context.
    /// </summary>
    IDictionary<string, object> Items { get; }

    /// <summary>
    /// Gets the response if this is a request-response operation.
    /// </summary>
    object? Response { get; set; }

    /// <summary>
    /// Gets a value indicating whether the message has been handled.
    /// </summary>
    bool IsHandled { get; set; }

    /// <summary>
    /// Gets a value indicating whether the message processing has failed.
    /// </summary>
    bool HasFailed { get; }

    /// <summary>
    /// Gets the exception if the message processing has failed.
    /// </summary>
    Exception? Exception { get; set; }

    /// <summary>
    /// Gets the retry count for this message.
    /// </summary>
    int RetryCount { get; set; }

    /// <summary>
    /// Gets the timestamp when processing started.
    /// </summary>
    DateTimeOffset ProcessingStarted { get; }

    /// <summary>
    /// Gets the processing duration.
    /// </summary>
    TimeSpan ProcessingDuration { get; }

    /// <summary>
    /// Gets a value from the context items.
    /// </summary>
    T? GetItem<T>(string key) where T : class;

    /// <summary>
    /// Sets a value in the context items.
    /// </summary>
    void SetItem<T>(string key, T value) where T : class;

    /// <summary>
    /// Marks the message as handled with a response.
    /// </summary>
    void Complete(object? response = null);

    /// <summary>
    /// Marks the message as failed with an exception.
    /// </summary>
    void Fail(Exception exception);

    /// <summary>
    /// Creates a child context for nested operations.
    /// </summary>
    IMessageContext CreateChildContext(IMessage message);
}

/// <summary>
/// Envelope containing message metadata and routing information.
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation ID.
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the message timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the source component ID.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the destination component ID or routing key.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for request-response patterns.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the message priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the message TTL in milliseconds.
    /// </summary>
    public long Ttl { get; set; } = -1;

    /// <summary>
    /// Gets or sets additional headers.
    /// </summary>
    public Dictionary<string, object> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content encoding.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the message version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets routing tags for content-based routing.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();
}