using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conduit.Serialization;

/// <summary>
/// Serialization component for Conduit framework integration.
/// Manages multiple serialization formats and provides unified serialization services.
/// </summary>
public class SerializationComponent : AbstractPluggableComponent
{
    private readonly ISerializerRegistry _serializerRegistry;
    private readonly ILogger<SerializationComponent> _logger;
    private readonly SerializationOptions _options;

    public SerializationComponent(
        ISerializerRegistry serializerRegistry,
        ILogger<SerializationComponent> logger,
        IOptions<SerializationOptions>? options = null) : base(logger)
    {
        _serializerRegistry = serializerRegistry ?? throw new ArgumentNullException(nameof(serializerRegistry));
        _logger = logger;
        _options = options?.Value ?? new SerializationOptions();

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.serialization",
            Name = "Conduit.Serialization",
            Version = "0.9.0-alpha",
            Description = "Multi-format serialization support for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "serialization", "json", "messagepack", "formats", "codecs" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Serialization component '{Name}' initialized with {SerializerCount} serializers",
            Name, _serializerRegistry.GetAllSerializers().Count());
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Serialization component '{Name}' started", Name);

        // Log available serializers
        var serializers = _serializerRegistry.GetAllSerializers();
        foreach (var serializer in serializers)
        {
            Logger.LogDebug("Available serializer: {SerializerType} for mime type: {MimeType}",
                serializer.GetType().Name, serializer.MimeType);
        }

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Serialization component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Serialization component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        var features = new List<ComponentFeature>
        {
            new ComponentFeature
            {
                Id = "JsonSerialization",
                Name = "JSON Serialization",
                Description = "System.Text.Json based serialization support",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "MessagePackSerialization",
                Name = "MessagePack Serialization",
                Description = "Binary MessagePack serialization support",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SerializerRegistry",
                Name = "Serializer Registry",
                Description = "Multi-format serializer registry and content type negotiation",
                Version = Version,
                IsEnabledByDefault = true
            }
        };

        return features;
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(ISerializerRegistry),
                ImplementationType = _serializerRegistry.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _serializerRegistry
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var serializerCount = _serializerRegistry.GetAllSerializers().Count();
        var isHealthy = serializerCount > 0;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["SerializerCount"] = serializerCount,
            ["DefaultFormat"] = _serializerRegistry.DefaultFormat.ToString()
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "No serializers registered", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var serializerCount = _serializerRegistry.GetAllSerializers().Count();
        var isHealthy = serializerCount > 0;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["SerializerCount"] = serializerCount,
            ["DefaultFormat"] = _serializerRegistry.DefaultFormat.ToString()
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Unhealthy(Id, "No serializers registered", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("serializers_registered", _serializerRegistry.GetAllSerializers().Count());
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the serializer registry.
    /// </summary>
    public ISerializerRegistry GetSerializerRegistry() => _serializerRegistry;
}