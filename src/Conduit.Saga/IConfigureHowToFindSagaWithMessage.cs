using System;

namespace Conduit.Saga;

/// <summary>
/// Configuration interface for mapping messages to saga instances.
/// Based on NServiceBus saga correlation configuration.
/// </summary>
public interface IConfigureHowToFindSagaWithMessage
{
    /// <summary>
    /// Configure correlation between a message property and saga data property.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <typeparam name="TSagaData">The saga data type</typeparam>
    /// <typeparam name="TProperty">The property type</typeparam>
    /// <param name="messagePropertyExtractor">Function to extract property from message</param>
    /// <param name="sagaDataPropertyExtractor">Function to extract property from saga data</param>
    /// <returns>This configuration object for fluent chaining</returns>
    IConfigureHowToFindSagaWithMessage CorrelateMessage<TMessage, TSagaData, TProperty>(
        Func<TMessage, TProperty> messagePropertyExtractor,
        Func<TSagaData, TProperty> sagaDataPropertyExtractor)
        where TSagaData : IContainSagaData;

    /// <summary>
    /// Configure correlation using correlation ID.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <param name="correlationIdExtractor">Function to extract correlation ID from message</param>
    /// <returns>This configuration object for fluent chaining</returns>
    IConfigureHowToFindSagaWithMessage CorrelateByCorrelationId<TMessage>(
        Func<TMessage, string> correlationIdExtractor);
}

/// <summary>
/// Implementation of saga correlation configuration.
/// </summary>
public class SagaCorrelationConfiguration : IConfigureHowToFindSagaWithMessage
{
    public IConfigureHowToFindSagaWithMessage CorrelateMessage<TMessage, TSagaData, TProperty>(
        Func<TMessage, TProperty> messagePropertyExtractor,
        Func<TSagaData, TProperty> sagaDataPropertyExtractor)
        where TSagaData : IContainSagaData
    {
        // Store correlation configuration
        // In a full implementation, this would be used by the orchestrator
        return this;
    }

    public IConfigureHowToFindSagaWithMessage CorrelateByCorrelationId<TMessage>(
        Func<TMessage, string> correlationIdExtractor)
    {
        // Store correlation ID extractor
        // In a full implementation, this would be used by the orchestrator
        return this;
    }
}
