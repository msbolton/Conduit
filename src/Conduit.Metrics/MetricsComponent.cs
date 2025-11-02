using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Metrics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conduit.Metrics;

/// <summary>
/// Metrics component for Conduit framework integration.
/// </summary>
public class MetricsComponent : AbstractPluggableComponent
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
        StartupHealthCheck? startupHealthCheck = null) : base(logger)
    {
        _metricsCollector = metricsCollector;
        _healthCheckService = healthCheckService;
        _reporter = reporter;
        _configuration = configuration.Value;
        _logger = logger;
        _startupHealthCheck = startupHealthCheck;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.metrics",
            Name = "Conduit.Metrics",
            Version = "0.6.0",
            Description = "Metrics and monitoring component for Conduit framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "metrics", "monitoring", "telemetry", "health-checks" }
        };
    }


    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Metrics component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    public override Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Initializing Metrics component");

        if (_configuration.Enabled)
        {
            Logger.LogInformation("Metrics collection enabled with provider: {Provider}", _configuration.Provider);
            Logger.LogInformation("Prometheus endpoint: {Endpoint}", _configuration.PrometheusEndpoint);
            Logger.LogInformation("Health check endpoint: {Endpoint}", _configuration.HealthCheckEndpoint);

            // Record initialization
            _metricsCollector.Increment("component_initializations_total",
                1.0,
                ("component", Name));
        }

        // Mark startup as ready
        _startupHealthCheck?.MarkAsReady();

        return base.OnAttachAsync(context, cancellationToken);
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Starting Metrics component");

        if (_configuration.EnableConsoleExporter && Logger.IsEnabled(LogLevel.Debug))
        {
            _reporter.ReportToConsole();
        }

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Stopping Metrics component");

        if (_configuration.EnableConsoleExporter)
        {
            _reporter.ReportToConsole();
        }

        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Shutting down Metrics component");
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

    public override Task OnDetachAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Metrics component '{Name}' detached", Name);
        return base.OnDetachAsync(cancellationToken);
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
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

    public override IEnumerable<ServiceContract> ProvideServices()
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

    public override bool IsCompatibleWith(string coreVersion)
    {
        return Version.CompareTo(coreVersion) >= 0;
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
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

    protected override ComponentHealth? PerformHealthCheck()
    {
        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["Enabled"] = _configuration.Enabled
        };

        return ComponentHealth.Healthy(Id, healthData);
    }
}
