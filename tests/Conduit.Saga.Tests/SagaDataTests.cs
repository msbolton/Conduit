using FluentAssertions;
using Conduit.Saga;

namespace Conduit.Saga.Tests;

public class SagaDataTests
{
    [Fact]
    public void SagaData_Constructor_ShouldSetDefaultValues()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var sagaData = new TestSagaData();
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        // Assert
        sagaData.Id.Should().NotBeEmpty();
        sagaData.Originator.Should().BeNull();
        sagaData.OriginalMessageId.Should().BeNull();
        sagaData.CreatedAt.Should().BeAfter(beforeCreation);
        sagaData.CreatedAt.Should().BeBefore(afterCreation);
        sagaData.LastUpdated.Should().BeNull();
        sagaData.State.Should().Be("STARTED");
        sagaData.CorrelationId.Should().BeEmpty();
    }

    [Fact]
    public void SagaData_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var sagaData = new TestSagaData();
        var newId = Guid.NewGuid();
        var originator = "test-originator";
        var originalMessageId = "msg-123";
        var lastUpdated = DateTime.UtcNow.AddHours(1);
        var state = "PROCESSING";
        var correlationId = "corr-456";

        // Act
        sagaData.Id = newId;
        sagaData.Originator = originator;
        sagaData.OriginalMessageId = originalMessageId;
        sagaData.LastUpdated = lastUpdated;
        sagaData.State = state;
        sagaData.CorrelationId = correlationId;

        // Assert
        sagaData.Id.Should().Be(newId);
        sagaData.Originator.Should().Be(originator);
        sagaData.OriginalMessageId.Should().Be(originalMessageId);
        sagaData.LastUpdated.Should().Be(lastUpdated);
        sagaData.State.Should().Be(state);
        sagaData.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void SagaData_MultipleInstances_ShouldHaveUniqueIds()
    {
        // Act
        var sagaData1 = new TestSagaData();
        var sagaData2 = new TestSagaData();

        // Assert
        sagaData1.Id.Should().NotBe(sagaData2.Id);
    }

    [Fact]
    public void SagaData_IContainSagaDataInterface_ShouldBeImplemented()
    {
        // Act
        var sagaData = new TestSagaData();

        // Assert
        sagaData.Should().BeAssignableTo<IContainSagaData>();
    }

    [Fact]
    public void SagaData_StateTransitions_ShouldBeTracked()
    {
        // Arrange
        var sagaData = new TestSagaData();
        var updateTime = DateTime.UtcNow.AddMinutes(30);

        // Act
        sagaData.State = "PROCESSING";
        sagaData.LastUpdated = updateTime;

        // Assert
        sagaData.State.Should().Be("PROCESSING");
        sagaData.LastUpdated.Should().Be(updateTime);
    }

    [Fact]
    public void SagaData_CorrelationIdHandling_ShouldAllowEmptyAndNonEmpty()
    {
        // Arrange
        var sagaData = new TestSagaData();

        // Act & Assert - Default is empty
        sagaData.CorrelationId.Should().BeEmpty();

        // Act & Assert - Can be set to specific value
        var correlationId = "test-correlation-123";
        sagaData.CorrelationId = correlationId;
        sagaData.CorrelationId.Should().Be(correlationId);

        // Act & Assert - Can be set back to empty
        sagaData.CorrelationId = "";
        sagaData.CorrelationId.Should().BeEmpty();
    }

    [Fact]
    public void SagaData_TimestampHandling_ShouldAllowNullAndValues()
    {
        // Arrange
        var sagaData = new TestSagaData();

        // Act & Assert - Default LastUpdated is null
        sagaData.LastUpdated.Should().BeNull();

        // Act & Assert - Can be set to specific time
        var updateTime = DateTime.UtcNow;
        sagaData.LastUpdated = updateTime;
        sagaData.LastUpdated.Should().Be(updateTime);

        // Act & Assert - Can be set back to null
        sagaData.LastUpdated = null;
        sagaData.LastUpdated.Should().BeNull();
    }

    // Test saga data class for testing
    public class TestSagaData : SagaData
    {
        public string? CustomProperty { get; set; }
    }
}