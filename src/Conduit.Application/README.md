# Conduit.Application

Application host and bootstrap for building production-ready Conduit framework applications with integrated dependency injection, configuration management, and lifecycle control.

## Features

- **Generic Host Integration**: Built on Microsoft.Extensions.Hosting
- **Dependency Injection**: Full DI container with all Conduit services
- **Configuration Management**: JSON, environment variables, command line
- **Fluent Builder API**: Intuitive application setup
- **Component Discovery**: Automatic component registration
- **Lifecycle Management**: Graceful startup and shutdown
- **Module Integration**: Seamless integration of all Conduit modules
- **Logging**: Structured logging with multiple providers
- **Feature Flags**: Runtime feature toggle support

## Quick Start

### Basic Application

```csharp
using Conduit.Application;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = ConduitHostBuilder
            .CreateDefaultBuilder(args)
            .ConfigureConduit(config =>
            {
                config.ApplicationName = "My App";
                config.Version = "1.0.0";
            })
            .Build();

        await host.RunAsync();
    }
}
```

### With Configuration File

```csharp
var host = ConduitHostBuilder
    .CreateDefaultBuilder(args)
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables("CONDUIT_")
    .Build();

await host.RunAsync();
```

### With Custom Services

```csharp
var host = ConduitHostBuilder
    .CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Add your services
        services.AddTransient<IMyService, MyService>();

        // Add command handlers
        services.AddCommandHandler<CreateOrderCommand, OrderResponse, CreateOrderHandler>();

        // Add event handlers
        services.AddEventHandler<OrderCreatedEvent, OrderCreatedHandler>();

        // Add query handlers
        services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();
    })
    .Build();

await host.RunAsync();
```

## Configuration

### appsettings.json

```json
{
  "Conduit": {
    "ApplicationName": "My Conduit Application",
    "Version": "1.0.0",
    "Environment": "Development",

    "ComponentDiscovery": {
      "Enabled": true,
      "AssembliesToScan": ["MyApp.Components"],
      "PluginDirectories": ["./plugins"],
      "EnableHotReload": false
    },

    "Messaging": {
      "Enabled": true,
      "MaxRetryAttempts": 3,
      "MaxConcurrentMessages": 100
    },

    "Security": {
      "EnableAuthentication": true,
      "JwtSecretKey": "your-secret-key",
      "JwtIssuer": "MyApp",
      "JwtExpirationMinutes": 60
    },

    "Resilience": {
      "EnableCircuitBreaker": true,
      "CircuitBreakerFailureThreshold": 5,
      "EnableRetry": true,
      "DefaultRetryCount": 3
    },

    "Features": {
      "EnableMetrics": true,
      "EnableHealthChecks": true
    }
  }
}
```

### Configuration in Code

```csharp
.ConfigureConduit(config =>
{
    config.ApplicationName = "My App";
    config.Version = "1.0.0";
    config.Environment = "Production";

    // Component discovery
    config.ComponentDiscovery.Enabled = true;
    config.ComponentDiscovery.AssembliesToScan.Add("MyApp.Components");
    config.ComponentDiscovery.PluginDirectories.Add("./plugins");

    // Messaging
    config.Messaging.Enabled = true;
    config.Messaging.MaxConcurrentMessages = 200;

    // Security
    config.Security.EnableAuthentication = true;
    config.Security.JwtSecretKey = "secret";

    // Feature flags
    config.Features["EnableMetrics"] = true;
    config.Features["EnableSwagger"] = true;
})
```

## Builder API

### ConduitHostBuilder Methods

```csharp
// Create builder
var builder = ConduitHostBuilder.CreateDefaultBuilder(args);

// Configure Conduit
builder.ConfigureConduit(config => { /* ... */ });

// Configure services
builder.ConfigureServices(services => { /* ... */ });

// Configure app configuration
builder.ConfigureAppConfiguration((context, config) => { /* ... */ });

// Configure logging
builder.ConfigureLogging((context, logging) => { /* ... */ });

// Set environment
builder.UseEnvironment("Production");

// Set content root
builder.UseContentRoot("/app");

// Add configuration sources
builder.AddJsonFile("appsettings.json");
builder.AddEnvironmentVariables("MYAPP_");
builder.AddCommandLine(args);

// Build
var host = builder.Build();
```

## Dependency Injection

All Conduit modules are automatically registered with the DI container.

### Core Services

```csharp
// Registered automatically
IComponentRegistry componentRegistry
IComponentLifecycleManager lifecycleManager
IMetricsCollector metricsCollector
IComponentFactory componentFactory
IComponentContainer componentContainer
```

### Messaging Services

```csharp
// When Messaging.Enabled = true
IMessageBus messageBus
IHandlerRegistry handlerRegistry
ISubscriptionManager subscriptionManager
IMessageCorrelator correlator
IDeadLetterQueue deadLetterQueue
IFlowController flowController
```

### Security Services

