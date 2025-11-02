using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Transports.Core;
using Conduit.Transports.Udp;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Udp;

/// <summary>
/// UDP transport component for Conduit framework integration.
/// Manages UDP network transport for message transmission with multicast and broadcast support.
/// </summary>
public class UdpTransportComponent : AbstractPluggableComponent
{
    private readonly UdpTransport _udpTransport;
    private readonly UdpConfiguration _configuration;
    private readonly ILogger<UdpTransportComponent> _logger;

    public UdpTransportComponent(
        UdpTransport udpTransport,
        UdpConfiguration configuration,
        ILogger<UdpTransportComponent> logger) : base(logger)
    {
        _udpTransport = udpTransport ?? throw new ArgumentNullException(nameof(udpTransport));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.transport.udp",
            Name = "Conduit.Transport.UDP",
            Version = "0.8.2",
            Description = "UDP network transport with multicast and broadcast support for fast, connectionless message transmission in the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "transport", "udp", "multicast", "broadcast", "connectionless", "fast", "datagram" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("UDP transport component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("UDP transport component '{Name}' starting", Name);

        // Start the UDP transport
        await _udpTransport.ConnectAsync(cancellationToken);

        var mode = !string.IsNullOrEmpty(_configuration.MulticastGroup) ? "multicast" :
                   _configuration.AllowBroadcast ? "broadcast" : "unicast";
        var endpoint = $"{_configuration.Host}:{_configuration.Port}";

        Logger.LogInformation("UDP transport component '{Name}' started in {Mode} mode on {Endpoint}",
            Name, mode, endpoint);

        if (!string.IsNullOrEmpty(_configuration.MulticastGroup))
        {
            Logger.LogInformation("UDP transport joined multicast group {MulticastGroup} with TTL {TTL}",
                _configuration.MulticastGroup, _configuration.MulticastTimeToLive);
        }
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("UDP transport component '{Name}' stopping", Name);

        // Stop the UDP transport
        try
        {
            await _udpTransport.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error stopping UDP transport");
        }

        Logger.LogInformation("UDP transport component '{Name}' stopped", Name);
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("UDP transport component '{Name}' disposing", Name);

        // Dispose the UDP transport
        try
        {
            _udpTransport?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error disposing UDP transport");
        }

        Logger.LogInformation("UDP transport component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "UdpTransport",
                Name = "UDP Transport",
                Description = "Fast, connectionless UDP datagram transport",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MulticastSupport",
                Name = "Multicast Support",
                Description = "IP multicast for one-to-many communication",
                Version = Version,
                IsEnabledByDefault = !string.IsNullOrEmpty(_configuration.MulticastGroup)
            },
            new ComponentFeature
            {
                Id = "BroadcastSupport",
                Name = "Broadcast Support",
                Description = "Network broadcast for local subnet communication",
                Version = Version,
                IsEnabledByDefault = _configuration.AllowBroadcast
            },
            new ComponentFeature
            {
                Id = "IPv6Support",
                Name = "IPv6 Support",
                Description = "Internet Protocol version 6 support",
                Version = Version,
                IsEnabledByDefault = _configuration.UseIPv6
            },
            new ComponentFeature
            {
                Id = "Fragmentation",
                Name = "Message Fragmentation",
                Description = "Automatic fragmentation for large messages exceeding datagram size",
                Version = Version,
                IsEnabledByDefault = _configuration.EnableFragmentation
            },
            new ComponentFeature
            {
                Id = "Acknowledgement",
                Name = "Message Acknowledgement",
                Description = "Optional acknowledgement mechanism for reliable delivery",
                Version = Version,
                IsEnabledByDefault = _configuration.RequireAcknowledgement
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(UdpTransport),
                ImplementationType = _udpTransport.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _udpTransport
            },
            new ServiceContract
            {
                ServiceType = typeof(UdpConfiguration),
                ImplementationType = _configuration.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _configuration
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var transportHealthy = _udpTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _udpTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["UdpTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected,
            ["Host"] = _configuration?.Host ?? "Unknown",
            ["Port"] = _configuration?.Port ?? 0,
            ["RemoteHost"] = _configuration?.RemoteHost ?? "N/A",
            ["RemotePort"] = _configuration?.RemotePort ?? 0,
            ["MulticastGroup"] = _configuration?.MulticastGroup ?? "N/A",
            ["AllowBroadcast"] = _configuration?.AllowBroadcast ?? false,
            ["UseIPv6"] = _configuration?.UseIPv6 ?? false,
            ["EnableFragmentation"] = _configuration?.EnableFragmentation ?? false,
            ["RequireAcknowledgement"] = _configuration?.RequireAcknowledgement ?? false,
            ["MaxDatagramSize"] = _configuration?.MaxDatagramSize ?? 0
        };

        ComponentHealth health;
        if (isHealthy)
        {
            health = ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            health = ComponentHealth.Degraded(Id, "UDP transport not connected", data: healthData);
        }
        else
        {
            health = ComponentHealth.Unhealthy(Id, "UDP transport or configuration unavailable", data: healthData);
        }

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var transportHealthy = _udpTransport != null;
        var configurationHealthy = _configuration != null;
        var isConnected = _udpTransport?.IsConnected ?? false;

        var isHealthy = transportHealthy && configurationHealthy && isConnected;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["UdpTransport"] = transportHealthy ? "Available" : "Unavailable",
            ["Configuration"] = configurationHealthy ? "Available" : "Unavailable",
            ["IsConnected"] = isConnected
        };

        if (isHealthy)
        {
            return ComponentHealth.Healthy(Id, healthData);
        }
        else if (transportHealthy && configurationHealthy && !isConnected)
        {
            return ComponentHealth.Degraded(Id, "UDP transport not connected", data: healthData);
        }
        else
        {
            return ComponentHealth.Unhealthy(Id, "UDP transport or configuration unavailable", data: healthData);
        }
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("udp_transport_available", _udpTransport != null ? 1 : 0);
        metrics.SetCounter("udp_configuration_available", _configuration != null ? 1 : 0);
        metrics.SetCounter("udp_connected", _udpTransport?.IsConnected == true ? 1 : 0);
        metrics.SetCounter("udp_multicast_enabled", !string.IsNullOrEmpty(_configuration?.MulticastGroup) ? 1 : 0);
        metrics.SetCounter("udp_broadcast_enabled", _configuration?.AllowBroadcast == true ? 1 : 0);
        metrics.SetCounter("udp_ipv6_enabled", _configuration?.UseIPv6 == true ? 1 : 0);
        metrics.SetCounter("udp_fragmentation_enabled", _configuration?.EnableFragmentation == true ? 1 : 0);
        metrics.SetCounter("udp_acknowledgement_enabled", _configuration?.RequireAcknowledgement == true ? 1 : 0);
        metrics.SetGauge("udp_port", _configuration?.Port ?? 0);
        metrics.SetGauge("udp_remote_port", _configuration?.RemotePort ?? 0);
        metrics.SetGauge("udp_max_datagram_size", _configuration?.MaxDatagramSize ?? 0);
        metrics.SetGauge("udp_receive_buffer_size", _configuration?.ReceiveBufferSize ?? 0);
        metrics.SetGauge("udp_send_buffer_size", _configuration?.SendBufferSize ?? 0);
        metrics.SetGauge("udp_multicast_ttl", _configuration?.MulticastTimeToLive ?? 0);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the UDP transport.
    /// </summary>
    public UdpTransport GetUdpTransport() => _udpTransport;

    /// <summary>
    /// Gets the UDP configuration.
    /// </summary>
    public new UdpConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Gets whether the UDP transport is connected.
    /// </summary>
    public bool IsConnected => _udpTransport.IsConnected;

    /// <summary>
    /// Gets the transport connection statistics.
    /// </summary>
    public TransportStatistics GetStatistics()
    {
        return _udpTransport.GetStatistics();
    }

    /// <summary>
    /// Sends a message through the UDP transport.
    /// </summary>
    public async Task SendMessageAsync(
        IMessage message,
        string? destination = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _udpTransport.SendAsync(message, destination ?? "", cancellationToken);
    }

    /// <summary>
    /// Creates a subscription to receive messages from the UDP transport.
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

        return await _udpTransport.SubscribeAsync(source ?? "", handler, cancellationToken);
    }

    /// <summary>
    /// Gets the connection string representation of the UDP configuration.
    /// </summary>
    public string GetConnectionString()
    {
        return _configuration.BuildConnectionString();
    }

    /// <summary>
    /// Gets information about the multicast configuration if enabled.
    /// </summary>
    public MulticastInfo? GetMulticastInfo()
    {
        if (string.IsNullOrEmpty(_configuration.MulticastGroup))
            return null;

        return new MulticastInfo
        {
            Group = _configuration.MulticastGroup,
            Interface = _configuration.MulticastInterface,
            TimeToLive = _configuration.MulticastTimeToLive,
            Loopback = _configuration.MulticastLoopback
        };
    }
}

/// <summary>
/// Information about multicast configuration.
/// </summary>
public class MulticastInfo
{
    /// <summary>
    /// Gets or sets the multicast group address.
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local interface address (null for default).
    /// </summary>
    public string? Interface { get; set; }

    /// <summary>
    /// Gets or sets the time-to-live (TTL) value.
    /// </summary>
    public byte TimeToLive { get; set; }

    /// <summary>
    /// Gets or sets whether multicast loopback is enabled.
    /// </summary>
    public bool Loopback { get; set; }
}