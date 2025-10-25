namespace Conduit.Api;

/// <summary>
/// Provides message bus functionality for sending commands, publishing events, and executing queries.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Sends a command to be processed by a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="command">The command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The command response</returns>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command without expecting a response.
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to all registered handlers.
    /// </summary>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events in order.
    /// </summary>
    /// <param name="events">The events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="query">The query to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query result</returns>
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <typeparam name="TCommand">The command type</typeparam>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="handler">The command handler</param>
    /// <returns>A subscription that can be disposed to unregister</returns>
    ISubscription Subscribe<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>;

    /// <summary>
    /// Registers an event handler.
    /// </summary>
    /// <typeparam name="TEvent">The event type</typeparam>
    /// <param name="handler">The event handler</param>
    /// <returns>A subscription that can be disposed to unregister</returns>
    ISubscription Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;

    /// <summary>
    /// Registers a query handler.
    /// </summary>
    /// <typeparam name="TQuery">The query type</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="handler">The query handler</param>
    /// <returns>A subscription that can be disposed to unregister</returns>
    ISubscription Subscribe<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Sends a message through the pipeline without a specific handler type.
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The message result if any</returns>
    Task<object?> SendAsync(IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the message pipeline for custom processing.
    /// </summary>
    IPipeline<IMessage, object?> Pipeline { get; }
}

/// <summary>
/// Represents a subscription that can be disposed to unregister a handler.
/// </summary>
public interface ISubscription : IDisposable
{
    /// <summary>
    /// Gets the subscription ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a value indicating whether the subscription is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Pauses the subscription.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the subscription.
    /// </summary>
    void Resume();
}