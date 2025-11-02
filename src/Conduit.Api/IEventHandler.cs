namespace Conduit.Api;

/// <summary>
/// Interface for handling events.
/// Event handlers should be idempotent as events may be replayed.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}