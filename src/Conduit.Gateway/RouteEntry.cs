using System;
using System.Collections.Generic;
using System.Net;
using Conduit.Transports.Core;

namespace Conduit.Gateway
{
    /// <summary>
    /// Represents a single entry in the routing table.
    /// </summary>
    public class RouteEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for this route.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the direction this route applies to.
        /// </summary>
        public RouteDirection Direction { get; set; }

        /// <summary>
        /// Gets or sets the source network to match (CIDR notation supported).
        /// </summary>
        public string? SourceNetwork { get; set; }

        /// <summary>
        /// Gets or sets the source port to match.
        /// </summary>
        public int? SourcePort { get; set; }

        /// <summary>
        /// Gets or sets the destination network to match (CIDR notation supported).
        /// </summary>
        public string? DestinationNetwork { get; set; }

        /// <summary>
        /// Gets or sets the destination port to match.
        /// </summary>
        public int? DestinationPort { get; set; }

        /// <summary>
        /// Gets or sets the protocol to match.
        /// </summary>
        public Protocol Protocol { get; set; } = Protocol.Any;

        /// <summary>
        /// Gets or sets the action to take when this route matches.
        /// </summary>
        public RouteAction Action { get; set; }

        /// <summary>
        /// Gets or sets the target transport for this route.
        /// </summary>
        public ITransport? TargetTransport { get; set; }

        /// <summary>
        /// Gets or sets the transport mode for this route.
        /// </summary>
        public TransportMode TransportMode { get; set; }

        /// <summary>
        /// Gets or sets the priority of this route (higher numbers = higher priority).
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether this route is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the load balancing strategy for this route.
        /// </summary>
        public LoadBalancingStrategy? LoadBalancingStrategy { get; set; }

        /// <summary>
        /// Gets or sets the rate limit for this route (requests per second).
        /// </summary>
        public int? RateLimit { get; set; }

        /// <summary>
        /// Gets or sets custom metadata for this route.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when this route was created.
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this route was last used.
        /// </summary>
        public DateTime? LastUsedTime { get; set; }

        /// <summary>
        /// Gets or sets the number of times this route has been matched.
        /// </summary>
        public long MatchCount { get; set; }

        /// <summary>
        /// Checks if this route matches the given connection info.
        /// </summary>
        /// <param name="connectionInfo">The connection to check</param>
        /// <returns>True if this route matches the connection</returns>
        public bool Matches(ConnectionInfo connectionInfo)
        {
            if (!Enabled)
                return false;

            // Check protocol
            if (Protocol != Protocol.Any && Protocol != connectionInfo.Protocol)
                return false;

            // Check source network and port
            if (SourceNetwork != null && connectionInfo.SourceEndpoint != null)
            {
                if (!IsIpInNetwork(connectionInfo.SourceEndpoint.Address, SourceNetwork))
                    return false;
            }

            if (SourcePort.HasValue && connectionInfo.SourceEndpoint?.Port != SourcePort.Value)
                return false;

            // Check destination network and port
            if (DestinationNetwork != null && connectionInfo.DestinationEndpoint != null)
            {
                if (!IsIpInNetwork(connectionInfo.DestinationEndpoint.Address, DestinationNetwork))
                    return false;
            }

            if (DestinationPort.HasValue && connectionInfo.DestinationEndpoint?.Port != DestinationPort.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Records that this route was used.
        /// </summary>
        public void RecordMatch()
        {
            LastUsedTime = DateTime.UtcNow;
            MatchCount++;
        }

        /// <summary>
        /// Checks if an IP address is within the specified network.
        /// </summary>
        private bool IsIpInNetwork(IPAddress ipAddress, string network)
        {
            try
            {
                // Handle CIDR notation (e.g., "192.168.1.0/24")
                if (network.Contains('/'))
                {
                    var parts = network.Split('/');
                    var networkAddress = IPAddress.Parse(parts[0]);
                    var prefixLength = int.Parse(parts[1]);

                    return IsIpInCidr(ipAddress, networkAddress, prefixLength);
                }
                else
                {
                    // Direct IP match
                    return ipAddress.Equals(IPAddress.Parse(network));
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an IP address is within a CIDR range.
        /// </summary>
        private bool IsIpInCidr(IPAddress ipAddress, IPAddress networkAddress, int prefixLength)
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            if (ipBytes.Length != networkBytes.Length)
                return false;

            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            // Check full bytes
            for (int i = 0; i < bytesToCheck; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                    return false;
            }

            // Check partial byte if needed
            if (bitsToCheck > 0 && bytesToCheck < ipBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                return (ipBytes[bytesToCheck] & mask) == (networkBytes[bytesToCheck] & mask);
            }

            return true;
        }

        /// <summary>
        /// Returns a string representation of this route.
        /// </summary>
        public override string ToString()
        {
            var direction = Direction.ToString().ToLower();
            var protocol = Protocol == Protocol.Any ? "*" : Protocol.ToString();
            var source = SourceNetwork ?? "*";
            var sourcePort = SourcePort?.ToString() ?? "*";
            var dest = DestinationNetwork ?? "*";
            var destPort = DestinationPort?.ToString() ?? "*";
            var action = Action.ToString().ToLower();

            return $"{direction} {protocol} {source}:{sourcePort} -> {dest}:{destPort} {action} (pri:{Priority})";
        }
    }
}