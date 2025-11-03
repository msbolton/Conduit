# 🚀 Getting Started with Conduit v0.9.0-alpha

Welcome to the Conduit Framework! This guide will help you build your first application using the v0.9.0-alpha release.

## 📋 Prerequisites

- **.NET 8 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Visual Studio 2022** or **VS Code** (recommended)
- **Docker** (optional, for examples with infrastructure)

## 🎯 Your First Conduit Application

Let's build a simple order processing system to showcase the framework's capabilities.

### Step 1: Create a New Project

```bash
# Create a new console application
dotnet new console -n MyConduitApp
cd MyConduitApp

# Add Conduit packages (when published to NuGet)
dotnet add package Conduit.Core --version 0.9.0-alpha
dotnet add package Conduit.Messaging --version 0.9.0-alpha
dotnet add package Conduit.Components --version 0.9.0-alpha
```

**For now (pre-NuGet), clone and reference locally:**
```bash
git clone https://github.com/msbolton/Conduit.git
cd MyConduitApp

# Add local project references
dotnet add reference ../Conduit/src/Conduit.Api/Conduit.Api.csproj
dotnet add reference ../Conduit/src/Conduit.Core/Conduit.Core.csproj
dotnet add reference ../Conduit/src/Conduit.Messaging/Conduit.Messaging.csproj
dotnet add reference ../Conduit/src/Conduit.Components/Conduit.Components.csproj
dotnet add reference ../Conduit/src/Conduit.Application/Conduit.Application.csproj
```

### Step 2: Define Your Messages

Create `Messages.cs`:

```csharp
using Conduit.Api;

namespace MyConduitApp;

// Command to create an order
public class CreateOrderCommand : ICommand<OrderCreatedResult>
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string MessageType => nameof(CreateOrderCommand);
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
    public string? CausationId { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    public bool IsSystemMessage { get; set; }
    public TimeSpan? Ttl { get; set; }
    public bool IsExpired => Ttl.HasValue && Timestamp.Add(Ttl.Value) < DateTimeOffset.UtcNow;
    public object? Payload { get; set; }

    // Command properties
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<OrderItem> Items { get; set; } = new();

    public T? GetHeader<T>(string key) => Headers.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    public object? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;
}

// Event published when order is created
public class OrderCreatedEvent : IEvent
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string MessageType => nameof(OrderCreatedEvent);
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
    public string? CausationId { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    public bool IsSystemMessage { get; set; }
    public TimeSpan? Ttl { get; set; }
    public bool IsExpired => Ttl.HasValue && Timestamp.Add(Ttl.Value) < DateTimeOffset.UtcNow;
    public object? Payload { get; set; }

    // Event properties
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public long SequenceNumber { get; set; }

    public T? GetHeader<T>(string key) => Headers.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    public object? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;
}

// Query to get order details
public class GetOrderQuery : IQuery<OrderDetails>
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string MessageType => nameof(GetOrderQuery);
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
    public string? CausationId { get; set; }
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    public bool IsSystemMessage { get; set; }
    public TimeSpan? Ttl { get; set; }
    public bool IsExpired => Ttl.HasValue && Timestamp.Add(Ttl.Value) < DateTimeOffset.UtcNow;
    public object? Payload { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public T? GetHeader<T>(string key) => Headers.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    public object? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;
}

// Supporting types
public class OrderCreatedResult
{
    public string OrderId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class OrderDetails
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class OrderItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

### Step 3: Create Your Component

Create `OrderComponent.cs`:

```csharp
using Conduit.Api;
using Conduit.Components;
using Microsoft.Extensions.Logging;

namespace MyConduitApp;

public class OrderComponent : AbstractPluggableComponent
{
    private readonly Dictionary<string, OrderDetails> _orders = new();

    public OrderComponent(ILogger<OrderComponent> logger) : base(logger)
    {
        // Set up component manifest
        Manifest = new ComponentManifest
        {
            Id = "order-processor",
            Name = "Order Processing Component",
            Version = "1.0.0",
            Description = "Handles order creation and queries",
            Author = "MyConduitApp",
            MinFrameworkVersion = "0.9.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "orders", "business-logic" }
        };
    }

    public override IEnumerable<MessageHandlerRegistration> RegisterHandlers()
    {
        yield return MessageHandlerRegistration.ForCommand<CreateOrderCommand, OrderCreatedResult>(HandleCreateOrder);
        yield return MessageHandlerRegistration.ForQuery<GetOrderQuery, OrderDetails>(HandleGetOrder);
        yield return MessageHandlerRegistration.ForEvent<OrderCreatedEvent>(HandleOrderCreated);
    }

    private async Task<OrderCreatedResult> HandleCreateOrder(CreateOrderCommand command)
    {
        Logger?.LogInformation("Creating order for customer: {CustomerName}", command.CustomerName);

        var orderId = Guid.NewGuid().ToString();
        var orderDetails = new OrderDetails
        {
            OrderId = orderId,
            CustomerName = command.CustomerName,
            Amount = command.Amount,
            Items = command.Items,
            CreatedAt = DateTime.UtcNow
        };

        _orders[orderId] = orderDetails;

        // Simulate some processing
        await Task.Delay(100);

        Logger?.LogInformation("Order created successfully: {OrderId}", orderId);

        return new OrderCreatedResult
        {
            OrderId = orderId,
            Success = true,
            Message = "Order created successfully"
        };
    }

