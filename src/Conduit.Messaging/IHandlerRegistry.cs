using System.Reflection;
using Conduit.Api;

namespace Conduit.Messaging;

/// <summary>
/// Interface for managing command and query handlers.
/// </summary>
public interface IHandlerRegistry
{
    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <param name="commandType">The command type</param>
    /// <param name="handler">The handler instance</param>
    void RegisterCommandHandler(Type commandType, object handler);

    /// <summary>
    /// Registers a command handler with generic type parameters.
    /// </summary>
    void RegisterCommandHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>;

    /// <summary>
    /// Gets a command handler for the specified command type.
    /// </summary>
    object? GetCommandHandler(Type commandType);

    /// <summary>
    /// Gets a typed command handler.
    /// </summary>
    ICommandHandler<TCommand, TResponse>? GetCommandHandler<TCommand, TResponse>()
        where TCommand : ICommand<TResponse>;

    /// <summary>
    /// Registers a query handler.
    /// </summary>
    /// <param name="queryType">The query type</param>
    /// <param name="handler">The handler instance</param>
    void RegisterQueryHandler(Type queryType, object handler);

    /// <summary>
    /// Registers a query handler with generic type parameters.
    /// </summary>
    void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Gets a query handler for the specified query type.
    /// </summary>
    object? GetQueryHandler(Type queryType);

    /// <summary>
    /// Gets a typed query handler.
    /// </summary>
    IQueryHandler<TQuery, TResult>? GetQueryHandler<TQuery, TResult>()
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Registers an event handler.
    /// </summary>
    /// <param name="eventType">The event type</param>
    /// <param name="handler">The handler instance</param>
    void RegisterEventHandler(Type eventType, object handler);

    /// <summary>
    /// Registers an event handler with generic type parameters.
    /// </summary>
    void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;

    /// <summary>
    /// Gets all event handlers for the specified event type.
    /// </summary>
    IEnumerable<object> GetEventHandlers(Type eventType);

    /// <summary>
    /// Gets typed event handlers.
    /// </summary>
    IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>()
        where TEvent : IEvent;

    /// <summary>
    /// Unregisters a command handler.
    /// </summary>
    bool UnregisterCommandHandler(Type commandType);

    /// <summary>
    /// Unregisters a query handler.
    /// </summary>
    bool UnregisterQueryHandler(Type queryType);

    /// <summary>
    /// Unregisters an event handler.
    /// </summary>
    bool UnregisterEventHandler(Type eventType, object handler);

    /// <summary>
    /// Checks if a command handler is registered.
    /// </summary>
    bool HasCommandHandler(Type commandType);

    /// <summary>
    /// Checks if a query handler is registered.
    /// </summary>
    bool HasQueryHandler(Type queryType);

    /// <summary>
    /// Checks if any event handlers are registered for the specified type.
    /// </summary>
    bool HasEventHandlers(Type eventType);

    /// <summary>
    /// Gets the total count of registered handlers.
    /// </summary>
    int GetHandlerCount();

    /// <summary>
    /// Gets statistics about registered handlers.
    /// </summary>
    HandlerStatistics GetStatistics();

    /// <summary>
    /// Clears all registered handlers.
    /// </summary>
    void Clear();

    /// <summary>
    /// Discovers and registers handlers from an assembly.
    /// </summary>
    void RegisterHandlersFromAssembly(Assembly assembly);
}