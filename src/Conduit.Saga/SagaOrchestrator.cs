using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Conduit.Saga;

/// <summary>
/// Default implementation of saga orchestrator.
/// Acts as the central coordinator for distributed saga workflows.
/// </summary>
public class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ISagaPersister _persister;
    private readonly ILogger<SagaOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, Type> _registeredSagas = new();

    public SagaOrchestrator(
        ISagaPersister persister,
        ILogger<SagaOrchestrator> logger)
    {
        _persister = persister;
        _logger = logger;
    }

    public async Task<object?> HandleMessageAsync(
        Type sagaType,
        object message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find or create saga instance
            var saga = await FindOrCreateSagaAsync(sagaType, context.CorrelationId, cancellationToken);

            // Handle the message
            await saga.HandleAsync(message, context, cancellationToken);

            // Save saga state if not completed
            if (!saga.IsCompleted)
            {
                await SaveSagaAsync(saga, cancellationToken);
            }
            else
            {
                // Remove completed saga
                await RemoveSagaAsync(saga, cancellationToken);
                _logger.LogInformation("Saga {SagaType} with correlation ID {CorrelationId} completed",
                    sagaType.Name, context.CorrelationId);
            }

            return null; // Return result if needed

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message in saga {SagaType}: {Message}",
                sagaType.Name, ex.Message);
            throw;
        }
    }

    public void RegisterSaga(Type sagaType)
    {
        if (!typeof(Saga).IsAssignableFrom(sagaType))
        {
            throw new ArgumentException($"Type {sagaType.Name} must inherit from Saga", nameof(sagaType));
        }

        _registeredSagas[sagaType.Name] = sagaType;
        _logger.LogInformation("Registered saga: {SagaType}", sagaType.Name);
    }

    public void RegisterSaga<TSaga>() where TSaga : Saga
    {
        RegisterSaga(typeof(TSaga));
    }

    public Saga CreateSaga(Type sagaType)
    {
        try
        {
            if (!_registeredSagas.TryGetValue(sagaType.Name, out var registeredType))
            {
                throw new ArgumentException($"Unknown saga type: {sagaType.Name}");
            }

            var saga = (Saga)Activator.CreateInstance(registeredType)!;
            return saga;

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create saga instance: {sagaType.Name}", ex);
        }
    }

    public async Task<Saga?> FindSagaAsync(
        Type sagaType,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the saga data type from the saga type
            var sagaDataType = GetSagaDataType(sagaType);
            if (sagaDataType == null)
            {
                _logger.LogWarning("Could not determine saga data type for {SagaType}", sagaType.Name);
                return null;
            }

            // Use reflection to call the generic FindAsync method
            var findMethod = typeof(ISagaPersister)
                .GetMethod(nameof(ISagaPersister.FindAsync))!
                .MakeGenericMethod(sagaDataType);

            var sagaDataTask = (Task?)findMethod.Invoke(_persister,
                new object[] { sagaType.Name, correlationId, cancellationToken });

            if (sagaDataTask == null)
            {
                return null;
            }

            await sagaDataTask;

            var sagaData = (IContainSagaData?)sagaDataTask.GetType()
                .GetProperty(nameof(Task<object>.Result))!
                .GetValue(sagaDataTask);

            if (sagaData == null)
            {
                return null;
            }

            var saga = CreateSaga(sagaType);
            saga.Entity = sagaData;
            return saga;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding saga {SagaType} with correlation {CorrelationId}",
                sagaType.Name, correlationId);
            return null;
        }
    }

    public async Task SaveSagaAsync(Saga saga, CancellationToken cancellationToken = default)
    {
        await _persister.SaveAsync(saga.Entity, cancellationToken);
    }

    public async Task RemoveSagaAsync(Saga saga, CancellationToken cancellationToken = default)
    {
        await _persister.RemoveAsync(saga.Entity, cancellationToken);
    }

    private async Task<Saga> FindOrCreateSagaAsync(
        Type sagaType,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // Try to find existing saga
        var existingSaga = await FindSagaAsync(sagaType, correlationId, cancellationToken);
        if (existingSaga != null)
        {
            return existingSaga;
        }

        // Create new saga
        var newSaga = CreateSaga(sagaType);

        // Initialize saga data
        var sagaDataType = GetSagaDataType(sagaType);
        if (sagaDataType != null)
        {
            newSaga.Entity = (IContainSagaData)Activator.CreateInstance(sagaDataType)!;
            newSaga.Entity.CorrelationId = correlationId;
        }

        return newSaga;
    }

    private Type? GetSagaDataType(Type sagaType)
    {
        // Look for Entity property to determine saga data type
        var entityProperty = sagaType.GetProperty(nameof(Saga.Entity));
        if (entityProperty != null && entityProperty.PropertyType != typeof(IContainSagaData))
        {
            return entityProperty.PropertyType;
        }

        // Look for nested SagaData class
        var nestedDataType = sagaType.GetNestedType("SagaData");
        if (nestedDataType != null && typeof(IContainSagaData).IsAssignableFrom(nestedDataType))
        {
            return nestedDataType;
        }

        // Return base SagaData type as fallback
        return typeof(SagaData);
    }
}
