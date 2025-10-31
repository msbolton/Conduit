using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Metrics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conduit.Metrics;

/// <summary>
/// Metrics component for Conduit framework integration.
/// </summary>
public class MetricsComponent : IPluggableComponent
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly HealthCheckService _healthCheckService;
    private readonly MetricsReporter _reporter;
    private readonly MetricsConfiguration _configuration;
    private readonly ILogger<MetricsComponent> _logger;
    private readonly StartupHealthCheck? _startupHealthCheck;

    public MetricsComponent(
        IMetricsCollector metricsCollector,
        HealthCheckService healthCheckService,
        MetricsReporter reporter,
        IOptions<MetricsConfiguration> configuration,
        ILogger<MetricsComponent> logger,
        StartupHealthCheck? startupHealthCheck = null)
    {
        _metricsCollector = metricsCollector;
        _healthCheckService = healthCheckService;
        _reporter = reporter;
        _configuration = configuration.Value;
        _logger = logger;
        _startupHealthCheck = startupHealthCheck;

        Id = Guid.NewGuid().ToString();
        Name = "Conduit.Metrics";
        Version = "0.6.0";

        Manifest = new ComponentManifest
        {
            Id = Id,
            Name = Name,
            Version = Version,
            Description = Description,
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "metrics", "monitoring", "telemetry", "health-checks" }
        };

        IsolationRequirements = IsolationRequirements.Standard();
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Description { get; } = "Metrics and monitoring component for Conduit framework";
    public ComponentConfiguration? Configuration { get; set; }
    public ISecurityContext? SecurityContext { get; set; }

    public ComponentManifest Manifest { get; }
    public IsolationRequirements IsolationRequirements { get; }

    public Task InitializeAsync(ComponentConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Configuration = configuration;
        _logger.LogInformation("Metrics component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    public Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Metrics component");

        if (_configuration.Enabled)
        {
            _logger.LogInformation("Metrics collection enabled with provider: {Provider}", _configuration.Provider);
            _logger.LogInformation("Prometheus endpoint: {Endpoint}", _configuration.PrometheusEndpoint);
            _logger.LogInformation("Health check endpoint: {Endpoint}", _configuration.HealthCheckEndpoint);

            // Record initialization
            _metricsCollector.Increment("component_initializations_total",
                1.0,
                ("component", Name));
        }

        // Mark startup as ready
        _startupHealthCheck?.MarkAsReady();

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Metrics component");

        if (_configuration.EnableConsoleExporter && _logger.IsEnabled(LogLevel.Debug))
        {
            _reporter.ReportToConsole();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Metrics component");

        if (_configuration.EnableConsoleExporter)
        {
            _reporter.ReportToConsole();
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down Metrics component");
        return Task.CompletedTask;
    }


    /// <summary>
    /// Gets the metrics collector.
    /// </summary>
    public IMetricsCollector GetMetricsCollector() => _metricsCollector;

    /// <summary>
    /// Gets the health check service.
    /// </summary>
    public HealthCheckService GetHealthCheckService() => _healthCheckService;

    /// <summary>
    /// Gets the metrics reporter.
    /// </summary>
    public MetricsReporter GetReporter() => _reporter;

    public Task OnDetachAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Metrics component '{Name}' detached", Name);
        return Task.CompletedTask;
    }

    public IEnumerable<IBehaviorContribution> ContributeBehaviors()
    {
        return Array.Empty<IBehaviorContribution>();
    }

    public IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "Metrics",
                Name = "Metrics Collection",
                Description = "Collects and reports application metrics",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "HealthChecks",
                Name = "Health Checks",
                Description = "Provides health check capabilities",
                Version = Version,
                IsEnabledByDefault = true
            }
        };
    }

    public IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(IMetricsCollector),
                ImplementationType = _metricsCollector.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _metricsCollector
            },
            new ServiceContract
            {
                ServiceType = typeof(HealthCheckService),
                ImplementationType = _healthCheckService.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _healthCheckService
            }
        };
    }

    public IEnumerable<MessageHandlerRegistration> RegisterHandlers()
    {
        return Array.Empty<MessageHandlerRegistration>();
    }

    public bool IsCompatibleWith(string coreVersion)
    {
        return Version.CompareTo(coreVersion) >= 0;
    }

    public ComponentState GetState()
    {
        return ComponentState.Running;
    }

    public void OnDetach()
    {
        _logger.LogInformation("Metrics component '{Name}' detached (sync)", Name);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["Enabled"] = _configuration.Enabled
        };

        var health = ComponentHealth.Healthy(Id, healthData);
        return Task.FromResult(health);
    }

    public void Dispose()
    {
        _logger.LogInformation("Metrics component '{Name}' disposed", Name);
    }
}
