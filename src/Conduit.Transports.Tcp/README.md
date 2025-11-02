# Conduit.Transports.Tcp

TCP/Socket transport implementation for the Conduit messaging framework with message framing protocol support.

## Overview

This module provides reliable TCP/IP socket connectivity for the Conduit framework, supporting both server and client modes with connection pooling, message framing, and heartbeat mechanisms.

### Key Features

- **Server and Client Modes** - Run as TCP server or client
- **Connection Pooling** - Efficient connection reuse in client mode
- **Message Framing** - Multiple framing protocols (length-prefixed, newline-delimited, CRLF, custom)
- **Heartbeat/Keep-Alive** - TCP keep-alive and application-level heartbeats
- **Connection Management** - Automatic reconnection and connection limits
- **Buffer Management** - Configurable send/receive buffers
- **TCP Optimization** - NoDelay (Nagle disable), socket option tuning
- **Broadcast Support** - Server can broadcast to all connected clients
- **Pause/Resume** - Subscription flow control
- **SSL/TLS Support** - Secure transport (future enhancement)

## Installation

```bash
dotnet add package Conduit.Transports.Tcp
```

## Quick Start

### TCP Server

```csharp
using Conduit.Transports.Tcp;
using Conduit.Serialization;
using Microsoft.Extensions.Logging;

// Create configuration
var config = new TcpConfiguration
{
    IsServer = true,
    Host = "0.0.0.0",
    Port = 5000,
    MaxConnections = 100,
    FramingProtocol = FramingProtocol.LengthPrefixed
};

// Create transport
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<TcpTransport>();
var serializer = new JsonMessageSerializer();
var transport = new TcpTransport(config, serializer, logger);

// Connect (starts server)
await transport.ConnectAsync();

// Subscribe to messages
var subscription = await transport.SubscribeAsync(
    null, // Listen to all connections
    async (transportMessage) =>
    {
        Console.WriteLine($"Received: {transportMessage.MessageId}");
        await Task.CompletedTask;
    });
```

### TCP Client

```csharp
// Create configuration
var config = new TcpConfiguration
{
    IsServer = false,
    RemoteHost = "localhost",
    RemotePort = 5000,
    UseConnectionPooling = true,
    ConnectionPoolSize = 5,
    FramingProtocol = FramingProtocol.LengthPrefixed
};

// Create transport
var transport = new TcpTransport(config, serializer, logger);

// Connect to server
await transport.ConnectAsync();

// Send message
var command = new MyCommand { Data = "Hello Server" };
await transport.SendAsync(command);

// Subscribe to responses
var subscription = await transport.SubscribeAsync(
    null,
    async (transportMessage) =>
    {
        Console.WriteLine($"Response: {transportMessage.MessageId}");
        await Task.CompletedTask;
    });
```

## Configuration

### Connection Settings

```csharp
var config = new TcpConfiguration
{
    // Mode
    IsServer = false,

    // Server settings
    Host = "0.0.0.0",
    Port = 5000,
    MaxConnections = 100,
    Backlog = 100,

    // Client settings
    RemoteHost = "server.example.com",
    RemotePort = 5000,

    // Buffer sizes
    ReceiveBufferSize = 8192,  // 8 KB
    SendBufferSize = 8192,     // 8 KB

    // TCP options
    NoDelay = true,            // Disable Nagle's algorithm
    UseKeepAlive = true,
    KeepAliveInterval = 60000, // 60 seconds
    KeepAliveRetryCount = 3,
    LingerTime = 0,            // Disable linger
    ReuseAddress = true,

    // Message framing
    FramingProtocol = FramingProtocol.LengthPrefixed,
    MaxMessageSize = 1048576,  // 1 MB

    // Heartbeat
    HeartbeatInterval = 30000, // 30 seconds
    HeartbeatTimeout = 60000,  // 60 seconds

    // Connection pooling (client mode)
    UseConnectionPooling = true,
    ConnectionPoolSize = 5,
    ConnectionPoolTimeout = 30000 // 30 seconds
};
```

### Framing Protocols

#### Length-Prefixed (Recommended)

