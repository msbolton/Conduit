using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// TCP server for accepting and managing incoming connections.
    /// </summary>
    public class TcpServer : IDisposable, IAsyncDisposable
    {
        private readonly TcpConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, TcpConnection> _connections;

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private volatile bool _isRunning;
        private volatile bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the TcpServer class.
        /// </summary>
        /// <param name="configuration">The TCP configuration</param>
        /// <param name="logger">The logger instance</param>
        public TcpServer(TcpConfiguration configuration, ILogger logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connections = new ConcurrentDictionary<string, TcpConnection>();
        }

        /// <summary>
        /// Gets a value indicating whether the server is running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the number of active connections.
        /// </summary>
        public int ActiveConnectionCount => _connections.Count;

        /// <summary>
        /// Event raised when a message is received from any connection.
        /// </summary>
        public event Func<byte[], string, Task>? MessageReceived;

        /// <summary>
        /// Event raised when a new connection is accepted.
        /// </summary>
        public event Action<string>? ConnectionAccepted;

        /// <summary>
        /// Event raised when a connection is closed.
        /// </summary>
        public event Action<string>? ConnectionClosed;

        /// <summary>
        /// Starts the TCP server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TcpServer));

            if (_isRunning)
                throw new InvalidOperationException("Server is already running");

            _logger.LogInformation("Starting TCP server on {Host}:{Port}", _configuration.Host, _configuration.Port);

            var address = IPAddress.Parse(_configuration.Host);
            _listener = new TcpListener(address, _configuration.Port);

            // Set socket options
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);

            _listener.Start(_configuration.Backlog);
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _logger.LogInformation("TCP server started successfully on {Host}:{Port}", _configuration.Host, _configuration.Port);

            // Start accepting connections
            _ = Task.Run(() => AcceptConnectionsAsync(_cts.Token), cancellationToken);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the TCP server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return;

            _logger.LogInformation("Stopping TCP server");

            _isRunning = false;
            _cts?.Cancel();

            // Stop listener
            _listener?.Stop();

            // Close all connections
            var closeTasks = _connections.Values.Select(c => c.DisposeAsync().AsTask());
            await Task.WhenAll(closeTasks);

            _connections.Clear();

            _logger.LogInformation("TCP server stopped");
        }

        /// <summary>
        /// Sends a message to a specific connection.
        /// </summary>
        /// <param name="connectionId">The connection ID</param>
        /// <param name="data">The message data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SendToConnectionAsync(string connectionId, byte[] data, CancellationToken cancellationToken = default)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
                throw new InvalidOperationException($"Connection {connectionId} not found");

            await connection.SendMessageAsync(data, cancellationToken);
        }

        /// <summary>
        /// Broadcasts a message to all connections.
        /// </summary>
        /// <param name="data">The message data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var sendTasks = _connections.Values
                .Where(c => c.IsConnected)
                .Select(c => c.SendMessageAsync(data, cancellationToken));

            await Task.WhenAll(sendTasks);

            _logger.LogDebug("Broadcasted message to {Count} connections", _connections.Count);
        }

        /// <summary>
        /// Accepts incoming connections.
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting to accept connections");

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check connection limit
                    if (_connections.Count >= _configuration.MaxConnections)
                    {
                        _logger.LogWarning("Maximum connections ({MaxConnections}) reached, waiting...", _configuration.MaxConnections);
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    // Accept connection
                    var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                    var connection = new TcpConnection(client, _configuration, _logger);

                    _logger.LogInformation("Accepted connection {ConnectionId} from {RemoteEndpoint}",
                        connection.ConnectionId, connection.RemoteEndpoint);

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
                        _connections.TryRemove(connId, out _);
                        ConnectionClosed?.Invoke(connId);
                        _logger.LogInformation("Connection {ConnectionId} closed and removed", connId);
                    };

                    // Add to connection list
                    _connections.TryAdd(connection.ConnectionId, connection);

                    // Notify connection accepted
                    ConnectionAccepted?.Invoke(connection.ConnectionId);

                    // Start receiving messages
                    _ = Task.Run(() => connection.StartReceivingAsync(cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Accept connections operation cancelled");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("Listener disposed, stopping accept loop");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting connection");
                    await Task.Delay(1000, cancellationToken); // Back off on error
                }
            }

            _logger.LogDebug("Stopped accepting connections");
        }

        /// <summary>
        /// Disposes the server.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Disposes the server asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            await StopAsync();

            _cts?.Dispose();
            _listener?.Server?.Dispose();

            _logger.LogInformation("TCP server disposed");
        }
    }
}