    private Task<OrderDetails> HandleGetOrder(GetOrderQuery query)
    {
        Logger?.LogInformation("Retrieving order: {OrderId}", query.OrderId);

        if (_orders.TryGetValue(query.OrderId, out var order))
        {
            return Task.FromResult(order);
        }

        throw new InvalidOperationException($"Order not found: {query.OrderId}");
    }

    private Task HandleOrderCreated(OrderCreatedEvent orderEvent)
    {
        Logger?.LogInformation("Order created event received: {OrderId}", orderEvent.OrderId);

        // Here you could trigger other business processes:
        // - Send confirmation email
        // - Update inventory
        // - Start fulfillment process

        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        yield return new ComponentFeature
        {
            Id = "OrderProcessing",
            Name = "Order Processing",
            Description = "Create and manage customer orders",
            Version = Version,
            IsEnabledByDefault = true
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        // This component doesn't provide additional services
        return Enumerable.Empty<ServiceContract>();
    }

    public override IEnumerable<IBehaviorContribution> ContributeBehaviors()
    {
        // This component doesn't contribute pipeline behaviors
        return Enumerable.Empty<IBehaviorContribution>();
    }
}
```

### Step 4: Configure Your Application

Update `Program.cs`:

```csharp
using Conduit.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyConduitApp;

Console.WriteLine("🚀 Starting Conduit Order Processing Application...");

// Build the host
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Add Conduit services
        services.AddConduit(builder =>
        {
            builder.AddComponent<OrderComponent>();
        });

        // Add logging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

// Get the message bus
var messageBus = host.Services.GetRequiredService<IMessageBus>();

Console.WriteLine("✅ Application started! Processing sample orders...");

try
{
    // Create a sample order
    var createOrder = new CreateOrderCommand
    {
        CustomerName = "John Doe",
        Amount = 99.99m,
        Items = new List<OrderItem>
        {
            new() { Name = "Widget A", Quantity = 2, Price = 29.99m },
            new() { Name = "Widget B", Quantity = 1, Price = 39.99m }
        }
    };

    // Send the command
    var result = await messageBus.SendAsync(createOrder);
    Console.WriteLine($"📦 Order created: {result.OrderId}");

    // Query the order
    var getOrder = new GetOrderQuery { OrderId = result.OrderId };
    var orderDetails = await messageBus.QueryAsync(getOrder);

    Console.WriteLine($"📋 Order details retrieved:");
    Console.WriteLine($"   Customer: {orderDetails.CustomerName}");
    Console.WriteLine($"   Amount: ${orderDetails.Amount:F2}");
    Console.WriteLine($"   Items: {orderDetails.Items.Count}");

    // Publish an event
    var orderCreatedEvent = new OrderCreatedEvent
    {
        OrderId = result.OrderId,
        CustomerName = orderDetails.CustomerName,
        Amount = orderDetails.Amount
    };

    await messageBus.PublishAsync(orderCreatedEvent);
    Console.WriteLine($"📢 Order created event published");

}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}

Console.WriteLine("🎉 Application completed successfully!");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
```

### Step 5: Run Your Application

```bash
dotnet run
```

You should see output like:
```
🚀 Starting Conduit Order Processing Application...
✅ Application started! Processing sample orders...
📦 Order created: a1b2c3d4-e5f6-7890-abcd-ef1234567890
📋 Order details retrieved:
   Customer: John Doe
   Amount: $99.99
   Items: 2
📢 Order created event published
🎉 Application completed successfully!
```

## 🎯 What You've Built

Congratulations! You've created a complete Conduit application with:

- **✅ Component Architecture** - Order processing as a pluggable component
- **✅ CQRS Pattern** - Commands for mutations, queries for reads, events for notifications
- **✅ Message Bus** - Central routing for all message types
- **✅ Lifecycle Management** - Proper component initialization and cleanup
- **✅ Logging Integration** - Structured logging throughout

## 🚀 Next Steps

### Explore More Features

1. **Add Security** - Integrate authentication and authorization
2. **Add Persistence** - Store orders in a database
3. **Add Transports** - Enable distributed messaging
4. **Add Resilience** - Implement retry policies and circuit breakers
5. **Add Metrics** - Monitor performance and health

### Learn Advanced Patterns

- **[Component Development](docs/components.md)** - Build reusable components
- **[Pipeline Behaviors](docs/pipeline.md)** - Add cross-cutting concerns
- **[Message Patterns](docs/messaging.md)** - Advanced messaging scenarios
- **[Transport Configuration](docs/transports.md)** - Distributed communication

### Join the Community

- **⭐ Star the repo** - [github.com/msbolton/Conduit](https://github.com/msbolton/Conduit)
- **💬 Join discussions** - Share your experiences
- **🐛 Report issues** - Help us improve
- **🤝 Contribute** - Add features and fixes

## 🆘 Troubleshooting

### Common Issues

**Build Errors:**
- Ensure .NET 8 SDK is installed
- Check project references are correct
- Run `dotnet restore` to restore packages

**Runtime Errors:**
- Check that handlers are registered properly
- Verify component inheritance from `AbstractPluggableComponent`
- Enable detailed logging to see what's happening

**Performance Issues:**
- This is an alpha release - performance optimization is ongoing
- Report performance issues on GitHub

### Getting Help

- **📚 Documentation** - Check the guides in the repo
- **💬 GitHub Discussions** - Ask questions and share ideas
- **🐛 GitHub Issues** - Report bugs and request features

---

**Happy coding with Conduit!** 🎉

*Built with ❤️ for the .NET community*