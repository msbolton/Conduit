# Conduit.Transports.ActiveMq

ActiveMQ Artemis transport implementation for the Conduit messaging framework using AMQP 1.0 protocol.

## Overview

This module provides connectivity to **ActiveMQ Artemis** message brokers using the AMQP 1.0 protocol through the Apache NMS.AMQP library. It implements the `ITransport` interface and integrates seamlessly with the Conduit framework.

### Key Features

- **AMQP 1.0 Protocol** - Native Artemis protocol support
- **Queue and Topic Support** - Full pub/sub and point-to-point messaging
- **Connection Pooling** - Efficient connection and session management
- **Message Acknowledgement** - Multiple acknowledgement modes (auto, client, transactional)
- **Persistent Delivery** - Durable message storage
- **Message Priority** - Priority-based message delivery
- **Redelivery Policies** - Configurable retry with exponential backoff
- **Temporary Destinations** - Support for temporary queues and topics
- **Pause/Resume** - Subscription flow control
- **Auto-Reconnect** - Automatic reconnection on connection failures

## Installation

```bash
dotnet add package Conduit.Transports.ActiveMq
```

## Quick Start

### Basic Configuration

```csharp
using Conduit.Transports.ActiveMq;
using Microsoft.Extensions.Logging;

// Create configuration
var config = new ActiveMqConfiguration
{
    BrokerUri = "amqp://localhost:5672",
    Username = "admin",
    Password = "admin",
    PersistentDelivery = true,
    AcknowledgementMode = AcknowledgementMode.AutoAcknowledge
};

// Create transport
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ActiveMqTransport>();
var transport = new ActiveMqTransport(config, logger);

// Connect
await transport.ConnectAsync();
```

### Sending Messages to Queues

```csharp
using Conduit.Api;

// Create a command
var command = new CreateOrderCommand
{
    MessageId = Guid.NewGuid().ToString(),
    CustomerId = "123",
    Items = new List<OrderItem> { /* ... */ }
};

// Send to queue
await transport.SendAsync(command, "queue://orders");
```

### Sending Messages to Topics

```csharp
// Create an event
var orderCreated = new OrderCreatedEvent
{
    MessageId = Guid.NewGuid().ToString(),
    OrderId = "456",
    CustomerId = "123",
    TotalAmount = 99.99m
};

// Publish to topic
await transport.SendAsync(orderCreated, "topic://order-events");
```

### Subscribing to Queues

```csharp
// Subscribe to queue
var subscription = await transport.SubscribeAsync(
    "queue://orders",
    async (transportMessage) =>
    {
        Console.WriteLine($"Received message: {transportMessage.MessageId}");

        // Process message
        var payload = Encoding.UTF8.GetString(transportMessage.Payload);
        Console.WriteLine($"Payload: {payload}");

        await Task.CompletedTask;
    });

// Pause subscription
await subscription.PauseAsync();

// Resume subscription
await subscription.ResumeAsync();

// Unsubscribe
await subscription.UnsubscribeAsync();
```

### Subscribing to Topics

```csharp
// Subscribe to topic
var subscription = await transport.SubscribeAsync(
    "topic://order-events",
    async (transportMessage) =>
    {
        Console.WriteLine($"Event received: {transportMessage.MessageType}");
        await Task.CompletedTask;
    });
```

## Configuration

### Connection Settings

```csharp
var config = new ActiveMqConfiguration
{
    // Broker connection
    BrokerUri = "amqp://localhost:5672",
    Username = "admin",
    Password = "admin",
    ClientId = "my-app-001", // Required for durable subscriptions

    // Timeouts
    SendTimeout = 30000,      // 30 seconds
    RequestTimeout = 60000,   // 60 seconds
    CloseTimeout = 15000,     // 15 seconds

    // Performance
    AsyncSend = true,
    PrefetchPolicy = 100,     // Number of messages to prefetch
    UseCompression = false,

    // Delivery
    PersistentDelivery = true,
    DefaultMessagePriority = 4,
    DefaultTimeToLive = 0,    // 0 = no expiration

    // Acknowledgement
    AcknowledgementMode = AcknowledgementMode.AutoAcknowledge,

    // Redelivery
    MaxRedeliveryAttempts = 6,
    RedeliveryDelay = 1000,   // 1 second
    UseExponentialBackoff = true,
    BackoffMultiplier = 2.0,
    MaxRedeliveryDelay = 60000, // 60 seconds

    // Temporary destinations
    AllowTemporaryQueues = true,
    AllowTemporaryTopics = true
};
```

