using System;
using System.Net;
using Conduit.Transports.Core;

namespace Conduit.Gateway
{
    /// <summary>
    /// Configuration for a client-side endpoint connection.
    /// </summary>
    public class ClientEndpoint
    {
        /// <summary>
        /// Gets or sets the unique name for this endpoint.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote endpoint to connect to.
        /// </summary>
        public IPEndPoint Endpoint { get; set; } = new(IPAddress.Loopback, 80);

        /// <summary>
        /// Gets or sets the transport type for this endpoint.
        /// </summary>
        public TransportType Transport { get; set; }

        /// <summary>
        /// Gets or sets the network protocol.
        /// </summary>
        public Protocol Protocol { get; set; } = Protocol.TCP;

        /// <summary>
        /// Gets or sets whether to automatically connect to this endpoint on startup.
        /// </summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the connection retry policy.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; } = new();

        /// <summary>
        /// Gets or sets the socket options for connections to this endpoint.
        /// </summary>
        public SocketOptions SocketOptions { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this endpoint is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a description for this endpoint.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections to this endpoint.
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Gets or sets the connection pool settings.
        /// </summary>
        public ConnectionPoolSettings ConnectionPool { get; set; } = new();

        /// <summary>
        /// Validates the client endpoint configuration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Endpoint name cannot be empty", nameof(Name));

            if (Endpoint == null)
                throw new ArgumentNullException(nameof(Endpoint));

            if (Endpoint.Port < 1 || Endpoint.Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Endpoint), "Port must be between 1 and 65535");

            if (MaxConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxConnections), "MaxConnections must be greater than 0");

            RetryPolicy?.Validate();
            SocketOptions?.Validate();
            ConnectionPool?.Validate();
        }

        /// <summary>
        /// Returns a string representation of this client endpoint.
        /// </summary>
        public override string ToString()
        {
            return $"{Name}: {Endpoint} ({Protocol}) -> {Transport}";
        }
    }

    /// <summary>
    /// Retry policy for client connections.
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial delay between retry attempts.
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the maximum delay between retry attempts.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets whether to use exponential backoff.
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Gets or sets the backoff multiplier (for exponential backoff).
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Validates the retry policy.
        /// </summary>
        public void Validate()
        {
            if (MaxAttempts < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "MaxAttempts cannot be negative");

            if (InitialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(InitialDelay), "InitialDelay cannot be negative");

            if (MaxDelay < InitialDelay)
                throw new ArgumentOutOfRangeException(nameof(MaxDelay), "MaxDelay must be greater than or equal to InitialDelay");

            if (BackoffMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(BackoffMultiplier), "BackoffMultiplier must be greater than 0");
        }
    }

    /// <summary>
    /// Connection pool settings for client endpoints.
    /// </summary>
    public class ConnectionPoolSettings
    {
        /// <summary>
        /// Gets or sets the minimum number of connections to maintain in the pool.
        /// </summary>
        public int MinConnections { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of connections in the pool.
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Gets or sets the timeout for acquiring a connection from the pool.
        /// </summary>
        public TimeSpan AcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the idle timeout for connections in the pool.
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether connection pooling is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Validates the connection pool settings.
        /// </summary>
        public void Validate()
        {
            if (MinConnections < 0)
                throw new ArgumentOutOfRangeException(nameof(MinConnections), "MinConnections cannot be negative");

            if (MaxConnections < MinConnections)
                throw new ArgumentOutOfRangeException(nameof(MaxConnections), "MaxConnections must be greater than or equal to MinConnections");

            if (AcquisitionTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(AcquisitionTimeout), "AcquisitionTimeout cannot be negative");

            if (IdleTimeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(IdleTimeout), "IdleTimeout cannot be negative");
        }
    }
}