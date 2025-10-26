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
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }

    public Task InitializeAsync(IComponentContext context, CancellationToken cancellationToken = default)
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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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
}
