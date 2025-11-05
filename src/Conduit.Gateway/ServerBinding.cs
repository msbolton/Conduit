using System;
using System.Net;
using Conduit.Transports.Core;

namespace Conduit.Gateway
{
    /// <summary>
    /// Configuration for a server-side port binding.
    /// </summary>
    public class ServerBinding
    {
        /// <summary>
        /// Gets or sets the port to bind to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the IP address to bind to.
        /// </summary>
        public IPAddress BindAddress { get; set; } = IPAddress.Any;

        /// <summary>
        /// Gets or sets the network protocol.
        /// </summary>
        public Protocol Protocol { get; set; } = Protocol.TCP;

        /// <summary>
        /// Gets or sets the default transport type for this binding.
        /// </summary>
        public TransportType DefaultTransport { get; set; }

        /// <summary>
        /// Gets or sets the socket options for this binding.
        /// </summary>
        public SocketOptions SocketOptions { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this binding is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a description for this binding.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the transport mode for this binding.
        /// </summary>
        public TransportMode TransportMode { get; set; } = TransportMode.Server;

        /// <summary>
        /// Validates the server binding configuration.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when configuration is invalid</exception>
        public void Validate()
        {
            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (BindAddress == null)
                throw new ArgumentNullException(nameof(BindAddress));

            SocketOptions?.Validate();
        }

        /// <summary>
        /// Returns a string representation of this server binding.
        /// </summary>
        public override string ToString()
        {
            return $"{BindAddress}:{Port} ({Protocol}) -> {DefaultTransport}";
        }
    }
}