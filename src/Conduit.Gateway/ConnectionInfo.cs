using System;
using System.Net;

namespace Conduit.Gateway
{
    /// <summary>
    /// Information about a network connection.
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// Gets or sets the source IP endpoint.
        /// </summary>
        public IPEndPoint? SourceEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the destination IP endpoint.
        /// </summary>
        public IPEndPoint? DestinationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the network protocol.
        /// </summary>
        public Protocol Protocol { get; set; }

        /// <summary>
        /// Gets or sets when the connection was established.
        /// </summary>
        public DateTime EstablishedTime { get; set; }

        /// <summary>
        /// Gets or sets custom metadata for the connection.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Creates a new ConnectionInfo instance.
        /// </summary>
        public ConnectionInfo()
        {
            EstablishedTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new ConnectionInfo instance with specified endpoints.
        /// </summary>
        /// <param name="source">Source endpoint</param>
        /// <param name="destination">Destination endpoint</param>
        /// <param name="protocol">Network protocol</param>
        public ConnectionInfo(IPEndPoint source, IPEndPoint destination, Protocol protocol)
        {
            SourceEndpoint = source;
            DestinationEndpoint = destination;
            Protocol = protocol;
            EstablishedTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the connection.
        /// </summary>
        public override string ToString()
        {
            return $"{Protocol}: {SourceEndpoint} -> {DestinationEndpoint}";
        }
    }
}