```csharp
config.FramingProtocol = FramingProtocol.LengthPrefixed;
// Format: [4-byte length][message data]
// - Most efficient
// - No content restrictions
// - Binary-safe
```

#### Newline-Delimited

```csharp
config.FramingProtocol = FramingProtocol.NewlineDelimited;
// Format: [message data]\n
// - Simple text protocol
// - Message cannot contain \n
// - Good for line-based protocols
```

#### CRLF-Delimited

```csharp
config.FramingProtocol = FramingProtocol.CrlfDelimited;
// Format: [message data]\r\n
// - HTTP-style line endings
// - Message cannot contain \r\n
// - Good for text protocols
```

#### Custom Delimiter

```csharp
config.FramingProtocol = FramingProtocol.CustomDelimiter;
// Requires custom delimiter bytes
// Currently configured in code
```

### Socket Options

```csharp
// Disable Nagle's algorithm for low latency
config.NoDelay = true;

// Enable TCP keep-alive
config.UseKeepAlive = true;
config.KeepAliveInterval = 60000;

// Configure buffers
config.ReceiveBufferSize = 65536; // 64 KB for high throughput
config.SendBufferSize = 65536;

// Configure linger
config.LingerTime = 5; // Wait 5 seconds on close
```

## Advanced Usage

### Server Broadcasting

```csharp
// Server mode: broadcast to all clients
await transport.SendAsync(message); // No destination = broadcast

// Or send to specific connection
await transport.SendAsync(message, connectionId);
```

### Connection Pooling (Client Mode)

```csharp
var config = new TcpConfiguration
{
    IsServer = false,
    RemoteHost = "localhost",
    RemotePort = 5000,
    UseConnectionPooling = true,
    ConnectionPoolSize = 10, // Maintain 10 connections
    ConnectionPoolTimeout = 30000
};

// Connections are automatically pooled and reused
await transport.SendAsync(message1);
await transport.SendAsync(message2); // Reuses connection from pool
```

### Heartbeat Monitoring

```csharp
// Configure heartbeat
config.HeartbeatInterval = 30000;  // Send heartbeat every 30s
config.HeartbeatTimeout = 60000;   // Close if no activity for 60s

// Heartbeats are automatic
// Empty messages sent when idle
// Connection closed on timeout
```

### Subscription Filtering (Server Mode)

```csharp
// Subscribe to all connections
var allSubscription = await transport.SubscribeAsync(
    null,
    async (msg) => { /* Handle from any connection */ });

// Subscribe to specific connection
var specificSubscription = await transport.SubscribeAsync(
    connectionId,
    async (msg) => { /* Handle from specific connection */ });
```

### Pause/Resume Subscriptions

```csharp
var subscription = await transport.SubscribeAsync(null, handler);

// Pause message delivery
await subscription.PauseAsync();

// Do some work...

// Resume message delivery
await subscription.ResumeAsync();

// Cleanup
await subscription.UnsubscribeAsync();
```

## Server Mode Examples

### Echo Server

```csharp
var config = new TcpConfiguration
{
    IsServer = true,
    Host = "0.0.0.0",
    Port = 8000,
    MaxConnections = 50
};

var transport = new TcpTransport(config, serializer, logger);
await transport.ConnectAsync();

// Echo back all received messages
await transport.SubscribeAsync(null, async (transportMessage) =>
{
    // Send back to source connection
    await transport.SendAsync(
        new EchoResponse { Original = transportMessage.Payload },
        transportMessage.Source);
});
```

### Pub/Sub Server

```csharp
var config = new TcpConfiguration
{
    IsServer = true,
    Host = "0.0.0.0",
    Port = 9000
};

var transport = new TcpTransport(config, serializer, logger);
await transport.ConnectAsync();

// When client publishes, broadcast to all clients
await transport.SubscribeAsync(null, async (transportMessage) =>
{
    Console.WriteLine($"Publishing from {transportMessage.Source}");

    // Broadcast to all connections (including sender)
    await transport.SendAsync(transportMessage);
});
```

### Request-Response Server

