using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Messaging;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Low-level network gateway that manages connections and routes them to appropriate transports.
    /// Acts as a networking gateway sitting between external connections and the messaging infrastructure.
    /// </summary>
    public class Gateway : IDisposable
    {
        private readonly GatewayConfiguration _configuration;
        private readonly RoutingTable _routingTable;
        private readonly ConnectionTable _connectionTable;
        private readonly SocketManager _socketManager;
        private readonly TransportRegistry _transportRegistry;
        private readonly TransportLoadBalancer _loadBalancer;
        private readonly RateLimiter _rateLimiter;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly ILogger<Gateway>? _logger;
        private readonly IMessageBus? _messageBus;

        private readonly ConcurrentDictionary<int, CancellationTokenSource> _serverTasks;
        private readonly ConcurrentDictionary<string, Task> _clientConnections;
        private readonly SemaphoreSlim _connectionSemaphore;

        private volatile bool _isRunning;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the Gateway class.
        /// </summary>
        /// <param name="configuration">The gateway configuration</param>
        /// <param name="logger">Optional logger instance</param>
        /// <param name="messageBus">Optional message bus for integration</param>
        public Gateway(
            GatewayConfiguration configuration,
            ILogger<Gateway>? logger = null,
            IMessageBus? messageBus = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
            _messageBus = messageBus;

            _configuration.Validate();

            _routingTable = new RoutingTable(null);
            _connectionTable = new ConnectionTable(_configuration.IdleConnectionTimeout, null);
            _socketManager = new SocketManager(null);
            _transportRegistry = new TransportRegistry(null);
            _loadBalancer = new TransportLoadBalancer(null);
            _rateLimiter = new RateLimiter(null, _configuration.DefaultRateLimit);
            _circuitBreaker = new CircuitBreaker(null, TimeSpan.FromMilliseconds(_configuration.CircuitBreakerRecoveryInterval));

            _serverTasks = new ConcurrentDictionary<int, CancellationTokenSource>();
            _clientConnections = new ConcurrentDictionary<string, Task>();
            _connectionSemaphore = new SemaphoreSlim(
                _configuration.MaxConcurrentConnections,
                _configuration.MaxConcurrentConnections);

            InitializeRouting();
        }

        /// <summary>
        /// Gets whether the gateway is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the routing table.
        /// </summary>
        public RoutingTable RoutingTable => _routingTable;

        /// <summary>
        /// Gets the connection table.
        /// </summary>
        public ConnectionTable ConnectionTable => _connectionTable;

        /// <summary>
        /// Gets the transport registry.
        /// </summary>
        public TransportRegistry TransportRegistry => _transportRegistry;

        /// <summary>
        /// Starts the gateway.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the gateway is started</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_isRunning)
                throw new InvalidOperationException("Gateway is already running");

            _logger?.LogInformation("Starting Gateway '{Name}'", _configuration.Name);

            try
            {
                // Start all registered transports
                await _transportRegistry.StartAllTransportsAsync(cancellationToken);

                // Start server bindings (inbound connections)
                await StartServerBindingsAsync(cancellationToken);

                // Start client connections (outbound connections)
                await StartClientConnectionsAsync(cancellationToken);

                _isRunning = true;

                _logger?.LogInformation("Gateway '{Name}' started successfully with {ServerBindings} server bindings and {ClientEndpoints} client endpoints",
                    _configuration.Name, _configuration.ServerBindings.Count, _configuration.ClientEndpoints.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start Gateway '{Name}'", _configuration.Name);
                await StopAsync(CancellationToken.None);
                throw;
            }
        }

        /// <summary>
        /// Stops the gateway.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the gateway is stopped</returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return;

            _logger?.LogInformation("Stopping Gateway '{Name}'", _configuration.Name);

            _isRunning = false;

            // Stop server tasks
            foreach (var kvp in _serverTasks.ToArray())
            {
                try
                {
                    kvp.Value.Cancel();
                    _socketManager.UnbindPort(kvp.Key);
                    _serverTasks.TryRemove(kvp.Key, out _);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error stopping server on port {Port}", kvp.Key);
                }
            }

            // Close all connections
            await _connectionTable.CloseConnectionsAsync();

            // Stop all transports
            await _transportRegistry.StopAllTransportsAsync(cancellationToken);

            _logger?.LogInformation("Gateway '{Name}' stopped", _configuration.Name);
        }

        /// <summary>
        /// Processes an incoming connection request.
        /// </summary>
        /// <param name="connectionInfo">The connection information</param>
        /// <param name="socket">The socket for the connection</param>
        /// <returns>Task that completes when the connection is processed</returns>
        public async Task<GatewayResponse> ProcessConnectionAsync(ConnectionInfo connectionInfo, Socket? socket = null)
        {
            ThrowIfDisposed();

            if (!_isRunning)
                return new GatewayResponse { Success = false, Message = "Gateway is not running" };

            // Check connection limits
            if (!await _connectionSemaphore.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 503,
                    Message = "Connection limit exceeded"
                };
            }

            try
            {
                // Look up route for inbound connection first
                var route = _routingTable.LookupInbound(connectionInfo);

                // Apply rate limiting
                if (_configuration.EnableRateLimiting)
                {
                    var clientKey = connectionInfo.SourceEndpoint?.Address?.ToString() ?? "unknown";
                    var rateLimit = route?.RateLimit ?? _configuration.DefaultRateLimit;

                    if (!_rateLimiter.AllowRequest(clientKey, rateLimit))
                    {
                        return new GatewayResponse
                        {
                            Success = false,
                            StatusCode = 429,
                            Message = "Rate limit exceeded"
                        };
                    }
                }
                if (route == null)
                {
                    _logger?.LogWarning("No route found for inbound connection: {ConnectionInfo}", connectionInfo);
                    return new GatewayResponse
                    {
                        Success = false,
                        StatusCode = 404,
                        Message = "No route found for connection"
                    };
                }

                // Apply route action
                switch (route.Action)
                {
                    case RouteAction.Accept:
                        return await AcceptConnectionAsync(connectionInfo, route, socket);

                    case RouteAction.Reject:
                        _logger?.LogInformation("Rejecting connection: {ConnectionInfo}", connectionInfo);
                        socket?.Close();
                        return new GatewayResponse
                        {
                            Success = false,
                            StatusCode = 403,
                            Message = "Connection rejected by routing rules"
                        };

                    case RouteAction.Drop:
                        _logger?.LogInformation("Dropping connection: {ConnectionInfo}", connectionInfo);
                        socket?.Close();
                        return new GatewayResponse
                        {
                            Success = false,
                            StatusCode = 444,
                            Message = "Connection dropped"
                        };

                    default:
                        _logger?.LogWarning("Unsupported route action {Action} for inbound connection", route.Action);
                        return new GatewayResponse
                        {
                            Success = false,
                            StatusCode = 500,
                            Message = "Unsupported route action"
                        };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing connection: {ConnectionInfo}", connectionInfo);
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Internal gateway error",
                    Error = ex.Message
                };
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Creates an outbound connection through the gateway.
        /// </summary>
        /// <param name="destination">The destination endpoint</param>
        /// <param name="protocol">The protocol to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that returns the connected socket</returns>
        public async Task<Socket?> CreateOutboundConnectionAsync(IPEndPoint destination, Protocol protocol = Protocol.TCP,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var connectionInfo = new ConnectionInfo
            {
                DestinationEndpoint = destination,
                Protocol = protocol
            };

            // Look up route for outbound connection
            var route = _routingTable.LookupOutbound(connectionInfo);
            if (route?.Action != RouteAction.Connect)
            {
                _logger?.LogWarning("No outbound route found for destination: {Destination}", destination);
                return null;
            }

            try
            {
                var protocolType = protocol == Protocol.TCP ? ProtocolType.Tcp : ProtocolType.Udp;
                var socket = await _socketManager.ConnectAsync(destination, protocolType, cancellationToken: cancellationToken);

                // Track the connection
                var connectionState = new ConnectionState
                {
                    ConnectionInfo = connectionInfo,
                    Status = ConnectionStatus.Connected,
                    Socket = socket,
                    MatchedRoute = route,
                    AssignedTransport = route.TargetTransport
                };

                _connectionTable.AddConnection(connectionState);

                _logger?.LogInformation("Created outbound connection to {Destination}", destination);
                return socket;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create outbound connection to {Destination}", destination);
                throw;
            }
        }

        /// <summary>
        /// Gets gateway statistics.
        /// </summary>
        /// <returns>Gateway statistics</returns>
        public GatewayStatistics GetStatistics()
        {
            var connectionStats = _connectionTable.GetStatistics();
            var routingStats = _routingTable.GetStatistics();
            var transportHealth = _transportRegistry.GetTransportHealth();

            return new GatewayStatistics
            {
                IsRunning = _isRunning,
                Name = _configuration.Name,
                ServerBindings = _configuration.ServerBindings.Count,
                ClientEndpoints = _configuration.ClientEndpoints.Count,
                RegisteredTransports = _transportRegistry.Count,
                ConnectionStatistics = connectionStats,
                RoutingStatistics = routingStats,
                TransportHealth = transportHealth,
                BoundPorts = _socketManager.GetBoundPorts()
            };
        }

        /// <summary>
        /// Initializes the routing table with static routes from configuration.
        /// </summary>
        private void InitializeRouting()
        {
            foreach (var route in _configuration.StaticRoutes)
            {
                _routingTable.AddRoute(route);
            }

            _logger?.LogInformation("Initialized routing table with {RouteCount} static routes",
                _configuration.StaticRoutes.Count);
        }

        /// <summary>
        /// Starts server bindings for accepting inbound connections.
        /// </summary>
        private Task StartServerBindingsAsync(CancellationToken cancellationToken)
        {
            foreach (var binding in _configuration.ServerBindings.Where(b => b.Enabled))
            {
                try
                {
                    var protocolType = binding.Protocol == Protocol.TCP ? ProtocolType.Tcp : ProtocolType.Udp;
                    var socket = _socketManager.BindPort(binding.Port, binding.BindAddress, protocolType, binding.SocketOptions);

                    // Start accepting connections for TCP
                    if (binding.Protocol == Protocol.TCP)
                    {
                        var cts = new CancellationTokenSource();
                        _serverTasks.TryAdd(binding.Port, cts);

                        _ = Task.Run(() => AcceptConnectionsAsync(socket, binding, cts.Token), cancellationToken);
                    }

                    _logger?.LogInformation("Started server binding on {Address}:{Port} ({Protocol}) -> {Transport}",
                        binding.BindAddress, binding.Port, binding.Protocol, binding.DefaultTransport);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start server binding on {Address}:{Port}",
                        binding.BindAddress, binding.Port);
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts client connections for outbound endpoints.
        /// </summary>
        private async Task StartClientConnectionsAsync(CancellationToken cancellationToken)
        {
            foreach (var endpoint in _configuration.ClientEndpoints.Where(e => e.Enabled && e.AutoConnect))
            {
                try
                {
                    var connectionTask = EstablishClientConnectionAsync(endpoint, cancellationToken);
                    _clientConnections.TryAdd(endpoint.Name, connectionTask);

                    _logger?.LogInformation("Starting client connection to {Name}: {Endpoint} ({Transport})",
                        endpoint.Name, endpoint.Endpoint, endpoint.Transport);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start client connection to {Name}: {Endpoint}",
                        endpoint.Name, endpoint.Endpoint);
                }
            }

            if (_configuration.ClientEndpoints.Any(e => e.Enabled && e.AutoConnect))
            {
                await Task.Delay(100, cancellationToken); // Give connections time to start
            }
        }

        /// <summary>
        /// Accepts incoming connections on a server socket.
        /// </summary>
        private async Task AcceptConnectionsAsync(Socket serverSocket, ServerBinding binding, CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Starting to accept connections on port {Port}", binding.Port);

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var clientSocket = await _socketManager.AcceptAsync(serverSocket, cancellationToken);
                    var connectionInfo = _socketManager.ExtractConnectionInfo(clientSocket, binding.Protocol);

                    // Handle connection in background
                    _ = Task.Run(() => ProcessConnectionAsync(connectionInfo, clientSocket), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error accepting connection on port {Port}", binding.Port);
                    await Task.Delay(1000, cancellationToken); // Brief delay before retrying
                }
            }

            _logger?.LogDebug("Stopped accepting connections on port {Port}", binding.Port);
        }

        /// <summary>
        /// Establishes a client connection to an endpoint.
        /// </summary>
        private async Task EstablishClientConnectionAsync(ClientEndpoint endpoint, CancellationToken cancellationToken)
        {
            var attempt = 0;
            var delay = endpoint.RetryPolicy.InitialDelay;

            while (!cancellationToken.IsCancellationRequested && attempt < endpoint.RetryPolicy.MaxAttempts)
            {
                try
                {
                    var protocolType = endpoint.Protocol == Protocol.TCP ? ProtocolType.Tcp : ProtocolType.Udp;
                    var socket = await _socketManager.ConnectAsync(endpoint.Endpoint, protocolType,
                        endpoint.SocketOptions, cancellationToken);

                    var connectionInfo = _socketManager.ExtractConnectionInfo(socket, endpoint.Protocol);
                    var connectionState = new ConnectionState
                    {
                        ConnectionInfo = connectionInfo,
                        Status = ConnectionStatus.Connected,
                        Socket = socket
                    };

                    _connectionTable.AddConnection(connectionState);

                    _logger?.LogInformation("Established client connection to {Name}: {Endpoint}",
                        endpoint.Name, endpoint.Endpoint);
                    return;
                }
                catch (Exception ex)
                {
                    attempt++;
                    _logger?.LogWarning(ex, "Failed to connect to {Name}: {Endpoint} (attempt {Attempt}/{MaxAttempts})",
                        endpoint.Name, endpoint.Endpoint, attempt, endpoint.RetryPolicy.MaxAttempts);

                    if (attempt < endpoint.RetryPolicy.MaxAttempts)
                    {
                        await Task.Delay(delay, cancellationToken);

                        if (endpoint.RetryPolicy.UseExponentialBackoff)
                        {
                            delay = TimeSpan.FromMilliseconds(Math.Min(
                                delay.TotalMilliseconds * endpoint.RetryPolicy.BackoffMultiplier,
                                endpoint.RetryPolicy.MaxDelay.TotalMilliseconds));
                        }
                    }
                }
            }

            _logger?.LogError("Failed to establish client connection to {Name}: {Endpoint} after {MaxAttempts} attempts",
                endpoint.Name, endpoint.Endpoint, endpoint.RetryPolicy.MaxAttempts);
        }

        /// <summary>
        /// Accepts an incoming connection and routes it to the appropriate transport.
        /// </summary>
        private async Task<GatewayResponse> AcceptConnectionAsync(ConnectionInfo connectionInfo, RouteEntry route, Socket? socket)
        {
            // Use load balancing to select the best transport
            var transport = SelectTransportForRoute(route, connectionInfo);
            if (transport == null)
            {
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 502,
                    Message = "No transport assigned to route"
                };
            }

            // Use circuit breaker to protect transport operations
            var circuitKey = $"transport_{transport.Type}_{transport.Name}";

            try
            {
                return await _circuitBreaker.ExecuteAsync(circuitKey, () =>
                {
                    // Create connection state
                    var connectionState = new ConnectionState
                    {
                        ConnectionInfo = connectionInfo,
                        Status = ConnectionStatus.Connected,
                        Socket = socket,
                        MatchedRoute = route,
                        AssignedTransport = transport
                    };

                    // Add to connection table
                    _connectionTable.AddConnection(connectionState);

                    // TODO: Hand off the socket to the transport
                    // This will depend on how we modify the transport interface to accept raw sockets

                    _logger?.LogInformation("Accepted connection {ConnectionId}: {ConnectionInfo} -> {Transport}",
                        connectionState.ConnectionId, connectionInfo, transport.Type);

                    return Task.FromResult(new GatewayResponse
                    {
                        Success = true,
                        StatusCode = 200,
                        Message = "Connection accepted"
                    });
                },
                _configuration.CircuitBreakerFailureThreshold,
                TimeSpan.FromMilliseconds(_configuration.CircuitBreakerTimeout));
            }
            catch (CircuitBreakerOpenException)
            {
                _logger?.LogWarning("Circuit breaker open for transport {Transport}, rejecting connection", transport.Name);
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 503,
                    Message = "Transport circuit breaker is open"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting connection: {ConnectionInfo}", connectionInfo);
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Error accepting connection",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Selects the best transport for a route using load balancing.
        /// </summary>
        /// <param name="route">The route entry</param>
        /// <param name="connectionInfo">Connection information for context</param>
        /// <returns>Selected transport, or the route's target transport if no load balancing</returns>
        private ITransport? SelectTransportForRoute(RouteEntry route, ConnectionInfo connectionInfo)
        {
            // If no load balancing strategy specified, return the route's target transport
            if (route.LoadBalancingStrategy == null || route.TargetTransport == null)
                return route.TargetTransport;

            // Get all transports of the same type as the target transport
            var sameTypeTransports = _transportRegistry.GetAllTransports()
                .Where(t => t.Type == route.TargetTransport.Type)
                .ToList();

            // If only one transport available, no need for load balancing
            if (sameTypeTransports.Count <= 1)
                return route.TargetTransport;

            // Use load balancer to select the best transport
            var selectedTransport = _loadBalancer.SelectTransport(
                sameTypeTransports,
                route.LoadBalancingStrategy.Value,
                connectionInfo,
                _connectionTable);

            _logger?.LogDebug("Load balancer selected transport {Transport} for route {RouteId} using {Strategy}",
                selectedTransport?.Name, route.Id, route.LoadBalancingStrategy);

            return selectedTransport ?? route.TargetTransport;
        }

        /// <summary>
        /// Gets the load balancer instance.
        /// </summary>
        /// <returns>The load balancer</returns>
        public TransportLoadBalancer GetLoadBalancer() => _loadBalancer;

        /// <summary>
        /// Gets the rate limiter instance.
        /// </summary>
        /// <returns>The rate limiter</returns>
        public RateLimiter GetRateLimiter() => _rateLimiter;

        /// <summary>
        /// Gets the circuit breaker instance.
        /// </summary>
        /// <returns>The circuit breaker</returns>
        public CircuitBreaker GetCircuitBreaker() => _circuitBreaker;

        /// <summary>
        /// Throws an exception if the gateway has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Gateway));
        }

        /// <summary>
        /// Disposes the gateway and all its resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping gateway during disposal");
            }

            _routingTable?.Dispose();
            _connectionTable?.Dispose();
            _socketManager?.Dispose();
            _transportRegistry?.Dispose();
            _loadBalancer?.Dispose();
            _circuitBreaker?.Dispose();
            _connectionSemaphore?.Dispose();

            _logger?.LogInformation("Gateway '{Name}' disposed", _configuration.Name);
        }
    }

    /// <summary>
    /// Response from a gateway operation.
    /// </summary>
    public class GatewayResponse
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the HTTP-style status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets the error message (if any).
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Statistics about the gateway.
    /// </summary>
    public class GatewayStatistics
    {
        /// <summary>
        /// Gets or sets whether the gateway is running.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Gets or sets the gateway name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of server bindings.
        /// </summary>
        public int ServerBindings { get; set; }

        /// <summary>
        /// Gets or sets the number of client endpoints.
        /// </summary>
        public int ClientEndpoints { get; set; }

        /// <summary>
        /// Gets or sets the number of registered transports.
        /// </summary>
        public int RegisteredTransports { get; set; }

        /// <summary>
        /// Gets or sets the connection statistics.
        /// </summary>
        public ConnectionTableStatistics? ConnectionStatistics { get; set; }

        /// <summary>
        /// Gets or sets the routing statistics.
        /// </summary>
        public RoutingTableStatistics? RoutingStatistics { get; set; }

        /// <summary>
        /// Gets or sets the transport health information.
        /// </summary>
        public Dictionary<Transports.Core.TransportType, TransportHealth>? TransportHealth { get; set; }

        /// <summary>
        /// Gets or sets the bound port numbers.
        /// </summary>
        public int[] BoundPorts { get; set; } = Array.Empty<int>();
    }
}