```csharp
// When Security.EnableAuthentication = true
IAuthenticationProvider authProvider

// When Security.EnableEncryption = true
IEncryptionService encryptionService

// When Security.EnableAuthorization = true
IAccessControl accessControl
```

### Resilience Services

```csharp
// When resilience features enabled
IResiliencePolicyRegistry policyRegistry
// + default policies (circuit-breaker, retry, timeout)
```

### Serialization Services

```csharp
// Registered automatically
ISerializerRegistry serializerRegistry
IMessageSerializer (JSON)
IMessageSerializer (MessagePack)
```

## Handler Registration

### Command Handlers

```csharp
// Define command
public class CreateOrderCommand : ICommand<OrderResponse>
{
    public string CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

// Define handler
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, OrderResponse>
{
    public async Task<OrderResponse> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Handle command
        return new OrderResponse { OrderId = Guid.NewGuid() };
    }
}

// Register
services.AddCommandHandler<CreateOrderCommand, OrderResponse, CreateOrderHandler>();
```

### Event Handlers

```csharp
// Define event
public class OrderCreatedEvent : IEvent
{
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Define handler
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        // Handle event
        await Task.CompletedTask;
    }
}

// Register
services.AddEventHandler<OrderCreatedEvent, OrderCreatedHandler>();
```

### Query Handlers

```csharp
// Define query
public class GetOrderQuery : IQuery<OrderDto>
{
    public Guid OrderId { get; set; }
}

// Define handler
public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        // Handle query
        return new OrderDto { Id = query.OrderId };
    }
}

// Register
services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();
```

## Logging

### Configuration

```csharp
builder.ConfigureLogging((context, logging) =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();

    if (context.HostingEnvironment.IsDevelopment())
    {
        logging.SetMinimumLevel(LogLevel.Debug);
    }
    else
    {
        logging.SetMinimumLevel(LogLevel.Information);
    }
});
```

### In appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Conduit": "Debug"
    }
  }
}
```

### Using ILogger

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.LogInformation("Doing work");
        _logger.LogDebug("Debug information");
        _logger.LogError(ex, "An error occurred");
    }
}
```

## Lifecycle Management

### Application Lifecycle

The host manages the complete application lifecycle:

1. **Configuration Loading**: JSON, environment variables, command line
2. **Service Registration**: All Conduit and custom services
3. **Component Discovery**: Scan and register pluggable components
4. **Initialization**: Initialize message bus, security, resilience
5. **Startup**: Start hosted services and components
6. **Running**: Process messages and handle requests
7. **Shutdown**: Graceful shutdown of all services
8. **Disposal**: Clean up resources

### Graceful Shutdown

```csharp
var host = builder.Build();

// Register shutdown handlers
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("Application started");
});

lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Application stopping");
});

lifetime.ApplicationStopped.Register(() =>
{
    Console.WriteLine("Application stopped");
});

await host.RunAsync();
```

## Component Discovery

### Automatic Discovery

```csharp
.ConfigureConduit(config =>
{
    config.ComponentDiscovery.Enabled = true;
    config.ComponentDiscovery.AssembliesToScan.Add("MyApp.Components");
    config.ComponentDiscovery.PluginDirectories.Add("./plugins");
})
```

### Plugin Directory Structure

```
./plugins/
├── MyPlugin.dll
├── MyPlugin.deps.json
└── dependencies/
    └── SomeDependency.dll
```

### Hot Reload (Development)

```csharp
.ConfigureConduit(config =>
{
    config.ComponentDiscovery.EnableHotReload = true;
    config.ComponentDiscovery.HotReloadInterval = 5000; // 5 seconds
})
```

## Feature Flags

### Defining Features

```csharp
.ConfigureConduit(config =>
{
    config.Features["EnableMetrics"] = true;
    config.Features["EnableHealthChecks"] = true;
    config.Features["EnableSwagger"] = false;
    config.Features["BetaFeature"] = true;
})
```

### Using Features

```csharp
public class MyService
{
    private readonly ConduitConfiguration _config;

    public MyService(IOptions<ConduitConfiguration> config)
    {
        _config = config.Value;
    }

    public void DoWork()
    {
        if (_config.Features.GetValueOrDefault("BetaFeature", false))
        {
            // Use beta feature
        }
    }
}
```

## Environment-Specific Configuration

### appsettings.Development.json

