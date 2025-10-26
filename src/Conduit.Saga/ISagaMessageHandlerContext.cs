using Conduit.Messaging;

namespace Conduit.Saga;

/// <summary>
/// Saga-specific message handling context.
/// Extends the base message context with saga-specific messaging operations.
/// </summary>
public interface ISagaMessageHandlerContext : IMessageContext
{
    /// <summary>
    /// Sends a message to a specific endpoint.
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="endpoint">The target endpoint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the send operation</returns>
    Task SendAsync(object message, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message using default routing.
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the send operation</returns>
    Task SendAsync(object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to all interested subscribers.
    /// </summary>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the publish operation</returns>
    Task PublishAsync(object @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replies to the current message.
    /// </summary>
    /// <param name="reply">The reply message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the reply operation</returns>
    Task ReplyAsync(object reply, CancellationToken cancellationToken = default);
}
