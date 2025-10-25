using System;
using System.Collections.Generic;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Configuration for transport adapters.
    /// Contains all settings needed to configure a specific transport implementation.
    /// </summary>
    public class TransportConfiguration
    {
        /// <summary>
        /// Gets or sets the transport type.
        /// </summary>
        public TransportType Type { get; set; }

        /// <summary>
        /// Gets or sets the transport name/identifier.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets custom properties for transport-specific configuration.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// Gets or sets connection settings.
        /// </summary>
        public ConnectionSettings Connection { get; set; } = new();

        /// <summary>
        /// Gets or sets protocol settings.
        /// </summary>
        public ProtocolSettings Protocol { get; set; } = new();

        /// <summary>
        /// Gets or sets security settings.
        /// </summary>
        public SecuritySettings Security { get; set; } = new();

        /// <summary>
        /// Gets or sets performance settings.
        /// </summary>
        public PerformanceSettings Performance { get; set; } = new();

        /// <summary>
        /// Gets a property value with type casting.
        /// </summary>
        /// <typeparam name="T">The expected type</typeparam>
        /// <param name="key">The property key</param>
        /// <returns>The property value, or default if not found</returns>
        public T? GetProperty<T>(string key)
        {
            if (Properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a property value.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <param name="value">The property value</param>
        public void SetProperty(string key, object value)
        {
            Properties[key] = value;
        }
    }

    /// <summary>
    /// Connection-related settings.
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Gets or sets the connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the read/receive timeout.
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the write/send timeout.
        /// </summary>
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the keep-alive interval.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum number of connection retry attempts.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay between retry attempts.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets whether to automatically reconnect on connection failure.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the reconnect delay.
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections.
        /// </summary>
        public int MaxConcurrentConnections { get; set; } = 10;

        /// <summary>
        /// Gets or sets the connection pool size.
        /// </summary>
        public int PoolSize { get; set; } = 5;

        /// <summary>
        /// Gets or sets the idle connection timeout.
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Protocol-related settings.
    /// </summary>
    public class ProtocolSettings
    {
        /// <summary>
        /// Gets or sets the preferred protocol version.
        /// </summary>
        public string? PreferredVersion { get; set; }

        /// <summary>
        /// Gets or sets supported protocol versions.
        /// </summary>
        public string[] SupportedVersions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets whether to auto-negotiate protocol version.
        /// </summary>
        public bool AutoNegotiate { get; set; } = true;

        /// <summary>
        /// Gets or sets the protocol negotiation timeout.
        /// </summary>
        public TimeSpan NegotiationTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the maximum message size in bytes.
        /// </summary>
        public long MaxMessageSize { get; set; } = 1024 * 1024; // 1 MB

        /// <summary>
        /// Gets or sets whether to compress messages.
        /// </summary>
        public bool CompressionEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the compression threshold (messages larger than this will be compressed).
        /// </summary>
        public int CompressionThreshold { get; set; } = 1024; // 1 KB

        /// <summary>
        /// Gets or sets custom protocol headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    /// <summary>
    /// Security-related settings.
    /// </summary>
    public class SecuritySettings
    {
        /// <summary>
        /// Gets or sets whether TLS/SSL is enabled.
        /// </summary>
        public bool TlsEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to verify the server hostname.
        /// </summary>
        public bool VerifyHostname { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to verify the server certificate.
        /// </summary>
        public bool VerifyCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets the path to the certificate file.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the certificate password.
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Gets or sets the path to the trusted CA certificate.
        /// </summary>
        public string? TrustedCertificatePath { get; set; }

        /// <summary>
        /// Gets or sets supported cipher suites.
        /// </summary>
        public string[] CipherSuites { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the minimum TLS version.
        /// </summary>
        public string? MinimumTlsVersion { get; set; } = "TLS 1.2";

        /// <summary>
        /// Gets or sets whether to require client certificates.
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;

        /// <summary>
        /// Gets or sets the authentication username.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the authentication password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        public string? Token { get; set; }
    }

    /// <summary>
    /// Performance-related settings.
    /// </summary>
    public class PerformanceSettings
    {
        /// <summary>
        /// Gets or sets the send buffer size in bytes.
        /// </summary>
        public int SendBufferSize { get; set; } = 8192; // 8 KB

        /// <summary>
        /// Gets or sets the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192; // 8 KB

        /// <summary>
        /// Gets or sets whether to use Nagle's algorithm.
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable TCP keep-alive.
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// Gets or sets the prefetch count for message consumption.
        /// </summary>
        public int PrefetchCount { get; set; } = 10;

        /// <summary>
        /// Gets or sets the batch size for message sending.
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the batch timeout.
        /// </summary>
        public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations.
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to enable message batching.
        /// </summary>
        public bool BatchingEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable message pipelining.
        /// </summary>
        public bool PipeliningEnabled { get; set; } = false;
    }
}