### Acknowledgement Modes

```csharp
// Auto-acknowledge (default)
config.AcknowledgementMode = AcknowledgementMode.AutoAcknowledge;

// Client acknowledge (manual)
config.AcknowledgementMode = AcknowledgementMode.ClientAcknowledge;

// Duplicates OK (allows duplicates for better performance)
config.AcknowledgementMode = AcknowledgementMode.DupsOkAcknowledge;

// Transactional (requires session transactions)
config.AcknowledgementMode = AcknowledgementMode.Transactional;

// Individual acknowledge (ActiveMQ extension)
config.AcknowledgementMode = AcknowledgementMode.IndividualAcknowledge;
```

### Connection Pooling

```csharp
// Inherited from TransportConfiguration
config.Connection = new ConnectionSettings
{
    ConnectTimeout = TimeSpan.FromSeconds(30),
    MaxRetries = 3,
    RetryDelay = TimeSpan.FromSeconds(5),
    AutoReconnect = true,
    PoolSize = 5,              // Number of connections in pool
    MaxIdleTime = TimeSpan.FromMinutes(10)
};
```

### TLS/SSL Configuration

```csharp
config.Security = new SecuritySettings
{
    UseTls = true,
    TlsVersion = "1.2",
    ValidateCertificate = true,
    CertificatePath = "/path/to/cert.pem",
    CertificatePassword = "password"
};
```

## Advanced Usage

### Request-Response Pattern

```csharp
// Create temporary reply queue
var replyTo = "temp-queue://replies";

// Send request with reply-to
var request = new GetOrderQuery { OrderId = "123" };
var transportMessage = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)),
    ContentType = "application/json",
    MessageType = typeof(GetOrderQuery).FullName,
    ReplyTo = replyTo,
    Expiration = DateTimeOffset.UtcNow.AddSeconds(30)
};

// Subscribe to reply queue
var replyReceived = new TaskCompletionSource<TransportMessage>();
var replySubscription = await transport.SubscribeAsync(
    replyTo,
    async (reply) =>
    {
        replyReceived.SetResult(reply);
        await Task.CompletedTask;
    });

// Send request
await transport.SendAsync(request, "queue://queries");

// Wait for reply
var reply = await replyReceived.Task;

// Cleanup
await replySubscription.UnsubscribeAsync();
```

### Message Correlation

```csharp
// Send correlated messages
var correlationId = Guid.NewGuid().ToString();

var message1 = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    CorrelationId = correlationId,
    Payload = /* ... */
};

var message2 = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    CorrelationId = correlationId,
    CausationId = message1.MessageId, // This message caused by message1
    Payload = /* ... */
};
```

### Message Priority

```csharp
// High priority message
var urgentMessage = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Priority = 9,  // 0-10 scale (9 = very high)
    Payload = /* ... */
};
```

### Message Expiration

```csharp
// Message expires in 5 minutes
var expiringMessage = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Expiration = DateTimeOffset.UtcNow.AddMinutes(5),
    Payload = /* ... */
};
```

### Custom Headers

```csharp
var message = new TransportMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Payload = /* ... */,
    Headers =
    {
        ["TenantId"] = "tenant-123",
        ["UserId"] = "user-456",
        ["TraceId"] = "trace-789"
    }
};
```

### Durable Subscriptions

```csharp
// Set client ID (required for durable subscriptions)
config.ClientId = "my-service-001";

var transport = new ActiveMqTransport(config, logger);
await transport.ConnectAsync();

// Create durable subscription to topic
var subscription = await transport.SubscribeAsync(
    "topic://events",
    async (message) =>
    {
        // Process message
        await Task.CompletedTask;
    });

// Even if the application disconnects, messages will be retained
// for this subscription when using topics
```

## Destination Formats

The transport supports multiple destination URI formats:

```csharp
// Queues
"queue://orders"
"queue://payments"

// Topics
"topic://events"
"topic://notifications"

// Temporary queues (auto-generated name)
"temp-queue://"

// Temporary topics (auto-generated name)
"temp-topic://"

// Simple name (defaults to queue)
"orders"  // Same as "queue://orders"
```

## Error Handling

### Connection Failures

```csharp
try
{
    await transport.ConnectAsync();
}
catch (NMSException ex)
{
    // Handle NMS-specific errors
    logger.LogError(ex, "Failed to connect to broker");
}
catch (Exception ex)
{
    // Handle general errors
    logger.LogError(ex, "Unexpected error");
}
```

### Message Send Failures

