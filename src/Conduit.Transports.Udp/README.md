# Conduit.Transports.Udp

UDP transport implementation for the Conduit messaging framework with multicast and broadcast support.

## Overview

This module provides connectionless UDP/IP datagram connectivity for the Conduit framework, ideal for scenarios where low latency and simplicity are more important than guaranteed delivery.

### Key Features

- **Connectionless Protocol** - No connection overhead, fire-and-forget messaging
- **Multicast Support** - Send messages to multiple receivers efficiently
- **Broadcast Support** - Send to all devices on local network
- **Low Latency** - Minimal overhead compared to TCP
- **Message Fragmentation** - Automatic fragmentation for large messages (optional)
- **Configurable Buffers** - Tunable send/receive buffers
- **IPv4 and IPv6** - Support for both IP versions
- **Pause/Resume** - Subscription flow control

### UDP Characteristics

**Advantages:**
- Very low latency
- No connection setup overhead
- Efficient for small, frequent messages
- Multicast/broadcast capabilities
- Simpler than TCP

**Limitations:**
- No guaranteed delivery (packets may be lost)
- No ordering guarantees
- No flow control
- Max datagram size (~64KB)
- No built-in acknowledgements

**Best Use Cases:**
- Real-time monitoring and metrics
- Service discovery
- Game state updates
- Sensor data collection
- Time-sensitive notifications
- Video/audio streaming

## Installation

```bash
dotnet add package Conduit.Transports.Udp
```

## Quick Start

### Basic UDP Sender

```csharp
using Conduit.Transports.Udp;
using Conduit.Serialization;
using Microsoft.Extensions.Logging;

// Create configuration
var config = new UdpConfiguration
{
    RemoteHost = "localhost",
    RemotePort = 9000,
    MaxDatagramSize = 8192
};

// Create transport
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<UdpTransport>();
var serializer = new JsonMessageSerializer();
var transport = new UdpTransport(config, serializer, logger);

// Connect
await transport.ConnectAsync();

// Send message
var message = new SensorData { Temperature = 22.5, Humidity = 65.0 };
await transport.SendAsync(message);
```

### Basic UDP Receiver

```csharp
// Create configuration
var config = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 9000,
    MaxDatagramSize = 8192
};

// Create transport
var transport = new UdpTransport(config, serializer, logger);

// Connect (starts listening)
await transport.ConnectAsync();

// Subscribe to messages
var subscription = await transport.SubscribeAsync(
    null, // Listen to all sources
    async (transportMessage) =>
    {
        Console.WriteLine($"Received: {transportMessage.MessageId} from {transportMessage.Source}");
        await Task.CompletedTask;
    });
```

## Configuration

### Basic Settings

```csharp
var config = new UdpConfiguration
{
    // Local binding
    Host = "0.0.0.0",
    Port = 9000,

    // Remote endpoint (for sending)
    RemoteHost = "192.168.1.100",
    RemotePort = 9001,

    // Datagram size
    MaxDatagramSize = 8192, // 8 KB (default: 65507)

    // Buffer sizes
    ReceiveBufferSize = 65536, // 64 KB
    SendBufferSize = 65536,

    // Timeouts
    ReceiveTimeout = 0,  // 0 = infinite
    SendTimeout = 5000,  // 5 seconds

    // Socket options
    ReuseAddress = true,
    UseIPv6 = false
};
```

### Multicast Configuration

```csharp
var config = new UdpConfiguration
{
    Port = 9000,
    MulticastGroup = "239.255.42.99", // Multicast address (239.0.0.0 - 239.255.255.255)
    MulticastTimeToLive = 1,           // 1 = local network only
    MulticastLoopback = true,          // Receive own multicast messages
    MulticastInterface = null          // null = default interface
};

// Multicast sender
var sender = new UdpTransport(config, serializer, logger);
await sender.ConnectAsync();
await sender.SendAsync(message); // Sent to all multicast group members

// Multicast receiver
var receiver = new UdpTransport(config, serializer, logger);
await receiver.ConnectAsync(); // Automatically joins multicast group
await receiver.SubscribeAsync(null, async (msg) => { /* Handle */ });
```

### Broadcast Configuration

