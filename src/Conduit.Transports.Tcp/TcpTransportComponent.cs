using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Transports.Core;
using Conduit.Transports.Tcp;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp;

/// <summary>
/// TCP transport component for Conduit framework integration.
/// Manages TCP network transport for message transmission.
/// </summary>
public class TcpTransportComponent : AbstractPluggableComponent
{
    private readonly TcpTransport _tcpTransport;
    private readonly TcpConfiguration _configuration;
    private readonly ILogger<TcpTransportComponent> _logger;

    public TcpTransportComponent(
        TcpTransport tcpTransport,
        TcpConfiguration configuration,
        ILogger<TcpTransportComponent> logger) : base(logger)
    {
        _tcpTransport = tcpTransport ?? throw new ArgumentNullException(nameof(tcpTransport));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.transport.tcp",
            Name = "Conduit.Transport.TCP",
            Version = "0.8.2",
            Description = "TCP/Socket network transport for reliable message transmission in the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "transport", "tcp", "socket", "network", "reliable", "connection-oriented" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TCP transport component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TCP transport component '{Name}' starting", Name);

        // Start the TCP transport
        await _tcpTransport.ConnectAsync(cancellationToken);

        var mode = _configuration.IsServer ? "server" : "client";
        var endpoint = _configuration.IsServer
            ? $"{_configuration.Host}:{_configuration.Port}"
            : $"{_configuration.RemoteHost}:{_configuration.RemotePort}";

        Logger.LogInformation("TCP transport component '{Name}' started in {Mode} mode on {Endpoint}",
            Name, mode, endpoint);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("TCP transport component '{Name}' stopping", Name);

        // Stop the TCP transport
        try
        {
            await _tcpTransport.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error stopping TCP transport");
        }

        Logger.LogInformation("TCP transport component '{Name}' stopped", Name);
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("TCP transport component '{Name}' disposing", Name);

        // Dispose the TCP transport
        try
        {
            _tcpTransport?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing TCP transport");
        }

        Logger.LogInformation("TCP transport component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "TcpTransport",
                Name = "TCP Transport",
                Description = "Reliable TCP/Socket based message transport",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "TcpServer",
                Name = "TCP Server",
                Description = "TCP server mode for accepting incoming connections",
                Version = Version,
                IsEnabledByDefault = _configuration.IsServer
            },
            new ComponentFeature
            {
                Id = "TcpClient",
                Name = "TCP Client",
                Description = "TCP client mode for connecting to remote servers",
                Version = Version,
                IsEnabledByDefault = !_configuration.IsServer
            },
            new ComponentFeature
            {
                Id = "MessageFraming",
                Name = "Message Framing",
                Description = "Length-prefixed message framing for reliable message boundaries",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "ConnectionPooling",
                Name = "Connection Pooling",
                Description = "Connection pooling for efficient resource utilization",
                Version = Version,
                IsEnabledByDefault = !_configuration.IsServer
            },
            new ComponentFeature
            {
                Id = "KeepAlive",
                Name = "Keep Alive",
                Description = "TCP keep-alive for connection health monitoring",
                Version = Version,
                IsEnabledByDefault = _configuration.UseKeepAlive
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(TcpTransport),
                ImplementationType = _tcpTransport.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _tcpTransport
            },
            new ServiceContract
            {
                ServiceType = typeof(TcpConfiguration),
                ImplementationType = _configuration.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _configuration
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var transportHealthy = _tcpTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _tcpTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["TcpTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected,
            ["Mode"] = _configuration?.IsServer == true ? "Server" : "Client",
            ["Host"] = _configuration?.Host ?? "Unknown",
            ["Port"] = _configuration?.Port ?? 0,
            ["RemoteHost"] = _configuration?.RemoteHost ?? "N/A",
            ["RemotePort"] = _configuration?.RemotePort ?? 0,
            ["KeepAlive"] = _configuration?.UseKeepAlive ?? false,
            ["MaxConnections"] = _configuration?.MaxConnections ?? 0
        };

        ComponentHealth health;
        if (isHealthy)
        {
            health = ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            health = ComponentHealth.Degraded(Id, "TCP transport not connected", data: healthData);
        }
        else
        {
            health = ComponentHealth.Unhealthy(Id, "TCP transport or configuration unavailable", data: healthData);
        }

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var transportHealthy = _tcpTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _tcpTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["TcpTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected,
            ["Mode"] = _configuration?.IsServer == true ? "Server" : "Client"
        };

        if (isHealthy)
        {
            return ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            return ComponentHealth.Degraded(Id, "TCP transport not connected", data: healthData);
        }
        else
        {
            return ComponentHealth.Unhealthy(Id, "TCP transport or configuration unavailable", data: healthData);
        }
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("tcp_transport_available", _tcpTransport != null ? 1 : 0);
        metrics.SetCounter("tcp_configuration_available", _configuration != null ? 1 : 0);
        metrics.SetCounter("tcp_connected", _tcpTransport?.IsConnected == true ? 1 : 0);
        metrics.SetCounter("tcp_server_mode", _configuration?.IsServer == true ? 1 : 0);
        metrics.SetCounter("tcp_client_mode", _configuration?.IsServer == false ? 1 : 0);
        metrics.SetCounter("tcp_keep_alive_enabled", _configuration?.UseKeepAlive == true ? 1 : 0);
        metrics.SetGauge("tcp_max_connections", _configuration?.MaxConnections ?? 0);
        metrics.SetGauge("tcp_port", _configuration?.Port ?? 0);
        metrics.SetGauge("tcp_remote_port", _configuration?.RemotePort ?? 0);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the TCP transport.
    /// </summary>
    public TcpTransport GetTcpTransport() => _tcpTransport;

    /// <summary>
    /// Gets the TCP configuration.
    /// </summary>
    public new TcpConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Gets whether the TCP transport is connected.
    /// </summary>
    public bool IsConnected => _tcpTransport.IsConnected;

    /// <summary>
    /// Gets the transport connection statistics.
    /// </summary>
    public TransportStatistics GetStatistics()
    {
        return _tcpTransport.GetStatistics();
    }

    /// <summary>
    /// Sends a message through the TCP transport.
    /// </summary>
    public async Task SendMessageAsync(
        IMessage message,
        string? destination = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _tcpTransport.SendAsync(message, destination ?? "", cancellationToken);
    }

    /// <summary>
    /// Creates a subscription to receive messages from the TCP transport.
    /// </summary>
    public async Task<ITransportSubscription> SubscribeAsync(
        string? source = null,
        Func<TransportMessage, Task>? handler = null,
        CancellationToken cancellationToken = default)
    {
        if (handler == null)
        {
            handler = _ => Task.CompletedTask;
        }

        return await _tcpTransport.SubscribeAsync(source ?? "", handler, cancellationToken);
    }
}