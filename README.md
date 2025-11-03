# Conduit Framework for .NET

[![Version](https://img.shields.io/badge/version-0.9.0--alpha-blue.svg)](VERSION)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A comprehensive messaging and component framework for building scalable, maintainable microservices and distributed systems in C#/.NET.

## Overview

Conduit is a plugin-based messaging framework that provides:
- **Component-Based Architecture**: Dynamic discovery and lifecycle management of pluggable components
- **CQRS Pattern**: First-class support for Commands, Queries, and Events
- **Pipeline Processing**: Flexible behavior chain for message processing
- **Multi-Transport Support**: AMQP/RabbitMQ, gRPC, TCP, and Kafka adapters
- **Enterprise Features**: Saga orchestration, security, resilience patterns, and metrics
- **Hot Reload**: Dynamic component loading and unloading without restart

## Project Structure

```
Conduit/
├── src/
│   ├── Conduit.Api/                 # Core interfaces and contracts
│   ├── Conduit.Core/                # Framework implementation
│   ├── Conduit.Common/              # Shared utilities
│   ├── Conduit.Pipeline/            # Pipeline framework
│   ├── Conduit.Components/          # Component system
│   ├── Conduit.Messaging/           # Message bus and routing
│   ├── Conduit.Saga/                # Distributed transaction orchestration
│   ├── Conduit.Security/            # Authentication and authorization
│   ├── Conduit.Resilience/          # Circuit breakers and retry policies
│   ├── Conduit.Persistence/         # Database adapters
│   ├── Conduit.Serialization/       # Multi-format serialization
│   ├── Conduit.Transports/          # Transport implementations
│   │   ├── Conduit.Transports.Core/
│   │   ├── Conduit.Transports.Amqp/
│   │   ├── Conduit.Transports.Grpc/
│   │   └── Conduit.Transports.Tcp/
│   └── Conduit.Application/         # Application host
├── tests/                           # Unit and integration tests
├── examples/                        # Example implementations
└── docker/                          # Docker configuration
```

## Quick Start

### Prerequisites
- .NET 8 SDK
- Docker (optional, for running infrastructure)

### Building the Framework

```bash
# Clone the repository
git clone https://github.com/conduit/conduit-dotnet.git
cd Conduit

# Build the solution
dotnet build

# Run tests
dotnet test

# Run with Docker
docker-compose up -d
```

### Creating a Component

```csharp
using Conduit.Api;

[Component("my-component", "My Component", "1.0.0")]
public class MyComponent : IPluggableComponent
{
    public string Id => "my-component";
    public string Name => "My Component";
    public string Version => "1.0.0";
    public string Description => "A sample component";

    public async Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken)
    {
        // Initialize component
        context.Logger.LogInformation("Component attached");
    }

    public IEnumerable<IBehaviorContribution> ContributeBehaviors()
    {
        // Return behaviors to add to the pipeline
        yield break;
    }

    public IEnumerable<MessageHandlerRegistration> RegisterHandlers()
    {
        // Register command, event, and query handlers
        yield return MessageHandlerRegistration.ForCommand<CreateOrderCommand, CreateOrderCommandHandler>();
    }
}
```

### Sending Commands

```csharp
public class CreateOrderCommand : ICommand<OrderCreatedResult>
{
    public string CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

// Send command
var result = await messageBus.SendAsync(new CreateOrderCommand
{
    CustomerId = "123",
    Items = new List<OrderItem> { ... }
});
```

### Publishing Events

```csharp
public class OrderCreatedEvent : IEvent
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
}

// Publish event
await messageBus.PublishAsync(new OrderCreatedEvent
{
    OrderId = "456",
    CustomerId = "123",
    TotalAmount = 99.99m
});
```

### Executing Queries

```csharp
public class GetOrderQuery : IQuery<Order>
{
    public string OrderId { get; set; }
}

// Execute query
var order = await messageBus.QueryAsync(new GetOrderQuery { OrderId = "456" });
```

## Core Concepts

### Components
Components are the building blocks of the Conduit framework. They can:
- Contribute behaviors to the message pipeline
- Register message handlers
- Provide services to other components
- Expose features for discovery

### Pipeline
The message pipeline processes all messages through a chain of behaviors. Components contribute behaviors that can:
- Validate messages
- Add security context
- Log and collect metrics
- Transform messages
- Handle errors

### Message Bus
The central message bus handles:
- Command routing (one handler)
- Event publishing (multiple handlers)
- Query execution (one handler)
- Pipeline execution

## Features

### 🔌 Plugin Architecture
- Dynamic component discovery
- Hot reload support
- Dependency management
- Isolation levels

### 📬 Messaging
- CQRS pattern implementation
- Request-response for commands/queries
- Pub-sub for events
- Message correlation and causation tracking

### 🔄 Pipeline Processing
- Behavior chain pattern
- Ordered behavior placement
- Interceptors and middleware
- Conditional behavior execution

### 🔒 Security
- Authentication and authorization
- Security context propagation
- Audit logging
- Encryption support

### 💪 Resilience
- Circuit breaker pattern
- Retry policies with backoff
- Timeout management
- Bulkhead isolation

### 📊 Observability
- Metrics collection (Prometheus compatible)
- Distributed tracing support
- Structured logging
- Health checks

### 🗄️ Persistence
- Multiple database support (PostgreSQL, MongoDB, Redis)
- Caching abstraction
- Transaction management
- Repository pattern

### 🚀 Transports
- AMQP/RabbitMQ
- gRPC
- TCP/Sockets
- Kafka (planned)

## Configuration

Configuration is handled through appsettings.json:

```json
{
  "Conduit": {
    "Components": {
      "DiscoveryPath": "./components",
      "EnableHotReload": true
    },
    "Messaging": {
      "DefaultTimeout": 30000,
      "MaxRetries": 3
    },
    "Transports": {
      "Amqp": {
        "ConnectionString": "amqp://localhost:5672"
      }
    }
  }
}
```

## Documentation

- [Getting Started Guide](docs/getting-started.md)
- [Component Development](docs/components.md)
- [Message Patterns](docs/messaging.md)
- [Pipeline Configuration](docs/pipeline.md)
- [API Reference](docs/api/index.md)

## Examples

See the `/examples` directory for complete examples:
- **OrderService**: CQRS implementation with event sourcing
- **NotificationService**: Event-driven notifications
- **GatewayService**: API gateway with routing

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

This project is licensed under the Business Source License 1.1 - see the [LICENSE](LICENSE) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/conduit/conduit-dotnet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/conduit/conduit-dotnet/discussions)
- **Documentation**: [https://docs.conduit.io](https://docs.conduit.io)

## Acknowledgments

This is a C# port of the original Java Conduit framework, maintaining architectural compatibility while leveraging .NET ecosystem features.