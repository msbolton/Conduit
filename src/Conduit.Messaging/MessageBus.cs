using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline;
using Microsoft.Extensions.Logging;

namespace Conduit.Messaging
{
    /// <summary>
    /// Central message bus for processing commands, events, and queries.
    /// Implements the mediator pattern for decoupled message handling.
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private readonly ILogger<MessageBus>? _logger;
        private readonly IPipeline<IMessage, object?>? _pipeline;
        private readonly HandlerRegistry _handlerRegistry;
        private readonly SubscriptionManager _subscriptionManager;
        private readonly MessageCorrelator _correlator;
        private readonly DeadLetterQueue _deadLetterQueue;
        private readonly FlowController _flowController;
        private readonly MessageRetryPolicy _defaultRetryPolicy;
        private readonly MessageBusConfiguration _configuration;
        private readonly MessageBusMetrics _metrics;
        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Initializes a new instance of the MessageBus class.
        /// </summary>
        public MessageBus(
            MessageBusConfiguration? configuration = null,
            IPipeline<IMessage, object?>? pipeline = null,
            ILogger<MessageBus>? logger = null)
        {
            _configuration = configuration ?? MessageBusConfiguration.Default();
            _logger = logger;
            _pipeline = pipeline;
            _handlerRegistry = new HandlerRegistry();
            _subscriptionManager = new SubscriptionManager();
            _correlator = new MessageCorrelator(_configuration.CorrelationTimeout);
            _deadLetterQueue = new DeadLetterQueue(_configuration.DeadLetterQueueCapacity);
            _flowController = new FlowController(_configuration.MaxConcurrentMessages, _configuration.RateLimit);
            _defaultRetryPolicy = _configuration.DefaultRetryPolicy ?? MessageRetryPolicy.Default();
            _metrics = new MessageBusMetrics();
            _semaphore = new SemaphoreSlim(_configuration.MaxConcurrentMessages);
        }

        /// <summary>
        /// Sends a command and returns the response.
        /// </summary>
        public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(command, nameof(command));

            var context = CreateMessageContext(command);
            _metrics.RecordCommand();

