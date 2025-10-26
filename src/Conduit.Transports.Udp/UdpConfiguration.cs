using System;
using Conduit.Transports.Core;

namespace Conduit.Transports.Udp
{
    /// <summary>
    /// Configuration for UDP transport.
    /// </summary>
    public class UdpConfiguration : TransportConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the UdpConfiguration class.
        /// </summary>
        public UdpConfiguration()
        {
            Type = TransportType.Custom; // UDP is custom type in TransportType enum
            Name = "UDP";
        }

        /// <summary>
        /// Gets or sets the local host to bind to.
        /// </summary>
        public string Host { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the local port to bind to.
        /// </summary>
        public int Port { get; set; } = 0; // 0 = any available port

        /// <summary>
        /// Gets or sets the remote host to send to (for client mode).
        /// </summary>
        public string? RemoteHost { get; set; }

        /// <summary>
        /// Gets or sets the remote port to send to (for client mode).
        /// </summary>
        public int? RemotePort { get; set; }

        /// <summary>
        /// Gets or sets the maximum datagram size in bytes.
        /// </summary>
        public int MaxDatagramSize { get; set; } = 65507; // Max UDP payload (65535 - 8 UDP header - 20 IP header)

        /// <summary>
        /// Gets or sets the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 65536;

        /// <summary>
        /// Gets or sets the send buffer size in bytes.
        /// </summary>
        public int SendBufferSize { get; set; } = 65536;

        /// <summary>
        /// Gets or sets whether to allow broadcast.
        /// </summary>
        public bool AllowBroadcast { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to reuse the address.
        /// </summary>
        public bool ReuseAddress { get; set; } = true;

        /// <summary>
        /// Gets or sets the time-to-live (TTL) for multicast packets.
        /// </summary>
        public byte MulticastTimeToLive { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether to enable multicast loopback.
        /// </summary>
        public bool MulticastLoopback { get; set; } = true;

        /// <summary>
        /// Gets or sets the multicast group address to join.
        /// </summary>
        public string? MulticastGroup { get; set; }

        /// <summary>
        /// Gets or sets the local interface address for multicast (null for default).
        /// </summary>
        public string? MulticastInterface { get; set; }

        /// <summary>
        /// Gets or sets whether to use IPv6.
        /// </summary>
        public bool UseIPv6 { get; set; } = false;

        /// <summary>
        /// Gets or sets the receive timeout in milliseconds (0 for infinite).
        /// </summary>
        public int ReceiveTimeout { get; set; } = 0;

        /// <summary>
        /// Gets or sets the send timeout in milliseconds (0 for infinite).
        /// </summary>
        public int SendTimeout { get; set; } = 5000;

        /// <summary>
        /// Gets or sets whether to use exclusive address binding.
        /// </summary>
        public bool ExclusiveAddressUse { get; set; } = false;

        /// <summary>
        /// Gets or sets whether messages should be acknowledged (custom protocol).
        /// </summary>
        public bool RequireAcknowledgement { get; set; } = false;

        /// <summary>
        /// Gets or sets the acknowledgement timeout in milliseconds.
        /// </summary>
        public int AcknowledgementTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum retransmission attempts.
        /// </summary>
        public int MaxRetransmissions { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether to enable automatic packet fragmentation for large messages.
        /// </summary>
        public bool EnableFragmentation { get; set; } = false;

        /// <summary>
        /// Gets or sets the fragment size in bytes (for fragmentation).
        /// </summary>
        public int FragmentSize { get; set; } = 1400; // Safe size to avoid IP fragmentation

        /// <summary>
        /// Creates a connection string representation.
        /// </summary>
        /// <returns>The connection string</returns>
        public string BuildConnectionString()
        {
            if (!string.IsNullOrEmpty(MulticastGroup))
            {
                return $"udp://{MulticastGroup}:{Port}?multicast=true";
            }
            else if (!string.IsNullOrEmpty(RemoteHost))
            {
                return $"udp://{RemoteHost}:{RemotePort}";
            }
            else
            {
                return $"udp://{Host}:{Port}?listen=true";
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

            if (MaxDatagramSize <= 0 || MaxDatagramSize > 65507)
                throw new ArgumentOutOfRangeException(nameof(MaxDatagramSize), "MaxDatagramSize must be between 1 and 65507");

            if (ReceiveBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize), "ReceiveBufferSize must be greater than 0");

            if (SendBufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(SendBufferSize), "SendBufferSize must be greater than 0");

            if (MulticastTimeToLive == 0)
                throw new ArgumentOutOfRangeException(nameof(MulticastTimeToLive), "MulticastTimeToLive must be greater than 0");

            if (FragmentSize <= 0 || FragmentSize > MaxDatagramSize)
                throw new ArgumentOutOfRangeException(nameof(FragmentSize), $"FragmentSize must be between 1 and {MaxDatagramSize}");

            if (!string.IsNullOrEmpty(MulticastGroup) && AllowBroadcast)
                throw new InvalidOperationException("Cannot use both multicast and broadcast simultaneously");
        }
    }
}
