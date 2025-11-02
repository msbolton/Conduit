using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Transports.ActiveMq;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.ActiveMq;

/// <summary>
/// ActiveMQ transport component for Conduit framework integration.
/// Manages ActiveMQ Artemis message broker integration with AMQP 1.0 support.
/// </summary>
public class ActiveMqTransportComponent : AbstractPluggableComponent
{
    private readonly ActiveMqTransport _activeMqTransport;
    private readonly ActiveMqConfiguration _configuration;
    private readonly ILogger<ActiveMqTransportComponent> _logger;

    public ActiveMqTransportComponent(
        ActiveMqTransport activeMqTransport,
        ActiveMqConfiguration configuration,
        ILogger<ActiveMqTransportComponent> logger) : base(logger)
    {
        _activeMqTransport = activeMqTransport ?? throw new ArgumentNullException(nameof(activeMqTransport));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.transport.activemq",
            Name = "Conduit.Transport.ActiveMQ",
            Version = "0.9.0-alpha",
            Description = "ActiveMQ Artemis message broker transport with AMQP 1.0 support for enterprise-grade messaging in the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "transport", "activemq", "amqp", "message-broker", "enterprise", "reliable", "persistent" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("ActiveMQ transport component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("ActiveMQ transport component '{Name}' starting", Name);

        // Start the ActiveMQ transport
        await _activeMqTransport.ConnectAsync(cancellationToken);

        Logger.LogInformation("ActiveMQ transport component '{Name}' started, connected to broker at {BrokerUri}",
            Name, _configuration.BrokerUri);

        Logger.LogDebug("ActiveMQ connection settings: AckMode={AckMode}, Persistent={Persistent}, AsyncSend={AsyncSend}",
            _configuration.AcknowledgementMode,
            _configuration.PersistentDelivery,
            _configuration.AsyncSend);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("ActiveMQ transport component '{Name}' stopping", Name);

        // Stop the ActiveMQ transport
        try
        {
            await _activeMqTransport.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error stopping ActiveMQ transport");
        }

        Logger.LogInformation("ActiveMQ transport component '{Name}' stopped", Name);
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("ActiveMQ transport component '{Name}' disposing", Name);

        // Dispose the ActiveMQ transport
        try
        {
            _activeMqTransport?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing ActiveMQ transport");
        }

        Logger.LogInformation("ActiveMQ transport component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "ActiveMqTransport",
                Name = "ActiveMQ Transport",
                Description = "Enterprise-grade message broker transport with AMQP 1.0",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "QueueSupport",
                Name = "Queue Support",
                Description = "Point-to-point messaging with persistent queues",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "TopicSupport",
                Name = "Topic Support",
                Description = "Publish-subscribe messaging with topics",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "PersistentMessaging",
                Name = "Persistent Messaging",
                Description = "Message persistence for durability and recovery",
                Version = Version,
                IsEnabledByDefault = _configuration.PersistentDelivery
            },
            new ComponentFeature
            {
                Id = "TransactionalMessaging",
                Name = "Transactional Messaging",
                Description = "ACID transactional message processing",
                Version = Version,
                IsEnabledByDefault = _configuration.AcknowledgementMode == AcknowledgementMode.Transactional
            },
            new ComponentFeature
            {
                Id = "MessagePriority",
                Name = "Message Priority",
                Description = "Priority-based message ordering and delivery",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MessageExpiration",
                Name = "Message Expiration",
                Description = "Time-to-live (TTL) based message expiration",
                Version = Version,
                IsEnabledByDefault = _configuration.DefaultTimeToLive > 0
            },
            new ComponentFeature
            {
                Id = "DeadLetterHandling",
                Name = "Dead Letter Handling",
                Description = "Automatic dead letter queue routing for failed messages",
                Version = Version,
                IsEnabledByDefault = _configuration.MaxRedeliveryAttempts > 0
            },
            new ComponentFeature
            {
                Id = "MessageCompression",
                Name = "Message Compression",
                Description = "Automatic message compression for bandwidth optimization",
                Version = Version,
                IsEnabledByDefault = _configuration.UseCompression
            },
            new ComponentFeature
            {
                Id = "TemporaryDestinations",
                Name = "Temporary Destinations",
                Description = "Dynamic temporary queues and topics for request-reply patterns",
                Version = Version,
                IsEnabledByDefault = _configuration.AllowTemporaryQueues || _configuration.AllowTemporaryTopics
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(ActiveMqTransport),
                ImplementationType = _activeMqTransport.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _activeMqTransport
            },
            new ServiceContract
            {
                ServiceType = typeof(ActiveMqConfiguration),
                ImplementationType = _configuration.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _configuration
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var transportHealthy = _activeMqTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _activeMqTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ActiveMqTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected,
            ["BrokerUri"] = _configuration?.BrokerUri ?? "Unknown",
            ["ClientId"] = _configuration?.ClientId ?? "N/A",
            ["AcknowledgementMode"] = _configuration?.AcknowledgementMode.ToString() ?? "Unknown",
            ["PersistentDelivery"] = _configuration?.PersistentDelivery ?? false,
            ["AsyncSend"] = _configuration?.AsyncSend ?? false,
            ["UseCompression"] = _configuration?.UseCompression ?? false,
            ["PrefetchPolicy"] = _configuration?.PrefetchPolicy ?? 0,
            ["SendTimeout"] = _configuration?.SendTimeout ?? 0,
            ["RequestTimeout"] = _configuration?.RequestTimeout ?? 0,
            ["MaxRedeliveryAttempts"] = _configuration?.MaxRedeliveryAttempts ?? 0
        };

        ComponentHealth health;
        if (isHealthy)
        {
            health = ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            health = ComponentHealth.Degraded(Id, "ActiveMQ transport not connected to broker", data: healthData);
        }
        else
        {
            health = ComponentHealth.Unhealthy(Id, "ActiveMQ transport or configuration unavailable", data: healthData);
        }

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var transportHealthy = _activeMqTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _activeMqTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["ActiveMqTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected,
            ["BrokerUri"] = _configuration?.BrokerUri ?? "Unknown"
        };

        if (isHealthy)
        {
            return ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            return ComponentHealth.Degraded(Id, "ActiveMQ transport not connected to broker", data: healthData);
        }
        else
        {
            return ComponentHealth.Unhealthy(Id, "ActiveMQ transport or configuration unavailable", data: healthData);
        }
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("activemq_transport_available", _activeMqTransport != null ? 1 : 0);
        metrics.SetCounter("activemq_configuration_available", _configuration != null ? 1 : 0);
        metrics.SetCounter("activemq_connected", _activeMqTransport?.IsConnected == true ? 1 : 0);
        metrics.SetCounter("activemq_persistent_delivery", _configuration?.PersistentDelivery == true ? 1 : 0);
        metrics.SetCounter("activemq_async_send", _configuration?.AsyncSend == true ? 1 : 0);
        metrics.SetCounter("activemq_compression_enabled", _configuration?.UseCompression == true ? 1 : 0);
        metrics.SetCounter("activemq_temporary_queues_allowed", _configuration?.AllowTemporaryQueues == true ? 1 : 0);
        metrics.SetCounter("activemq_temporary_topics_allowed", _configuration?.AllowTemporaryTopics == true ? 1 : 0);
        metrics.SetGauge("activemq_prefetch_policy", _configuration?.PrefetchPolicy ?? 0);
        metrics.SetGauge("activemq_send_timeout", _configuration?.SendTimeout ?? 0);
        metrics.SetGauge("activemq_request_timeout", _configuration?.RequestTimeout ?? 0);
        metrics.SetGauge("activemq_default_priority", _configuration?.DefaultMessagePriority ?? 0);
        metrics.SetGauge("activemq_default_ttl", _configuration?.DefaultTimeToLive ?? 0);
        metrics.SetGauge("activemq_max_redelivery_attempts", _configuration?.MaxRedeliveryAttempts ?? 0);
        metrics.SetGauge("activemq_redelivery_delay", _configuration?.RedeliveryDelay ?? 0);
        metrics.SetGauge("activemq_acknowledgement_mode", (int)(_configuration?.AcknowledgementMode ?? AcknowledgementMode.AutoAcknowledge));
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the ActiveMQ transport.
    /// </summary>
    public ActiveMqTransport GetActiveMqTransport() => _activeMqTransport;

    /// <summary>
    /// Gets the ActiveMQ configuration.
    /// </summary>
    public new ActiveMqConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Gets whether the ActiveMQ transport is connected to the broker.
    /// </summary>
    public bool IsConnected => _activeMqTransport.IsConnected;

    /// <summary>
    /// Gets the transport connection statistics.
    /// </summary>
    public TransportStatistics GetStatistics()
    {
        return _activeMqTransport.GetStatistics();
    }

    /// <summary>
    /// Sends a message through the ActiveMQ transport to a queue.
    /// </summary>
    public async Task SendToQueueAsync(
        IMessage message,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        var destination = $"queue://{queueName}";
        await _activeMqTransport.SendAsync(message, destination, cancellationToken);
    }

    /// <summary>
    /// Sends a message through the ActiveMQ transport to a topic.
    /// </summary>
    public async Task SendToTopicAsync(
        IMessage message,
        string topicName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(topicName);

        var destination = $"topic://{topicName}";
        await _activeMqTransport.SendAsync(message, destination, cancellationToken);
    }

    /// <summary>
    /// Creates a subscription to receive messages from a queue.
    /// </summary>
    public async Task<ITransportSubscription> SubscribeToQueueAsync(
        string queueName,
        Func<TransportMessage, Task>? handler = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        if (handler == null)
        {
            handler = _ => Task.CompletedTask;
        }

        var source = $"queue://{queueName}";
        return await _activeMqTransport.SubscribeAsync(source, handler, cancellationToken);
    }

    /// <summary>
    /// Creates a subscription to receive messages from a topic.
    /// </summary>
    public async Task<ITransportSubscription> SubscribeToTopicAsync(
        string topicName,
        Func<TransportMessage, Task>? handler = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(topicName);

        if (handler == null)
        {
            handler = _ => Task.CompletedTask;
        }

        var source = $"topic://{topicName}";
        return await _activeMqTransport.SubscribeAsync(source, handler, cancellationToken);
    }

    /// <summary>
    /// Gets the connection URI string for the ActiveMQ broker.
    /// </summary>
    public string GetConnectionUri()
    {
        return _configuration.BuildConnectionUri();
    }

    /// <summary>
    /// Gets information about the broker connection.
    /// </summary>
    public BrokerConnectionInfo GetBrokerConnectionInfo()
    {
        return new BrokerConnectionInfo
        {
            BrokerUri = _configuration.BrokerUri,
            ClientId = _configuration.ClientId,
            Username = _configuration.Username,
            IsConnected = _activeMqTransport.IsConnected,
            AcknowledgementMode = _configuration.AcknowledgementMode,
            PersistentDelivery = _configuration.PersistentDelivery,
            AsyncSend = _configuration.AsyncSend,
            UseCompression = _configuration.UseCompression,
            PrefetchPolicy = _configuration.PrefetchPolicy,
            SendTimeout = _configuration.SendTimeout,
            RequestTimeout = _configuration.RequestTimeout
        };
    }
}

/// <summary>
/// Information about the ActiveMQ broker connection.
/// </summary>
public class BrokerConnectionInfo
{
    /// <summary>
    /// Gets or sets the broker URI.
    /// </summary>
    public string BrokerUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the username (password excluded for security).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets whether the connection is active.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the acknowledgement mode.
    /// </summary>
    public AcknowledgementMode AcknowledgementMode { get; set; }

    /// <summary>
    /// Gets or sets whether persistent delivery is enabled.
    /// </summary>
    public bool PersistentDelivery { get; set; }

    /// <summary>
    /// Gets or sets whether async send is enabled.
    /// </summary>
    public bool AsyncSend { get; set; }

    /// <summary>
    /// Gets or sets whether compression is enabled.
    /// </summary>
    public bool UseCompression { get; set; }

    /// <summary>
    /// Gets or sets the prefetch policy.
    /// </summary>
    public int PrefetchPolicy { get; set; }

    /// <summary>
    /// Gets or sets the send timeout.
    /// </summary>
    public int SendTimeout { get; set; }

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public int RequestTimeout { get; set; }
}