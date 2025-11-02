using FluentAssertions;
using Conduit.Resilience;

namespace Conduit.Resilience.Tests;

public class ResilienceConfigurationTests
{
    [Fact]
    public void ResilienceConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new ResilienceConfiguration();

        // Assert
        config.Should().NotBeNull();
        config.CircuitBreaker.Should().NotBeNull();
        config.Retry.Should().NotBeNull();
        config.Bulkhead.Should().NotBeNull();
        config.Timeout.Should().NotBeNull();
        config.RateLimiter.Should().NotBeNull();
    }

    [Fact]
    public void CircuitBreakerConfig_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new ResilienceConfiguration.CircuitBreakerConfig();

        // Assert
        config.Enabled.Should().BeTrue();
        config.FailureThreshold.Should().Be(5);
        config.SuccessThreshold.Should().Be(3);
        config.WaitDurationInOpenState.Should().Be(TimeSpan.FromSeconds(30));
        config.SlowCallDurationThreshold.Should().Be(TimeSpan.FromMilliseconds(500));
        config.MinimumThroughput.Should().Be(10);
        config.FailureRateThreshold.Should().Be(0.5);
    }

    [Fact]
    public void RetryConfig_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new ResilienceConfiguration.RetryConfig();

        // Assert
        config.Enabled.Should().BeTrue();
        config.MaxAttempts.Should().Be(3);
        config.WaitDuration.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void CircuitBreakerConfig_CanSetCustomValues()
    {
        // Arrange
        var config = new ResilienceConfiguration.CircuitBreakerConfig();

        // Act
        config.Enabled = false;
        config.FailureThreshold = 10;
        config.SuccessThreshold = 5;
        config.WaitDurationInOpenState = TimeSpan.FromMinutes(1);
        config.FailureRateThreshold = 0.7;

        // Assert
        config.Enabled.Should().BeFalse();
        config.FailureThreshold.Should().Be(10);
        config.SuccessThreshold.Should().Be(5);
        config.WaitDurationInOpenState.Should().Be(TimeSpan.FromMinutes(1));
        config.FailureRateThreshold.Should().Be(0.7);
    }

    [Fact]
    public void RetryConfig_CanSetCustomValues()
    {
        // Arrange
        var config = new ResilienceConfiguration.RetryConfig();

        // Act
        config.Enabled = false;
        config.MaxAttempts = 5;
        config.WaitDuration = TimeSpan.FromSeconds(1);

        // Assert
        config.Enabled.Should().BeFalse();
        config.MaxAttempts.Should().Be(5);
        config.WaitDuration.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ResilienceConfiguration_AllConfigs_ShouldBeIndependent()
    {
        // Arrange
        var config = new ResilienceConfiguration();

        // Act - modify one config
        config.CircuitBreaker.Enabled = false;
        config.Retry.MaxAttempts = 10;

        // Assert - other configs should remain at defaults
        config.CircuitBreaker.Enabled.Should().BeFalse();
        config.Retry.MaxAttempts.Should().Be(10);
        config.Bulkhead.Should().NotBeNull();
        config.Timeout.Should().NotBeNull();
        config.RateLimiter.Should().NotBeNull();
    }

    [Fact]
    public void CircuitBreakerConfig_FailureRateThreshold_ShouldBeValidRange()
    {
        // Arrange
        var config = new ResilienceConfiguration.CircuitBreakerConfig();

        // Act & Assert - default should be in valid range
        config.FailureRateThreshold.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void CircuitBreakerConfig_TimeSpanProperties_ShouldBePositive()
    {
        // Arrange
        var config = new ResilienceConfiguration.CircuitBreakerConfig();

        // Assert
        config.WaitDurationInOpenState.Should().BePositive();
        config.SlowCallDurationThreshold.Should().BePositive();
    }

    [Fact]
    public void RetryConfig_WaitDuration_ShouldBePositive()
    {
        // Arrange
        var config = new ResilienceConfiguration.RetryConfig();

        // Assert
        config.WaitDuration.Should().BePositive();
    }

    [Fact]
    public void RetryConfig_MaxAttempts_ShouldBePositive()
    {
        // Arrange
        var config = new ResilienceConfiguration.RetryConfig();

        // Assert
        config.MaxAttempts.Should().BePositive();
    }
}