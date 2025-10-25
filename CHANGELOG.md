# Changelog

All notable changes to the Conduit Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2025-10-25

### Added
- **Conduit.Api** - Core interfaces and contracts
  - IMessage, ICommand, IEvent, IQuery interfaces
  - ICommandHandler, IEventHandler, IQueryHandler interfaces
  - IPluggableComponent and IBehaviorContribution interfaces
  - IMessageBus and IPipeline abstractions
  - Component attributes and metadata types

- **Conduit.Common** - Shared utilities and extensions
  - Guard clauses for parameter validation
  - Result<T> pattern for error handling
  - Collection, string, and task extensions
  - Async helpers (retry, timeout, debounce, throttle)
  - Thread-safe collections (ConcurrentHashSet, LruCache)
  - Reflection and assembly scanning utilities

- **Conduit.Core** - Framework implementation
  - Component registry with lifecycle management (13-state machine)
  - Component validator with comprehensive validation
  - Event dispatcher with async support
  - Dependency resolver with topological sorting
  - Component lifecycle manager
  - Plugin load context for assembly isolation
  - Multiple discovery strategies (classpath, directory, watching, service loader)

- **Conduit.Pipeline** - Pipeline framework
  - Generic pipeline implementation with behavior chain
  - Pipeline stages with pre/post processing
  - Composition operations (map, filter, branch, parallel, cache)
  - Interceptors and metrics collection
  - Resilience features (retry, timeout, fallback)

- **Conduit.Messaging** - Message bus implementation
  - CQRS support (Commands, Events, Queries)
  - Message bus with handler registry
  - Subscription manager with filtering
  - Message context with correlation and tracing
  - Message correlator for tracking
  - Dead letter queue for failed messages
  - Flow controller with backpressure management
  - Retry policies with Polly integration

- **Conduit.Components** - Component system
  - AbstractPluggableComponent base class
  - Behavior contribution system with placement rules
  - Component factory with DI support
  - Component container for hosting
  - Component initializer with dependency resolution

- **Conduit.Serialization** - Multi-format serialization
  - Serialization format enumeration (JSON, MessagePack, XML, Protobuf, Avro, YAML)
  - IMessageSerializer interface
  - JSON serialization using System.Text.Json
  - MessagePack binary serialization
  - Serializer registry with content negotiation
  - Format detection from data, extensions, and MIME types
  - Compression support (GZIP)

- **Conduit.Security** - Authentication, encryption, and authorization
  - Claims-based security context
  - IAuthenticationProvider interface
  - JWT authentication with token lifecycle management
  - IEncryptionService interface
  - AES encryption (GCM and CBC modes)
  - Stream encryption/decryption
  - Key generation, rotation, and management
  - Role-Based Access Control (RBAC)
  - Attribute-Based Access Control (ABAC)
  - Policy-based authorization
  - Wildcard permission matching
  - Authorization caching with TTL
  - Multi-tenant support

### Infrastructure
- Solution structure with .NET 8 SDK configuration
- global.json for SDK version pinning
- Comprehensive .gitignore for .NET projects
- README with project overview and quick start
- TASK.md for detailed progress tracking
- QUICK_START.md for development resumption

### Documentation
- XML documentation for all public APIs
- Inline code comments
- Architecture decision records in code

### Development
- .NET 8.0 LTS target framework
- Nullable reference types enabled
- Warnings as errors
- Latest C# language features
- Code style enforcement

### Dependencies
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- System.Text.Json
- MessagePack-CSharp
- System.IdentityModel.Tokens.Jwt
- Polly (resilience)

### Progress
- 8 of 23 planned modules completed (~35%)
- 2,810 lines of security code
- Thread-safe concurrent operations throughout
- Comprehensive error handling and logging

### Known Issues
- .NET SDK build requires fixing obj/bin directory permissions (created with root)
- No unit tests yet (planned for future release)
- Missing transport implementations (AMQP, TCP, gRPC)
- No example projects yet

### Next Steps
- Conduit.Resilience - Circuit breakers, retry policies, bulkhead, timeout
- Conduit.Transports.Core - Transport abstractions
- Conduit.Transports.Amqp - RabbitMQ implementation
- Conduit.Transports.Grpc - gRPC implementation
- Conduit.Transports.Tcp - TCP/Socket implementation

---

## Version History

- **0.1.0** (2025-10-25) - Initial development release with core modules

[0.1.0]: https://github.com/conduit/conduit-dotnet/releases/tag/v0.1.0
