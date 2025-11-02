using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// Manages TCP client connections with connection pooling.
    /// </summary>
    public class TcpClientManager : IDisposable, IAsyncDisposable
    {
        private readonly TcpConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ConcurrentBag<TcpConnection> _connectionPool;
        private readonly SemaphoreSlim _poolSemaphore;
        private readonly ConcurrentDictionary<string, TcpConnection> _activeConnections;

        private volatile bool _isDisposed;
        private int _connectionCount;

        /// <summary>
        /// Initializes a new instance of the TcpClientManager class.
        /// </summary>
        /// <param name="configuration">The TCP configuration</param>
        /// <param name="logger">The logger instance</param>
        public TcpClientManager(TcpConfiguration configuration, ILogger logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _connectionPool = new ConcurrentBag<TcpConnection>();
            _activeConnections = new ConcurrentDictionary<string, TcpConnection>();
            _poolSemaphore = new SemaphoreSlim(
                _configuration.UseConnectionPooling ? _configuration.ConnectionPoolSize : int.MaxValue,
                _configuration.UseConnectionPooling ? _configuration.ConnectionPoolSize : int.MaxValue);

            _connectionCount = 0;
        }

        /// <summary>
        /// Gets the number of active connections.
        /// </summary>
        public int ActiveConnectionCount => _activeConnections.Count;

        /// <summary>
        /// Gets the number of pooled connections.
        /// </summary>
        public int PooledConnectionCount => _connectionPool.Count;

        /// <summary>
        /// Event raised when a message is received on any connection.
        /// </summary>
        public event Func<byte[], string, Task>? MessageReceived;

        /// <summary>
        /// Gets a connection from the pool or creates a new one.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A TCP connection</returns>
        public async Task<TcpConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TcpClientManager));

            // Wait for available slot in pool
            var timeoutCts = new CancellationTokenSource(_configuration.ConnectionPoolTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await _poolSemaphore.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Timeout waiting for connection from pool ({_configuration.ConnectionPoolTimeout}ms)");
            }

            try
            {
                // Try to get connection from pool
                if (_configuration.UseConnectionPooling)
                {
                    while (_connectionPool.TryTake(out var pooledConnection))
                    {
                        if (pooledConnection.IsConnected)
                        {
                            _activeConnections.TryAdd(pooledConnection.ConnectionId, pooledConnection);
                            _logger.LogDebug("Reused connection {ConnectionId} from pool", pooledConnection.ConnectionId);
                            return pooledConnection;
                        }
                        else
                        {
                            // Connection is dead, dispose it
                            await pooledConnection.DisposeAsync();
                            Interlocked.Decrement(ref _connectionCount);
                        }
                    }
                }

                // Create new connection
                return await CreateConnectionAsync(cancellationToken);
            }
            catch
            {
                _poolSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Returns a connection to the pool.
        /// </summary>
        /// <param name="connection">The connection to return</param>
        public async Task ReturnConnectionAsync(TcpConnection connection)
        {
            if (connection == null)
                return;

            _activeConnections.TryRemove(connection.ConnectionId, out _);

            if (_configuration.UseConnectionPooling && connection.IsConnected && !_isDisposed)
            {
                _connectionPool.Add(connection);
                _logger.LogDebug("Returned connection {ConnectionId} to pool", connection.ConnectionId);
            }
            else
            {
                await connection.DisposeAsync();
                Interlocked.Decrement(ref _connectionCount);
                _logger.LogDebug("Disposed connection {ConnectionId}", connection.ConnectionId);
            }

            _poolSemaphore.Release();
        }

        /// <summary>
        /// Sends a message using a connection from the pool.
        /// </summary>
        /// <param name="data">The message data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SendMessageAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var connection = await GetConnectionAsync(cancellationToken);

            try
            {
                await connection.SendMessageAsync(data, cancellationToken);
            }
            finally
            {
                await ReturnConnectionAsync(connection);
            }
        }

        /// <summary>
        /// Creates a new TCP connection.
        /// </summary>
        private async Task<TcpConnection> CreateConnectionAsync(CancellationToken cancellationToken)
        {
            var host = _configuration.RemoteHost ?? throw new InvalidOperationException("RemoteHost not configured");
            var port = _configuration.RemotePort ?? throw new InvalidOperationException("RemotePort not configured");

            _logger.LogDebug("Creating new connection to {Host}:{Port}", host, port);

            var client = new TcpClient();

            try
            {
                // Set socket options before connecting
                client.ReceiveBufferSize = _configuration.ReceiveBufferSize;
                client.SendBufferSize = _configuration.SendBufferSize;
                client.NoDelay = _configuration.NoDelay;

                // Connect with timeout
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(_configuration.Connection.ConnectTimeout, cancellationToken);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    client.Close();
                    throw new TimeoutException($"Connection timeout connecting to {host}:{port}");
                }

                await connectTask; // Re-await to propagate exceptions

                var connection = new TcpConnection(client, _configuration, _logger);

                // Subscribe to connection events
                connection.MessageReceived += async (data) =>
                {
                    if (MessageReceived != null)
                    {
                        await MessageReceived(data, connection.ConnectionId);
                    }
                };

                connection.ConnectionClosed += (connId) =>
                {
                    _activeConnections.TryRemove(connId, out _);
                    _logger.LogInformation("Connection {ConnectionId} closed", connId);
                };

                // Add to active connections
                _activeConnections.TryAdd(connection.ConnectionId, connection);
                Interlocked.Increment(ref _connectionCount);

                // Start receiving if not pooling (pooled connections receive on-demand)
                if (!_configuration.UseConnectionPooling)
                {
                    _ = Task.Run(() => connection.StartReceivingAsync(cancellationToken), cancellationToken);
                }

                _logger.LogInformation("Created connection {ConnectionId} to {Host}:{Port}",
                    connection.ConnectionId, host, port);

                return connection;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Disposes the client manager.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Disposes the client manager asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _logger.LogInformation("Disposing TCP client manager");

            // Dispose all active connections
            var activeDisposeTasks = _activeConnections.Values.Select(c => c.DisposeAsync().AsTask());
            await Task.WhenAll(activeDisposeTasks);
            _activeConnections.Clear();

            // Dispose all pooled connections
            var pooledDisposeTasks = _connectionPool.Select(c => c.DisposeAsync().AsTask());
            await Task.WhenAll(pooledDisposeTasks);
            _connectionPool.Clear();

            _poolSemaphore.Dispose();

            _logger.LogInformation("TCP client manager disposed");
        }
    }
}
