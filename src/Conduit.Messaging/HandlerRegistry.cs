using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Messaging
{
    /// <summary>
    /// Registry for managing command and query handlers.
    /// </summary>
    public class HandlerRegistry
    {
        private readonly ConcurrentDictionary<Type, object> _commandHandlers;
        private readonly ConcurrentDictionary<Type, object> _queryHandlers;
        private readonly ConcurrentDictionary<Type, List<object>> _eventHandlers;

        /// <summary>
        /// Initializes a new instance of the HandlerRegistry class.
        /// </summary>
        public HandlerRegistry()
        {
            _commandHandlers = new ConcurrentDictionary<Type, object>();
            _queryHandlers = new ConcurrentDictionary<Type, object>();
            _eventHandlers = new ConcurrentDictionary<Type, List<object>>();
        }

        /// <summary>
        /// Registers a command handler.
        /// </summary>
        /// <param name="commandType">The command type</param>
        /// <param name="handler">The handler instance</param>
        public void RegisterCommandHandler(Type commandType, object handler)
        {
            Guard.AgainstNull(commandType, nameof(commandType));
            Guard.AgainstNull(handler, nameof(handler));

            if (_commandHandlers.ContainsKey(commandType))
            {
                throw new HandlerAlreadyRegisteredException(
                    $"A handler for command type {commandType.Name} is already registered. " +
                    "Commands should have exactly one handler.");
            }

            if (!_commandHandlers.TryAdd(commandType, handler))
            {
                throw new InvalidOperationException($"Failed to register handler for command type {commandType.Name}");
            }
        }

        /// <summary>
        /// Registers a command handler with generic type parameters.
        /// </summary>
        public void RegisterCommandHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
            where TCommand : ICommand<TResponse>
        {
            RegisterCommandHandler(typeof(TCommand), handler);
        }

        /// <summary>
        /// Gets a command handler for the specified command type.
        /// </summary>
        public object? GetCommandHandler(Type commandType)
        {
            Guard.AgainstNull(commandType, nameof(commandType));
            return _commandHandlers.TryGetValue(commandType, out var handler) ? handler : null;
        }

        /// <summary>
        /// Gets a typed command handler.
        /// </summary>
        public ICommandHandler<TCommand, TResponse>? GetCommandHandler<TCommand, TResponse>()
            where TCommand : ICommand<TResponse>
        {
            return GetCommandHandler(typeof(TCommand)) as ICommandHandler<TCommand, TResponse>;
        }

        /// <summary>
        /// Registers a query handler.
        /// </summary>
        /// <param name="queryType">The query type</param>
        /// <param name="handler">The handler instance</param>
        public void RegisterQueryHandler(Type queryType, object handler)
        {
            Guard.AgainstNull(queryType, nameof(queryType));
            Guard.AgainstNull(handler, nameof(handler));

            if (_queryHandlers.ContainsKey(queryType))
            {
                throw new HandlerAlreadyRegisteredException(
                    $"A handler for query type {queryType.Name} is already registered. " +
                    "Queries should have exactly one handler.");
            }

            if (!_queryHandlers.TryAdd(queryType, handler))
            {
                throw new InvalidOperationException($"Failed to register handler for query type {queryType.Name}");
            }
        }

        /// <summary>
        /// Registers a query handler with generic type parameters.
        /// </summary>
        public void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : IQuery<TResult>
        {
            RegisterQueryHandler(typeof(TQuery), handler);
        }

        /// <summary>
        /// Gets a query handler for the specified query type.
        /// </summary>
        public object? GetQueryHandler(Type queryType)
        {
            Guard.AgainstNull(queryType, nameof(queryType));
            return _queryHandlers.TryGetValue(queryType, out var handler) ? handler : null;
        }

        /// <summary>
        /// Gets a typed query handler.
        /// </summary>
        public IQueryHandler<TQuery, TResult>? GetQueryHandler<TQuery, TResult>()
            where TQuery : IQuery<TResult>
        {
            return GetQueryHandler(typeof(TQuery)) as IQueryHandler<TQuery, TResult>;
        }

        /// <summary>
        /// Registers an event handler.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="handler">The handler instance</param>
        public void RegisterEventHandler(Type eventType, object handler)
        {
            Guard.AgainstNull(eventType, nameof(eventType));
            Guard.AgainstNull(handler, nameof(handler));

            _eventHandlers.AddOrUpdate(
                eventType,
                new List<object> { handler },
                (_, handlers) =>
                {
                    handlers.Add(handler);
                    return handlers;
                });
        }

        /// <summary>
        /// Registers an event handler with generic type parameters.
        /// </summary>
        public void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent
        {
            RegisterEventHandler(typeof(TEvent), handler);
        }

        /// <summary>
        /// Gets all event handlers for the specified event type.
        /// </summary>
        public IEnumerable<object> GetEventHandlers(Type eventType)
        {
            Guard.AgainstNull(eventType, nameof(eventType));

            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                return handlers.ToList(); // Return a copy to avoid concurrent modification
            }

            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Gets typed event handlers.
        /// </summary>
        public IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>()
            where TEvent : IEvent
        {
            return GetEventHandlers(typeof(TEvent)).Cast<IEventHandler<TEvent>>();
        }

        /// <summary>
        /// Unregisters a command handler.
        /// </summary>
        public bool UnregisterCommandHandler(Type commandType)
        {
            Guard.AgainstNull(commandType, nameof(commandType));
            return _commandHandlers.TryRemove(commandType, out _);
        }

        /// <summary>
        /// Unregisters a query handler.
        /// </summary>
        public bool UnregisterQueryHandler(Type queryType)
        {
            Guard.AgainstNull(queryType, nameof(queryType));
            return _queryHandlers.TryRemove(queryType, out _);
        }

        /// <summary>
        /// Unregisters an event handler.
        /// </summary>
        public bool UnregisterEventHandler(Type eventType, object handler)
        {
            Guard.AgainstNull(eventType, nameof(eventType));
            Guard.AgainstNull(handler, nameof(handler));

            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                return handlers.Remove(handler);
            }

            return false;
        }

        /// <summary>
        /// Checks if a command handler is registered.
        /// </summary>
        public bool HasCommandHandler(Type commandType)
        {
            Guard.AgainstNull(commandType, nameof(commandType));
            return _commandHandlers.ContainsKey(commandType);
        }

        /// <summary>
        /// Checks if a query handler is registered.
        /// </summary>
        public bool HasQueryHandler(Type queryType)
        {
            Guard.AgainstNull(queryType, nameof(queryType));
            return _queryHandlers.ContainsKey(queryType);
        }

        /// <summary>
        /// Checks if any event handlers are registered for the specified type.
        /// </summary>
        public bool HasEventHandlers(Type eventType)
        {
            Guard.AgainstNull(eventType, nameof(eventType));
            return _eventHandlers.TryGetValue(eventType, out var handlers) && handlers.Count > 0;
        }

        /// <summary>
        /// Gets the total count of registered handlers.
        /// </summary>
        public int GetHandlerCount()
        {
            return _commandHandlers.Count + _queryHandlers.Count +
                   _eventHandlers.Values.Sum(h => h.Count);
        }

        /// <summary>
        /// Gets statistics about registered handlers.
        /// </summary>
        public HandlerStatistics GetStatistics()
        {
            return new HandlerStatistics
            {
                CommandHandlerCount = _commandHandlers.Count,
                QueryHandlerCount = _queryHandlers.Count,
                EventHandlerCount = _eventHandlers.Values.Sum(h => h.Count),
                EventTypesWithHandlers = _eventHandlers.Count,
                CommandTypes = _commandHandlers.Keys.Select(t => t.Name).ToList(),
                QueryTypes = _queryHandlers.Keys.Select(t => t.Name).ToList(),
                EventTypes = _eventHandlers.Keys.Select(t => t.Name).ToList()
            };
        }

        /// <summary>
        /// Clears all registered handlers.
        /// </summary>
        public void Clear()
        {
            _commandHandlers.Clear();
            _queryHandlers.Clear();
            _eventHandlers.Clear();
        }

        /// <summary>
        /// Discovers and registers handlers from an assembly.
        /// </summary>
        public void RegisterHandlersFromAssembly(System.Reflection.Assembly assembly)
        {
            Guard.AgainstNull(assembly, nameof(assembly));

            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType);

            foreach (var handlerType in handlerTypes)
            {
                RegisterHandlerType(handlerType);
            }
        }

        private void RegisterHandlerType(Type handlerType)
        {
            // Check for command handler interfaces
            var commandHandlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

            foreach (var commandHandlerInterface in commandHandlerInterfaces)
            {
                var commandType = commandHandlerInterface.GetGenericArguments()[0];
                var handlerInstance = Activator.CreateInstance(handlerType);

                if (handlerInstance != null)
                {
                    RegisterCommandHandler(commandType, handlerInstance);
                }
            }

            // Check for query handler interfaces
            var queryHandlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

            foreach (var queryHandlerInterface in queryHandlerInterfaces)
            {
                var queryType = queryHandlerInterface.GetGenericArguments()[0];
                var handlerInstance = Activator.CreateInstance(handlerType);

                if (handlerInstance != null)
                {
                    RegisterQueryHandler(queryType, handlerInstance);
                }
            }

            // Check for event handler interfaces
            var eventHandlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (var eventHandlerInterface in eventHandlerInterfaces)
            {
                var eventType = eventHandlerInterface.GetGenericArguments()[0];
                var handlerInstance = Activator.CreateInstance(handlerType);

                if (handlerInstance != null)
                {
                    RegisterEventHandler(eventType, handlerInstance);
                }
            }
        }
    }

    /// <summary>
    /// Statistics about registered handlers.
    /// </summary>
    public class HandlerStatistics
    {
        public int CommandHandlerCount { get; set; }
        public int QueryHandlerCount { get; set; }
        public int EventHandlerCount { get; set; }
        public int EventTypesWithHandlers { get; set; }
        public int TotalHandlers => CommandHandlerCount + QueryHandlerCount + EventHandlerCount;
        public List<string> CommandTypes { get; set; } = new();
        public List<string> QueryTypes { get; set; } = new();
        public List<string> EventTypes { get; set; } = new();
    }

    /// <summary>
    /// Exception thrown when a handler is already registered for a message type.
    /// </summary>
    public class HandlerAlreadyRegisteredException : Exception
    {
        public HandlerAlreadyRegisteredException(string message) : base(message) { }
        public HandlerAlreadyRegisteredException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}