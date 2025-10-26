# Changelog

All notable changes to the Conduit Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - Unreleased

### Added
- (Future additions will be listed here)

### Changed
- All project versions bumped to 0.4.0

---

## [0.3.0] - 2025-10-25

### Added
- **Conduit.Transports.Core** - Core transport abstractions and infrastructure
  - ITransport interface for all transport implementations
  - ITransportSubscription with pause/resume capabilities
  - TransportMessage envelope with headers, metadata, and expiration
  - TransportStatistics for comprehensive metrics tracking
  - TransportConfiguration with connection, protocol, security, and performance settings
  - IConnectionManager and ITransportConnection for connection pooling
  - TransportAdapterBase abstract class with template method pattern
  - InMemoryTransport implementation for testing
  - TransportType enumeration for all supported transports
  - Support for 12 transport types (TCP, AMQP, gRPC, Kafka, Redis, WebSocket, HTTP, etc.)

- **Conduit.Transports.ActiveMq** - ActiveMQ Artemis transport using AMQP 1.0
  - ActiveMqTransport adapter implementing ITransport interface
  - ActiveMqConfiguration with comprehensive Artemis settings
  - ActiveMqMessageConverter for bidirectional NMS message conversion
  - ActiveMqSubscription with pause/resume capabilities
  - Queue and topic messaging support (point-to-point and pub/sub)
  - Multiple acknowledgement modes (Auto, Client, DupsOk, Transactional, Individual)
  - Persistent and non-persistent delivery modes
  - Message priority (0-10 mapped to NMS MsgPriority)
  - Message expiration and time-to-live (TTL)
  - Redelivery policies with exponential backoff
  - Temporary queue and topic support
  - Request-response messaging patterns
  - Message correlation and causation tracking
  - Durable topic subscriptions
  - Auto-reconnect on connection failures
  - Prefetch policy for performance tuning

- **Conduit.Transports.Tcp** - TCP/Socket transport with connection pooling
  - TcpTransport adapter with server and client modes
  - TcpConfiguration with comprehensive socket options
  - MessageFramer with multiple framing protocols (length-prefixed, newline, CRLF, custom)
  - TcpServer for accepting and managing connections
  - TcpClientManager with connection pooling
  - TcpConnection wrapper with heartbeat monitoring
  - Server mode: accept connections, broadcast, send to specific connection
  - Client mode: connection pooling with automatic reuse
  - Socket optimization (NoDelay, keep-alive, buffer tuning, linger)
  - Connection lifecycle events (accepted, closed)
  - Heartbeat and keep-alive monitoring
  - Configurable connection limits and backlog

- **Conduit.Transports.Udp** - UDP transport with multicast and broadcast
  - UdpTransport for connectionless messaging
  - UdpConfiguration with multicast/broadcast settings
  - Multicast group support (join/leave, TTL, loopback, interface selection)
  - Broadcast support for local network discovery
  - IPv4 and IPv6 dual-mode support
  - Message fragmentation for large payloads (optional)
  - Configurable datagram size (up to 65507 bytes)
  - Buffer size tuning (send/receive)
  - Async receive loop
  - Use cases: metrics collection, service discovery, heartbeats, sensor data

- **Conduit.Gateway** - API Gateway with routing, load balancing, and rate limiting
  - ApiGateway orchestration class with request processing pipeline
  - GatewayConfiguration for gateway and route settings
  - RouteManager with pattern-based route matching and parameter extraction
  - LoadBalancer with 5 load balancing strategies (Round-robin, Least Connections, Random, IP Hash, Weighted)
  - RateLimiter with token bucket algorithm for per-client and per-route limits
  - Pattern-based route matching with regex ({id} parameter extraction)
  - Route specificity calculation (static segments prioritized)
  - Health tracking for upstream servers with automatic failover
  - Upstream state monitoring (connections, requests, success rate)
  - HTTP request forwarding with HttpClient
  - Custom header injection (upstream and downstream)
  - Metrics collection (requests, success rate, response times)
  - Concurrency control with semaphore (max concurrent requests)
  - Request timeout and cancellation support
  - Multiple HTTP methods per route
  - Route enable/disable control
  - Authentication and role-based access control (configured)
  - Circuit breaker and health check integration
  - CORS support configuration
  - Comprehensive error handling (404, 429, 500, 502, 503, 504)

