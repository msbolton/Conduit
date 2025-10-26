using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Conduit.Core;
using Conduit.Messaging;
using Conduit.Components;
using Conduit.Pipeline;
using Conduit.Serialization;
using Conduit.Security;
using Conduit.Resilience;

namespace Conduit.Application
{
    /// <summary>
    /// Extension methods for IServiceCollection to register Conduit services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Conduit services to the service collection.
        /// </summary>
        public static IServiceCollection AddConduitServices(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Core services
            services.AddConduitCore(configuration);

            // Component system
            services.AddConduitComponents(configuration);

            // Messaging
            if (configuration.Messaging.Enabled)
            {
                services.AddConduitMessaging(configuration);
            }

            // Serialization
            services.AddConduitSerialization();

            // Security
            if (configuration.Security.EnableAuthentication || configuration.Security.EnableAuthorization)
            {
                services.AddConduitSecurity(configuration);
            }

            // Resilience
            if (configuration.Resilience.EnableCircuitBreaker ||
                configuration.Resilience.EnableRetry ||
                configuration.Resilience.EnableTimeout)
            {
                services.AddConduitResilience(configuration);
            }

            return services;
        }

        /// <summary>
        /// Adds Conduit core services.
        /// </summary>
        public static IServiceCollection AddConduitCore(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            // Component registry
            services.TryAddSingleton<IComponentRegistry, ComponentRegistry>();

            // Component lifecycle manager
            services.TryAddSingleton<IComponentLifecycleManager, ComponentLifecycleManager>();

            // Metrics collector
            services.TryAddSingleton<IMetricsCollector, DefaultMetricsCollector>();

            return services;
        }

        /// <summary>
        /// Adds Conduit component system.
        /// </summary>
        public static IServiceCollection AddConduitComponents(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            // Component factory
            services.TryAddSingleton<IComponentFactory, ComponentFactory>();

            // Component container
            services.TryAddSingleton<IComponentContainer, ComponentContainer>();

            return services;
        }

        /// <summary>
        /// Adds Conduit messaging services.
        /// </summary>
        public static IServiceCollection AddConduitMessaging(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            // Message bus
            services.TryAddSingleton<IMessageBus, MessageBus>();

            // Handler registry
            services.TryAddSingleton<IHandlerRegistry, HandlerRegistry>();

            // Subscription manager
            services.TryAddSingleton<ISubscriptionManager, SubscriptionManager>();

            // Message correlator
            services.TryAddSingleton<IMessageCorrelator, MessageCorrelator>();

            // Dead letter queue
            services.TryAddSingleton<IDeadLetterQueue, DeadLetterQueue>();

            // Flow controller
            if (configuration.Messaging.EnableFlowControl)
            {
                services.TryAddSingleton<IFlowController>(sp =>
                    new FlowController(
                        maxConcurrent: configuration.Messaging.MaxConcurrentMessages,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FlowController>>()));
            }

            return services;
        }

        /// <summary>
        /// Adds Conduit serialization services.
        /// </summary>
        public static IServiceCollection AddConduitSerialization(this IServiceCollection services)
        {
            // Serializer registry
            services.TryAddSingleton<ISerializerRegistry, SerializerRegistry>();

            // JSON serializer
            services.TryAddSingleton<IMessageSerializer>(sp =>
                new JsonMessageSerializer());

            // MessagePack serializer
            services.TryAddSingleton<IMessageSerializer>(sp =>
                new MessagePackMessageSerializer());

            return services;
        }

        /// <summary>
        /// Adds Conduit security services.
        /// </summary>
        public static IServiceCollection AddConduitSecurity(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            // Authentication provider
            if (configuration.Security.EnableAuthentication &&
                !string.IsNullOrEmpty(configuration.Security.JwtSecretKey))
            {
                services.TryAddSingleton<IAuthenticationProvider>(sp =>
                    new JwtAuthenticationProvider(
                        configuration.Security.JwtSecretKey!,
                        configuration.Security.JwtIssuer ?? "Conduit",
                        configuration.Security.JwtAudience ?? "Conduit",
                        TimeSpan.FromMinutes(configuration.Security.JwtExpirationMinutes),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JwtAuthenticationProvider>>()));
            }

            // Encryption service
            if (configuration.Security.EnableEncryption &&
                !string.IsNullOrEmpty(configuration.Security.EncryptionKey))
            {
                services.TryAddSingleton<IEncryptionService>(sp =>
                    new AesEncryptionService(
                        configuration.Security.EncryptionKey!,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AesEncryptionService>>()));
            }

            // Access control
            if (configuration.Security.EnableAuthorization)
            {
                services.TryAddSingleton<IAccessControl>(sp =>
                    new AccessControl(
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AccessControl>>()));
            }

            return services;
        }

        /// <summary>
        /// Adds Conduit resilience services.
        /// </summary>
        public static IServiceCollection AddConduitResilience(
            this IServiceCollection services,
            ConduitConfiguration configuration)
        {
            // Resilience policy registry
            services.TryAddSingleton<IResiliencePolicyRegistry, ResiliencePolicyRegistry>();

            // Add default policies
            services.AddSingleton(sp =>
            {
                var registry = sp.GetRequiredService<IResiliencePolicyRegistry>();

                // Circuit breaker
                if (configuration.Resilience.EnableCircuitBreaker)
                {
                    var cbPolicy = ResiliencePolicyRegistry.CreateCircuitBreaker(
                        "default-circuit-breaker",
                        configuration.Resilience.CircuitBreakerFailureThreshold,
                        TimeSpan.FromMilliseconds(configuration.Resilience.CircuitBreakerTimeoutMs));
                    registry.Register("default-circuit-breaker", cbPolicy);
                }

                // Retry
                if (configuration.Resilience.EnableRetry)
                {
                    var retryPolicy = ResiliencePolicyRegistry.CreateRetry(
                        "default-retry",
                        configuration.Resilience.DefaultRetryCount,
                        Conduit.Resilience.RetryStrategy.Exponential);
                    registry.Register("default-retry", retryPolicy);
                }

                // Timeout
                if (configuration.Resilience.EnableTimeout)
                {
                    var timeoutPolicy = ResiliencePolicyRegistry.CreateTimeout(
                        "default-timeout",
                        TimeSpan.FromMilliseconds(configuration.Resilience.DefaultTimeoutMs));
                    registry.Register("default-timeout", timeoutPolicy);
                }

                return registry;
            });

            return services;
        }

        /// <summary>
        /// Adds a command handler.
        /// </summary>
        public static IServiceCollection AddCommandHandler<TCommand, TResponse, THandler>(
            this IServiceCollection services)
            where TCommand : ICommand<TResponse>
            where THandler : class, ICommandHandler<TCommand, TResponse>
        {
            services.TryAddTransient<ICommandHandler<TCommand, TResponse>, THandler>();
            return services;
        }

        /// <summary>
        /// Adds an event handler.
        /// </summary>
        public static IServiceCollection AddEventHandler<TEvent, THandler>(
            this IServiceCollection services)
            where TEvent : IEvent
            where THandler : class, IEventHandler<TEvent>
        {
            services.TryAddTransient<IEventHandler<TEvent>, THandler>();
            return services;
        }

        /// <summary>
        /// Adds a query handler.
        /// </summary>
        public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(
            this IServiceCollection services)
            where TQuery : IQuery<TResult>
            where THandler : class, IQueryHandler<TQuery, TResult>
        {
            services.TryAddTransient<IQueryHandler<TQuery, TResult>, THandler>();
            return services;
        }
    }
}
