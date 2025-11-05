using System;
using System.Collections.Generic;

namespace Conduit.Configuration
{
    /// <summary>
    /// Configuration for all transport types.
    /// </summary>
    public class TransportsConfiguration
    {
        /// <summary>
        /// Gets or sets HTTP transport configuration.
        /// </summary>
        public HttpTransportConfiguration Http { get; set; } = new();

        /// <summary>
        /// Gets or sets TCP transport configuration.
        /// </summary>
        public TcpTransportConfiguration Tcp { get; set; } = new();

        /// <summary>
        /// Gets or sets UDP transport configuration.
        /// </summary>
        public UdpTransportConfiguration Udp { get; set; } = new();

        /// <summary>
        /// Gets or sets ActiveMQ transport configuration.
        /// </summary>
        public ActiveMqTransportConfiguration ActiveMq { get; set; } = new();

        /// <summary>
        /// Gets or sets ZeroMQ transport configuration.
        /// </summary>
        public ZeroMqTransportConfiguration ZeroMq { get; set; } = new();

        /// <summary>
        /// Gets or sets WebSocket transport configuration.
        /// </summary>
        public WebSocketTransportConfiguration WebSocket { get; set; } = new();

        /// <summary>
        /// Gets or sets gRPC transport configuration.
        /// </summary>
        public GrpcTransportConfiguration Grpc { get; set; } = new();

        /// <summary>
        /// Gets or sets custom transport configurations.
        /// </summary>
        public Dictionary<string, object> Custom { get; set; } = new();

        /// <summary>
        /// Validates all transport configurations.
        /// </summary>
        public void Validate()
        {
            Http?.Validate();
            Tcp?.Validate();
            Udp?.Validate();
            ActiveMq?.Validate();
            ZeroMq?.Validate();
            WebSocket?.Validate();
            Grpc?.Validate();
        }
    }

    /// <summary>
    /// HTTP transport configuration.
    /// </summary>
    public class HttpTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether HTTP transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the HTTP listen address.
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the HTTP listen port.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Gets or sets whether to enable HTTPS.
        /// </summary>
        public bool EnableHttps { get; set; } = false;

        /// <summary>
        /// Gets or sets the HTTPS port.
        /// </summary>
        public int HttpsPort { get; set; } = 8443;

        /// <summary>
        /// Gets or sets the SSL certificate path.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the SSL certificate password.
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int RequestTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum request body size in bytes.
        /// </summary>
        public long MaxRequestBodySize { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Gets or sets whether to enable compression.
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Gets or sets CORS configuration.
        /// </summary>
        public CorsConfiguration Cors { get; set; } = new();

        /// <summary>
        /// Validates the HTTP transport configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (EnableHttps && (HttpsPort < 1 || HttpsPort > 65535))
                throw new ArgumentOutOfRangeException(nameof(HttpsPort), "HTTPS Port must be between 1 and 65535");

            if (RequestTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(RequestTimeout), "RequestTimeout must be greater than 0");

            if (MaxRequestBodySize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxRequestBodySize), "MaxRequestBodySize must be greater than 0");

