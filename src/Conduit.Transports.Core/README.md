# Conduit.Transports.Core

Core transport abstractions and infrastructure for the Conduit messaging framework. Provides the foundation for implementing different transport protocols (TCP, AMQP, gRPC, Kafka, etc.).

## Features

### Core Abstractions
- **ITransport** - Core transport interface with connect, send, and subscribe operations
- **TransportAdapterBase** - Base class for transport implementations with common functionality
- **TransportMessage** - Message envelope for transport layer with headers and metadata
- **ITransportSubscription** - Subscription management with pause/resume support

### Transport Types Supported
- InMemory - For testing and local communication
- TCP - Socket-based transport
- AMQP - RabbitMQ, ActiveMQ, etc.
- gRPC - High-performance RPC
- Kafka - Apache Kafka messaging
- Redis - Redis Pub/Sub
- WebSocket - WebSocket connections
- Http - HTTP/REST transport
- NamedPipes - Inter-process communication
- ServiceBus - Azure Service Bus
- Sqs - AWS SQS
- Custom - Custom transport implementations

### Configuration System
- **TransportConfiguration** - Comprehensive configuration with fluent API
- **ConnectionSettings** - Timeout, retry, keep-alive, pooling settings
- **ProtocolSettings** - Version negotiation, compression, message size limits
- **SecuritySettings** - TLS/SSL, certificates, authentication
- **PerformanceSettings** - Buffers, batching, pipelining, prefetch

### Connection Management
- **IConnectionManager** - Connection pooling interface
- **ITransportConnection** - Individual connection abstraction
- **ConnectionPoolStatistics** - Pool metrics and monitoring

### Metrics and Monitoring
- **TransportStatistics** - Comprehensive transport metrics
  - Message send/receive counts
  - Bytes sent/received
  - Success/failure rates
  - Connection statistics
  - Performance metrics (throughput, latency)

## Usage

### Basic Usage - InMemory Transport

```csharp
using Conduit.Transports.Core;

// Create an in-memory transport for testing
var transport = new InMemoryTransport("test-transport");

// Connect
await transport.ConnectAsync();

// Subscribe to messages
var subscription = await transport.SubscribeAsync(async message =>
{
    Console.WriteLine($"Received: {message.MessageType}");
    // Process message
});

// Send a message
await transport.SendAsync(myMessage);

// Send to specific destination
await transport.SendAsync(myMessage, "orders.queue");

// Cleanup
subscription.Dispose();
await transport.DisconnectAsync();
transport.Dispose();
```

### Custom Transport Implementation

```csharp
public class MyCustomTransport : TransportAdapterBase
{
    public override TransportType Type => TransportType.Custom;
    public override string Name => "MyTransport";

    public MyCustomTransport(TransportConfiguration config, ILogger logger)
        : base(config, logger)
    {
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        // Implement connection logic
        await OpenConnectionAsync();
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        // Implement disconnection logic
        await CloseConnectionAsync();
    }

    protected override async Task SendCoreAsync(
        IMessage message,
        string? destination,
        CancellationToken cancellationToken)
    {
        // Serialize and send message
        var payload = SerializeMessage(message);
        await SendToDestinationAsync(payload, destination);
    }

    protected override async Task<ITransportSubscription> SubscribeCoreAsync(
        string? source,
        Func<TransportMessage, Task> handler,
        CancellationToken cancellationToken)
    {
        // Set up message reception
        var subscription = await SetupSubscriptionAsync(source, handler);
        return subscription;
    }
}
```

### Transport Configuration

