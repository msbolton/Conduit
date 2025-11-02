using FluentAssertions;
using Conduit.Transports.Core;

namespace Conduit.Transports.Core.Tests;

public class TransportStatisticsTests
{
    [Fact]
    public void TransportStatistics_Constructor_ShouldSetDefaultValues()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        var stats = new TransportStatistics();
        var afterCreation = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        stats.MessagesSent.Should().Be(0);
        stats.MessagesReceived.Should().Be(0);
        stats.BytesSent.Should().Be(0);
        stats.BytesReceived.Should().Be(0);
        stats.SendFailures.Should().Be(0);
        stats.ReceiveFailures.Should().Be(0);
        stats.ConnectionAttempts.Should().Be(0);
        stats.SuccessfulConnections.Should().Be(0);
        stats.ConnectionFailures.Should().Be(0);
        stats.Disconnections.Should().Be(0);
        stats.ActiveConnections.Should().Be(0);
        stats.ActiveSubscriptions.Should().Be(0);
        stats.AverageSendTimeMs.Should().Be(0);
        stats.AverageReceiveTimeMs.Should().Be(0);
        stats.StartTime.Should().BeAfter(beforeCreation);
        stats.StartTime.Should().BeBefore(afterCreation);
        stats.LastUpdated.Should().BeAfter(beforeCreation);
        stats.LastUpdated.Should().BeBefore(afterCreation);
    }

    [Fact]
    public void TransportStatistics_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var stats = new TransportStatistics();

        // Act
        stats.MessagesSent = 100;
        stats.MessagesReceived = 85;
        stats.BytesSent = 50000;
        stats.BytesReceived = 42000;
        stats.SendFailures = 5;
        stats.ReceiveFailures = 3;
        stats.ActiveConnections = 10;
        stats.AverageSendTimeMs = 15.5;

        // Assert
        stats.MessagesSent.Should().Be(100);
        stats.MessagesReceived.Should().Be(85);
        stats.BytesSent.Should().Be(50000);
        stats.BytesReceived.Should().Be(42000);
        stats.SendFailures.Should().Be(5);
        stats.ReceiveFailures.Should().Be(3);
        stats.ActiveConnections.Should().Be(10);
        stats.AverageSendTimeMs.Should().Be(15.5);
    }

    [Fact]
    public void TransportStatistics_SendSuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesSent = 90,
            SendFailures = 10
        };

        // Act
        var successRate = stats.SendSuccessRate;

        // Assert
        successRate.Should().Be(0.9); // 90/100 = 0.9
    }

    [Fact]
    public void TransportStatistics_SendSuccessRate_WithNoMessages_ShouldReturnZero()
    {
        // Arrange
        var stats = new TransportStatistics();

        // Act
        var successRate = stats.SendSuccessRate;

        // Assert
        successRate.Should().Be(0.0);
    }

    [Fact]
    public void TransportStatistics_ReceiveSuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesReceived = 75,
            ReceiveFailures = 25
        };

        // Act
        var successRate = stats.ReceiveSuccessRate;

        // Assert
        successRate.Should().Be(0.75); // 75/100 = 0.75
    }

    [Fact]
    public void TransportStatistics_ConnectionSuccessRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            ConnectionAttempts = 20,
            SuccessfulConnections = 18
        };

        // Act
        var successRate = stats.ConnectionSuccessRate;

        // Assert
        successRate.Should().Be(0.9); // 18/20 = 0.9
    }

    [Fact]
    public void TransportStatistics_ConnectionSuccessRate_WithNoAttempts_ShouldReturnZero()
    {
        // Arrange
        var stats = new TransportStatistics();

        // Act
        var successRate = stats.ConnectionSuccessRate;

        // Assert
        successRate.Should().Be(0.0);
    }

    [Fact]
    public void TransportStatistics_Uptime_ShouldCalculateFromStartTime()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        // Act
        var uptime = stats.Uptime;

        // Assert
        uptime.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TransportStatistics_MessagesPerSecond_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesSent = 60,
            MessagesReceived = 40,
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-100) // 100 seconds ago
        };

        // Act
        var messagesPerSecond = stats.MessagesPerSecond;

        // Assert
        messagesPerSecond.Should().BeApproximately(1.0, 0.1); // 100 messages / 100 seconds = 1.0
    }

    [Fact]
    public void TransportStatistics_MessagesPerSecond_WithZeroUptime_ShouldReturnZero()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesSent = 100,
            StartTime = DateTimeOffset.UtcNow // Just now, so uptime is ~0
        };

        // Act
        var messagesPerSecond = stats.MessagesPerSecond;

        // Assert
        messagesPerSecond.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void TransportStatistics_BytesPerSecond_ShouldCalculateCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            BytesSent = 5000,
            BytesReceived = 3000,
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-80) // 80 seconds ago
        };

        // Act
        var bytesPerSecond = stats.BytesPerSecond;

        // Assert
        bytesPerSecond.Should().BeApproximately(100.0, 10.0); // 8000 bytes / 80 seconds = 100
    }

    [Fact]
    public void TransportStatistics_Reset_ShouldClearAllValues()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesSent = 100,
            MessagesReceived = 85,
            BytesSent = 50000,
            SendFailures = 5,
            ActiveConnections = 10,
            AverageSendTimeMs = 15.5,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var beforeReset = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        stats.Reset();
        var afterReset = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        stats.MessagesSent.Should().Be(0);
        stats.MessagesReceived.Should().Be(0);
        stats.BytesSent.Should().Be(0);
        stats.BytesReceived.Should().Be(0);
        stats.SendFailures.Should().Be(0);
        stats.ReceiveFailures.Should().Be(0);
        stats.ConnectionAttempts.Should().Be(0);
        stats.SuccessfulConnections.Should().Be(0);
        stats.ConnectionFailures.Should().Be(0);
        stats.Disconnections.Should().Be(0);
        stats.ActiveConnections.Should().Be(0);
        stats.ActiveSubscriptions.Should().Be(0);
        stats.AverageSendTimeMs.Should().Be(0);
        stats.AverageReceiveTimeMs.Should().Be(0);
        stats.StartTime.Should().BeAfter(beforeReset);
        stats.StartTime.Should().BeBefore(afterReset);
        stats.LastUpdated.Should().BeAfter(beforeReset);
        stats.LastUpdated.Should().BeBefore(afterReset);
    }

    [Fact]
    public void TransportStatistics_Snapshot_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new TransportStatistics
        {
            MessagesSent = 100,
            MessagesReceived = 85,
            BytesSent = 50000,
            BytesReceived = 42000,
            SendFailures = 5,
            ReceiveFailures = 3,
            ConnectionAttempts = 20,
            SuccessfulConnections = 18,
            ActiveConnections = 10,
            AverageSendTimeMs = 15.5,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        var beforeSnapshot = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        var snapshot = original.Snapshot();
        var afterSnapshot = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        snapshot.Should().NotBeSameAs(original);
        snapshot.MessagesSent.Should().Be(original.MessagesSent);
        snapshot.MessagesReceived.Should().Be(original.MessagesReceived);
        snapshot.BytesSent.Should().Be(original.BytesSent);
        snapshot.BytesReceived.Should().Be(original.BytesReceived);
        snapshot.SendFailures.Should().Be(original.SendFailures);
        snapshot.ReceiveFailures.Should().Be(original.ReceiveFailures);
        snapshot.ConnectionAttempts.Should().Be(original.ConnectionAttempts);
        snapshot.SuccessfulConnections.Should().Be(original.SuccessfulConnections);
        snapshot.ActiveConnections.Should().Be(original.ActiveConnections);
        snapshot.AverageSendTimeMs.Should().Be(original.AverageSendTimeMs);
        snapshot.StartTime.Should().Be(original.StartTime);
        snapshot.LastUpdated.Should().BeAfter(beforeSnapshot);
        snapshot.LastUpdated.Should().BeBefore(afterSnapshot);

        // Modify original to ensure independence
        original.MessagesSent = 200;
        snapshot.MessagesSent.Should().Be(100); // Should remain unchanged
    }

    [Fact]
    public void TransportStatistics_CalculatedRates_WithComplexScenarios_ShouldWorkCorrectly()
    {
        // Arrange
        var stats = new TransportStatistics
        {
            MessagesSent = 80,
            SendFailures = 20, // 80% success rate
            MessagesReceived = 90,
            ReceiveFailures = 10, // 90% success rate
            ConnectionAttempts = 25,
            SuccessfulConnections = 20 // 80% success rate
        };

        // Act & Assert
        stats.SendSuccessRate.Should().Be(0.8);
        stats.ReceiveSuccessRate.Should().Be(0.9);
        stats.ConnectionSuccessRate.Should().Be(0.8);
    }

    [Fact]
    public void TransportStatistics_EdgeCases_ShouldHandleCorrectly()
    {
        // Test with max values
        var stats = new TransportStatistics
        {
            MessagesSent = long.MaxValue,
            BytesSent = long.MaxValue
        };

        stats.MessagesSent.Should().Be(long.MaxValue);
        stats.BytesSent.Should().Be(long.MaxValue);

        // Test with negative uptime (should not happen in practice, but should not crash)
        stats.StartTime = DateTimeOffset.UtcNow.AddMinutes(10); // Future start time
        var uptime = stats.Uptime;
        uptime.Should().BeLessThanOrEqualTo(TimeSpan.Zero);
    }
}