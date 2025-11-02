using FluentAssertions;
using Moq;
using Conduit.Api;
using Conduit.Transports.Core;

namespace Conduit.Transports.Core.Tests;

public class TransportMessageTests
{
    [Fact]
    public void TransportMessage_Constructor_ShouldSetDefaultValues()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        var message = new TransportMessage();
        var afterCreation = DateTimeOffset.UtcNow.AddSeconds(1);

        // Assert
        message.MessageId.Should().NotBeNullOrEmpty();
        message.CorrelationId.Should().BeNull();
        message.CausationId.Should().BeNull();
        message.Payload.Should().BeNull();
        message.ContentType.Should().Be("application/json");
        message.ContentEncoding.Should().BeNull();
        message.MessageType.Should().BeNull();
        message.Source.Should().BeNull();
        message.Destination.Should().BeNull();
        message.ReplyTo.Should().BeNull();
        message.Timestamp.Should().BeAfter(beforeCreation);
        message.Timestamp.Should().BeBefore(afterCreation);
        message.Expiration.Should().BeNull();
        message.Priority.Should().Be(5);
        message.Persistent.Should().BeTrue();
        message.DeliveryAttempts.Should().Be(0);
        message.Headers.Should().NotBeNull().And.BeEmpty();
        message.TransportProperties.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TransportMessage_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var message = new TransportMessage();
        var correlationId = "corr-123";
        var payload = System.Text.Encoding.UTF8.GetBytes("test payload");
        var expiration = DateTimeOffset.UtcNow.AddMinutes(30);

        // Act
        message.CorrelationId = correlationId;
        message.Payload = payload;
        message.ContentType = "application/xml";
        message.MessageType = "TestMessage";
        message.Priority = 8;
        message.Persistent = false;
        message.Expiration = expiration;

