using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Saga;

/// <summary>
/// In-memory implementation of saga persister for testing and development.
/// </summary>
public class InMemorySagaPersister : ISagaPersister
{
    private readonly ConcurrentDictionary<string, IContainSagaData> _sagaStore = new();

    public Task SaveAsync(IContainSagaData sagaData, CancellationToken cancellationToken = default)
    {
        var key = GenerateKey(sagaData.GetType().Name, sagaData.CorrelationId);
        _sagaStore[key] = sagaData;
        return Task.CompletedTask;
    }

    public Task<TSagaData?> FindAsync<TSagaData>(
        string sagaType,
        string correlationId,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        var key = GenerateKey(sagaType, correlationId);
        _sagaStore.TryGetValue(key, out var sagaData);
        return Task.FromResult(sagaData as TSagaData);
    }

    public Task RemoveAsync(IContainSagaData sagaData, CancellationToken cancellationToken = default)
    {
        var key = GenerateKey(sagaData.GetType().Name, sagaData.CorrelationId);
        _sagaStore.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the count of sagas currently in the store (for testing).
    /// </summary>
    public int Count => _sagaStore.Count;

    /// <summary>
    /// Clears all sagas from the store (for testing).
    /// </summary>
    public void Clear()
    {
        _sagaStore.Clear();
    }

    private static string GenerateKey(string sagaType, string correlationId)
    {
        return $"{sagaType}:{correlationId}";
    }
}
