using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Saga;

/// <summary>
/// Interface for persisting saga state.
/// </summary>
public interface ISagaPersister
{
    /// <summary>
    /// Saves saga data.
    /// </summary>
    /// <param name="sagaData">The saga data to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the save operation</returns>
    Task SaveAsync(IContainSagaData sagaData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds saga data by correlation ID.
    /// </summary>
    /// <typeparam name="TSagaData">The saga data type</typeparam>
    /// <param name="sagaType">The saga type name</param>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task with the found saga data, or null if not found</returns>
    Task<TSagaData?> FindAsync<TSagaData>(string sagaType, string correlationId, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;

    /// <summary>
    /// Removes saga data.
    /// </summary>
    /// <param name="sagaData">The saga data to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the removal operation</returns>
    Task RemoveAsync(IContainSagaData sagaData, CancellationToken cancellationToken = default);
}
