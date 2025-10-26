namespace Conduit.Saga;

/// <summary>
/// Interface for orchestrating saga workflows and managing saga instances.
/// Acts as the central coordinator for distributed saga workflows.
/// </summary>
public interface ISagaOrchestrator
{
    /// <summary>
    /// Handles a message by finding or creating the appropriate saga instance.
    /// </summary>
    /// <param name="sagaType">The saga type</param>
    /// <param name="message">The message to handle</param>
    /// <param name="context">The message handler context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the handling completion</returns>
    Task<object?> HandleMessageAsync(
        Type sagaType,
        object message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a saga type with the orchestrator.
    /// </summary>
    /// <param name="sagaType">The saga type</param>
    void RegisterSaga(Type sagaType);

    /// <summary>
    /// Registers a saga type with the orchestrator.
    /// </summary>
    /// <typeparam name="TSaga">The saga type</typeparam>
    void RegisterSaga<TSaga>() where TSaga : Saga;

    /// <summary>
    /// Creates a new saga instance.
    /// </summary>
    /// <param name="sagaType">The saga type</param>
    /// <returns>The created saga instance</returns>
    Saga CreateSaga(Type sagaType);

    /// <summary>
    /// Finds an existing saga instance.
    /// </summary>
    /// <param name="sagaType">The saga type</param>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task with the found saga instance, or null if not found</returns>
    Task<Saga?> FindSagaAsync(Type sagaType, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a saga instance.
    /// </summary>
    /// <param name="saga">The saga to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the save completion</returns>
    Task SaveSagaAsync(Saga saga, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a completed saga instance.
    /// </summary>
    /// <param name="saga">The saga to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the removal completion</returns>
    Task RemoveSagaAsync(Saga saga, CancellationToken cancellationToken = default);
}
