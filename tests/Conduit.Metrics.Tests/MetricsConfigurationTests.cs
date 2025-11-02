using FluentAssertions;
using Conduit.Metrics;

namespace Conduit.Metrics.Tests;

public class MetricsConfigurationTests
{
    [Fact]
    public void MetricsConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new MetricsConfiguration();

        // Assert
        config.Enabled.Should().BeTrue();
        config.Prefix.Should().Be("conduit");
        config.Provider.Should().Be(MetricsProvider.Prometheus);
        config.EnablePrometheus.Should().BeTrue();
        config.PrometheusEndpoint.Should().Be("/metrics");
        config.PrometheusPort.Should().Be(0);
        config.EnableOpenTelemetry.Should().BeFalse();
        config.EnableConsoleExporter.Should().BeFalse();
        config.EnableRuntimeMetrics.Should().BeTrue();
        config.EnableHealthChecks.Should().BeTrue();
        config.HealthCheckEndpoint.Should().Be("/health");
        config.DetailedHealthCheckEndpoint.Should().Be("/health/detailed");
        config.CollectionIntervalSeconds.Should().Be(15);
        config.EnableAutomaticInstrumentation.Should().BeTrue();
        config.HealthCheckTimeoutSeconds.Should().Be(5);
    }

    [Fact]
    public void MetricsConfiguration_DefaultHistogramBuckets_ShouldBeCorrect()
    {
        // Act
        var config = new MetricsConfiguration();

        // Assert
        config.DefaultHistogramBuckets.Should().NotBeEmpty();
        config.DefaultHistogramBuckets.Should().Contain(0.001);
        config.DefaultHistogramBuckets.Should().Contain(10.0);
        config.DefaultHistogramBuckets.Should().BeInAscendingOrder();
    }

    [Fact]
    public void MetricsConfiguration_GlobalLabels_ShouldBeInitialized()
    {
        // Act
        var config = new MetricsConfiguration();

        // Assert
        config.GlobalLabels.Should().NotBeNull();
        config.GlobalLabels.Should().BeEmpty();
    }

    [Fact]
    public void MetricsProvider_Enumeration_ShouldHaveCorrectValues()
    {
        // Assert
        MetricsProvider.Prometheus.Should().Be(MetricsProvider.Prometheus);
        MetricsProvider.OpenTelemetry.Should().Be(MetricsProvider.OpenTelemetry);
        MetricsProvider.Both.Should().Be(MetricsProvider.Both);
        MetricsProvider.Custom.Should().Be(MetricsProvider.Custom);
    }

    [Fact]
    public void HealthCheckOptions_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var options = new HealthCheckOptions();

        // Assert
        options.Name.Should().Be(string.Empty);
        options.Tags.Should().NotBeNull();
        options.Tags.Should().BeEmpty();
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        options.FailureStatus.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void MetricsConfiguration_CanSetCustomValues()
    {
        // Arrange
        var config = new MetricsConfiguration();

        // Act
        config.Enabled = false;
        config.Prefix = "custom";
        config.Provider = MetricsProvider.OpenTelemetry;
        config.PrometheusPort = 9090;

        // Assert
        config.Enabled.Should().BeFalse();
        config.Prefix.Should().Be("custom");
        config.Provider.Should().Be(MetricsProvider.OpenTelemetry);
        config.PrometheusPort.Should().Be(9090);
    }

    [Fact]
    public void MetricsConfiguration_GlobalLabels_CanAddCustomLabels()
    {
        // Arrange
        var config = new MetricsConfiguration();

        // Act
        config.GlobalLabels["environment"] = "test";
        config.GlobalLabels["version"] = "1.0.0";

        // Assert
        config.GlobalLabels.Should().HaveCount(2);
        config.GlobalLabels["environment"].Should().Be("test");
        config.GlobalLabels["version"].Should().Be("1.0.0");
    }
}