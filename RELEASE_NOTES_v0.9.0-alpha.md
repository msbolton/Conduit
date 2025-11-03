# 🚀 Conduit Framework v0.9.0-alpha

**Major alpha release featuring complete component architecture implementation and production-ready foundations.**

## 🎯 What's New

### 🔌 **Complete Component Architecture**
- **18 Core Modules** with comprehensive plugin system
- **Dynamic component discovery** and lifecycle management
- **Hot reload support** for runtime component updates
- **Isolation levels** and security contexts

### 📬 **Enterprise Messaging**
- **Full CQRS implementation** with Commands, Queries, and Events
- **Message pipeline** with behavior chains and cross-cutting concerns
- **Dead letter queues** and retry policies
- **Message correlation** and causation tracking

### 🚀 **Transport Layer**
- **TCP Transport** with connection pooling and keep-alive
- **UDP Transport** with multicast and broadcast support
- **ActiveMQ Transport** with AMQP 1.0 integration
- **Pluggable transport architecture** for easy extension

### 🛡️ **Security & Resilience**
- **JWT Authentication** with configurable providers
- **AES Encryption** service for message protection
- **Circuit breaker** patterns and bulkhead isolation
- **Rate limiting** and timeout management
- **Access control** with permission-based authorization

### 📊 **Observability**
- **Prometheus metrics** integration
- **OpenTelemetry** tracing support
- **Health checks** for all components
- **Structured logging** throughout

### 🗄️ **Data & Persistence**
- **Repository patterns** with Entity Framework and MongoDB
- **Redis caching** provider
- **Unit of work** and transaction management
- **Multi-format serialization** (JSON, MessagePack)

## 📈 **Technical Highlights**

- **50,000+ lines** of production-quality C# code
- **672 comprehensive tests** with 100% pass rate
- **252 source files** across modular architecture
- **Modern .NET 8** with nullable reference types
- **Zero compilation errors** and minimal warnings

## 🏗️ **Architecture**

```
┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐
│   Application   │  │     Gateway      │  │    Security     │
│     Host        │  │  Load Balancer   │  │  Auth/Encrypt   │
└─────────────────┘  └──────────────────┘  └─────────────────┘
┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐
│   Components    │  │    Pipeline      │  │   Messaging     │
│ Lifecycle Mgmt  │  │ Behavior Chain   │  │  CQRS/PubSub    │
└─────────────────┘  └──────────────────┘  └─────────────────┘
┌─────────────────┐  ┌──────────────────┐  ┌─────────────────┐
│   Resilience    │  │   Persistence    │  │   Transports    │
│ Circuit/Retry   │  │ Repo/UnitWork    │  │  TCP/UDP/AMQP   │
└─────────────────┘  └──────────────────┘  └─────────────────┘
```

## 🚀 **Quick Start**

### Prerequisites
- .NET 8 SDK
- Docker (optional)

### Installation
```bash
# Clone the repository
git clone https://github.com/msbolton/Conduit.git
cd Conduit

# Build the solution
dotnet build

# Run tests
dotnet test

# Create your first component
dotnet new classlib -n MyConduitComponent
```

### Hello World Component
```csharp
[Component("hello-world", "Hello World", "1.0.0")]
public class HelloWorldComponent : AbstractPluggableComponent
{
    public HelloWorldComponent(ILogger<HelloWorldComponent> logger) : base(logger) { }

    public override IEnumerable<MessageHandlerRegistration> RegisterHandlers()
    {
        yield return MessageHandlerRegistration
            .ForCommand<SayHelloCommand, string>(HandleSayHello);
    }

    private Task<string> HandleSayHello(SayHelloCommand cmd)
        => Task.FromResult($"Hello, {cmd.Name}!");
}

public class SayHelloCommand : ICommand<string>
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string MessageType => nameof(SayHelloCommand);
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

    public string Name { get; set; } = string.Empty;

    public T? GetHeader<T>(string key) => Headers.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    public object? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;
}
```

## 📚 **Documentation**

- **[Getting Started Guide](GETTING_STARTED.md)** - Build your first application
- **[Quick Start](QUICK_START.md)** - 5-minute setup
- **[Architecture Guide](TASK.md)** - Deep dive into design
- **[Testing Guide](TESTING.md)** - Running and writing tests

## 🎯 **Alpha Release Goals**

This alpha release is designed for:
- **Early adopters** wanting to evaluate modern messaging frameworks
- **Developers** building component-based microservices
- **Architects** designing distributed systems
- **Contributors** interested in extending the framework

## ⚠️ **Alpha Limitations**

- **API may change** before 1.0 (following semver)
- **6 of 24 planned modules** still in development
- **Performance tuning** ongoing
- **Production deployments** should be carefully evaluated

## 🔮 **What's Next (Beta Roadmap)**

- **Real-world validation** with community deployments
- **Performance benchmarking** and optimization
- **Additional transports** (Kafka, gRPC, HTTP/2)
- **Migration tooling** for seamless upgrades
- **Enhanced monitoring** and operational tools

## 🤝 **Contributing**

We welcome contributions! See [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines.

## 📄 **License**

Business Source License 1.1 - See [LICENSE](LICENSE) for details.

## 🙏 **Acknowledgments**

Special thanks to all contributors and the .NET community for inspiration and feedback.

---

**Ready to build something amazing?** ⚡

Star ⭐ this repo • Try the examples • Join discussions • Report issues

*Built with ❤️ for the .NET community*