```csharp
var config = new TransportConfiguration
{
    Type = TransportType.Tcp,
    Name = "tcp-transport",
    Enabled = true,

    Connection = new ConnectionSettings
    {
        ConnectTimeout = TimeSpan.FromSeconds(30),
        ReadTimeout = TimeSpan.FromSeconds(60),
        MaxRetries = 3,
        RetryDelay = TimeSpan.FromSeconds(5),
        AutoReconnect = true,
        PoolSize = 5,
        MaxConcurrentConnections = 10
    },

    Protocol = new ProtocolSettings
    {
        MaxMessageSize = 1024 * 1024 * 10, // 10 MB
        CompressionEnabled = true,
        CompressionThreshold = 1024 // 1 KB
    },

    Security = new SecuritySettings
    {
        TlsEnabled = true,
        VerifyHostname = true,
        VerifyCertificate = true,
        MinimumTlsVersion = "TLS 1.2"
    },

    Performance = new PerformanceSettings
    {
        SendBufferSize = 8192,
        ReceiveBufferSize = 8192,
        NoDelay = true,
        PrefetchCount = 10,
        BatchingEnabled = true,
        BatchSize = 100
    }
};

var transport = new MyCustomTransport(config, logger);
```

### Transport Message

```csharp
// Creating a transport message from a domain message
var message = new MyCommand { Data = "test" };
var payload = JsonSerializer.SerializeToUtf8Bytes(message);

var transportMessage = TransportMessage.FromMessage(message, payload, "application/json");
transportMessage.Destination = "orders.queue";
transportMessage.Priority = 8; // High priority
transportMessage.Persistent = true; // Durable message
transportMessage.Expiration = DateTimeOffset.UtcNow.AddMinutes(5);

// Add custom headers
transportMessage.SetHeader("x-tenant-id", "tenant123");
transportMessage.SetHeader("x-request-source", "web-app");

// Add transport-specific properties
transportMessage.SetTransportProperty("delivery-mode", 2);
transportMessage.SetTransportProperty("routing-key", "orders.new");

// Check if expired
if (transportMessage.IsExpired)
{
    // Handle expired message
}

// Clone for routing
var clone = transportMessage.Clone();
```

### Connection Management

```csharp
public interface IConnectionManager
{
    // Get a connection from the pool
    Task<ITransportConnection> GetConnectionAsync(string destination);

    // Return connection to pool
    Task ReturnConnectionAsync(ITransportConnection connection);

    // Remove failed connection
    Task RemoveConnectionAsync(ITransportConnection connection);

    // Pool statistics
    ConnectionPoolStatistics GetStatistics();

    // Shutdown
    Task ShutdownAsync();
}

// Using connections
var connectionManager = new MyConnectionManager(config);
var connection = await connectionManager.GetConnectionAsync("server1");

try
{
    await connection.OpenAsync();
    // Use connection...
}
finally
{
    await connectionManager.ReturnConnectionAsync(connection);
}
```

### Subscriptions

```csharp
// Global subscription (all messages)
var globalSub = await transport.SubscribeAsync(async message =>
{
    Console.WriteLine($"Received: {message.MessageId}");
});

// Source-specific subscription
var ordersSub = await transport.SubscribeAsync("orders.queue", async message =>
{
    var order = DeserializeOrder(message.Payload);
    await ProcessOrderAsync(order);
});

// Pause/resume subscription
await ordersSub.PauseAsync();
// ... do some work ...
await ordersSub.ResumeAsync();

// Check subscription status
Console.WriteLine($"Active: {ordersSub.IsActive}");
Console.WriteLine($"Messages: {ordersSub.MessagesReceived}");

// Unsubscribe
ordersSub.Dispose();
```

### Transport Statistics