### Features
- Unified transport abstraction for different protocols
- Connection pooling with statistics and health monitoring
- Message correlation, expiration, priority, and persistence
- Comprehensive configuration (timeouts, retry, TLS, compression, batching)
- Transport metrics (throughput, success rates, latency, connections)
- Subscription management with source-specific filtering
- Auto-reconnect with configurable retry and backoff
- TLS/SSL support with certificate verification
- Message compression with configurable threshold
- Batching and pipelining support for performance
- Thread-safe concurrent operations
- Proper resource disposal with IDisposable pattern
- ActiveMQ Artemis integration with AMQP 1.0 protocol
- NMS (Native Messaging Service) abstraction layer
- Destination URI parsing (queue://, topic://, temp-queue://, temp-topic://)
- Flow control with pause/resume subscriptions
- TCP server/client modes with connection pooling
- Message framing protocols (length-prefixed, delimited)
- UDP multicast and broadcast support
- Connectionless datagram messaging
- API Gateway with routing, load balancing, and rate limiting
- Pattern-based route matching with parameter extraction
- Multiple load balancing strategies (Round-robin, Least Connections, Random, IP Hash)
- Token bucket rate limiting algorithm
- Health tracking and automatic failover
- HTTP request proxying with custom headers
- Gateway metrics collection and monitoring

### Documentation
- Comprehensive README for Transports.Core module
- Usage examples for transport operations
- Custom transport implementation guide
- Configuration examples for all settings
- Connection pooling examples
- Best practices for production deployments
- Comprehensive README for ActiveMq module
- ActiveMQ Artemis quick start guide
- Acknowledgement mode documentation
- Request-response pattern examples
- Message correlation examples
- Troubleshooting guide for ActiveMQ
- Comprehensive README for Tcp module (682 lines)
- TCP server/client examples and patterns
- Message framing protocol documentation
- Connection pooling examples
- Comprehensive README for Udp module (829 lines)
- UDP multicast and broadcast examples
- Service discovery patterns
- Real-time metrics collection examples
- Comprehensive README for Gateway module (840 lines)
- API Gateway quick start guide
- Route configuration examples and patterns
- Load balancing strategy comparison
- Rate limiting configuration guide
- Metrics collection and monitoring examples
- Multi-service gateway examples
- Troubleshooting guide for gateway issues

### Dependencies
- Microsoft.Extensions.Logging.Abstractions (>= 8.0.0)
- Microsoft.Extensions.ObjectPool (>= 8.0.0)
- Microsoft.Extensions.Http (>= 8.0.0) - For API Gateway HTTP client
- Apache.NMS.AMQP (>= 2.1.0) - For ActiveMQ Artemis transport

### Progress
- 14 of 24 planned modules completed (~58%)
- 117+ C# files created
- ~21,684+ lines of code
- Transport implementations: ActiveMQ Artemis, TCP/Socket, UDP
- API Gateway with routing, load balancing, rate limiting

---

## [0.2.0] - 2025-10-25

### Added
- **Conduit.Resilience** - Resilience patterns for fault tolerance
  - Circuit Breaker policy with advanced failure rate threshold
  - Retry policy with Fixed, Linear, and Exponential backoff strategies
  - Bulkhead policy for resource isolation and concurrent execution limiting
  - Timeout policy with Optimistic and Pessimistic cancellation strategies
  - Rate Limiter policy with sliding window algorithm
  - Resilience policy registry for centralized management
  - Policy composition support (chaining multiple policies)
  - Comprehensive metrics tracking for all policies
  - ResilienceComponent for Conduit framework integration
  - Factory methods for all policy types
  - Default policy initialization from configuration
  - Jitter support (±25%) to prevent thundering herd
  - Thread-safe concurrent operations throughout
  - IDisposable support for proper cleanup

### Features
- Circuit breaker with Closed, Open, HalfOpen, and Isolated states
- Manual isolation capability for circuit breakers
- Three retry backoff strategies with configurable multipliers
- Bulkhead with queue management and overflow handling
- Sliding window rate limiting with 10 segments for smooth behavior
- Policy composition for nested resilience application
- Integration with Polly 8.2.0 library

### Documentation
- Comprehensive README for Resilience module
- Usage examples for all policy types
- Configuration examples
- Best practices and recommendations
- Metrics collection documentation

### Dependencies
- Polly (>= 8.2.0)
- Polly.Extensions (>= 8.2.0)
- System.Threading.RateLimiting

### Progress
- 9 of 23 planned modules completed (~39%)
- 80+ C# files created
- ~13,000+ lines of code

---

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

- **0.3.0** (2025-10-25) - Transport abstractions with connection pooling and InMemory implementation
- **0.2.0** (2025-10-25) - Resilience module with circuit breaker, retry, bulkhead, timeout, and rate limiting
- **0.1.0** (2025-10-25) - Initial development release with core modules

[0.3.0]: https://github.com/conduit/conduit-dotnet/releases/tag/v0.3.0
[0.2.0]: https://github.com/conduit/conduit-dotnet/releases/tag/v0.2.0
[0.1.0]: https://github.com/conduit/conduit-dotnet/releases/tag/v0.1.0
