using System;
using Conduit.Transports.Core;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// Configuration for TCP/Socket transport.
    /// </summary>
    public class TcpConfiguration : TransportConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the TcpConfiguration class.
        /// </summary>
        public TcpConfiguration()
        {
            Type = TransportType.Tcp;
            Name = "TCP";
        }

        /// <summary>
        /// Gets or sets the host to bind to (for server mode).
        /// </summary>
        public string Host { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the port to bind to (for server mode) or connect to (for client mode).
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// Gets or sets whether to run in server mode.
        /// </summary>
        public bool IsServer { get; set; } = false;

        /// <summary>
        /// Gets or sets the remote host to connect to (for client mode).
        /// </summary>
        public string? RemoteHost { get; set; }

        /// <summary>
        /// Gets or sets the remote port to connect to (for client mode).
        /// </summary>
        public int? RemotePort { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections (server mode).
        /// </summary>
        public int MaxConnections { get; set; } = 100;

        /// <summary>
        /// Gets or sets the backlog size for the listening socket.
        /// </summary>
        public int Backlog { get; set; } = 100;

        /// <summary>
        /// Gets or sets the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the send buffer size in bytes.
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets whether to use TCP keep-alive.
        /// </summary>
        public bool UseKeepAlive { get; set; } = true;

        /// <summary>
        /// Gets or sets the keep-alive interval in milliseconds.
        /// </summary>
        public int KeepAliveInterval { get; set; } = 60000; // 60 seconds

        /// <summary>
        /// Gets or sets the keep-alive retry count.
        /// </summary>
        public int KeepAliveRetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to disable Nagle's algorithm (enable TCP_NODELAY).
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// Gets or sets the linger time in seconds (0 to disable).
        /// </summary>
        public int LingerTime { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to reuse the address.
        /// </summary>
        public bool ReuseAddress { get; set; } = true;

        /// <summary>
        /// Gets or sets the framing protocol to use.
        /// </summary>
        public FramingProtocol FramingProtocol { get; set; } = FramingProtocol.LengthPrefixed;

        /// <summary>
        /// Gets or sets the maximum message size in bytes.
        /// </summary>
        public int MaxMessageSize { get; set; } = 1048576; // 1 MB

        /// <summary>
        /// Gets or sets whether to use TLS/SSL.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Gets or sets the SSL certificate path (server mode).
        /// </summary>
        public string? SslCertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the SSL certificate password (server mode).
        /// </summary>
        public string? SslCertificatePassword { get; set; }

        /// <summary>
        /// Gets or sets whether to validate the server certificate (client mode).
        /// </summary>
        public bool ValidateServerCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets the expected server name for certificate validation (client mode).
        /// </summary>
        public string? ServerName { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat interval in milliseconds (0 to disable).
        /// </summary>
        public int HeartbeatInterval { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Gets or sets the heartbeat timeout in milliseconds.
        /// </summary>
        public int HeartbeatTimeout { get; set; } = 60000; // 60 seconds

        /// <summary>
        /// Gets or sets whether to enable connection pooling (client mode).
        /// </summary>
        public bool UseConnectionPooling { get; set; } = true;

        /// <summary>
        /// Gets or sets the connection pool size (client mode).
        /// </summary>
        public int ConnectionPoolSize { get; set; } = 5;

        /// <summary>
        /// Gets or sets the connection pool timeout in milliseconds.
        /// </summary>
        public int ConnectionPoolTimeout { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Creates a connection string representation.
        /// </summary>
        /// <returns>The connection string</returns>
        public string BuildConnectionString()
        {
            if (IsServer)
            {
                return $"tcp://{Host}:{Port}?server=true";
            }
            else
            {
                var host = RemoteHost ?? "localhost";
                var port = RemotePort ?? 5000;
                return $"tcp://{host}:{port}";
            }
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 0 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535");

            if (RemotePort.HasValue && (RemotePort.Value < 0 || RemotePort.Value > 65535))
                throw new ArgumentOutOfRangeException(nameof(RemotePort), "RemotePort must be between 0 and 65535");

            if (MaxConnections <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxConnections), "MaxConnections must be greater than 0");

            if (Backlog <= 0)
                throw new ArgumentOutOfRangeException(nameof(Backlog), "Backlog must be greater than 0");

            if (ReceiveBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize), "ReceiveBufferSize must be greater than 0");

            if (SendBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(SendBufferSize), "SendBufferSize must be greater than 0");

            if (MaxMessageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "MaxMessageSize must be greater than 0");

            if (!IsServer && string.IsNullOrEmpty(RemoteHost))
                throw new ArgumentException("RemoteHost must be specified for client mode", nameof(RemoteHost));

            if (UseSsl && IsServer && string.IsNullOrEmpty(SslCertificatePath))
                throw new ArgumentException("SslCertificatePath must be specified when UseSsl is enabled in server mode", nameof(SslCertificatePath));
        }
    }

    /// <summary>
    /// Framing protocols for TCP message delimitation.
    /// </summary>
    public enum FramingProtocol
    {
        /// <summary>
        /// Length-prefixed framing (4-byte length header).
        /// </summary>
        LengthPrefixed = 0,

        /// <summary>
        /// Newline-delimited framing (\n).
        /// </summary>
        NewlineDelimited = 1,

        /// <summary>
        /// CRLF-delimited framing (\r\n).
        /// </summary>
        CrlfDelimited = 2,

        /// <summary>
        /// Custom delimiter framing.
        /// </summary>
        CustomDelimiter = 3
    }
}
