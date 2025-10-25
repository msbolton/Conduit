using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core
{
    /// <summary>
    /// Dispatches component lifecycle events to registered handlers.
    /// </summary>
    public class ComponentEventDispatcher : IDisposable
    {
        private readonly ILogger<ComponentEventDispatcher>? _logger;
        private readonly ConcurrentDictionary<ComponentEventType, List<IComponentEventHandler>> _handlers;
        private readonly ConcurrentDictionary<Guid, ComponentEventSubscription> _subscriptions;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        /// <summary>
        /// Gets a value indicating whether event dispatching is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the ComponentEventDispatcher class.
        /// </summary>
        /// <param name="logger">Optional logger</param>
        public ComponentEventDispatcher(ILogger<ComponentEventDispatcher>? logger = null)
        {
            _logger = logger;
            _handlers = new ConcurrentDictionary<ComponentEventType, List<IComponentEventHandler>>();
            _subscriptions = new ConcurrentDictionary<Guid, ComponentEventSubscription>();
            _semaphore = new SemaphoreSlim(1, 1);

            // Initialize handler lists for all event types
            foreach (ComponentEventType eventType in Enum.GetValues<ComponentEventType>())
            {
                _handlers[eventType] = new List<IComponentEventHandler>();
            }
        }

        /// <summary>
        /// Subscribes to a specific component event type.
        /// </summary>
        /// <param name="eventType">The event type to subscribe to</param>
        /// <param name="handler">The event handler</param>
        /// <returns>A subscription that can be disposed to unsubscribe</returns>
        public async Task<IDisposable> SubscribeAsync(ComponentEventType eventType, IComponentEventHandler handler)
        {
            Guard.AgainstNull(handler, nameof(handler));

            await _semaphore.WaitAsync();
            try
            {
                _handlers[eventType].Add(handler);

                var subscription = new ComponentEventSubscription(
                    Guid.NewGuid(),
                    eventType,
                    handler,
                    () => UnsubscribeAsync(eventType, handler).GetAwaiter().GetResult());

                _subscriptions[subscription.Id] = subscription;

                _logger?.LogDebug("Subscribed handler {Handler} to event {EventType}",
                    handler.GetType().Name, eventType);

                return subscription;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Subscribes to a component event with a callback action.
        /// </summary>
        /// <param name="eventType">The event type to subscribe to</param>
        /// <param name="callback">The callback action</param>
        /// <returns>A subscription that can be disposed to unsubscribe</returns>
        public Task<IDisposable> SubscribeAsync(ComponentEventType eventType, Action<ComponentEvent> callback)
        {
            Guard.AgainstNull(callback, nameof(callback));

            var handler = new ActionEventHandler(callback);
            return SubscribeAsync(eventType, handler);
        }

        /// <summary>
        /// Subscribes to a component event with an async callback.
        /// </summary>
        /// <param name="eventType">The event type to subscribe to</param>
        /// <param name="callback">The async callback function</param>
        /// <returns>A subscription that can be disposed to unsubscribe</returns>
        public Task<IDisposable> SubscribeAsync(ComponentEventType eventType, Func<ComponentEvent, Task> callback)
        {
            Guard.AgainstNull(callback, nameof(callback));

            var handler = new AsyncActionEventHandler(callback);
            return SubscribeAsync(eventType, handler);
        }

        /// <summary>
        /// Unsubscribes from a specific component event type.
        /// </summary>
        /// <param name="eventType">The event type to unsubscribe from</param>
        /// <param name="handler">The event handler to remove</param>
        public async Task UnsubscribeAsync(ComponentEventType eventType, IComponentEventHandler handler)
        {
            Guard.AgainstNull(handler, nameof(handler));

            await _semaphore.WaitAsync();
            try
            {
                _handlers[eventType].Remove(handler);

                var subscription = _subscriptions.Values.FirstOrDefault(s =>
                    s.EventType == eventType && s.Handler == handler);

                if (subscription != null)
                {
                    _subscriptions.TryRemove(subscription.Id, out _);
                }

                _logger?.LogDebug("Unsubscribed handler {Handler} from event {EventType}",
                    handler.GetType().Name, eventType);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Dispatches a component event to all registered handlers.
        /// </summary>
        /// <param name="componentEvent">The event to dispatch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task DispatchAsync(ComponentEvent componentEvent, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(componentEvent, nameof(componentEvent));

            if (!IsEnabled)
            {
                _logger?.LogTrace("Event dispatching is disabled, skipping event {EventType}", componentEvent.EventType);
                return;
            }

            _logger?.LogDebug("Dispatching {EventType} event for component {ComponentId}",
                componentEvent.EventType, componentEvent.ComponentId);

            var handlers = _handlers[componentEvent.EventType].ToList();

            if (handlers.Count == 0)
            {
                _logger?.LogTrace("No handlers registered for event type {EventType}", componentEvent.EventType);
                return;
            }

            // Execute handlers in parallel with error isolation
            var tasks = handlers.Select(async handler =>
            {
                try
                {
                    await handler.HandleAsync(componentEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Handler {Handler} failed to process {EventType} event",
                        handler.GetType().Name, componentEvent.EventType);
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Dispatches a component loaded event.
        /// </summary>
        public Task DispatchComponentLoadedAsync(ComponentDescriptor descriptor, Type componentType, CancellationToken cancellationToken = default)
        {
            var evt = new ComponentEvent(
                ComponentEventType.Loaded,
                descriptor.ComponentId,
                descriptor,
                componentType);

            return DispatchAsync(evt, cancellationToken);
        }

        /// <summary>
        /// Dispatches a component unloaded event.
        /// </summary>
        public Task DispatchComponentUnloadedAsync(ComponentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            var evt = new ComponentEvent(
                ComponentEventType.Unloaded,
                descriptor.ComponentId,
                descriptor,
                null);

            return DispatchAsync(evt, cancellationToken);
        }

        /// <summary>
        /// Dispatches a component started event.
        /// </summary>
        public Task DispatchComponentStartedAsync(ComponentDescriptor descriptor, IPluggableComponent instance, CancellationToken cancellationToken = default)
        {
            var evt = new ComponentEvent(
                ComponentEventType.Started,
                descriptor.ComponentId,
                descriptor,
                instance.GetType())
            {
                ComponentInstance = instance
            };

            return DispatchAsync(evt, cancellationToken);
        }

        /// <summary>
        /// Dispatches a component stopped event.
        /// </summary>
        public Task DispatchComponentStoppedAsync(ComponentDescriptor descriptor, IPluggableComponent instance, CancellationToken cancellationToken = default)
        {
            var evt = new ComponentEvent(
                ComponentEventType.Stopped,
                descriptor.ComponentId,
                descriptor,
                instance.GetType())
            {
                ComponentInstance = instance
            };

            return DispatchAsync(evt, cancellationToken);
        }

        /// <summary>
        /// Dispatches a component error event.
        /// </summary>
        public Task DispatchComponentErrorAsync(ComponentDescriptor descriptor, Exception error, CancellationToken cancellationToken = default)
        {
            var evt = new ComponentEvent(
                ComponentEventType.Error,
                descriptor.ComponentId,
                descriptor,
                null)
            {
                Error = error
            };

            return DispatchAsync(evt, cancellationToken);
        }

        /// <summary>
        /// Clears all event handlers.
        /// </summary>
        public async Task ClearHandlersAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                foreach (var handlerList in _handlers.Values)
                {
                    handlerList.Clear();
                }

                _subscriptions.Clear();

                _logger?.LogInformation("Cleared all event handlers");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the count of handlers for a specific event type.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <returns>The number of registered handlers</returns>
        public int GetHandlerCount(ComponentEventType eventType)
        {
            return _handlers[eventType].Count;
        }

        /// <summary>
        /// Gets the total count of all handlers.
        /// </summary>
        /// <returns>The total number of registered handlers</returns>
        public int GetTotalHandlerCount()
        {
            return _handlers.Values.Sum(list => list.Count);
        }

        /// <summary>
        /// Disposes of the event dispatcher.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the event dispatcher.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ClearHandlersAsync().GetAwaiter().GetResult();
                    _semaphore.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Action-based event handler implementation.
        /// </summary>
        private class ActionEventHandler : IComponentEventHandler
        {
            private readonly Action<ComponentEvent> _callback;

            public ActionEventHandler(Action<ComponentEvent> callback)
            {
                _callback = callback;
            }

            public Task HandleAsync(ComponentEvent componentEvent, CancellationToken cancellationToken)
            {
                _callback(componentEvent);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Async action-based event handler implementation.
        /// </summary>
        private class AsyncActionEventHandler : IComponentEventHandler
        {
            private readonly Func<ComponentEvent, Task> _callback;

            public AsyncActionEventHandler(Func<ComponentEvent, Task> callback)
            {
                _callback = callback;
            }

            public Task HandleAsync(ComponentEvent componentEvent, CancellationToken cancellationToken)
            {
                return _callback(componentEvent);
            }
        }

        /// <summary>
        /// Represents an event subscription.
        /// </summary>
        private class ComponentEventSubscription : IDisposable
        {
            public Guid Id { get; }
            public ComponentEventType EventType { get; }
            public IComponentEventHandler Handler { get; }
            private readonly Action _unsubscribe;

            public ComponentEventSubscription(
                Guid id,
                ComponentEventType eventType,
                IComponentEventHandler handler,
                Action unsubscribe)
            {
                Id = id;
                EventType = eventType;
                Handler = handler;
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                _unsubscribe();
            }
        }
    }

    /// <summary>
    /// Interface for component event handlers.
    /// </summary>
    public interface IComponentEventHandler
    {
        /// <summary>
        /// Handles a component event.
        /// </summary>
        /// <param name="componentEvent">The event to handle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task HandleAsync(ComponentEvent componentEvent, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Types of component events.
    /// </summary>
    public enum ComponentEventType
    {
        /// <summary>Component has been loaded/discovered</summary>
        Loaded,
        /// <summary>Component has been unloaded</summary>
        Unloaded,
        /// <summary>Component has been started</summary>
        Started,
        /// <summary>Component has been stopped</summary>
        Stopped,
        /// <summary>Component has been reloaded</summary>
        Reloaded,
        /// <summary>Component encountered an error</summary>
        Error,
        /// <summary>Component state changed</summary>
        StateChanged,
        /// <summary>Component configuration changed</summary>
        ConfigurationChanged
    }

    /// <summary>
    /// Represents a component event.
    /// </summary>
    public class ComponentEvent : EventArgs
    {
        /// <summary>
        /// Gets the event type.
        /// </summary>
        public ComponentEventType EventType { get; }

        /// <summary>
        /// Gets the component ID.
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// Gets the component descriptor.
        /// </summary>
        public ComponentDescriptor? Descriptor { get; }

        /// <summary>
        /// Gets the component type.
        /// </summary>
        public Type? ComponentType { get; }

        /// <summary>
        /// Gets or sets the component instance (if available).
        /// </summary>
        public IPluggableComponent? ComponentInstance { get; set; }

        /// <summary>
        /// Gets or sets the error (for error events).
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Gets or sets additional event data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// Gets the timestamp of the event.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the ComponentEvent class.
        /// </summary>
        public ComponentEvent(
            ComponentEventType eventType,
            string componentId,
            ComponentDescriptor? descriptor = null,
            Type? componentType = null)
        {
            EventType = eventType;
            ComponentId = componentId;
            Descriptor = descriptor;
            ComponentType = componentType;
            Data = new Dictionary<string, object>();
            Timestamp = DateTimeOffset.UtcNow;
        }
    }
}