using FluentAssertions;
using Moq;
using Conduit.Api;
using Conduit.Messaging;

namespace Conduit.Messaging.Tests;

public class DeadLetterQueueBasicTests : IDisposable
{
    private readonly DeadLetterQueue _deadLetterQueue;
    private readonly Mock<IMessage> _mockMessage;

    public DeadLetterQueueBasicTests()
    {
        _deadLetterQueue = new DeadLetterQueue();
        _mockMessage = new Mock<IMessage>();
        _mockMessage.Setup(m => m.MessageId).Returns("test-message-id");
        _mockMessage.Setup(m => m.CorrelationId).Returns("test-correlation-id");
        _mockMessage.Setup(m => m.MessageType).Returns("TestMessage");
    }

    public void Dispose()
    {
        _deadLetterQueue?.Dispose();
    }

    [Fact]
    public void DeadLetterQueue_Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        using var queue = new DeadLetterQueue();

        // Assert
        queue.Count.Should().Be(0);
        queue.IsAtCapacity.Should().BeFalse();
        queue.TotalEnqueued.Should().Be(0);
        queue.TotalDequeued.Should().Be(0);
        queue.TotalReprocessed.Should().Be(0);
        queue.TotalExpired.Should().Be(0);
    }

    [Fact]
    public void DeadLetterQueue_ConstructorWithParameters_ShouldInitializeCorrectly()
    {
        // Act
        using var queue = new DeadLetterQueue(maxCapacity: 50, retentionPeriod: TimeSpan.FromHours(2));

        // Assert
        queue.Count.Should().Be(0);
        queue.IsAtCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_WithValidMessage_ShouldAddToQueue()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var entry = await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        // Assert
        entry.Should().NotBeNull();
        entry.Message.MessageId.Should().Be("test-message-id");
        entry.Exception.Should().Be(exception);
        _deadLetterQueue.Count.Should().Be(1);
        _deadLetterQueue.TotalEnqueued.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_WithNullMessage_ShouldThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act & Assert
        var act = async () => await _deadLetterQueue.AddAsync(null!, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public async Task AddAsync_WithNullException_ShouldThrow()
    {
        // Act & Assert
        var act = async () => await _deadLetterQueue.AddAsync(_mockMessage.Object, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("exception");
    }

    [Fact]
    public async Task AddAsync_ShouldTriggerMessageAddedEvent()
    {
        // Arrange
        var eventTriggered = false;
        DeadLetterEventArgs? capturedArgs = null;
        _deadLetterQueue.MessageAdded += (sender, args) =>
        {
            eventTriggered = true;
            capturedArgs = args;
        };

        var exception = new InvalidOperationException("Test error");

        // Act
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        // Assert
        eventTriggered.Should().BeTrue();
        capturedArgs.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingMessage_ShouldReturnEntry()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        // Act
        var entry = _deadLetterQueue.GetById("test-message-id");

        // Assert
        entry.Should().NotBeNull();
        entry!.Message.MessageId.Should().Be("test-message-id");
    }

    [Fact]
    public void GetById_WithNonExistentMessage_ShouldReturnNull()
    {
        // Act
        var entry = _deadLetterQueue.GetById("non-existent-id");

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public void GetById_WithNullId_ShouldThrow()
    {
        // Act & Assert
        var act = () => _deadLetterQueue.GetById(null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    [Fact]
    public async Task GetMessages_WithMessages_ShouldReturnAllEntries()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.MessageType).Returns("TestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, exception);

        // Act
        var entries = _deadLetterQueue.GetMessages();

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Message.MessageId == "test-message-id");
        entries.Should().Contain(e => e.Message.MessageId == "second-message");
    }

    [Fact]
    public void GetMessages_WithEmptyQueue_ShouldReturnEmptyList()
    {
        // Act
        var entries = _deadLetterQueue.GetMessages();

        // Assert
        entries.Should().NotBeNull();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessages_WithLimit_ShouldReturnLimitedResults()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.MessageType).Returns("TestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, exception);

        // Act
        var entries = _deadLetterQueue.GetMessages(limit: 1);

        // Assert
        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMessages_WithFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.MessageType).Returns("AnotherTestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, exception);

        // Act
        var entries = _deadLetterQueue.GetMessages(filter: e => e.MessageType.Contains("Another"));

        // Assert
        entries.Should().HaveCount(1);
        entries.First().Message.MessageId.Should().Be("second-message");
    }

    [Fact]
    public async Task GetByCorrelationId_WithMatchingMessages_ShouldReturnCorrectEntries()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.CorrelationId).Returns("different-correlation-id");
        secondMessage.Setup(m => m.MessageType).Returns("TestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, exception);

        // Act
        var entries = _deadLetterQueue.GetByCorrelationId("test-correlation-id");

        // Assert
        entries.Should().HaveCount(1);
        entries.First().Message.MessageId.Should().Be("test-message-id");
    }

    [Fact]
    public void GetByCorrelationId_WithNullCorrelationId_ShouldThrow()
    {
        // Act & Assert
        var act = () => _deadLetterQueue.GetByCorrelationId(null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("correlationId");
    }

    [Fact]
    public async Task GetByMessageType_WithMatchingMessages_ShouldReturnCorrectEntries()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        await _deadLetterQueue.AddAsync(_mockMessage.Object, exception);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.MessageType).Returns("AnotherTestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, exception);

        // Act
        var entries = _deadLetterQueue.GetByMessageType("TestMessage");

        // Assert
        entries.Should().HaveCount(2); // Both contain "TestMessage"
    }

    [Fact]
    public void GetByMessageType_WithNullMessageType_ShouldThrow()
    {
        // Act & Assert
        var act = () => _deadLetterQueue.GetByMessageType(null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("messageType");
    }

    [Fact]
    public async Task GetByErrorType_WithMatchingExceptions_ShouldReturnCorrectEntries()
    {
        // Arrange
        var invalidOpException = new InvalidOperationException("Test error");
        var argumentException = new ArgumentException("Argument error");

        await _deadLetterQueue.AddAsync(_mockMessage.Object, invalidOpException);

        var secondMessage = new Mock<IMessage>();
        secondMessage.Setup(m => m.MessageId).Returns("second-message");
        secondMessage.Setup(m => m.MessageType).Returns("TestMessage");
        await _deadLetterQueue.AddAsync(secondMessage.Object, argumentException);

        // Act
        var entries = _deadLetterQueue.GetByErrorType(typeof(InvalidOperationException));

        // Assert
        entries.Should().HaveCount(1);
        entries.First().Message.MessageId.Should().Be("test-message-id");
    }

    [Fact]
    public void GetByErrorType_WithNullExceptionType_ShouldThrow()
    {
        // Act & Assert
        var act = () => _deadLetterQueue.GetByErrorType(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("exceptionType");
    }

    [Fact]
    public void IsAtCapacity_WhenEmpty_ShouldReturnFalse()
    {
        // Assert
        _deadLetterQueue.IsAtCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task IsAtCapacity_WhenAtLimit_ShouldReturnTrue()
    {
        // Arrange
        using var queue = new DeadLetterQueue(maxCapacity: 1);
        var exception = new InvalidOperationException("Test error");

        // Act
        await queue.AddAsync(_mockMessage.Object, exception);

        // Assert
        queue.IsAtCapacity.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var exception = new InvalidOperationException("Test error");

        // Act - Run multiple add operations concurrently
        for (int i = 0; i < 10; i++)
        {
            var messageId = $"message-{i}";
            var message = new Mock<IMessage>();
            message.Setup(m => m.MessageId).Returns(messageId);
            message.Setup(m => m.MessageType).Returns("TestMessage");

            tasks.Add(_deadLetterQueue.AddAsync(message.Object, exception));
        }

        await Task.WhenAll(tasks);

        // Assert
        _deadLetterQueue.Count.Should().Be(10);
        _deadLetterQueue.TotalEnqueued.Should().Be(10);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var queue = new DeadLetterQueue();

        // Act & Assert
        var act = () => queue.Dispose();
        act.Should().NotThrow();

        // Should be safe to dispose multiple times
        act.Should().NotThrow();
    }
}