            Cors?.Validate();
        }
    }

    /// <summary>
    /// CORS configuration.
    /// </summary>
    public class CorsConfiguration
    {
        /// <summary>
        /// Gets or sets whether CORS is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new() { "*" };

        /// <summary>
        /// Gets or sets allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

        /// <summary>
        /// Gets or sets allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new() { "*" };

        /// <summary>
        /// Gets or sets whether credentials are allowed.
        /// </summary>
        public bool AllowCredentials { get; set; } = false;

        /// <summary>
        /// Gets or sets the preflight cache duration in seconds.
        /// </summary>
        public int PreflightMaxAge { get; set; } = 86400; // 24 hours

        /// <summary>
        /// Validates the CORS configuration.
        /// </summary>
        public void Validate()
        {
            if (PreflightMaxAge < 0)
                throw new ArgumentOutOfRangeException(nameof(PreflightMaxAge), "PreflightMaxAge cannot be negative");
        }
    }

    /// <summary>
    /// TCP transport configuration.
    /// </summary>
    public class TcpTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether TCP transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the TCP listen address.
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the TCP listen port.
        /// </summary>
        public int Port { get; set; } = 9090;

        /// <summary>
        /// Gets or sets whether to enable TCP keep-alive.
        /// </summary>
        public bool EnableKeepAlive { get; set; } = true;

        /// <summary>
        /// Gets or sets the keep-alive interval in milliseconds.
        /// </summary>
        public int KeepAliveInterval { get; set; } = 60000;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the send buffer size in bytes.
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets whether to enable Nagle's algorithm.
        /// </summary>
        public bool EnableNagle { get; set; } = false;

        /// <summary>
        /// Validates the TCP transport configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (ConnectionTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(ConnectionTimeout), "ConnectionTimeout must be greater than 0");

            if (ReceiveBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize), "ReceiveBufferSize must be greater than 0");

            if (SendBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(SendBufferSize), "SendBufferSize must be greater than 0");
        }
    }

    /// <summary>
    /// UDP transport configuration.
    /// </summary>
    public class UdpTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether UDP transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the UDP listen address.
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the UDP listen port.
        /// </summary>
        public int Port { get; set; } = 9091;

        /// <summary>
        /// Gets or sets the maximum packet size in bytes.
        /// </summary>
        public int MaxPacketSize { get; set; } = 65536;

        /// <summary>
        /// Gets or sets the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 65536;

        /// <summary>
        /// Gets or sets whether to enable broadcast.
        /// </summary>
        public bool EnableBroadcast { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable multicast.
        /// </summary>
        public bool EnableMulticast { get; set; } = false;

        /// <summary>
        /// Gets or sets multicast groups to join.
        /// </summary>
        public List<string> MulticastGroups { get; set; } = new();

        /// <summary>
        /// Validates the UDP transport configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (MaxPacketSize <= 0 || MaxPacketSize > 65536)
                throw new ArgumentOutOfRangeException(nameof(MaxPacketSize), "MaxPacketSize must be between 1 and 65536");

            if (ReceiveBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize), "ReceiveBufferSize must be greater than 0");
        }
    }

    /// <summary>
    /// ActiveMQ transport configuration.
    /// </summary>
    public class ActiveMqTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether ActiveMQ transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the ActiveMQ broker URI.
        /// </summary>
        public string BrokerUri { get; set; } = "tcp://localhost:61616";

        /// <summary>
        /// Gets or sets the username for authentication.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the password for authentication.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the session acknowledgment mode.
        /// </summary>
        public string AcknowledgmentMode { get; set; } = "AutoAcknowledge";

        /// <summary>
        /// Gets or sets the message time-to-live in milliseconds.
        /// </summary>
        public int MessageTimeToLive { get; set; } = 0; // 0 = no expiration

        /// <summary>
        /// Gets or sets connection pool configuration.
        /// </summary>
        public ConnectionPoolConfiguration ConnectionPool { get; set; } = new();

        /// <summary>
        /// Validates the ActiveMQ transport configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BrokerUri))
                throw new ArgumentException("BrokerUri cannot be null or empty");

            if (ConnectionTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(ConnectionTimeout), "ConnectionTimeout must be greater than 0");

            var validAckModes = new[] { "AutoAcknowledge", "ClientAcknowledge", "DupsOkAcknowledge", "Transactional" };
            if (!validAckModes.Contains(AcknowledgmentMode))
                throw new ArgumentException($"Invalid AcknowledgmentMode: {AcknowledgmentMode}");

            ConnectionPool?.Validate();
        }
    }

    /// <summary>
    /// ZeroMQ transport configuration.
    /// </summary>
    public class ZeroMqTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether ZeroMQ transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the ZeroMQ socket type.
        /// </summary>
        public string SocketType { get; set; } = "REQ"; // REQ, REP, PUSH, PULL, PUB, SUB

        /// <summary>
        /// Gets or sets the bind address for server sockets.
        /// </summary>
        public string? BindAddress { get; set; }

        /// <summary>
        /// Gets or sets the connect addresses for client sockets.
        /// </summary>
        public List<string> ConnectAddresses { get; set; } = new();

        /// <summary>
        /// Gets or sets the high water mark for outgoing messages.
        /// </summary>
        public int SendHighWaterMark { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the high water mark for incoming messages.
        /// </summary>
        public int ReceiveHighWaterMark { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the linger time in milliseconds.
        /// </summary>
        public int LingerTime { get; set; } = 1000;

        /// <summary>
        /// Gets or sets subscription topics for SUB sockets.
        /// </summary>
        public List<string> SubscriptionTopics { get; set; } = new();

        /// <summary>
        /// Validates the ZeroMQ transport configuration.
        /// </summary>
        public void Validate()
        {
            var validSocketTypes = new[] { "REQ", "REP", "PUSH", "PULL", "PUB", "SUB", "DEALER", "ROUTER" };
            if (!validSocketTypes.Contains(SocketType))
                throw new ArgumentException($"Invalid SocketType: {SocketType}");

            if (SendHighWaterMark < 0)
                throw new ArgumentOutOfRangeException(nameof(SendHighWaterMark), "SendHighWaterMark cannot be negative");

            if (ReceiveHighWaterMark < 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveHighWaterMark), "ReceiveHighWaterMark cannot be negative");
        }
    }

    /// <summary>
    /// WebSocket transport configuration.
    /// </summary>
    public class WebSocketTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether WebSocket transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the WebSocket listen address.
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the WebSocket listen port.
        /// </summary>
        public int Port { get; set; } = 8081;

        /// <summary>
        /// Gets or sets the WebSocket path.
        /// </summary>
        public string Path { get; set; } = "/ws";

        /// <summary>
        /// Gets or sets whether to enable SSL.
        /// </summary>
        public bool EnableSsl { get; set; } = false;

        /// <summary>
        /// Gets or sets the SSL certificate path.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the ping interval in milliseconds.
        /// </summary>
        public int PingInterval { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the ping timeout in milliseconds.
        /// </summary>
        public int PingTimeout { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the maximum message size in bytes.
        /// </summary>
        public int MaxMessageSize { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Validates the WebSocket transport configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (string.IsNullOrWhiteSpace(Path))
                throw new ArgumentException("Path cannot be null or empty");

            if (!Path.StartsWith("/"))
                throw new ArgumentException("Path must start with '/'");

            if (PingInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(PingInterval), "PingInterval must be greater than 0");

            if (PingTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(PingTimeout), "PingTimeout must be greater than 0");

            if (MaxMessageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "MaxMessageSize must be greater than 0");
        }
    }

    /// <summary>
    /// gRPC transport configuration.
    /// </summary>
    public class GrpcTransportConfiguration
    {
        /// <summary>
        /// Gets or sets whether gRPC transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the gRPC listen address.
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the gRPC listen port.
        /// </summary>
        public int Port { get; set; } = 9092;

        /// <summary>
        /// Gets or sets whether to enable TLS.
        /// </summary>
        public bool EnableTls { get; set; } = false;

        /// <summary>
        /// Gets or sets the TLS certificate path.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the TLS private key path.
        /// </summary>
        public string? PrivateKeyPath { get; set; }

        /// <summary>
        /// Gets or sets the maximum message size in bytes.
        /// </summary>
        public int MaxMessageSize { get; set; } = 4194304; // 4MB

        /// <summary>
        /// Gets or sets the keep-alive interval in milliseconds.
        /// </summary>
        public int KeepAliveInterval { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the keep-alive timeout in milliseconds.
        /// </summary>
        public int KeepAliveTimeout { get; set; } = 5000;

        /// <summary>
        /// Validates the gRPC transport configuration.
        /// </summary>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (MaxMessageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "MaxMessageSize must be greater than 0");

            if (KeepAliveInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(KeepAliveInterval), "KeepAliveInterval must be greater than 0");

            if (KeepAliveTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(KeepAliveTimeout), "KeepAliveTimeout must be greater than 0");
        }
    }

    /// <summary>
    /// Connection pool configuration.
    /// </summary>
    public class ConnectionPoolConfiguration
    {
        /// <summary>
        /// Gets or sets whether connection pooling is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum pool size.
        /// </summary>
        public int MinPoolSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum pool size.
        /// </summary>
        public int MaxPoolSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the connection idle timeout in milliseconds.
        /// </summary>
        public int IdleTimeout { get; set; } = 300000; // 5 minutes

        /// <summary>
        /// Gets or sets the connection validation interval in milliseconds.
        /// </summary>
        public int ValidationInterval { get; set; } = 60000; // 1 minute

        /// <summary>
        /// Validates the connection pool configuration.
        /// </summary>
        public void Validate()
        {
            if (MinPoolSize < 0)
                throw new ArgumentOutOfRangeException(nameof(MinPoolSize), "MinPoolSize cannot be negative");

            if (MaxPoolSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxPoolSize), "MaxPoolSize must be greater than 0");

            if (MinPoolSize > MaxPoolSize)
                throw new ArgumentException("MinPoolSize cannot be greater than MaxPoolSize");

            if (IdleTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(IdleTimeout), "IdleTimeout must be greater than 0");

            if (ValidationInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(ValidationInterval), "ValidationInterval must be greater than 0");
        }
    }
}