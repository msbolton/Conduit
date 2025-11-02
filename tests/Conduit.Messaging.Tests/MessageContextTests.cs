using FluentAssertions;
using Moq;
using System.Security.Claims;
using Conduit.Api;
using Conduit.Messaging;

namespace Conduit.Messaging.Tests;

public class MessageContextTests
{
    private readonly Mock<IMessage> _mockMessage;

    public MessageContextTests()
    {
        _mockMessage = new Mock<IMessage>();
        _mockMessage.Setup(m => m.MessageId).Returns("test-message-123");
        _mockMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());
    }

    [Fact]
    public void MessageContext_Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var context = new MessageContext(_mockMessage.Object);

        // Assert
        context.Message.Should().Be(_mockMessage.Object);
        context.MessageId.Should().Be("test-message-123");
        context.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        context.Headers.Should().NotBeNull();
        context.Items.Should().NotBeNull();
        context.RetryCount.Should().Be(0);
        context.IsRetry.Should().BeFalse();
        context.IsFaulted.Should().BeFalse();
        context.IsAcknowledged.Should().BeFalse();
        context.Priority.Should().Be(MessagePriority.Normal);
        context.DeliveryCount.Should().Be(0);
        context.NestingDepth.Should().Be(0);
    }

    [Fact]
    public void MessageContext_Constructor_WithNullMessage_ShouldThrow()
    {
        // Act & Assert
        var act = () => new MessageContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MessageContext_Constructor_WithMessageWithoutId_ShouldGenerateId()
    {
        // Arrange
        var mockMessage = new Mock<IMessage>();
        mockMessage.Setup(m => m.MessageId).Returns((string?)null);
        mockMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());

        // Act
        var context = new MessageContext(mockMessage.Object);

        // Assert
        context.MessageId.Should().NotBeNullOrEmpty();
        Guid.TryParse(context.MessageId, out _).Should().BeTrue();
    }

    [Fact]
    public void MessageContext_Constructor_WithHeaders_ShouldExtractStandardHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            { "CorrelationId", "corr-123" },
            { "CausationId", "cause-456" },
            { "ConversationId", "conv-789" },
            { "Priority", "High" },
            { "RetryCount", "2" }
        };
        _mockMessage.Setup(m => m.Headers).Returns(headers);

        // Act
        var context = new MessageContext(_mockMessage.Object);

        // Assert
        context.CorrelationId.Should().Be("corr-123");
        context.CausationId.Should().Be("cause-456");
        context.ConversationId.Should().Be("conv-789");
        context.Priority.Should().Be(MessagePriority.High);
        context.RetryCount.Should().Be(2);
        context.IsRetry.Should().BeTrue();
    }

    [Fact]
    public void MessageContext_ProcessingDuration_ShouldCalculateCorrectly()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(5);

        // Act
        context.ProcessingStartedAt = startTime;
        context.ProcessingCompletedAt = endTime;

        // Assert
        context.ProcessingDuration.Should().HaveValue();
        context.ProcessingDuration.Value.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MessageContext_ProcessingDuration_WithoutBothTimes_ShouldReturnNull()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act & Assert
        context.ProcessingDuration.Should().BeNull();

        // Act - Set only start time
        context.ProcessingStartedAt = DateTimeOffset.UtcNow;

        // Assert
        context.ProcessingDuration.Should().BeNull();
    }

    [Fact]
    public void MessageContext_IsExpired_ShouldReturnCorrectValue()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act & Assert - No expiration
        context.IsExpired.Should().BeFalse();

        // Act & Assert - Future expiration
        context.ExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        context.IsExpired.Should().BeFalse();

        // Act & Assert - Past expiration
        context.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        context.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void MessageContext_MarkProcessingStarted_ShouldSetTimestamp()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        context.MarkProcessingStarted();

        // Assert
        context.ProcessingStartedAt.Should().HaveValue();
        context.ProcessingStartedAt.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageContext_MarkProcessingCompleted_ShouldSetTimestamp()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        context.MarkProcessingCompleted();

        // Assert
        context.ProcessingCompletedAt.Should().HaveValue();
        context.ProcessingCompletedAt.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MessageContext_MarkAsFaulted_ShouldSetFaultProperties()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var exception = new InvalidOperationException("Test error");

        // Act
        context.MarkAsFaulted(exception, "Custom reason");

        // Assert
        context.IsFaulted.Should().BeTrue();
        context.FaultException.Should().Be(exception);
        context.FaultReason.Should().Be("Custom reason");
        context.ProcessingCompletedAt.Should().HaveValue();
    }

    [Fact]
    public void MessageContext_MarkAsFaulted_WithoutReason_ShouldUseExceptionMessage()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var exception = new InvalidOperationException("Test error");

        // Act
        context.MarkAsFaulted(exception);

        // Assert
        context.IsFaulted.Should().BeTrue();
        context.FaultException.Should().Be(exception);
        context.FaultReason.Should().Be("Test error");
    }

    [Fact]
    public void MessageContext_Acknowledge_ShouldSetAcknowledgedAndCompleted()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        context.Acknowledge();

        // Assert
        context.IsAcknowledged.Should().BeTrue();
        context.ProcessingCompletedAt.Should().HaveValue();
    }

    [Fact]
    public void MessageContext_Acknowledge_WhenAlreadyCompleted_ShouldNotOverrideTimestamp()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var originalTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.ProcessingCompletedAt = originalTime;

        // Act
        context.Acknowledge();

        // Assert
        context.IsAcknowledged.Should().BeTrue();
        context.ProcessingCompletedAt.Should().Be(originalTime);
    }

    [Fact]
    public void MessageContext_GetSetItem_ShouldWorkCorrectly()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var testObject = new TestObject { Name = "Test" };

        // Act
        context.SetItem("test-key", testObject);
        var result = context.GetItem<TestObject>("test-key");

        // Assert
        result.Should().Be(testObject);
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public void MessageContext_GetItem_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        var result = context.GetItem<TestObject>("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MessageContext_GetItem_WithWrongType_ShouldReturnNull()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        context.SetItem("test-key", "string-value");

        // Act
        var result = context.GetItem<TestObject>("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MessageContext_RemoveItem_ShouldRemoveAndReturnTrue()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        context.SetItem("test-key", new TestObject());

        // Act
        var removed = context.RemoveItem("test-key");

        // Assert
        removed.Should().BeTrue();
        context.GetItem<TestObject>("test-key").Should().BeNull();
    }

    [Fact]
    public void MessageContext_RemoveItem_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        var removed = context.RemoveItem("non-existent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void MessageContext_SetGetHeader_ShouldWorkCorrectly()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        context.SetHeader("custom-header", "custom-value");
        var result = context.GetHeader("custom-header");

        // Assert
        result.Should().Be("custom-value");
    }

    [Fact]
    public void MessageContext_GetHeader_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act
        var result = context.GetHeader("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MessageContext_PrepareForRetry_ShouldResetAndIncrementCounter()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        context.MarkProcessingStarted();
        context.MarkAsFaulted(new Exception("Test"));

        // Act
        context.PrepareForRetry();

        // Assert
        context.RetryCount.Should().Be(1);
        context.IsRetry.Should().BeTrue();
        context.ProcessingStartedAt.Should().BeNull();
        context.ProcessingCompletedAt.Should().BeNull();
        context.IsFaulted.Should().BeFalse();
        context.FaultException.Should().BeNull();
        context.FaultReason.Should().BeNull();
        context.IsAcknowledged.Should().BeFalse();
        context.GetHeader("RetryCount").Should().Be("1");
        context.GetHeader("RetryTimestamp").Should().NotBeNull();
    }

    [Fact]
    public void MessageContext_CreateChildContext_ShouldCreateCorrectChild()
    {
        // Arrange
        var parentContext = new MessageContext(_mockMessage.Object);
        parentContext.CorrelationId = "corr-123";
        parentContext.ConversationId = "conv-456";
        parentContext.SessionId = "sess-789";
        parentContext.OriginatingEndpoint = "endpoint1";
        parentContext.SetHeader("custom-header", "custom-value");

        var childMessage = new Mock<IMessage>();
        childMessage.Setup(m => m.MessageId).Returns("child-msg-123");
        childMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());

        // Act
        var childContext = parentContext.CreateChildContext(childMessage.Object);

        // Assert
        childContext.ParentContext.Should().Be(parentContext);
        childContext.NestingDepth.Should().Be(1);
        childContext.CorrelationId.Should().Be("corr-123");
        childContext.CausationId.Should().Be(parentContext.MessageId);
        childContext.ConversationId.Should().Be("conv-456");
        childContext.SessionId.Should().Be("sess-789");
        childContext.OriginatingEndpoint.Should().Be("endpoint1");
        childContext.GetHeader("Parent.custom-header").Should().Be("custom-value");
    }

    [Fact]
    public void MessageContext_CreateResponseContext_ShouldCreateCorrectResponse()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        context.CorrelationId = "corr-123";
        context.ConversationId = "conv-456";
        context.SessionId = "sess-789";
        context.OriginatingEndpoint = "endpoint1";
        context.SetHeader("ReplyTo", "reply-endpoint");

        var responseMessage = new Mock<IMessage>();
        responseMessage.Setup(m => m.MessageId).Returns("response-msg-123");
        responseMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());

        // Act
        var responseContext = context.CreateResponseContext(responseMessage.Object);

        // Assert
        responseContext.CorrelationId.Should().Be("corr-123");
        responseContext.CausationId.Should().Be(context.MessageId);
        responseContext.ConversationId.Should().Be("conv-456");
        responseContext.SessionId.Should().Be("sess-789");
        responseContext.DestinationEndpoint.Should().Be("reply-endpoint");
    }

    [Fact]
    public void MessageContext_Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        context.CorrelationId = "corr-123";
        context.Priority = MessagePriority.High;
        context.SetHeader("custom-header", "custom-value");
        context.SetItem("custom-item", new TestObject { Name = "Test" });
        context.MarkProcessingStarted();

        // Act
        var clone = context.Clone();

        // Assert
        clone.Should().NotBeSameAs(context);
        clone.MessageId.Should().Be(context.MessageId);
        clone.CorrelationId.Should().Be(context.CorrelationId);
        clone.Priority.Should().Be(context.Priority);
        clone.ProcessingStartedAt.Should().Be(context.ProcessingStartedAt);
        clone.GetHeader("custom-header").Should().Be("custom-value");
        clone.GetItem<TestObject>("custom-item").Should().NotBeNull();

        // Verify independence
        clone.SetHeader("new-header", "new-value");
        context.GetHeader("new-header").Should().BeNull();
    }

    [Fact]
    public void MessageContext_WithUser_ShouldStoreClaimsPrincipal()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var claims = new[] { new Claim(ClaimTypes.Name, "TestUser") };
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);

        // Act
        context.User = user;

        // Assert
        context.User.Should().Be(user);
        context.User!.Identity!.Name.Should().Be("TestUser");
    }

    [Fact]
    public void MessageContext_MessagePriority_ShouldSupportAllLevels()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);

        // Act & Assert
        foreach (MessagePriority priority in Enum.GetValues<MessagePriority>())
        {
            context.Priority = priority;
            context.Priority.Should().Be(priority);
        }
    }

    [Fact]
    public void MessageContext_WithTracing_ShouldSupportTracingOperations()
    {
        // Arrange
        var context = new MessageContext(_mockMessage.Object);
        var tracing = new MessageTracing();
        tracing.AddEvent("TestEvent", "Test description");

        // Act
        context.Tracing = tracing;

        // Assert
        context.Tracing.Should().Be(tracing);
        context.Tracing.Events.Should().HaveCount(1);
        context.Tracing.Events[0].Name.Should().Be("TestEvent");
    }

    [Fact]
    public void MessageTracing_Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var tracing = new MessageTracing();
        tracing.Tags["key1"] = "value1";
        tracing.AddEvent("Event1");

        // Act
        var clone = tracing.Clone();

        // Assert
        clone.Should().NotBeSameAs(tracing);
        clone.TraceId.Should().Be(tracing.TraceId);
        clone.Tags.Should().ContainKey("key1");
        clone.Tags["key1"].Should().Be("value1");

        // Verify independence
        clone.Tags["key2"] = "value2";
        tracing.Tags.Should().NotContainKey("key2");
    }

    [Fact]
    public void MessageContext_NestingDepth_ShouldCalculateCorrectly()
    {
        // Arrange
        var grandparent = new MessageContext(_mockMessage.Object);
        var parentMessage = new Mock<IMessage>();
        parentMessage.Setup(m => m.MessageId).Returns("parent-123");
        parentMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());

        var childMessage = new Mock<IMessage>();
        childMessage.Setup(m => m.MessageId).Returns("child-123");
        childMessage.Setup(m => m.Headers).Returns(new Dictionary<string, object>());

        // Act
        var parent = grandparent.CreateChildContext(parentMessage.Object);
        var child = parent.CreateChildContext(childMessage.Object);

        // Assert
        grandparent.NestingDepth.Should().Be(0);
        parent.NestingDepth.Should().Be(1);
        child.NestingDepth.Should().Be(2);
    }

    private class TestObject
    {
        public string Name { get; set; } = "";
    }
}