```csharp
await transport.SubscribeAsync(null, async (transportMessage) =>
{
    var request = Encoding.UTF8.GetString(transportMessage.Payload);

    // Process request
    var response = ProcessRequest(request);

    // Send response back to requester
    var responseMsg = new TransportMessage
    {
        MessageId = Guid.NewGuid().ToString(),
        CorrelationId = transportMessage.MessageId,
        Payload = Encoding.UTF8.GetBytes(response)
    };

    await transport.SendAsync(responseMsg, transportMessage.Source);
});
```

## Client Mode Examples

### Simple Client

```csharp
var config = new TcpConfiguration
{
    IsServer = false,
    RemoteHost = "localhost",
    RemotePort = 8000
};

var transport = new TcpTransport(config, serializer, logger);
await transport.ConnectAsync();

// Send messages
for (int i = 0; i < 10; i++)
{
    var msg = new MyMessage { Counter = i };
    await transport.SendAsync(msg);
}
```

### Request-Response Client

```csharp
var responseTcs = new TaskCompletionSource<TransportMessage>();

// Subscribe to responses
var subscription = await transport.SubscribeAsync(null, async (response) =>
{
    responseTcs.SetResult(response);
    await Task.CompletedTask;
});

// Send request
var request = new MyRequest { Query = "Hello" };
await transport.SendAsync(request);

// Wait for response
var response = await responseTcs.Task;
Console.WriteLine($"Got response: {response.MessageId}");

await subscription.UnsubscribeAsync();
```

### Load Testing Client

```csharp
var config = new TcpConfiguration
{
    IsServer = false,
    RemoteHost = "loadtest.example.com",
    RemotePort = 5000,
    UseConnectionPooling = true,
    ConnectionPoolSize = 20, // 20 concurrent connections
    NoDelay = true
};

var transport = new TcpTransport(config, serializer, logger);
await transport.ConnectAsync();

// Send many messages concurrently
var tasks = Enumerable.Range(0, 1000).Select(i =>
    transport.SendAsync(new TestMessage { Id = i }));

await Task.WhenAll(tasks);
```

## Error Handling

### Connection Failures

```csharp
try
{
    await transport.ConnectAsync();
}
catch (SocketException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    // Retry logic here
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Connection timeout: {ex.Message}");
}
```

### Send Failures

```csharp
try
{
    await transport.SendAsync(message);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Transport not connected: {ex.Message}");
}
catch (IOException ex)
{
    Console.WriteLine($"Send failed: {ex.Message}");
    // Connection likely lost
}
```

### Message Framing Errors

```csharp
try
{
    // Reading will throw if message exceeds max size
    // or framing is invalid
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Invalid message: {ex.Message}");
}
catch (EndOfStreamException ex)
{
    Console.WriteLine($"Connection closed: {ex.Message}");
}
```

## Monitoring

### Transport Statistics

```csharp
var stats = transport.GetStatistics();

Console.WriteLine($"Messages Sent: {stats.MessagesSent}");
Console.WriteLine($"Messages Received: {stats.MessagesReceived}");
Console.WriteLine($"Send Success Rate: {stats.SendSuccessRate:P}");
Console.WriteLine($"Average Send Time: {stats.AverageSendTimeMs}ms");

// Server mode
if (config.IsServer && server != null)
{
    Console.WriteLine($"Active Connections: {server.ActiveConnectionCount}");
}

// Client mode
if (!config.IsServer && clientManager != null)
{
    Console.WriteLine($"Pooled Connections: {clientManager.PooledConnectionCount}");
    Console.WriteLine($"Active Connections: {clientManager.ActiveConnectionCount}");
}
```

## Performance Tuning

### High Throughput

```csharp
var config = new TcpConfiguration
{
    // Large buffers
    ReceiveBufferSize = 65536,  // 64 KB
    SendBufferSize = 65536,

    // Disable Nagle
    NoDelay = true,

    // Connection pooling
    UseConnectionPooling = true,
    ConnectionPoolSize = 20,

    // Length-prefixed framing (most efficient)
    FramingProtocol = FramingProtocol.LengthPrefixed,

    // Large message size
    MaxMessageSize = 10485760 // 10 MB
};
```

### Low Latency