```csharp
try
{
    await transport.SendAsync(message, "queue://orders");
}
catch (InvalidOperationException ex)
{
    // Not connected
    logger.LogError(ex, "Transport not connected");
}
catch (NMSException ex)
{
    // Send failed
    logger.LogError(ex, "Failed to send message");
}
```

### Auto-Reconnect

```csharp
config.Connection.AutoReconnect = true;
config.Connection.MaxRetries = 3;
config.Connection.RetryDelay = TimeSpan.FromSeconds(5);

// Transport will automatically attempt to reconnect on failures
```

## Monitoring

### Transport Statistics

```csharp
var stats = transport.GetStatistics();

Console.WriteLine($"Messages Sent: {stats.MessagesSent}");
Console.WriteLine($"Messages Received: {stats.MessagesReceived}");
Console.WriteLine($"Send Success Rate: {stats.SendSuccessRate:P}");
Console.WriteLine($"Receive Success Rate: {stats.ReceiveSuccessRate:P}");
Console.WriteLine($"Average Send Time: {stats.AverageSendTimeMs}ms");
Console.WriteLine($"Average Receive Time: {stats.AverageReceiveTimeMs}ms");
Console.WriteLine($"Active Connections: {stats.ActiveConnections}");
```

## Integration with Conduit

### Registering with Dependency Injection

```csharp
services.AddSingleton<ActiveMqConfiguration>(sp =>
    new ActiveMqConfiguration
    {
        BrokerUri = Configuration["ActiveMq:BrokerUri"],
        Username = Configuration["ActiveMq:Username"],
        Password = Configuration["ActiveMq:Password"]
    });

services.AddSingleton<ITransport, ActiveMqTransport>();
```

### Using with Message Bus

```csharp
// Transport automatically integrates with Conduit message bus
// through the transport layer

var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
await messageBus.SendAsync(command); // Routed through ActiveMQ
```

## Best Practices

1. **Use Connection Pooling** - Configure appropriate pool size for your workload
2. **Enable Persistent Delivery** - For critical messages that must not be lost
3. **Set Message Expiration** - Prevent queue buildup with old messages
4. **Use Client Acknowledgement** - For at-least-once delivery guarantees
5. **Configure Redelivery** - Set appropriate retry limits and backoff
6. **Monitor Statistics** - Track performance metrics in production
7. **Use Durable Subscriptions** - For topics when subscribers may disconnect
8. **Set Client ID** - Required for durable subscriptions
9. **Handle Exceptions** - Implement proper error handling in message handlers
10. **Use Correlation IDs** - Track related messages across services

## Troubleshooting

### Connection Refused

**Problem**: Cannot connect to broker
**Solution**:
- Verify broker is running: `docker ps | grep artemis`
- Check broker URI is correct
- Verify network connectivity and firewall rules
- Check username/password credentials

### Messages Not Delivered

**Problem**: Messages sent but not received
**Solution**:
- Check queue/topic name matches exactly
- Verify subscription is active (not paused)
- Check acknowledgement mode settings
- Review broker logs for errors

### Slow Performance

**Problem**: Message throughput is low
**Solution**:
- Increase prefetch policy: `config.PrefetchPolicy = 500`
- Enable async send: `config.AsyncSend = true`
- Use non-persistent delivery for non-critical messages
- Increase connection pool size
- Consider message batching

### Memory Leaks

**Problem**: Memory usage grows over time
**Solution**:
- Always dispose subscriptions when done
- Use `using` statements or call `DisposeAsync()`
- Unsubscribe from topics when no longer needed
- Monitor active subscription count

## Dependencies

- **Apache.NMS.AMQP** (>= 2.1.0) - AMQP 1.0 provider for NMS
- **Conduit.Transports.Core** - Transport abstractions
- **Conduit.Serialization** - Message serialization
- **Microsoft.Extensions.Logging.Abstractions** (>= 8.0.0)

## Compatibility

- **.NET**: 8.0 or later
- **ActiveMQ Artemis**: 2.x or later
- **AMQP**: 1.0 protocol

## License

This module is part of the Conduit Framework and is licensed under the Business Source License 1.1.

## See Also

- [Conduit.Transports.Core](../Conduit.Transports.Core/README.md) - Transport abstractions
- [ActiveMQ Artemis Documentation](https://activemq.apache.org/components/artemis/)
- [Apache NMS Documentation](https://activemq.apache.org/nms/)
- [AMQP 1.0 Specification](https://www.amqp.org/specification/1.0/amqp-org-download)
