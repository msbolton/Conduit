using FluentAssertions;
using Conduit.Saga;

namespace Conduit.Saga.Tests;

public class SagaCorrelationConfigurationTests
{
    [Fact]
    public void SagaCorrelationConfiguration_Constructor_ShouldSucceed()
    {
        // Act
        var config = new SagaCorrelationConfiguration();

        // Assert
        config.Should().NotBeNull();
        config.Should().BeAssignableTo<IConfigureHowToFindSagaWithMessage>();
    }

    [Fact]
    public void CorrelateMessage_WithValidParameters_ShouldReturnSelf()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act
        var result = config.CorrelateMessage<TestMessage, TestSagaData, string>(
            msg => msg.OrderId,
            saga => saga.OrderId);

        // Assert
        result.Should().Be(config);
        result.Should().BeAssignableTo<IConfigureHowToFindSagaWithMessage>();
    }

    [Fact]
    public void CorrelateByCorrelationId_WithValidExtractor_ShouldReturnSelf()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act
        var result = config.CorrelateByCorrelationId<TestMessage>(msg => msg.CorrelationId ?? "");

        // Assert
        result.Should().Be(config);
        result.Should().BeAssignableTo<IConfigureHowToFindSagaWithMessage>();
    }

    [Fact]
    public void CorrelateMessage_FluentChaining_ShouldAllowMultipleCorrelations()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act
        var result = config
            .CorrelateMessage<TestMessage, TestSagaData, string>(
                msg => msg.OrderId,
                saga => saga.OrderId)
            .CorrelateMessage<AnotherTestMessage, TestSagaData, int>(
                msg => msg.CustomerId,
                saga => saga.CustomerId)
            .CorrelateByCorrelationId<TestMessage>(msg => msg.CorrelationId ?? "");

        // Assert
        result.Should().Be(config);
        result.Should().BeAssignableTo<IConfigureHowToFindSagaWithMessage>();
    }

    [Fact]
    public void CorrelateMessage_WithDifferentPropertyTypes_ShouldWork()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act & Assert - String property
        var result1 = config.CorrelateMessage<TestMessage, TestSagaData, string>(
            msg => msg.OrderId,
            saga => saga.OrderId);
        result1.Should().Be(config);

        // Act & Assert - Integer property
        var result2 = config.CorrelateMessage<AnotherTestMessage, TestSagaData, int>(
            msg => msg.CustomerId,
            saga => saga.CustomerId);
        result2.Should().Be(config);

        // Act & Assert - Guid property
        var result3 = config.CorrelateMessage<TestMessage, TestSagaData, Guid>(
            msg => msg.RequestId,
            saga => saga.Id);
        result3.Should().Be(config);
    }

    [Fact]
    public void CorrelateByCorrelationId_WithNullExtractor_ShouldNotThrow()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act
        var act = () => config.CorrelateByCorrelationId<TestMessage>(null!);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CorrelateMessage_WithNullExtractors_ShouldNotThrow()
    {
        // Arrange
        var config = new SagaCorrelationConfiguration();

        // Act
        var act = () => config.CorrelateMessage<TestMessage, TestSagaData, string>(null!, null!);

        // Assert
        act.Should().NotThrow();
    }

    // Test message and saga data classes
    public class TestMessage
    {
        public string OrderId { get; set; } = string.Empty;
        public Guid RequestId { get; set; }
        public string? CorrelationId { get; set; }
    }

    public class AnotherTestMessage
    {
        public int CustomerId { get; set; }
        public string? CorrelationId { get; set; }
    }

    public class TestSagaData : SagaData
    {
        public string OrderId { get; set; } = string.Empty;
        public int CustomerId { get; set; }
    }
}