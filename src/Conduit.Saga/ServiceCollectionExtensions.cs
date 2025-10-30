using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Conduit.Saga;

/// <summary>
/// Extension methods for configuring saga services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Conduit saga services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConduitSaga(
        this IServiceCollection services,
        Action<SagaConfiguration>? configure = null)
    {
        var configuration = new SagaConfiguration();
        configure?.Invoke(configuration);

        // Register saga orchestrator
        services.TryAddSingleton<ISagaOrchestrator, SagaOrchestrator>();

        // Register saga persister
        if (configuration.UseInMemoryPersister)
        {
            services.TryAddSingleton<ISagaPersister, InMemorySagaPersister>();
        }

        return services;
    }

    /// <summary>
    /// Registers a saga type with the orchestrator.
    /// </summary>
    /// <typeparam name="TSaga">The saga type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSaga<TSaga>(this IServiceCollection services)
        where TSaga : Saga
    {
        services.AddTransient<TSaga>();

        // Register with orchestrator when it's built
        services.AddSingleton<ISagaRegistration>(sp =>
        {
            var orchestrator = sp.GetRequiredService<ISagaOrchestrator>();
            orchestrator.RegisterSaga<TSaga>();
            return new SagaRegistration<TSaga>();
        });

        return services;
    }

    /// <summary>
    /// Adds a custom saga persister.
    /// </summary>
    /// <typeparam name="TPersister">The persister type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSagaPersister<TPersister>(this IServiceCollection services)
        where TPersister : class, ISagaPersister
    {
        services.AddSingleton<ISagaPersister, TPersister>();
        return services;
    }
}

/// <summary>
/// Configuration for saga services.
/// </summary>
public class SagaConfiguration
{
    /// <summary>
    /// Use in-memory saga persister (default: true for testing).
    /// </summary>
    public bool UseInMemoryPersister { get; set; } = true;
}

/// <summary>
/// Marker interface for saga registration.
/// </summary>
public interface ISagaRegistration
{
}

/// <summary>
/// Saga registration marker.
/// </summary>
internal class SagaRegistration<TSaga> : ISagaRegistration where TSaga : Saga
{
}