```json
{
  "Conduit": {
    "Environment": "Development",
    "Messaging": {
      "MaxConcurrentMessages": 10
    },
    "Security": {
      "EnableAuthentication": false
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### appsettings.Production.json

```json
{
  "Conduit": {
    "Environment": "Production",
    "Messaging": {
      "MaxConcurrentMessages": 500
    },
    "Security": {
      "EnableAuthentication": true,
      "JwtSecretKey": "${JWT_SECRET}"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### Loading Environment-Specific Config

```csharp
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var host = ConduitHostBuilder
    .CreateDefaultBuilder(args)
    .UseEnvironment(environment)
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .Build();
```

## Environment Variables

### Prefix-Based Loading

```csharp
builder.AddEnvironmentVariables("CONDUIT_");
```

Environment variables with `CONDUIT_` prefix will override settings:

```bash
export CONDUIT_ApplicationName="My App"
export CONDUIT_Messaging__MaxConcurrentMessages=200
export CONDUIT_Security__JwtSecretKey="secret"
```

### Hierarchical Configuration

Use double underscore `__` for nested properties:

```bash
CONDUIT_Messaging__MaxConcurrentMessages=200
CONDUIT_Security__EnableAuthentication=true
CONDUIT_Features__EnableMetrics=true
```

## Command Line Arguments

```bash
dotnet run --Conduit:ApplicationName="My App" --Conduit:Environment=Production
```

```csharp
builder.AddCommandLine(args);
```

## Complete Example

```csharp
using System;
using System.Threading.Tasks;
using Conduit.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class Program
{
    public static async Task Main(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var host = ConduitHostBuilder
            .CreateDefaultBuilder(args)
            .UseEnvironment(environment)
            .UseContentRoot(Directory.GetCurrentDirectory())

            // Configuration sources (in order of precedence)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables("CONDUIT_")
            .AddCommandLine(args)

            // Conduit configuration
            .ConfigureConduit(config =>
            {
                config.ApplicationName = "Order Processing Service";
                config.Version = "2.1.0";

                // Enable features
                config.Features["EnableMetrics"] = true;
                config.Features["EnableHealthChecks"] = true;

                // Custom settings
                config.CustomSettings["DatabaseConnectionString"] = "...";
            })

            // Service registration
            .ConfigureServices(services =>
            {
                // Domain services
                services.AddTransient<IOrderService, OrderService>();
                services.AddTransient<IInventoryService, InventoryService>();

                // Handlers
                services.AddCommandHandler<CreateOrderCommand, OrderResponse, CreateOrderHandler>();
                services.AddEventHandler<OrderCreatedEvent, SendOrderConfirmationHandler>();
                services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();

                // Repositories
                services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
            })

            // Logging
            .ConfigureLogging((context, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();

                if (context.HostingEnvironment.IsDevelopment())
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
            })

            .Build();

        // Run the application
        await host.RunAsync();
    }
}
```

## Best Practices

### 1. Use Configuration Files

```csharp
// Good: Externalized configuration
.AddJsonFile("appsettings.json")
.AddJsonFile($"appsettings.{environment}.json", optional: true)

// Bad: Hardcoded configuration
.ConfigureConduit(config =>
{
    config.Security.JwtSecretKey = "hardcoded-secret"; // Never do this
})
```

### 2. Environment-Specific Settings

```csharp
// Use environment-specific overrides
appsettings.Development.json
appsettings.Staging.json
appsettings.Production.json
```

### 3. Secure Secrets

```csharp
// Use environment variables for secrets
export CONDUIT_Security__JwtSecretKey="production-secret"

// Or use Azure Key Vault, AWS Secrets Manager, etc.
```

### 4. Register Handlers Early

```csharp
// Register all handlers during configuration
.ConfigureServices(services =>
{
    // All command handlers
    services.AddCommandHandler<Cmd1, Res1, Handler1>();
    services.AddCommandHandler<Cmd2, Res2, Handler2>();

    // All event handlers
    services.AddEventHandler<Event1, EventHandler1>();
    services.AddEventHandler<Event2, EventHandler2>();
})
```

### 5. Use Structured Logging

```csharp
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}",
    order.Id,
    order.CustomerId);

// Not: _logger.LogInformation($"Order {order.Id} created...");
```

## Troubleshooting

### Issue: Configuration Not Loading

**Solution**: Check file path and optional flag

```csharp
.AddJsonFile("appsettings.json", optional: false) // Will throw if missing
```

### Issue: Services Not Resolving

**Solution**: Ensure services are registered before calling Build()

```csharp
.ConfigureServices(services =>
{
    services.AddTransient<IMyService, MyService>();
})
.Build(); // Must be after ConfigureServices
```

### Issue: Environment Variables Not Working

**Solution**: Use correct prefix and format

```bash
# Correct
export CONDUIT_Messaging__MaxConcurrentMessages=200

# Wrong
export CONDUIT_Messaging:MaxConcurrentMessages=200
```

## Version History

- **0.5.0** (Current)
  - Initial release
  - Generic Host integration
  - Full DI support
  - Configuration management
  - Component discovery
  - All module integration

## License

Part of the Conduit framework. See main repository for license information.

## Related Modules

- **Conduit.Core**: Component system and lifecycle
- **Conduit.Messaging**: Message bus and CQRS
- **Conduit.Security**: Authentication and authorization
- **Conduit.Resilience**: Circuit breakers and retry
- **Conduit.Persistence**: Data access

## Support

For issues, questions, or contributions, please refer to the main Conduit repository.