```csharp
var config = new UdpConfiguration
{
    AllowBroadcast = true,
    RemotePort = 9000,
    Port = 0 // Ephemeral port for sender
};

// Broadcast sender
var sender = new UdpTransport(config, serializer, logger);
await sender.ConnectAsync();
await sender.SendAsync(message); // Sent to 255.255.255.255:9000

// Broadcast receiver
var receiverConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 9000,
    AllowBroadcast = true
};

var receiver = new UdpTransport(receiverConfig, serializer, logger);
await receiver.ConnectAsync();
await receiver.SubscribeAsync(null, handler);
```

### Fragmentation (for Large Messages)

```csharp
var config = new UdpConfiguration
{
    MaxDatagramSize = 65507,
    EnableFragmentation = true,
    FragmentSize = 1400 // Safe size to avoid IP fragmentation
};

// Messages larger than FragmentSize will be automatically fragmented
await transport.SendAsync(largeMessage); // Auto-fragmented if > 1400 bytes
```

## Advanced Usage

### Multicast Group Communication

```csharp
// Sender to multicast group
var senderConfig = new UdpConfiguration
{
    MulticastGroup = "239.255.10.1",
    MulticastTimeToLive = 2, // 2 hops
    Port = 0 // Ephemeral port
};

var sender = new UdpTransport(senderConfig, serializer, logger);
await sender.ConnectAsync();

// Broadcast to all group members
for (int i = 0; i < 100; i++)
{
    await sender.SendAsync(new Metric { Value = i });
    await Task.Delay(100);
}

// Multiple receivers join same group
var receiverConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 5000,
    MulticastGroup = "239.255.10.1"
};

var receiver1 = new UdpTransport(receiverConfig, serializer, logger1);
await receiver1.ConnectAsync();
await receiver1.SubscribeAsync(null, handler1);

var receiver2 = new UdpTransport(receiverConfig, serializer, logger2);
await receiver2.ConnectAsync();
await receiver2.SubscribeAsync(null, handler2);

// Both receivers get all messages
```

### Service Discovery Pattern

```csharp
// Service announcement (multicast)
var announcerConfig = new UdpConfiguration
{
    MulticastGroup = "239.255.0.1",
    Port = 5000,
    MulticastTimeToLive = 1
};

var announcer = new UdpTransport(announcerConfig, serializer, logger);
await announcer.ConnectAsync();

// Announce service every 5 seconds
var timer = new Timer(async _ =>
{
    var announcement = new ServiceAnnouncement
    {
        ServiceName = "MyService",
        Endpoint = "https://myservice.local:8080",
        Version = "1.0.0"
    };
    await announcer.SendAsync(announcement);
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

// Service discovery (multicast listener)
var discovererConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 5000,
    MulticastGroup = "239.255.0.1"
};

var discoverer = new UdpTransport(discovererConfig, serializer, logger);
await discoverer.ConnectAsync();

var services = new ConcurrentDictionary<string, ServiceAnnouncement>();

await discoverer.SubscribeAsync(null, async (transportMessage) =>
{
    var announcement = Deserialize<ServiceAnnouncement>(transportMessage.Payload);
    services.AddOrUpdate(announcement.ServiceName, announcement, (k, v) => announcement);
    Console.WriteLine($"Discovered: {announcement.ServiceName} at {announcement.Endpoint}");
    await Task.CompletedTask;
});
```

### Real-Time Metrics Collection

```csharp
// Metrics sender (runs on every server)
var senderConfig = new UdpConfiguration
{
    RemoteHost = "metrics-collector.local",
    RemotePort = 8125, // StatsD port
    MaxDatagramSize = 512
};

var metricsSender = new UdpTransport(senderConfig, serializer, logger);
await metricsSender.ConnectAsync();

// Send metrics continuously
while (true)
{
    var metric = new Metric
    {
        Name = "cpu.usage",
        Value = GetCpuUsage(),
        Timestamp = DateTimeOffset.UtcNow
    };

    await metricsSender.SendAsync(metric);
    await Task.Delay(1000);
}

// Metrics collector (aggregates from all servers)
var receiverConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 8125,
    ReceiveBufferSize = 262144 // 256 KB buffer for high throughput
};

var metricsReceiver = new UdpTransport(receiverConfig, serializer, logger);
await metricsReceiver.ConnectAsync();

await metricsReceiver.SubscribeAsync(null, async (transportMessage) =>
{
    var metric = Deserialize<Metric>(transportMessage.Payload);
    await StoreMetric(metric);
});
```

### Heartbeat/Keep-Alive Pattern