            try
            {
                // Apply flow control
                await _flowController.ExecuteWithFlowControlAsync(async () =>
                {
                    // Get correlation ID
                    var correlationId = _correlator.GetOrCreateCorrelationId(command);
                    context.CorrelationId = correlationId;

                    // Find handler
                    var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
                    var handler = _handlerRegistry.GetCommandHandler(command.GetType());

                    if (handler == null)
                    {
                        throw new HandlerNotFoundException($"No handler found for command type {command.GetType().Name}");
                    }

                    // Execute with retry policy
                    var result = await ExecuteWithRetryAsync(async () =>
                    {
                        if (_pipeline != null)
                        {
                            return await _pipeline.ExecuteAsync(command, cancellationToken);
                        }
                        else
                        {
                            var handleMethod = handler.GetType().GetMethod("HandleAsync");
                            var task = (Task<TResponse>)handleMethod!.Invoke(handler, new object[] { command, cancellationToken })!;
                            return await task;
                        }
                    }, context);

                    // Complete correlation
                    _correlator.CompleteCorrelation(correlationId);
                    _metrics.RecordSuccess();

                    return result;
                }, GetMessagePriority(command));

                return (TResponse)(await Task.FromResult(default(TResponse)))!;
            }
            catch (Exception ex)
            {
                _metrics.RecordFailure();
                await HandleFailedMessageAsync(command, ex, context);
                throw;
            }
        }

        /// <summary>
        /// Sends a command without expecting a response.
        /// </summary>
        public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(command, nameof(command));

            var context = CreateMessageContext(command);
            _metrics.RecordCommand();

            try
            {
                // Apply flow control
                await _flowController.ExecuteWithFlowControlAsync(async () =>
                {
                    // Get correlation ID
                    var correlationId = _correlator.GetOrCreateCorrelationId(command);
                    context.CorrelationId = correlationId;

                    // Find handler
                    var handler = _handlerRegistry.GetCommandHandler(command.GetType());

                    if (handler == null)
                    {
                        throw new HandlerNotFoundException($"No handler found for command type {command.GetType().Name}");
                    }

                    // Execute with retry policy
                    await ExecuteWithRetryAsync(async () =>
                    {
                        if (_pipeline != null)
                        {
                            await _pipeline.ExecuteAsync(command, cancellationToken);
                        }
                        else
                        {
                            var handleMethod = handler.GetType().GetMethod("HandleAsync");
                            var task = (Task)handleMethod!.Invoke(handler, new object[] { command, cancellationToken })!;
                            await task;
                        }
                        return Task.CompletedTask;
                    }, context);

                    // Complete correlation
                    _correlator.CompleteCorrelation(correlationId);
                    _metrics.RecordSuccess();
                }, GetMessagePriority(command));
            }
            catch (Exception ex)
            {
                _metrics.RecordFailure();
                await HandleFailedMessageAsync(command, ex, context);
                throw;
            }
        }

        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(@event, nameof(@event));

            var context = CreateMessageContext(@event);
            _metrics.RecordEvent();

            try
            {
                // Get correlation ID
                var correlationId = _correlator.GetOrCreateCorrelationId(@event);
                context.CorrelationId = correlationId;

                // Get all subscriptions for this event type
                var subscriptions = _subscriptionManager.GetSubscriptions(@event.GetType());

                if (subscriptions.Any())
                {
                    // Notify all subscribers in parallel
                    var tasks = subscriptions
                        .Where(s => s.IsActive && (s.Filter == null || s.Filter(@event)))
                        .Select(async subscription =>
                        {
                            try
                            {
                                await subscription.HandleAsync(@event, context, cancellationToken);
                                _metrics.RecordDelivery();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Subscription {SubscriptionId} failed to handle event {EventType}",
                                    subscription.Id, @event.GetType().Name);
                                _metrics.RecordDeliveryFailure();

                                if (!subscription.Options.IgnoreErrors)
                                {
                                    throw;
                                }
                            }
                        });

                    await Task.WhenAll(tasks);
                }
                else
                {
                    _logger?.LogDebug("No subscriptions found for event type {EventType}", @event.GetType().Name);
                }

                _metrics.RecordSuccess();
            }
            catch (Exception ex)
            {
                _metrics.RecordFailure();
                await HandleFailedMessageAsync(@event, ex, context);
                throw;
            }
        }

        /// <summary>
        /// Publishes multiple events in order.
        /// </summary>
        public async Task PublishAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(events, nameof(events));

            foreach (var @event in events)
            {
                await PublishAsync(@event, cancellationToken);
            }
        }

        /// <summary>
        /// Executes a query and returns the result.
        /// </summary>
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(query, nameof(query));

            var context = CreateMessageContext(query);
            _metrics.RecordQuery();

            try
            {
                // Apply flow control
                return await _flowController.ExecuteWithFlowControlAsync(async () =>
                {
                    // Get correlation ID
                    var correlationId = _correlator.GetOrCreateCorrelationId(query);
                    context.CorrelationId = correlationId;

                    // Find handler
                    var handler = _handlerRegistry.GetQueryHandler(query.GetType());

                    if (handler == null)
                    {
                        throw new HandlerNotFoundException($"No handler found for query type {query.GetType().Name}");
                    }

                    // Execute with retry policy (for queries that might fail due to transient errors)
                    var result = await ExecuteWithRetryAsync(async () =>
                    {
                        if (_pipeline != null)
                        {
                            var pipelineResult = await _pipeline.ExecuteAsync(query, cancellationToken);
                            return (TResult)pipelineResult!;
                        }
                        else
                        {
                            var handleMethod = handler.GetType().GetMethod("HandleAsync");
                            var task = (Task<TResult>)handleMethod!.Invoke(handler, new object[] { query, cancellationToken })!;
                            return await task;
                        }
                    }, context);

                    // Complete correlation
                    _correlator.CompleteCorrelation(correlationId);
                    _metrics.RecordSuccess();

                    return result;
                }, GetMessagePriority(query));
            }
            catch (Exception ex)
            {
                _metrics.RecordFailure();
                await HandleFailedMessageAsync(query, ex, context);
                throw;
            }
        }

        /// <summary>
        /// Sends a message through the pipeline without a specific handler type.
        /// </summary>
        public async Task<object?> SendAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(message, nameof(message));

            var context = CreateMessageContext(message);

            try
            {
                if (_pipeline != null)
                {
                    return await _pipeline.ExecuteAsync(message, cancellationToken);
                }
                else
                {
                    // Handle based on message type
                    if (message is ICommand<object> cmd)
                    {
                        return await SendAsync(cmd, cancellationToken);
                    }
                    else if (message is ICommand command)
                    {
                        await SendAsync(command, cancellationToken);
                        return null;
                    }
                    else if (message is IEvent evt)
                    {
                        await PublishAsync(evt, cancellationToken);
                        return null;
                    }
                    else if (message is IQuery<object> qry)
                    {
                        return await QueryAsync(qry, cancellationToken);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown message type: {message.GetType()}");
                    }
                }
            }
            catch (Exception ex)
            {
                await HandleFailedMessageAsync(message, ex, context);
                throw;
            }
        }

        /// <summary>
        /// Gets the message pipeline for custom processing.
        /// </summary>
        public IPipeline<IMessage, object?> Pipeline => _pipeline ?? throw new InvalidOperationException("No pipeline configured");

        /// <summary>
        /// Subscribes to events of a specific type.
        /// </summary>
        public IDisposable Subscribe<TEvent>(
            Action<TEvent> handler,
            Func<TEvent, bool>? filter = null,
            SubscriptionOptions? options = null)
            where TEvent : IEvent
        {
            Guard.AgainstNull(handler, nameof(handler));

            var subscription = new MessageSubscription<TEvent>(
                Guid.NewGuid().ToString(),
                handler,
                filter,
                options ?? new SubscriptionOptions());

            _subscriptionManager.AddSubscription(typeof(TEvent), subscription);

            _logger?.LogDebug("Created subscription {SubscriptionId} for event type {EventType}",
                subscription.Id, typeof(TEvent).Name);

            return new SubscriptionDisposable(() =>
            {
                _subscriptionManager.RemoveSubscription(typeof(TEvent), subscription);
                _logger?.LogDebug("Disposed subscription {SubscriptionId}", subscription.Id);
            });
        }

        /// <summary>
        /// Registers a command handler.
        /// </summary>
        public void RegisterCommandHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
            where TCommand : ICommand<TResponse>
        {
            Guard.AgainstNull(handler, nameof(handler));
            _handlerRegistry.RegisterCommandHandler(typeof(TCommand), handler);
            _logger?.LogInformation("Registered command handler for {CommandType}", typeof(TCommand).Name);
        }

        /// <summary>
        /// Registers a query handler.
        /// </summary>
        public void RegisterQueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : IQuery<TResult>
        {
            Guard.AgainstNull(handler, nameof(handler));
            _handlerRegistry.RegisterQueryHandler(typeof(TQuery), handler);
            _logger?.LogInformation("Registered query handler for {QueryType}", typeof(TQuery).Name);
        }

        /// <summary>
        /// Registers a command handler.
        /// </summary>
        public ISubscription Subscribe<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
            where TCommand : ICommand<TResponse>
        {
            Guard.AgainstNull(handler, nameof(handler));
            RegisterCommandHandler(handler);

            var subscription = new HandlerSubscription(Guid.NewGuid().ToString(), true, () =>
            {
                // Handler unregistration logic would go here
                _logger?.LogDebug("Unregistering command handler for {CommandType}", typeof(TCommand).Name);
            });

            _logger?.LogInformation("Subscribed command handler for {CommandType}", typeof(TCommand).Name);
            return subscription;
        }

        /// <summary>
        /// Registers an event handler.
        /// </summary>
        public ISubscription Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent
        {
            Guard.AgainstNull(handler, nameof(handler));

            // Create a subscription that adapts IEventHandler to Action<TEvent>
            var actionHandler = new Action<TEvent>(evt => handler.HandleAsync(evt, CancellationToken.None).GetAwaiter().GetResult());
            var disposable = Subscribe(actionHandler);

            var subscription = new HandlerSubscription(Guid.NewGuid().ToString(), true, () =>
            {
                disposable.Dispose();
                _logger?.LogDebug("Unregistering event handler for {EventType}", typeof(TEvent).Name);
            });

            _logger?.LogInformation("Subscribed event handler for {EventType}", typeof(TEvent).Name);
            return subscription;
        }

        /// <summary>
        /// Registers a query handler.
        /// </summary>
        public ISubscription Subscribe<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : IQuery<TResult>
        {
            Guard.AgainstNull(handler, nameof(handler));
            RegisterQueryHandler(handler);

            var subscription = new HandlerSubscription(Guid.NewGuid().ToString(), true, () =>
            {
                // Handler unregistration logic would go here
                _logger?.LogDebug("Unregistering query handler for {QueryType}", typeof(TQuery).Name);
            });

            _logger?.LogInformation("Subscribed query handler for {QueryType}", typeof(TQuery).Name);
            return subscription;
        }

        /// <summary>
        /// Gets the health status of the message bus.
        /// </summary>
        public MessageBusHealth GetHealth()
        {
            return new MessageBusHealth
            {
                IsHealthy = _flowController.IsHealthy,
                QueueDepth = _flowController.QueueDepth,
                ActiveCorrelations = _correlator.ActiveCorrelations,
                DeadLetterQueueSize = _deadLetterQueue.Count,
                RegisteredHandlers = _handlerRegistry.GetHandlerCount(),
                ActiveSubscriptions = _subscriptionManager.GetActiveSubscriptionCount()
            };
        }

        /// <summary>
        /// Gets metrics for the message bus.
        /// </summary>
        public MessageBusMetrics GetMetrics()
        {
            return _metrics.Clone();
        }

        private MessageContext CreateMessageContext(IMessage message)
        {
            return new MessageContext(message)
            {
                MessageId = message.MessageId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                Headers = message.Headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "") ?? new Dictionary<string, string>()
            };
        }

        private Priority GetMessagePriority(IMessage message)
        {
            if (message.Headers?.TryGetValue("Priority", out var priorityObj) == true &&
                Enum.TryParse<Priority>(priorityObj?.ToString(), out var priority))
            {
                return priority;
            }

            return message switch
            {
                ICommand<object> => Priority.High,
                IQuery<object> => Priority.Normal,
                IEvent => Priority.Low,
                _ => Priority.Normal
            };
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, MessageContext context)
        {
            var retryPolicy = GetRetryPolicy(context.Message);
            var attempt = 0;
            Exception? lastException = null;

            while (attempt <= retryPolicy.MaxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt > retryPolicy.MaxRetries || !retryPolicy.ShouldRetry(ex))
                    {
                        throw;
                    }

                    var delay = retryPolicy.GetRetryDelay(attempt);
                    _logger?.LogWarning(ex, "Attempt {Attempt} failed, retrying after {Delay}ms", attempt, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
            }

            throw lastException!;
        }

        private MessageRetryPolicy GetRetryPolicy(IMessage message)
        {
            // Check if message has custom retry policy
            if (message.Headers?.ContainsKey("RetryPolicy") == true)
            {
                // Parse custom retry policy from headers
                // For now, return default
            }

            return _defaultRetryPolicy;
        }

        private async Task HandleFailedMessageAsync(IMessage message, Exception exception, MessageContext context)
        {
            _logger?.LogError(exception, "Message {MessageId} of type {MessageType} failed",
                context.MessageId, message.GetType().Name);

            await _deadLetterQueue.AddAsync(message, exception, context);

            // Update correlation as failed
            if (!string.IsNullOrEmpty(context.CorrelationId))
            {
                _correlator.MarkAsFailed(context.CorrelationId, exception);
            }
        }

        private class SubscriptionDisposable : IDisposable
        {
            private readonly Action _disposeAction;

            public SubscriptionDisposable(Action disposeAction)
            {
                _disposeAction = disposeAction;
            }

            public void Dispose()
            {
                _disposeAction();
            }
        }

        private class HandlerSubscription : ISubscription
        {
            private readonly Action _disposeAction;
            private bool _isActive;

            public HandlerSubscription(string id, bool isActive, Action disposeAction)
            {
                Id = id;
                _isActive = isActive;
                _disposeAction = disposeAction;
            }

            public string Id { get; }
            public bool IsActive => _isActive;

            public void Pause()
            {
                _isActive = false;
            }

            public void Resume()
            {
                _isActive = true;
            }

            public void Dispose()
            {
                _disposeAction();
            }
        }
    }

    /// <summary>
    /// Configuration for the message bus.
    /// </summary>
    public class MessageBusConfiguration
    {
        public int MaxConcurrentMessages { get; set; } = 100;
        public int RateLimit { get; set; } = 1000;
        public TimeSpan CorrelationTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public int DeadLetterQueueCapacity { get; set; } = 10000;
        public MessageRetryPolicy? DefaultRetryPolicy { get; set; }
        public bool EnableMetrics { get; set; } = true;
        public bool EnableDeadLetterQueue { get; set; } = true;

        public static MessageBusConfiguration Default() => new();
    }

    /// <summary>
    /// Health status of the message bus.
    /// </summary>
    public class MessageBusHealth
    {
        public bool IsHealthy { get; set; }
        public int QueueDepth { get; set; }
        public int ActiveCorrelations { get; set; }
        public int DeadLetterQueueSize { get; set; }
        public int RegisteredHandlers { get; set; }
        public int ActiveSubscriptions { get; set; }
    }

    /// <summary>
    /// Metrics for the message bus.
    /// </summary>
    public class MessageBusMetrics
    {
        private long _totalCommands;
        private long _totalEvents;
        private long _totalQueries;
        private long _successfulMessages;
        private long _failedMessages;
        private long _deliveredEvents;
        private long _failedDeliveries;

        public long TotalCommands => _totalCommands;
        public long TotalEvents => _totalEvents;
        public long TotalQueries => _totalQueries;
        public long TotalMessages => _totalCommands + _totalEvents + _totalQueries;
        public long SuccessfulMessages => _successfulMessages;
        public long FailedMessages => _failedMessages;
        public long DeliveredEvents => _deliveredEvents;
        public long FailedDeliveries => _failedDeliveries;
        public double SuccessRate => TotalMessages > 0 ? (double)SuccessfulMessages / TotalMessages : 0;

        public void RecordCommand() => Interlocked.Increment(ref _totalCommands);
        public void RecordEvent() => Interlocked.Increment(ref _totalEvents);
        public void RecordQuery() => Interlocked.Increment(ref _totalQueries);
        public void RecordSuccess() => Interlocked.Increment(ref _successfulMessages);
        public void RecordFailure() => Interlocked.Increment(ref _failedMessages);
        public void RecordDelivery() => Interlocked.Increment(ref _deliveredEvents);
        public void RecordDeliveryFailure() => Interlocked.Increment(ref _failedDeliveries);

        public MessageBusMetrics Clone()
        {
            return new MessageBusMetrics
            {
                _totalCommands = _totalCommands,
                _totalEvents = _totalEvents,
                _totalQueries = _totalQueries,
                _successfulMessages = _successfulMessages,
                _failedMessages = _failedMessages,
                _deliveredEvents = _deliveredEvents,
                _failedDeliveries = _failedDeliveries
            };
        }
    }

    /// <summary>
    /// Exception thrown when no handler is found for a message.
    /// </summary>
    public class HandlerNotFoundException : Exception
    {
        public HandlerNotFoundException(string message) : base(message) { }
        public HandlerNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}