        // Assert
        message.CorrelationId.Should().Be(correlationId);
        message.Payload.Should().BeEquivalentTo(payload);
        message.ContentType.Should().Be("application/xml");
        message.MessageType.Should().Be("TestMessage");
        message.Priority.Should().Be(8);
        message.Persistent.Should().BeFalse();
        message.Expiration.Should().Be(expiration);
    }

    [Fact]
    public void TransportMessage_IsExpired_WithNoExpiration_ShouldReturnFalse()
    {
        // Arrange
        var message = new TransportMessage();

        // Act & Assert
        message.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void TransportMessage_IsExpired_WithFutureExpiration_ShouldReturnFalse()
    {
        // Arrange
        var message = new TransportMessage
        {
            Expiration = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        // Act & Assert
        message.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void TransportMessage_IsExpired_WithPastExpiration_ShouldReturnTrue()
    {
        // Arrange
        var message = new TransportMessage
        {
            Expiration = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        // Act & Assert
        message.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void TransportMessage_GetHeader_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var message = new TransportMessage();
        message.Headers["test-string"] = "test-value";
        message.Headers["test-int"] = 42;
        message.Headers["test-bool"] = true;

        // Act & Assert
        message.GetHeader<string>("test-string").Should().Be("test-value");
        message.GetHeader<int>("test-int").Should().Be(42);
        message.GetHeader<bool>("test-bool").Should().BeTrue();
    }

    [Fact]
    public void TransportMessage_GetHeader_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var message = new TransportMessage();

        // Act & Assert
        message.GetHeader<string>("non-existent").Should().BeNull();
        message.GetHeader<int>("non-existent").Should().Be(0);
        message.GetHeader<bool>("non-existent").Should().BeFalse();
    }

    [Fact]
    public void TransportMessage_GetHeader_WithWrongType_ShouldReturnDefault()
    {
        // Arrange
        var message = new TransportMessage();
        message.Headers["test-value"] = "string-value";

        // Act & Assert
        message.GetHeader<int>("test-value").Should().Be(0);
        message.GetHeader<bool>("test-value").Should().BeFalse();
    }

    [Fact]
    public void TransportMessage_SetHeader_ShouldStoreValue()
    {
        // Arrange
        var message = new TransportMessage();

        // Act
        message.SetHeader("custom-key", "custom-value");
        message.SetHeader("number", 123);
        message.SetHeader("flag", true);

        // Assert
        message.Headers["custom-key"].Should().Be("custom-value");
        message.Headers["number"].Should().Be(123);
        message.Headers["flag"].Should().Be(true);
    }

    [Fact]
    public void TransportMessage_GetTransportProperty_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var message = new TransportMessage();
        message.TransportProperties["connection-id"] = "conn-123";
        message.TransportProperties["timeout"] = 30000;

        // Act & Assert
        message.GetTransportProperty<string>("connection-id").Should().Be("conn-123");
        message.GetTransportProperty<int>("timeout").Should().Be(30000);
    }

    [Fact]
    public void TransportMessage_SetTransportProperty_ShouldStoreValue()
    {
        // Arrange
        var message = new TransportMessage();

        // Act
        message.SetTransportProperty("channel", "channel-1");
        message.SetTransportProperty("retry-count", 3);

        // Assert
        message.TransportProperties["channel"].Should().Be("channel-1");
        message.TransportProperties["retry-count"].Should().Be(3);
    }

    [Fact]
    public void TransportMessage_Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var original = new TransportMessage
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            Payload = System.Text.Encoding.UTF8.GetBytes("test payload"),
            ContentType = "application/xml",
            MessageType = "TestMessage",
            Priority = 7,
            Persistent = false
        };
        original.SetHeader("test-header", "header-value");
        original.SetTransportProperty("test-prop", "prop-value");

        // Act
        var cloned = original.Clone();

        // Assert
        cloned.Should().NotBeSameAs(original);
        cloned.MessageId.Should().Be(original.MessageId);
        cloned.CorrelationId.Should().Be(original.CorrelationId);
        cloned.Payload.Should().BeEquivalentTo(original.Payload);
        cloned.Payload.Should().NotBeSameAs(original.Payload); // Deep copy
        cloned.ContentType.Should().Be(original.ContentType);
        cloned.MessageType.Should().Be(original.MessageType);
        cloned.Priority.Should().Be(original.Priority);
        cloned.Persistent.Should().Be(original.Persistent);

        cloned.Headers.Should().BeEquivalentTo(original.Headers);
        cloned.Headers.Should().NotBeSameAs(original.Headers); // Deep copy
        cloned.TransportProperties.Should().BeEquivalentTo(original.TransportProperties);
        cloned.TransportProperties.Should().NotBeSameAs(original.TransportProperties); // Deep copy
    }

    [Fact]
    public void TransportMessage_Clone_WithNullPayload_ShouldHandleNullCorrectly()
    {
        // Arrange
        var original = new TransportMessage
        {
            MessageId = "msg-123",
            Payload = null
        };

        // Act
        var cloned = original.Clone();

        // Assert
        cloned.Payload.Should().BeNull();
        cloned.MessageId.Should().Be(original.MessageId);
    }

    [Fact]
    public void TransportMessage_FromMessage_ShouldCreateTransportMessage()
    {
        // Arrange
        var testMessage = new TestMessage
        {
            MessageId = "msg-456",
            CorrelationId = "corr-789",
            Timestamp = DateTimeOffset.UtcNow
        };
        testMessage.Headers.Add("custom-header", "header-value");
        testMessage.Headers.Add("priority", 10);

        var payload = System.Text.Encoding.UTF8.GetBytes("serialized message");

        // Act
        var transportMessage = TransportMessage.FromMessage(testMessage, payload, "application/json");

        // Assert
        transportMessage.MessageId.Should().Be("msg-456");
        transportMessage.CorrelationId.Should().Be("corr-789");
        transportMessage.Payload.Should().BeEquivalentTo(payload);
        transportMessage.ContentType.Should().Be("application/json");
        transportMessage.MessageType.Should().Be(typeof(TestMessage).FullName);
        transportMessage.Priority.Should().Be(5); // Default priority
        transportMessage.Headers.Should().ContainKey("custom-header").WhoseValue.Should().Be("header-value");
        transportMessage.Headers.Should().ContainKey("priority").WhoseValue.Should().Be(10);
    }

    [Fact]
    public void TransportMessage_FromMessage_WithDefaultContentType_ShouldUseJsonDefault()
    {
        // Arrange
        var testMessage = new TestMessage
        {
            MessageId = "msg-123"
        };

        var payload = System.Text.Encoding.UTF8.GetBytes("test");

        // Act
        var transportMessage = TransportMessage.FromMessage(testMessage, payload);

        // Assert
        transportMessage.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void TransportMessage_MultipleInstances_ShouldHaveUniqueIds()
    {
        // Act
        var message1 = new TransportMessage();
        var message2 = new TransportMessage();

        // Assert
        message1.MessageId.Should().NotBe(message2.MessageId);
    }

    // Test message class for testing
    public class TestMessage : IMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string MessageType { get; set; } = nameof(TestMessage);
        public Dictionary<string, object> Headers { get; set; } = new();
        IReadOnlyDictionary<string, object> IMessage.Headers => Headers;
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public int Priority { get; set; } = 5;
        public bool IsSystemMessage { get; set; }
        public long Ttl { get; set; } = -1;
        public bool IsExpired => Ttl > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > Ttl;
        public object? Payload { get; set; }

        public object? GetHeader(string key)
        {
            Headers.TryGetValue(key, out var value);
            return value;
        }

        public T? GetHeader<T>(string key) where T : class
        {
            var value = GetHeader(key);
            return value as T;
        }
    }
}