```csharp
// Send heartbeats
var config = new UdpConfiguration
{
    RemoteHost = "monitor.local",
    RemotePort = 9999
};

var heartbeat = new UdpTransport(config, serializer, logger);
await heartbeat.ConnectAsync();

var timer = new Timer(async _ =>
{
    var hb = new Heartbeat
    {
        ServiceId = "service-123",
        Timestamp = DateTimeOffset.UtcNow,
        Status = "healthy"
    };
    await heartbeat.SendAsync(hb);
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

// Monitor heartbeats
var monitorConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 9999
};

var monitor = new UdpTransport(monitorConfig, serializer, logger);
await monitor.ConnectAsync();

var lastHeartbeat = new ConcurrentDictionary<string, DateTimeOffset>();

await monitor.SubscribeAsync(null, async (transportMessage) =>
{
    var hb = Deserialize<Heartbeat>(transportMessage.Payload);
    lastHeartbeat.AddOrUpdate(hb.ServiceId, hb.Timestamp, (k, v) => hb.Timestamp);

    if (hb.Status != "healthy")
    {
        Console.WriteLine($"Warning: {hb.ServiceId} reports {hb.Status}");
    }

    await Task.CompletedTask;
});

// Check for dead services
var checker = new Timer(_ =>
{
    var now = DateTimeOffset.UtcNow;
    foreach (var (serviceId, lastSeen) in lastHeartbeat)
    {
        if ((now - lastSeen).TotalSeconds > 15)
        {
            Console.WriteLine($"Service {serviceId} is DOWN (last seen {lastSeen})");
        }
    }
}, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
```

### Broadcast Discovery

```csharp
// Client broadcasts "who's there?"
var clientConfig = new UdpConfiguration
{
    AllowBroadcast = true,
    RemotePort = 7000
};

var client = new UdpTransport(clientConfig, serializer, logger);
await client.ConnectAsync();

var discovery = new DiscoveryRequest { ClientId = "client-123" };
await client.SendAsync(discovery); // Broadcast

// Servers respond
var serverConfig = new UdpConfiguration
{
    Host = "0.0.0.0",
    Port = 7000,
    AllowBroadcast = true
};

var server = new UdpTransport(serverConfig, serializer, logger);
await server.ConnectAsync();

await server.SubscribeAsync(null, async (transportMessage) =>
{
    var request = Deserialize<DiscoveryRequest>(transportMessage.Payload);

    // Respond to client
    var response = new DiscoveryResponse
    {
        ServerId = "server-456",
        Endpoint = "http://192.168.1.50:8080"
    };

    // Send unicast response back to source
    await server.SendAsync(response, transportMessage.Source);
});
```

## Error Handling

### Handling Packet Loss

```csharp
// UDP doesn't guarantee delivery
// Implement application-level acknowledgement if needed

var ackTimeout = TimeSpan.FromSeconds(2);
var maxRetries = 3;

for (int retry = 0; retry < maxRetries; retry++)
{
    await transport.SendAsync(message);

    var ackReceived = await WaitForAck(message.MessageId, ackTimeout);

    if (ackReceived)
    {
        break;
    }

    _logger.LogWarning("No ACK for {MessageId}, retry {Retry}/{MaxRetries}",
        message.MessageId, retry + 1, maxRetries);
}
```

### Message Size Errors

```csharp
try
{
    await transport.SendAsync(largeMessage);
}
catch (InvalidOperationException ex)
{
    // Message too large for datagram
    _logger.LogError(ex, "Message exceeds max datagram size");

    // Enable fragmentation or split message manually
    config.EnableFragmentation = true;
}
```

### Socket Errors

```csharp
try
{
    await transport.ConnectAsync();
}
catch (SocketException ex)
{
    _logger.LogError(ex, "Failed to bind UDP socket");
    // Port may be in use or firewall blocking
}
```

## Performance Tuning

### High Throughput

```csharp
var config = new UdpConfiguration
{
    // Large buffers
    ReceiveBufferSize = 2097152, // 2 MB
    SendBufferSize = 2097152,

    // Large datagrams
    MaxDatagramSize = 65507,

    // No timeouts
    ReceiveTimeout = 0,
    SendTimeout = 0
};
```

### Low Latency

```csharp
var config = new UdpConfiguration
{
    // Small datagrams for minimum processing
    MaxDatagramSize = 1400,

    // Moderate buffers
    ReceiveBufferSize = 65536,
    SendBufferSize = 65536,

    // Disable fragmentation
    EnableFragmentation = false
};
```

### Multicast Optimization

