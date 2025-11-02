using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Conduit.Gateway;

namespace Conduit.Gateway.Tests;

public class SimpleRateLimiterTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly RateLimiter _rateLimiter;

    public SimpleRateLimiterTests()
    {
        _mockLogger = new Mock<ILogger>();
        _rateLimiter = new RateLimiter(_mockLogger.Object, defaultRateLimit: 10);
    }

    [Fact]
    public void RateLimiter_Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act
        var rateLimiter = new RateLimiter(_mockLogger.Object, 100);

        // Assert
        rateLimiter.Should().NotBeNull();
    }

    [Fact]
    public void RateLimiter_Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        var act = () => new RateLimiter(null!, 100);
        act.Should().Throw<ArgumentNullException>().WithMessage("*logger*");
    }

    [Fact]
    public void RateLimiter_Constructor_WithDefaultParameters_ShouldUseDefaults()
    {
        // Act
        var rateLimiter = new RateLimiter(_mockLogger.Object);

        // Assert
        rateLimiter.Should().NotBeNull();
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithValidClientId_ShouldAllow()
    {
        // Act
        var result = _rateLimiter.AllowRequest("test-client");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithNullClientId_ShouldThrow()
    {
        // Act & Assert
        var act = () => _rateLimiter.AllowRequest(null!);
        act.Should().Throw<ArgumentException>().WithMessage("*Client ID cannot be null or empty*");
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithEmptyClientId_ShouldThrow()
    {
        // Act & Assert
        var act = () => _rateLimiter.AllowRequest("");
        act.Should().Throw<ArgumentException>().WithMessage("*Client ID cannot be null or empty*");
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithinLimit_ShouldAllow()
    {
        // Act - Make requests within the limit
        var results = new List<bool>();
        for (int i = 0; i < 5; i++)
        {
            results.Add(_rateLimiter.AllowRequest("test-client"));
        }

        // Assert - All should be allowed initially
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact]
    public void RateLimiter_AllowRequest_ExceedingLimit_ShouldDeny()
    {
        // Arrange - Exhaust the bucket
        for (int i = 0; i < 10; i++)
        {
            _rateLimiter.AllowRequest("test-client");
        }

        // Act - Try to make one more request
        var result = _rateLimiter.AllowRequest("test-client");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithCustomRateLimit_ShouldUseCustomLimit()
    {
        // Act
        var result = _rateLimiter.AllowRequest("test-client", 20);

        // Assert
        result.Should().BeTrue();

        // Verify state reflects custom limit
        var state = _rateLimiter.GetState("test-client");
        state.Should().NotBeNull();
        state!.Capacity.Should().Be(20);
    }

    [Fact]
    public void RateLimiter_GetState_WithExistingClient_ShouldReturnState()
    {
        // Arrange
        _rateLimiter.AllowRequest("test-client");

        // Act
        var state = _rateLimiter.GetState("test-client");

        // Assert
        state.Should().NotBeNull();
        state!.ClientId.Should().Be("test-client");
        state.Capacity.Should().Be(10);
        state.TokensAvailable.Should().BeLessThan(10); // Should have consumed one token
    }

    [Fact]
    public void RateLimiter_GetState_WithNonExistentClient_ShouldReturnNull()
    {
        // Act
        var state = _rateLimiter.GetState("non-existent");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void RateLimiter_Reset_WithExistingClient_ShouldResetBucket()
    {
        // Arrange
        _rateLimiter.AllowRequest("test-client");

        // Act
        _rateLimiter.Reset("test-client");

        // Assert - Next request should get a fresh bucket
        var state = _rateLimiter.GetState("test-client");
        state.Should().BeNull(); // Should be removed
    }

    [Fact]
    public void RateLimiter_Reset_WithNonExistentClient_ShouldNotThrow()
    {
        // Act & Assert
        var act = () => _rateLimiter.Reset("non-existent");
        act.Should().NotThrow();
    }

    [Fact]
    public void RateLimiter_ResetAll_ShouldClearAllBuckets()
    {
        // Arrange
        _rateLimiter.AllowRequest("client1");
        _rateLimiter.AllowRequest("client2");

        // Act
        _rateLimiter.ResetAll();

        // Assert
        _rateLimiter.GetState("client1").Should().BeNull();
        _rateLimiter.GetState("client2").Should().BeNull();
    }

    [Fact]
    public void RateLimitState_PercentageRemaining_ShouldCalculateCorrectly()
    {
        // Arrange
        var state = new RateLimitState
        {
            ClientId = "test",
            TokensAvailable = 7,
            Capacity = 10,
            RefillRate = 10
        };

        // Act
        var percentage = state.PercentageRemaining;

        // Assert
        percentage.Should().Be(0.7);
    }

    [Fact]
    public void RateLimitState_PercentageRemaining_WithZeroCapacity_ShouldReturnZero()
    {
        // Arrange
        var state = new RateLimitState
        {
            ClientId = "test",
            TokensAvailable = 5,
            Capacity = 0,
            RefillRate = 10
        };

        // Act
        var percentage = state.PercentageRemaining;

        // Assert
        percentage.Should().Be(0.0);
    }

    [Fact]
    public void RateLimiter_TokensAvailable_ShouldReflectConsumption()
    {
        // Arrange
        var clientId = "test-client";

        // Act - Consume some tokens
        _rateLimiter.AllowRequest(clientId);
        _rateLimiter.AllowRequest(clientId);
        _rateLimiter.AllowRequest(clientId);

        var state = _rateLimiter.GetState(clientId);

        // Assert
        state.Should().NotBeNull();
        state!.TokensAvailable.Should().BeLessThan(10);
        state.TokensAvailable.Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void RateLimiter_DefaultCapacity_ShouldBe100()
    {
        // Arrange & Act
        var rateLimiter = new RateLimiter(_mockLogger.Object);
        var result = rateLimiter.AllowRequest("test-client");
        var state = rateLimiter.GetState("test-client");

        // Assert
        result.Should().BeTrue();
        state.Should().NotBeNull();
        state!.Capacity.Should().Be(100);
    }

    [Fact]
    public void RateLimiter_MultipleClientsWithDifferentLimits_ShouldTrackSeparately()
    {
        // Act
        var client1Result = _rateLimiter.AllowRequest("client1", 5);
        var client2Result = _rateLimiter.AllowRequest("client2", 20);

        // Assert
        client1Result.Should().BeTrue();
        client2Result.Should().BeTrue();

        var state1 = _rateLimiter.GetState("client1");
        var state2 = _rateLimiter.GetState("client2");

        state1.Should().NotBeNull();
        state2.Should().NotBeNull();
        state1!.Capacity.Should().Be(5);
        state2!.Capacity.Should().Be(20);
    }

    [Fact]
    public void RateLimiter_PercentageRemaining_ShouldCalculateCorrectly()
    {
        // Arrange
        var clientId = "test-client";

        // Act - Use 3 tokens out of 10
        _rateLimiter.AllowRequest(clientId);
        _rateLimiter.AllowRequest(clientId);
        _rateLimiter.AllowRequest(clientId);

        var state = _rateLimiter.GetState(clientId);

        // Assert
        state.Should().NotBeNull();
        state!.PercentageRemaining.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void RateLimiter_RateLimitChange_ShouldUpdateBucket()
    {
        // Arrange
        var clientId = "test-client";

        // Act - First request with default limit
        _rateLimiter.AllowRequest(clientId);
        var state1 = _rateLimiter.GetState(clientId);

        // Change rate limit
        _rateLimiter.AllowRequest(clientId, 20);
        var state2 = _rateLimiter.GetState(clientId);

        // Assert
        state1.Should().NotBeNull();
        state2.Should().NotBeNull();
        state1!.Capacity.Should().Be(10);
        state2!.Capacity.Should().Be(20);
    }

    [Fact]
    public void RateLimiter_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var clientId = "concurrent-client";
        var tasks = new List<Task<bool>>();

        // Act - Simulate concurrent requests
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => _rateLimiter.AllowRequest(clientId)));
        }

        Task.WaitAll(tasks.ToArray());
        var results = tasks.Select(t => t.Result).ToList();

        // Assert - Should have exactly 10 successful requests (within limit)
        var successCount = results.Count(r => r);
        var failureCount = results.Count(r => !r);

        successCount.Should().Be(10);
        failureCount.Should().Be(10);
    }

    [Fact]
    public void RateLimiter_AllowRequest_WithDifferentClients_ShouldTrackSeparately()
    {
        // Act
        var client1Result = _rateLimiter.AllowRequest("client1");
        var client2Result = _rateLimiter.AllowRequest("client2");

        // Assert
        client1Result.Should().BeTrue();
        client2Result.Should().BeTrue();

        // Verify separate tracking
        var state1 = _rateLimiter.GetState("client1");
        var state2 = _rateLimiter.GetState("client2");

        state1.Should().NotBeNull();
        state2.Should().NotBeNull();
        state1!.ClientId.Should().Be("client1");
        state2!.ClientId.Should().Be("client2");
    }
}