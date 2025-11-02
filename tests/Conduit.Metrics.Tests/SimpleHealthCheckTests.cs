using FluentAssertions;
using Conduit.Metrics;
using Conduit.Metrics.HealthChecks;

namespace Conduit.Metrics.Tests;

public class SimpleHealthCheckTests
{
    public class TestHealthCheck : IHealthCheck
    {
        public string Name { get; }
        private readonly Func<Task<HealthCheckResult>> _checkFunc;

        public TestHealthCheck(string name, Func<Task<HealthCheckResult>> checkFunc)
        {
            Name = name;
            _checkFunc = checkFunc;
        }

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return _checkFunc();
        }
    }

    [Fact]
    public async Task HealthCheck_WithHealthyResult_ShouldReturnHealthy()
    {
        // Arrange
        var healthCheck = new TestHealthCheck("test", () => Task.FromResult(HealthCheckResult.Healthy("All good")));

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("All good");
    }

    [Fact]
    public async Task HealthCheck_WithUnhealthyResult_ShouldReturnUnhealthy()
    {
        // Arrange
        var exception = new Exception("Something went wrong");
        var healthCheck = new TestHealthCheck("test", () => Task.FromResult(HealthCheckResult.Unhealthy("Failed", exception)));

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Failed");
        result.Exception.Should().Be(exception);
    }

    [Fact]
    public async Task HealthCheck_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var healthCheck = new TestHealthCheck("test", async () =>
        {
            await Task.Delay(1000, cts.Token);
            return HealthCheckResult.Healthy();
        });

        // Act & Assert
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => healthCheck.CheckHealthAsync(cts.Token));
    }

    [Fact]
    public void HealthCheck_Name_ShouldReturnCorrectName()
    {
        // Arrange
        var healthCheck = new TestHealthCheck("test-health-check", () => Task.FromResult(HealthCheckResult.Healthy()));

        // Act & Assert
        healthCheck.Name.Should().Be("test-health-check");
    }

    [Fact]
    public async Task HealthCheck_WithDegradedResult_ShouldReturnDegraded()
    {
        // Arrange
        var healthCheck = new TestHealthCheck("test", () => Task.FromResult(HealthCheckResult.Degraded("Performance issues")));

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Performance issues");
    }

    [Fact]
    public async Task HealthCheck_WithDataInResult_ShouldPreserveData()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["connections"] = 42 };
        var healthCheck = new TestHealthCheck("test", () => Task.FromResult(HealthCheckResult.Healthy("OK", data)));

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Data.Should().ContainKey("connections");
        result.Data["connections"].Should().Be(42);
    }
}