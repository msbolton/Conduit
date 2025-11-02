using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Messaging;
using Microsoft.Extensions.Logging;

namespace Conduit.Messaging;

/// <summary>
/// Messaging component for Conduit framework integration.
/// Manages message bus, handler registry, subscriptions, and message routing.
/// </summary>
public class MessagingComponent : AbstractPluggableComponent
{
    private readonly IHandlerRegistry _handlerRegistry;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IFlowController _flowController;
    private readonly IMessageCorrelator _messageCorrelator;
    private readonly IDeadLetterQueue _deadLetterQueue;
    private readonly ILogger<MessagingComponent> _logger;

    public MessagingComponent(
        IHandlerRegistry handlerRegistry,
        ISubscriptionManager subscriptionManager,
        IFlowController flowController,
        IMessageCorrelator messageCorrelator,
        IDeadLetterQueue deadLetterQueue,
        ILogger<MessagingComponent> logger) : base(logger)
    {
        _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
        _subscriptionManager = subscriptionManager ?? throw new ArgumentNullException(nameof(subscriptionManager));
        _flowController = flowController ?? throw new ArgumentNullException(nameof(flowController));
        _messageCorrelator = messageCorrelator ?? throw new ArgumentNullException(nameof(messageCorrelator));
        _deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.messaging",
            Name = "Conduit.Messaging",
            Version = "0.9.0-alpha",
            Description = "Message bus and routing implementation for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "messaging", "message-bus", "handlers", "routing", "pubsub", "cqrs" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Messaging component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Messaging component '{Name}' started", Name);

        // Log component statistics
        var handlerStats = _handlerRegistry.GetStatistics();
        Logger.LogInformation(
            "Handler registry: {CommandHandlers} command handlers, {QueryHandlers} query handlers, {EventHandlers} event handlers",
            handlerStats.CommandHandlerCount,
            handlerStats.QueryHandlerCount,
            handlerStats.EventHandlerCount);

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Messaging component '{Name}' stopping", Name);

        // Graceful shutdown - dispose subscriptions and flow controller
        try
        {
            _subscriptionManager?.Dispose();
            _flowController?.Dispose();
            _messageCorrelator?.Dispose();
            _deadLetterQueue?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during messaging component shutdown");
        }

        Logger.LogInformation("Messaging component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Messaging component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "HandlerRegistry",
                Name = "Handler Registry",
                Description = "CQRS handler registration and management for commands, queries, and events",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SubscriptionManager",
                Name = "Subscription Manager",
                Description = "Message subscription and unsubscription management",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "FlowController",
                Name = "Flow Controller",
                Description = "Message flow control and throttling capabilities",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MessageCorrelation",
                Name = "Message Correlation",
                Description = "Request-response correlation and tracking",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "DeadLetterQueue",
                Name = "Dead Letter Queue",
                Description = "Failed message handling and recovery",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MessageRouting",
                Name = "Message Routing",
                Description = "Smart message routing based on type and content",
                Version = Version,
                IsEnabledByDefault = true
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(IHandlerRegistry),
                ImplementationType = _handlerRegistry.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _handlerRegistry
            },
            new ServiceContract
            {
                ServiceType = typeof(ISubscriptionManager),
                ImplementationType = _subscriptionManager.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _subscriptionManager
            },
            new ServiceContract
            {
                ServiceType = typeof(IFlowController),
                ImplementationType = _flowController.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _flowController
            },
            new ServiceContract
            {
                ServiceType = typeof(IMessageCorrelator),
                ImplementationType = _messageCorrelator.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _messageCorrelator
            },
            new ServiceContract
            {
                ServiceType = typeof(IDeadLetterQueue),
                ImplementationType = _deadLetterQueue.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _deadLetterQueue
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var handlerStats = _handlerRegistry.GetStatistics();
        var totalHandlers = handlerStats.CommandHandlerCount + handlerStats.QueryHandlerCount + handlerStats.EventHandlerCount;
        var isHealthy = totalHandlers > 0; // Component is healthy if handlers are registered

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["CommandHandlers"] = handlerStats.CommandHandlerCount,
            ["QueryHandlers"] = handlerStats.QueryHandlerCount,
            ["EventHandlers"] = handlerStats.EventHandlerCount,
            ["TotalHandlers"] = totalHandlers,
            ["FlowControllerActive"] = _flowController != null,
            ["CorrelatorActive"] = _messageCorrelator != null,
            ["DeadLetterQueueActive"] = _deadLetterQueue != null
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "No message handlers registered", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var handlerStats = _handlerRegistry.GetStatistics();
        var totalHandlers = handlerStats.CommandHandlerCount + handlerStats.QueryHandlerCount + handlerStats.EventHandlerCount;
        var isHealthy = totalHandlers > 0;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["CommandHandlers"] = handlerStats.CommandHandlerCount,
            ["QueryHandlers"] = handlerStats.QueryHandlerCount,
            ["EventHandlers"] = handlerStats.EventHandlerCount,
            ["TotalHandlers"] = totalHandlers
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "No message handlers registered", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        var handlerStats = _handlerRegistry.GetStatistics();

        metrics.SetCounter("command_handlers", handlerStats.CommandHandlerCount);
        metrics.SetCounter("query_handlers", handlerStats.QueryHandlerCount);
        metrics.SetCounter("event_handlers", handlerStats.EventHandlerCount);
        metrics.SetCounter("total_handlers", handlerStats.CommandHandlerCount + handlerStats.QueryHandlerCount + handlerStats.EventHandlerCount);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the handler registry.
    /// </summary>
    public IHandlerRegistry GetHandlerRegistry() => _handlerRegistry;

    /// <summary>
    /// Gets the subscription manager.
    /// </summary>
    public ISubscriptionManager GetSubscriptionManager() => _subscriptionManager;

    /// <summary>
    /// Gets the flow controller.
    /// </summary>
    public IFlowController GetFlowController() => _flowController;

    /// <summary>
    /// Gets the message correlator.
    /// </summary>
    public IMessageCorrelator GetMessageCorrelator() => _messageCorrelator;

    /// <summary>
    /// Gets the dead letter queue.
    /// </summary>
    public IDeadLetterQueue GetDeadLetterQueue() => _deadLetterQueue;
}