using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Manages low-level socket operations for the gateway.
    /// Handles socket creation, binding, connection, and configuration.
    /// </summary>
    public class SocketManager : IDisposable
    {
        private readonly ILogger<SocketManager>? _logger;
        private readonly ConcurrentDictionary<int, Socket> _boundSockets;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the SocketManager class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        public SocketManager(ILogger<SocketManager>? logger = null)
        {
            _logger = logger;
            _boundSockets = new ConcurrentDictionary<int, Socket>();
        }

        /// <summary>
        /// Creates a new socket for the specified address family and protocol.
        /// </summary>
        /// <param name="addressFamily">The address family</param>
        /// <param name="socketType">The socket type</param>
        /// <param name="protocolType">The protocol type</param>
        /// <returns>A new socket instance</returns>
        public Socket CreateSocket(AddressFamily addressFamily = AddressFamily.InterNetwork,
            SocketType socketType = SocketType.Stream, ProtocolType protocolType = ProtocolType.Tcp)
        {
            ThrowIfDisposed();

            var socket = new Socket(addressFamily, socketType, protocolType);
            _logger?.LogDebug("Created socket: {AddressFamily}/{SocketType}/{ProtocolType}",
                addressFamily, socketType, protocolType);

            return socket;
        }

        /// <summary>
        /// Binds a socket to the specified port and address.
        /// </summary>
        /// <param name="port">The port to bind to</param>
        /// <param name="address">The IP address to bind to (defaults to Any)</param>
        /// <param name="protocolType">The protocol type</param>
        /// <param name="socketOptions">Optional socket options</param>
        /// <returns>The bound socket</returns>
        /// <exception cref="InvalidOperationException">Thrown when port is already bound</exception>
        public Socket BindPort(int port, IPAddress? address = null, ProtocolType protocolType = ProtocolType.Tcp,
            SocketOptions? socketOptions = null)
        {
            ThrowIfDisposed();

            if (_boundSockets.ContainsKey(port))
                throw new InvalidOperationException($"Port {port} is already bound");

            address ??= IPAddress.Any;
            var endpoint = new IPEndPoint(address, port);

            Socket socket;

            if (protocolType == ProtocolType.Tcp)
            {
                socket = CreateSocket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            else if (protocolType == ProtocolType.Udp)
            {
                socket = CreateSocket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            }
            else
            {
                throw new ArgumentException($"Unsupported protocol type: {protocolType}", nameof(protocolType));
            }

            try
            {
                // Apply socket options
                ApplySocketOptions(socket, socketOptions ?? new SocketOptions());

                // Bind the socket
                socket.Bind(endpoint);

                // For TCP sockets, start listening
                if (protocolType == ProtocolType.Tcp)
                {
                    socket.Listen(socketOptions?.Backlog ?? 100);
                }

                _boundSockets.TryAdd(port, socket);

                _logger?.LogInformation("Bound socket to {Endpoint} ({Protocol})", endpoint, protocolType);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Connects to a remote endpoint.
        /// </summary>
        /// <param name="remoteEndpoint">The remote endpoint to connect to</param>
        /// <param name="protocolType">The protocol type</param>
        /// <param name="socketOptions">Optional socket options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The connected socket</returns>
        public async Task<Socket> ConnectAsync(IPEndPoint remoteEndpoint, ProtocolType protocolType = ProtocolType.Tcp,
            SocketOptions? socketOptions = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            Socket socket;

            if (protocolType == ProtocolType.Tcp)
            {
                socket = CreateSocket(remoteEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            else if (protocolType == ProtocolType.Udp)
            {
                socket = CreateSocket(remoteEndpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            }
            else
            {
                throw new ArgumentException($"Unsupported protocol type: {protocolType}", nameof(protocolType));
            }

            try
            {
                // Apply socket options
                ApplySocketOptions(socket, socketOptions ?? new SocketOptions());

                // Connect to remote endpoint
                await socket.ConnectAsync(remoteEndpoint, cancellationToken);

                _logger?.LogDebug("Connected to {RemoteEndpoint} ({Protocol})", remoteEndpoint, protocolType);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Accepts an incoming connection on the specified socket.
        /// </summary>
        /// <param name="listeningSocket">The listening socket</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The accepted socket</returns>
        public async Task<Socket> AcceptAsync(Socket listeningSocket, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (listeningSocket == null)
                throw new ArgumentNullException(nameof(listeningSocket));

            var acceptedSocket = await listeningSocket.AcceptAsync();

            _logger?.LogDebug("Accepted connection from {RemoteEndpoint}",
                acceptedSocket.RemoteEndPoint);

            return acceptedSocket;
        }

        /// <summary>
        /// Unbinds a socket from the specified port.
        /// </summary>
        /// <param name="port">The port to unbind</param>
        /// <returns>True if the port was unbound, false if it wasn't bound</returns>
        public bool UnbindPort(int port)
        {
            if (_boundSockets.TryRemove(port, out var socket))
            {
                try
                {
                    socket.Close();
                    socket.Dispose();
                    _logger?.LogInformation("Unbound socket from port {Port}", port);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error unbinding socket from port {Port}", port);
                }
            }

            return false;
        }

        /// <summary>
        /// Gets information about the bound socket on the specified port.
        /// </summary>
        /// <param name="port">The port to check</param>
        /// <returns>Socket information if bound, null otherwise</returns>
        public SocketInfo? GetSocketInfo(int port)
        {
            if (_boundSockets.TryGetValue(port, out var socket))
            {
                return new SocketInfo
                {
                    Port = port,
                    LocalEndpoint = socket.LocalEndPoint as IPEndPoint,
                    SocketType = socket.SocketType,
                    ProtocolType = socket.ProtocolType,
                    IsBound = socket.IsBound,
                    IsConnected = socket.Connected
                };
            }

            return null;
        }

        /// <summary>
        /// Gets all bound ports.
        /// </summary>
        /// <returns>Array of bound port numbers</returns>
        public int[] GetBoundPorts()
        {
            return _boundSockets.Keys.ToArray();
        }

        /// <summary>
        /// Applies socket options to a socket.
        /// </summary>
        /// <param name="socket">The socket to configure</param>
        /// <param name="options">The socket options to apply</param>
        public void ApplySocketOptions(Socket socket, SocketOptions options)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            try
            {
                // Reuse address
                if (options.ReuseAddress)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                }

                // TCP-specific options
                if (socket.ProtocolType == ProtocolType.Tcp)
                {
                    // No delay (disable Nagle's algorithm)
                    if (options.NoDelay)
                    {
                        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                    }

                    // Keep alive
                    if (options.KeepAlive)
                    {
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    }

                    // Linger
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger,
                        new LingerOption(options.LingerTime > 0, options.LingerTime));
                }

                // Buffer sizes
                if (options.SendBufferSize > 0)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, options.SendBufferSize);
                }

                if (options.ReceiveBufferSize > 0)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, options.ReceiveBufferSize);
                }

                // Timeouts
                if (options.SendTimeout > 0)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, options.SendTimeout);
                }

                if (options.ReceiveTimeout > 0)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, options.ReceiveTimeout);
                }

                _logger?.LogDebug("Applied socket options to {Socket}", socket.LocalEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error applying socket options");
                throw;
            }
        }

        /// <summary>
        /// Extracts connection information from a socket.
        /// </summary>
        /// <param name="socket">The socket to extract information from</param>
        /// <param name="protocol">The protocol type</param>
        /// <returns>Connection information</returns>
        public ConnectionInfo ExtractConnectionInfo(Socket socket, Protocol protocol)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            var connectionInfo = new ConnectionInfo
            {
                Protocol = protocol,
                SourceEndpoint = socket.RemoteEndPoint as IPEndPoint,
                DestinationEndpoint = socket.LocalEndPoint as IPEndPoint,
                EstablishedTime = DateTime.UtcNow
            };

            return connectionInfo;
        }

        /// <summary>
        /// Throws an exception if the socket manager has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SocketManager));
        }

        /// <summary>
        /// Disposes the socket manager and all bound sockets.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Close all bound sockets
            foreach (var port in _boundSockets.Keys.ToArray())
            {
                UnbindPort(port);
            }

            _logger?.LogInformation("Socket manager disposed");
        }
    }

    /// <summary>
    /// Options for configuring sockets.
    /// </summary>
    public class SocketOptions
    {
        /// <summary>
        /// Gets or sets whether to reuse the address.
        /// </summary>
        public bool ReuseAddress { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to disable Nagle's algorithm (TCP_NODELAY).
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable keep-alive.
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// Gets or sets the linger time in seconds.
        /// </summary>
        public int LingerTime { get; set; } = 0;

        /// <summary>
        /// Gets or sets the send buffer size.
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the receive buffer size.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the send timeout in milliseconds.
        /// </summary>
        public int SendTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the receive timeout in milliseconds.
        /// </summary>
        public int ReceiveTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the listen backlog.
        /// </summary>
        public int Backlog { get; set; } = 100;

        /// <summary>
        /// Validates the socket options.
        /// </summary>
        public void Validate()
        {
            if (SendBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(SendBufferSize), "SendBufferSize cannot be negative");

            if (ReceiveBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize), "ReceiveBufferSize cannot be negative");

            if (SendTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(SendTimeout), "SendTimeout cannot be negative");

            if (ReceiveTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveTimeout), "ReceiveTimeout cannot be negative");

            if (Backlog < 0)
                throw new ArgumentOutOfRangeException(nameof(Backlog), "Backlog cannot be negative");

            if (LingerTime < 0)
                throw new ArgumentOutOfRangeException(nameof(LingerTime), "LingerTime cannot be negative");
        }
    }

    /// <summary>
    /// Information about a socket.
    /// </summary>
    public class SocketInfo
    {
        /// <summary>
        /// Gets or sets the port number.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the local endpoint.
        /// </summary>
        public IPEndPoint? LocalEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the socket type.
        /// </summary>
        public SocketType SocketType { get; set; }

        /// <summary>
        /// Gets or sets the protocol type.
        /// </summary>
        public ProtocolType ProtocolType { get; set; }

        /// <summary>
        /// Gets or sets whether the socket is bound.
        /// </summary>
        public bool IsBound { get; set; }

        /// <summary>
        /// Gets or sets whether the socket is connected.
        /// </summary>
        public bool IsConnected { get; set; }
    }
}