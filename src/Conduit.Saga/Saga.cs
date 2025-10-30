using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Saga;

/// <summary>
/// Base class for Conduit saga implementations.
/// Inspired by NServiceBus Saga pattern with reflection-based message handling.
/// </summary>
public abstract class Saga
{
    private readonly ILogger? _logger;
    private bool _completed;

    protected Saga()
    {
    }

    protected Saga(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The saga's typed data.
    /// </summary>
    public IContainSagaData Entity { get; set; } = null!;

    /// <summary>
    /// Indicates that the saga is complete.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Configure how this saga's data should be found when processing messages.
    /// </summary>
    /// <param name="sagaMessageFindingConfiguration">The configuration object</param>
    protected abstract void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage sagaMessageFindingConfiguration);

    /// <summary>
    /// Handles a message by finding and invoking the appropriate handler method.
    /// </summary>
    /// <param name="message">The message to handle</param>
    /// <param name="context">The message handler context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the handling completion</returns>
    public virtual async Task HandleAsync(
        object message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use reflection to find and call the appropriate handle method
            var handleMethod = FindHandleMethod(message.GetType());
            if (handleMethod != null)
            {
                var result = handleMethod.Invoke(this, new[] { message, context, cancellationToken });
                if (result is Task task)
                {
                    await task;
                }
            }
            else
            {
                _logger?.LogWarning("No handle method found for message type: {MessageType}",
                    message.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling message in saga {SagaType}: {Message}",
                GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Checks if this saga can handle the given message type.
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>True if this saga can handle the message type</returns>
    public virtual bool CanHandle(Type messageType)
    {
        return FindHandleMethod(messageType) != null;
    }

    /// <summary>
    /// Request for a timeout to occur after the given duration.
    /// </summary>
    /// <typeparam name="TTimeoutMessage">The type of timeout message</typeparam>
    /// <param name="context">The message handler context</param>
    /// <param name="within">Duration to delay timeout message by</param>
    /// <param name="timeoutMessage">The message to send after the duration expires</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the timeout request</returns>
    protected async Task RequestTimeoutAsync<TTimeoutMessage>(
        ISagaMessageHandlerContext context,
        TimeSpan within,
        TTimeoutMessage timeoutMessage,
        CancellationToken cancellationToken = default)
    {
        VerifyCanHandleTimeout(timeoutMessage!);

        // In a full implementation, this would schedule the timeout message
        // For now, send it immediately (simplified)
        await context.SendAsync(timeoutMessage!, cancellationToken);
    }

    /// <summary>
    /// Request for a timeout to occur at the given time.
    /// </summary>
    /// <typeparam name="TTimeoutMessage">The type of timeout message</typeparam>
    /// <param name="context">The message handler context</param>
    /// <param name="at">DateTime to deliver timeout message</param>
    /// <param name="timeoutMessage">The message to send at the specified time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the timeout request</returns>
    protected async Task RequestTimeoutAsync<TTimeoutMessage>(
        ISagaMessageHandlerContext context,
        DateTime at,
        TTimeoutMessage timeoutMessage,
        CancellationToken cancellationToken = default)
    {
        VerifyCanHandleTimeout(timeoutMessage!);

        // In a full implementation, this would schedule the timeout message
        // For now, send it immediately (simplified)
        await context.SendAsync(timeoutMessage!, cancellationToken);
    }

    /// <summary>
    /// Sends a reply to the originator of this saga.
    /// </summary>
    /// <param name="context">The message handler context</param>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the send operation</returns>
    protected async Task ReplyToOriginatorAsync(
        ISagaMessageHandlerContext context,
        object message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Entity.Originator))
        {
            throw new InvalidOperationException(
                "Entity.Originator cannot be null. Perhaps the sender is a SendOnly endpoint.");
        }

        await context.SendAsync(message, Entity.Originator, cancellationToken);
    }

    /// <summary>
    /// Marks the saga as complete.
    /// This may result in the saga's state being deleted by the persister.
    /// </summary>
    protected void MarkAsComplete()
    {
        _completed = true;
        Entity.State = "COMPLETED";
        Entity.LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Finds the appropriate handle method for a message type.
    /// Looks for methods named "Handle" or "HandleAsync" that accept the message type and context.
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The handle method, or null if not found</returns>
    private MethodInfo? FindHandleMethod(Type messageType)
    {
        // Look for HandleAsync method (preferred)
        var handleAsyncMethod = GetType().GetMethod(
            "HandleAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { messageType, typeof(ISagaMessageHandlerContext), typeof(CancellationToken) },
            null);

        if (handleAsyncMethod != null)
        {
            return handleAsyncMethod;
        }

        // Look for Handle method (legacy)
        var handleMethod = GetType().GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { messageType, typeof(ISagaMessageHandlerContext), typeof(CancellationToken) },
            null);

        if (handleMethod != null)
        {
            return handleMethod;
        }

        // Try to find a method that accepts a superclass or interface of the message type
        foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if ((method.Name == "HandleAsync" || method.Name == "Handle") &&
                method.GetParameters().Length == 3 &&
                method.GetParameters()[1].ParameterType == typeof(ISagaMessageHandlerContext) &&
                method.GetParameters()[2].ParameterType == typeof(CancellationToken) &&
                method.GetParameters()[0].ParameterType.IsAssignableFrom(messageType))
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Verifies that the saga can handle the specified timeout message type.
    /// </summary>
    /// <typeparam name="TTimeoutMessage">The timeout message type</typeparam>
    /// <param name="timeoutMessage">The timeout message instance</param>
    private void VerifyCanHandleTimeout<TTimeoutMessage>(TTimeoutMessage timeoutMessage)
    {
        var timeoutMessageType = typeof(TTimeoutMessage);

        if (!CanHandle(timeoutMessageType))
        {
            throw new InvalidOperationException(
                $"The type '{GetType().Name}' cannot request timeouts for '{timeoutMessageType.Name}' " +
                $"because it does not implement a handler for that message type.");
        }
    }
}
