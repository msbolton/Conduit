using FluentAssertions;
using Conduit.Metrics;
using Conduit.Metrics.HealthChecks;

namespace Conduit.Metrics.Tests;

public class HealthCheckTests
{
    [Fact]
    public void HealthCheckResult_Healthy_ShouldCreateCorrectly()
    {
        // Act
        var result = HealthCheckResult.Healthy("System is healthy");

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("System is healthy");
        result.Data.Should().NotBeNull();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_Degraded_ShouldCreateCorrectly()
    {
        // Act
        var result = HealthCheckResult.Degraded("System is degraded");

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("System is degraded");
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_ShouldCreateCorrectly()
    {
        // Arrange
        var exception = new Exception("Test error");

        // Act
        var result = HealthCheckResult.Unhealthy("System is unhealthy", exception);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("System is unhealthy");
        result.Exception.Should().Be(exception);
    }

    [Fact]
    public void HealthReport_GetAggregateStatus_WithHealthyResults_ShouldReturnHealthy()
    {
        // Arrange
        var results = new[]
        {
            HealthCheckResult.Healthy(),
            HealthCheckResult.Healthy()
        };

        // Act
        var status = HealthReport.GetAggregateStatus(results);

        // Assert
        status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void HealthReport_GetAggregateStatus_WithDegradedResults_ShouldReturnDegraded()
    {
        // Arrange
        var results = new[]
        {
            HealthCheckResult.Healthy(),
            HealthCheckResult.Degraded()
        };

        // Act
        var status = HealthReport.GetAggregateStatus(results);

        // Assert
        status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void HealthReport_GetAggregateStatus_WithUnhealthyResults_ShouldReturnUnhealthy()
    {
        // Arrange
        var results = new[]
        {
            HealthCheckResult.Healthy(),
            HealthCheckResult.Degraded(),
            HealthCheckResult.Unhealthy()
        };

        // Act
        var status = HealthReport.GetAggregateStatus(results);

        // Assert
        status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void HealthStatus_Enumeration_ShouldHaveCorrectValues()
    {
        // Assert
        HealthStatus.Healthy.Should().Be(HealthStatus.Healthy);
        HealthStatus.Degraded.Should().Be(HealthStatus.Degraded);
        HealthStatus.Unhealthy.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void HealthCheckResult_WithData_ShouldStoreData()
    {
        // Arrange
        var data = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        var result = HealthCheckResult.Healthy("Test", data);

        // Assert
        result.Data.Should().ContainKey("key1");
        result.Data.Should().ContainKey("key2");
        result.Data["key1"].Should().Be("value1");
        result.Data["key2"].Should().Be(42);
    }
}