```csharp
var config = new UdpConfiguration
{
    MulticastGroup = "239.255.10.1",
    MulticastTimeToLive = 1, // Local network only
    MulticastLoopback = false, // Don't receive own messages
    ReceiveBufferSize = 1048576 // 1 MB for many senders
};
```

## Monitoring

### Transport Statistics

```csharp
var stats = transport.GetStatistics();

Console.WriteLine($"Messages Sent: {stats.MessagesSent}");
Console.WriteLine($"Messages Received: {stats.MessagesReceived}");
Console.WriteLine($"Send Success Rate: {stats.SendSuccessRate:P}");
Console.WriteLine($"Average Send Time: {stats.AverageSendTimeMs}ms");

// Note: UDP doesn't track packet loss at transport level
// Implement application-level tracking if needed
```

## Best Practices

1. **Accept Packet Loss** - Design for occasional lost messages
2. **Keep Messages Small** - Under 1400 bytes to avoid IP fragmentation
3. **Use Multicast for One-to-Many** - More efficient than multiple unicasts
4. **Don't Expect Ordering** - Messages may arrive out of order
5. **Implement Application ACKs** - If reliability is needed
6. **Set Appropriate TTL** - For multicast scope control
7. **Monitor Buffer Usage** - Increase buffers if dropping messages
8. **Use Sequence Numbers** - Detect missing or duplicate messages
9. **Set Timeouts Appropriately** - Balance responsiveness vs. CPU usage
10. **Test with Packet Loss** - Simulate real-world conditions

## Troubleshooting

### Messages Not Received

**Problem**: Receiver not getting messages
**Solutions**:
- Verify firewall allows UDP on specified port
- Check receiver is bound to correct port
- For multicast, verify receiver joined group
- Check network connectivity
- Verify sender and receiver on same subnet (for multicast TTL=1)

### Multicast Not Working

**Problem**: Multicast messages not delivered
**Solutions**:
- Verify multicast address in valid range (224.0.0.0 - 239.255.255.255)
- Check MulticastTimeToLive is sufficient
- Verify router supports multicast (IGMP)
- Check firewall allows multicast
- Ensure receiver called JoinMulticastGroup

### Broadcast Not Working

**Problem**: Broadcast messages not delivered
**Solutions**:
- Set `AllowBroadcast = true`
- Verify using broadcast address (255.255.255.255)
- Check firewall allows broadcast
- Broadcast only works on local subnet

### Port Already in Use

**Problem**: Cannot bind to port
**Solutions**:
- Use different port
- Set `ReuseAddress = true` to allow multiple binds
- Check if another process is using the port
- On Windows, set `ExclusiveAddressUse = false`

### Message Too Large

**Problem**: Message exceeds max datagram size
**Solutions**:
- Enable `EnableFragmentation = true`
- Reduce message size
- Use compression
- Split into multiple messages
- Consider using TCP for large messages

### High Packet Loss

**Problem**: Many messages lost
**Solutions**:
- Increase receive buffer size
- Reduce send rate
- Check network congestion
- Verify receiver is processing fast enough
- Consider using TCP instead

## Comparison with TCP

| Feature | UDP | TCP |
|---------|-----|-----|
| Connection | Connectionless | Connection-oriented |
| Reliability | No guarantees | Guaranteed delivery |
| Ordering | No guarantees | Ordered delivery |
| Speed | Faster | Slower |
| Overhead | Lower | Higher |
| Multicast | Yes | No |
| Broadcast | Yes | No |
| Use Case | Real-time, metrics | Reliable transfers |

## Dependencies

- **Conduit.Transports.Core** - Transport abstractions
- **Conduit.Serialization** - Message serialization
- **Microsoft.Extensions.Logging.Abstractions** (>= 8.0.0)

## Compatibility

- **.NET**: 8.0 or later
- **OS**: Windows, Linux, macOS
- **Protocols**: UDP/IPv4, UDP/IPv6, Multicast, Broadcast

## License

This module is part of the Conduit Framework and is licensed under the Business Source License 1.1.

## See Also

- [Conduit.Transports.Core](../Conduit.Transports.Core/README.md) - Transport abstractions
- [Conduit.Transports.Tcp](../Conduit.Transports.Tcp/README.md) - TCP transport
- [System.Net.Sockets.UdpClient Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient)
- [UDP Protocol (RFC 768)](https://www.rfc-editor.org/rfc/rfc768)
- [IP Multicast (RFC 1112)](https://www.rfc-editor.org/rfc/rfc1112)
