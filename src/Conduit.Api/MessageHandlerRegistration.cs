namespace Conduit.Api;

/// <summary>
/// Represents a message handler registration from a component.
/// </summary>
public class MessageHandlerRegistration
{
    /// <summary>
    /// Gets or sets the message type this handler processes.
    /// </summary>
    public Type MessageType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the handler type.
    /// </summary>
    public Type HandlerType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the handler instance if pre-created.
    /// </summary>
    public object? HandlerInstance { get; set; }

    /// <summary>
    /// Gets or sets the handler factory if lazy creation is needed.
    /// </summary>
    public Func<IServiceProvider, object>? HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the handler method name if using reflection.
    /// </summary>
    public string HandlerMethodName { get; set; } = "HandleAsync";

    /// <summary>
    /// Gets or sets the handler priority for ordering multiple handlers.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this handler is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the handler category (Command, Event, Query).
    /// </summary>
    public MessageHandlerCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the maximum concurrent executions for this handler.
    /// </summary>
    public int MaxConcurrency { get; set; } = -1; // -1 means unlimited

    /// <summary>
    /// Gets or sets the timeout for handler execution.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets retry configuration for this handler.
    /// </summary>
    public RetryConfiguration? RetryConfiguration { get; set; }

    /// <summary>
    /// Gets or sets filter expressions for conditional handling.
    /// </summary>
    public Func<IMessage, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets tags for handler categorization.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a command handler registration.
    /// </summary>
    public static MessageHandlerRegistration ForCommand<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : ICommandHandler<TCommand>
    {
        return new MessageHandlerRegistration
        {
            MessageType = typeof(TCommand),
            HandlerType = typeof(THandler),
            Category = MessageHandlerCategory.Command
        };
    }

    /// <summary>
    /// Creates an event handler registration.
    /// </summary>
    public static MessageHandlerRegistration ForEvent<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        return new MessageHandlerRegistration
        {
            MessageType = typeof(TEvent),
            HandlerType = typeof(THandler),
            Category = MessageHandlerCategory.Event
        };
    }

    /// <summary>
    /// Creates a query handler registration.
    /// </summary>
    public static MessageHandlerRegistration ForQuery<TQuery, TResult, THandler>()
        where TQuery : IQuery<TResult>
        where THandler : IQueryHandler<TQuery, TResult>
    {
        return new MessageHandlerRegistration
        {
            MessageType = typeof(TQuery),
            HandlerType = typeof(THandler),
            Category = MessageHandlerCategory.Query
        };
    }
}

/// <summary>
/// Defines the category of message handler.
/// </summary>
public enum MessageHandlerCategory
{
    /// <summary>
    /// Command handler.
    /// </summary>
    Command,

    /// <summary>
    /// Event handler.
    /// </summary>
    Event,

    /// <summary>
    /// Query handler.
    /// </summary>
    Query,

    /// <summary>
    /// Generic message handler.
    /// </summary>
    Message
}

/// <summary>
/// Configuration for retry logic.
/// </summary>
public class RetryConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retries.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the backoff multiplier.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the retry strategy.
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.Exponential;

    /// <summary>
    /// Gets or sets exception types that should trigger a retry.
    /// </summary>
    public HashSet<Type> RetryableExceptions { get; set; } = new();

    /// <summary>
    /// Gets or sets exception types that should not trigger a retry.
    /// </summary>
    public HashSet<Type> NonRetryableExceptions { get; set; } = new();
}

/// <summary>
/// Defines retry strategies.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// Exponentially increasing delay.
    /// </summary>
    Exponential,

    /// <summary>
    /// Linearly increasing delay.
    /// </summary>
    Linear,

    /// <summary>
    /// Random jitter added to delays.
    /// </summary>
    Jitter
}