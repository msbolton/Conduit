using Conduit.Metrics.HealthChecks;
using Conduit.Metrics.OpenTelemetry;
using Conduit.Metrics.Prometheus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Conduit.Metrics;

/// <summary>
/// Extension methods for configuring metrics in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Conduit metrics services to the service collection.
    /// </summary>
    public static IServiceCollection AddConduitMetrics(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<MetricsConfiguration>? configure = null)
    {
        // Configure options
        if (configuration != null)
        {
            services.Configure<MetricsConfiguration>(configuration.GetSection("Conduit:Metrics"));
        }

        if (configure != null)
        {
            services.Configure(configure);
        }

        // Register metrics collector based on provider
        services.AddSingleton<IMetricsCollector>(sp =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetricsConfiguration>>().Value;
            var logger = sp.GetRequiredService<ILogger<PrometheusMetricsCollector>>();

            return config.Provider switch
            {
                MetricsProvider.Prometheus => new PrometheusMetricsCollector(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetricsConfiguration>>(),
                    logger),
                MetricsProvider.OpenTelemetry => new OpenTelemetryMetricsCollector(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetricsConfiguration>>(),
                    sp.GetRequiredService<ILogger<OpenTelemetryMetricsCollector>>()),
                _ => new PrometheusMetricsCollector(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetricsConfiguration>>(),
                    logger)
            };
        });

        // Register metrics reporter
        services.AddSingleton<MetricsReporter>();

        // Add health checks
        services.AddSingleton<HealthCheckService>();

        // Register built-in health checks
        services.AddSingleton<IHealthCheck, ComponentRegistryHealthCheck>();
        services.AddSingleton<IHealthCheck, MessageBusHealthCheck>();
        services.AddSingleton<IHealthCheck, MemoryHealthCheck>();
        services.AddSingleton<IHealthCheck, ThreadPoolHealthCheck>();
        services.AddSingleton<StartupHealthCheck>();
        services.AddSingleton<IHealthCheck>(sp => sp.GetRequiredService<StartupHealthCheck>());
        services.AddSingleton<LivenessHealthCheck>();
        services.AddSingleton<IHealthCheck>(sp => sp.GetRequiredService<LivenessHealthCheck>());

        // Add Microsoft Health Checks for ASP.NET Core integration
        var healthChecksBuilder = services.AddHealthChecks();
        healthChecksBuilder.AddCheck("conduit_health", () =>
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Conduit framework is healthy");
        });

        return services;
    }

    /// <summary>
    /// Adds Prometheus metrics exporter.
    /// </summary>
    public static IServiceCollection AddPrometheusMetrics(this IServiceCollection services)
    {
        services.Configure<MetricsConfiguration>(config =>
        {
            config.EnablePrometheus = true;
            config.Provider = MetricsProvider.Prometheus;
        });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry metrics exporter.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        Action<MeterProviderBuilder>? configure = null)
    {
        services.Configure<MetricsConfiguration>(config =>
        {
            config.EnableOpenTelemetry = true;
        });

        services.AddOpenTelemetry()
            .WithMetrics(builder =>
            {
                builder
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                configure?.Invoke(builder);
            });

        return services;
    }

    /// <summary>
    /// Adds a custom health check.
    /// </summary>
    public static IServiceCollection AddConduitHealthCheck<T>(this IServiceCollection services)
        where T : class, IHealthCheck
    {
        services.AddSingleton<IHealthCheck, T>();
        return services;
    }

    /// <summary>
    /// Adds a custom health check with factory.
    /// </summary>
    public static IServiceCollection AddConduitHealthCheck(
        this IServiceCollection services,
        Func<IServiceProvider, IHealthCheck> factory)
    {
        services.AddSingleton(factory);
        return services;
    }
}
