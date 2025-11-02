using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// Represents a TCP connection with framing support.
    /// </summary>
    public class TcpConnection : IDisposable, IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly MessageFramer _framer;
        private readonly ILogger _logger;
        private readonly TcpConfiguration _configuration;
        private readonly CancellationTokenSource _cts;

        private volatile bool _isDisposed;
        private DateTime _lastActivity;
        private readonly Timer? _heartbeatTimer;

        /// <summary>
        /// Initializes a new instance of the TcpConnection class.
        /// </summary>
        /// <param name="client">The TCP client</param>
        /// <param name="configuration">The TCP configuration</param>
        /// <param name="logger">The logger instance</param>
        public TcpConnection(
            TcpClient client,
            TcpConfiguration configuration,
            ILogger logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _stream = _client.GetStream();
            _framer = new MessageFramer(
                configuration.FramingProtocol,
                configuration.MaxMessageSize);

            _cts = new CancellationTokenSource();
            _lastActivity = DateTime.UtcNow;

            // Configure socket options
            ConfigureSocket();

            // Start heartbeat timer if enabled
            if (configuration.HeartbeatInterval > 0)
            {
                _heartbeatTimer = new Timer(
                    SendHeartbeat,
                    null,
                    TimeSpan.FromMilliseconds(configuration.HeartbeatInterval),
                    TimeSpan.FromMilliseconds(configuration.HeartbeatInterval));
            }

            ConnectionId = Guid.NewGuid().ToString();
            RemoteEndpoint = _client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Gets the connection ID.
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        public string RemoteEndpoint { get; }

        /// <summary>
        /// Gets a value indicating whether the connection is connected.
        /// </summary>
        public bool IsConnected => _client?.Connected == true && !_isDisposed;

        /// <summary>
        /// Gets the last activity timestamp.
        /// </summary>
        public DateTime LastActivity => _lastActivity;

        /// <summary>
        /// Event raised when a message is received.
        /// </summary>
        public event Func<byte[], Task>? MessageReceived;

        /// <summary>
        /// Event raised when the connection is closed.
        /// </summary>
        public event Action<string>? ConnectionClosed;

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        /// <param name="data">The message data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SendMessageAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TcpConnection));

            if (!IsConnected)
                throw new InvalidOperationException("Connection is not established");

            try
            {
                await _framer.WriteMessageAsync(_stream, data, cancellationToken);
                _lastActivity = DateTime.UtcNow;

                _logger.LogDebug("Sent message ({Size} bytes) on connection {ConnectionId}", data.Length, ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message on connection {ConnectionId}", ConnectionId);
                throw;
            }
        }

        /// <summary>
        /// Starts receiving messages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

            try
            {
                _logger.LogInformation("Started receiving on connection {ConnectionId} from {RemoteEndpoint}",
                    ConnectionId, RemoteEndpoint);

                while (!linkedCts.Token.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var data = await _framer.ReadMessageAsync(_stream, linkedCts.Token);
                        _lastActivity = DateTime.UtcNow;

                        _logger.LogDebug("Received message ({Size} bytes) on connection {ConnectionId}",
                            data.Length, ConnectionId);

                        // Invoke message handler
                        if (MessageReceived != null)
                        {
                            await MessageReceived(data);
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        _logger.LogInformation("Connection {ConnectionId} closed by remote endpoint", ConnectionId);
                        break;
                    }
                    catch (IOException ioEx) when (ioEx.InnerException is SocketException)
                    {
                        _logger.LogInformation("Connection {ConnectionId} lost: {Message}", ConnectionId, ioEx.Message);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("Receive operation cancelled on connection {ConnectionId}", ConnectionId);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving messages on connection {ConnectionId}", ConnectionId);
            }
            finally
            {
                ConnectionClosed?.Invoke(ConnectionId);
                await DisposeAsync();
            }
        }

        /// <summary>
        /// Configures socket options.
        /// </summary>
        private void ConfigureSocket()
        {
            var socket = _client.Client;

            // Set buffer sizes
            socket.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            socket.SendBufferSize = _configuration.SendBufferSize;

            // Set TCP no delay
            socket.NoDelay = _configuration.NoDelay;

            // Set linger option
            socket.LingerState = new LingerOption(
                _configuration.LingerTime > 0,
                _configuration.LingerTime);

            // Set keep-alive
            if (_configuration.UseKeepAlive)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }
        }

        /// <summary>
        /// Sends a heartbeat message.
        /// </summary>
        private void SendHeartbeat(object? state)
        {
            if (_isDisposed || !IsConnected)
                return;

            var timeSinceLastActivity = DateTime.UtcNow - _lastActivity;

            // Check for heartbeat timeout
            if (timeSinceLastActivity.TotalMilliseconds > _configuration.HeartbeatTimeout)
            {
                _logger.LogWarning("Heartbeat timeout on connection {ConnectionId}, closing", ConnectionId);
                _ = DisposeAsync();
                return;
            }

            // Send heartbeat if no recent activity
            if (timeSinceLastActivity.TotalMilliseconds >= _configuration.HeartbeatInterval / 2)
            {
                try
                {
                    // Send empty message as heartbeat
                    _ = SendMessageAsync(Array.Empty<byte>());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending heartbeat on connection {ConnectionId}", ConnectionId);
                }
            }
        }

        /// <summary>
        /// Disposes the connection.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Disposes the connection asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                _heartbeatTimer?.Dispose();
                _cts.Cancel();

                await Task.Run(() =>
                {
                    _stream?.Close();
                    _stream?.Dispose();
                    _client?.Close();
                    _client?.Dispose();
                });

                _cts.Dispose();

                _logger.LogInformation("Connection {ConnectionId} disposed", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing connection {ConnectionId}", ConnectionId);
            }
        }
    }
}