```csharp
var config = new TcpConfiguration
{
    // Disable Nagle's algorithm
    NoDelay = true,

    // Small buffers to reduce buffering delay
    ReceiveBufferSize = 4096,
    SendBufferSize = 4096,

    // Frequent heartbeats
    HeartbeatInterval = 10000,

    // Length-prefixed for efficiency
    FramingProtocol = FramingProtocol.LengthPrefixed
};
```

### Many Connections (Server)

```csharp
var config = new TcpConfiguration
{
    IsServer = true,
    MaxConnections = 10000,
    Backlog = 1000,

    // Moderate buffer sizes
    ReceiveBufferSize = 8192,
    SendBufferSize = 8192,

    // Less frequent heartbeats
    HeartbeatInterval = 60000,
    HeartbeatTimeout = 120000
};
```

## Best Practices

1. **Use Length-Prefixed Framing** - Most efficient and reliable
2. **Enable NoDelay** - For most applications (disables Nagle's algorithm)
3. **Configure Heartbeats** - Detect dead connections early
4. **Use Connection Pooling** - For client applications with frequent sends
5. **Set Appropriate Buffer Sizes** - Balance memory vs. throughput
6. **Handle Connection Events** - Monitor connection lifecycle
7. **Validate Configuration** - Call `config.Validate()` before use
8. **Set MaxMessageSize** - Prevent memory exhaustion
9. **Use Async/Await** - Don't block on I/O operations
10. **Dispose Properly** - Always dispose transport when done

## Troubleshooting

### "Connection refused"

**Problem**: Client cannot connect to server
**Solutions**:
- Verify server is running and listening
- Check host/port configuration
- Verify firewall rules allow connection
- Check network connectivity

### "Connection timeout"

**Problem**: Connection takes too long
**Solutions**:
- Increase `Connection.ConnectTimeout`
- Check network latency
- Verify server is responsive
- Check for network issues

### "Too many connections"

**Problem**: Server rejects new connections
**Solutions**:
- Increase `MaxConnections`
- Increase `Backlog`
- Close idle connections
- Use connection pooling on clients

### "Message too large"

**Problem**: Messages rejected for size
**Solutions**:
- Increase `MaxMessageSize`
- Reduce message payload
- Use compression
- Split large messages

### Poor Performance

**Problem**: Low throughput or high latency
**Solutions**:
- Enable `NoDelay` (disable Nagle)
- Increase buffer sizes
- Use connection pooling
- Check network bandwidth
- Profile application code
- Use length-prefixed framing

### Connection Drops

**Problem**: Connections close unexpectedly
**Solutions**:
- Increase `HeartbeatTimeout`
- Check network stability
- Verify keep-alive settings
- Monitor system resources
- Check for application exceptions

## Integration with Conduit

### Dependency Injection

```csharp
services.AddSingleton<TcpConfiguration>(sp =>
    new TcpConfiguration
    {
        IsServer = false,
        RemoteHost = Configuration["Tcp:Host"],
        RemotePort = int.Parse(Configuration["Tcp:Port"])
    });

services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
services.AddSingleton<ITransport, TcpTransport>();
```

### Configuration File

```json
{
  "Tcp": {
    "IsServer": false,
    "RemoteHost": "localhost",
    "RemotePort": 5000,
    "UseConnectionPooling": true,
    "ConnectionPoolSize": 5,
    "FramingProtocol": "LengthPrefixed",
    "NoDelay": true,
    "HeartbeatInterval": 30000
  }
}
```

## Dependencies

- **Conduit.Transports.Core** - Transport abstractions
- **Conduit.Serialization** - Message serialization
- **Microsoft.Extensions.Logging.Abstractions** (>= 8.0.0)

## Compatibility

- **.NET**: 8.0 or later
- **OS**: Windows, Linux, macOS
- **Protocols**: TCP/IPv4, TCP/IPv6

## License

This module is part of the Conduit Framework and is licensed under the Business Source License 1.1.

## See Also

- [Conduit.Transports.Core](../Conduit.Transports.Core/README.md) - Transport abstractions
- [Conduit.Serialization](../Conduit.Serialization/README.md) - Message serialization
- [System.Net.Sockets Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets)