```csharp
var stats = transport.GetStatistics();

Console.WriteLine($"Messages Sent: {stats.MessagesSent}");
Console.WriteLine($"Messages Received: {stats.MessagesReceived}");
Console.WriteLine($"Bytes Sent: {stats.BytesSent}");
Console.WriteLine($"Bytes Received: {stats.BytesReceived}");
Console.WriteLine($"Send Success Rate: {stats.SendSuccessRate:P2}");
Console.WriteLine($"Receive Success Rate: {stats.ReceiveSuccessRate:P2}");
Console.WriteLine($"Connection Success Rate: {stats.ConnectionSuccessRate:P2}");
Console.WriteLine($"Active Connections: {stats.ActiveConnections}");
Console.WriteLine($"Active Subscriptions: {stats.ActiveSubscriptions}");
Console.WriteLine($"Messages/sec: {stats.MessagesPerSecond:F2}");
Console.WriteLine($"Bytes/sec: {stats.BytesPerSecond:F2}");
Console.WriteLine($"Avg Send Time: {stats.AverageSendTimeMs:F2}ms");
Console.WriteLine($"Avg Receive Time: {stats.AverageReceiveTimeMs:F2}ms");
Console.WriteLine($"Uptime: {stats.Uptime}");
```

## Architecture

### Transport Layer Hierarchy

```
ITransport (interface)
    ↓
TransportAdapterBase (abstract class)
    ↓
Specific Implementations:
    - InMemoryTransport
    - TcpTransport
    - AmqpTransport
    - GrpcTransport
    - etc.
```

### Message Flow

```
Application Message (IMessage)
    ↓
Transport Layer (ITransport.SendAsync)
    ↓
TransportMessage (envelope with metadata)
    ↓
Serialization
    ↓
Network Protocol (TCP, AMQP, gRPC, etc.)
    ↓
Remote Transport
    ↓
TransportMessage (deserialized)
    ↓
Subscription Handler
    ↓
Application Message
```

### Connection Pooling

```
Application
    ↓
IConnectionManager
    ↓
Connection Pool
    ├─ Active Connections
    └─ Available Connections
        ↓
ITransportConnection
    ↓
Network Connection
```

## Best Practices

1. **Always dispose transports and subscriptions**
   ```csharp
   await using var transport = new InMemoryTransport();
   // Use transport
   ```

2. **Use connection pooling for efficiency**
   - Configure appropriate pool size based on load
   - Monitor pool statistics
   - Set idle timeouts to release unused connections

3. **Set appropriate timeouts**
   - Connect timeout: 30-60 seconds
   - Read timeout: Based on expected processing time
   - Keep-alive: 30-60 seconds to detect dead connections

4. **Enable auto-reconnect for resilience**
   ```csharp
   Connection = new ConnectionSettings
   {
       AutoReconnect = true,
       MaxRetries = 3,
       RetryDelay = TimeSpan.FromSeconds(5)
   }
   ```

5. **Use TLS for production**
   ```csharp
   Security = new SecuritySettings
   {
       TlsEnabled = true,
       VerifyHostname = true,
       MinimumTlsVersion = "TLS 1.2"
   }
   ```

6. **Monitor transport statistics**
   - Track success rates
   - Monitor throughput
   - Alert on high failure rates
   - Check connection pool utilization

7. **Set message expiration for time-sensitive data**
   ```csharp
   transportMessage.Expiration = DateTimeOffset.UtcNow.AddMinutes(5);
   ```

8. **Use appropriate message priorities**
   - 0-3: Low priority (batch jobs)
   - 4-6: Normal priority (regular messages)
   - 7-10: High priority (urgent/real-time)

9. **Enable compression for large messages**
   ```csharp
   Protocol = new ProtocolSettings
   {
       CompressionEnabled = true,
       CompressionThreshold = 1024 // Compress messages > 1KB
   }
   ```

10. **Handle connection failures gracefully**
    ```csharp
    try
    {
        await transport.ConnectAsync();
    }
    catch (TransportException ex)
    {
        // Log error
        // Retry with backoff
        // Fallback to alternative transport
    }
    ```

## Dependencies

- Conduit.Api (>= 0.2.0)
- Conduit.Common (>= 0.2.0)
- Conduit.Messaging (>= 0.2.0)
- Microsoft.Extensions.Logging.Abstractions (>= 8.0.0)
- Microsoft.Extensions.ObjectPool (>= 8.0.0)

## Version

0.2.0

## License

See LICENSE file in the repository root.
