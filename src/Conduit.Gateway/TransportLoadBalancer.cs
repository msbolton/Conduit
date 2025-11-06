using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Load balancer for selecting transports based on different strategies.
    /// </summary>
    public class TransportLoadBalancer : IDisposable
    {
        private readonly ILogger<TransportLoadBalancer>? _logger;
        private readonly ConcurrentDictionary<string, int> _roundRobinCounters;
        private readonly ConcurrentDictionary<string, TransportWeight> _transportWeights;
        private readonly Random _random;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TransportLoadBalancer class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        public TransportLoadBalancer(ILogger<TransportLoadBalancer>? logger = null)
        {
            _logger = logger;
            _roundRobinCounters = new ConcurrentDictionary<string, int>();
            _transportWeights = new ConcurrentDictionary<string, TransportWeight>();
            _random = new Random();
        }

        /// <summary>
        /// Selects a transport from available options using the specified strategy.
        /// </summary>
        /// <param name="availableTransports">List of available transports</param>
        /// <param name="strategy">Load balancing strategy to use</param>
        /// <param name="connectionInfo">Connection information for context</param>
        /// <param name="connectionTable">Connection table for least connections strategy</param>
        /// <returns>Selected transport, or null if none available</returns>
        public ITransport? SelectTransport(
            IList<ITransport> availableTransports,
            LoadBalancingStrategy strategy,
            ConnectionInfo? connectionInfo = null,
            ConnectionTable? connectionTable = null)
        {
            if (availableTransports == null || !availableTransports.Any())
                return null;

            ThrowIfDisposed();

            // Filter to only connected transports
            var connectedTransports = availableTransports.Where(t => t.IsConnected).ToList();
            if (!connectedTransports.Any())
            {
                _logger?.LogWarning("No connected transports available for load balancing");
                return null;
            }

            var selected = strategy switch
            {
                LoadBalancingStrategy.RoundRobin => SelectRoundRobin(connectedTransports),
                LoadBalancingStrategy.LeastConnections => SelectLeastConnections(connectedTransports, connectionTable),
                LoadBalancingStrategy.Random => SelectRandom(connectedTransports),
                LoadBalancingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(connectedTransports),
                LoadBalancingStrategy.IpHash => SelectIpHash(connectedTransports, connectionInfo),
                _ => SelectRoundRobin(connectedTransports)
            };

            _logger?.LogDebug("Selected transport {Transport} using {Strategy} strategy",
                selected?.Name, strategy);

            return selected;
        }

        /// <summary>
        /// Sets the weight for a transport in weighted round-robin load balancing.
        /// </summary>
        /// <param name="transportType">Transport type</param>
        /// <param name="weight">Weight value (higher = more traffic)</param>
        public void SetTransportWeight(Transports.Core.TransportType transportType, int weight)
        {
            if (weight < 0)
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be non-negative");

            var key = transportType.ToString();
            _transportWeights.AddOrUpdate(key, new TransportWeight { Weight = weight, CurrentWeight = 0 },
                (_, existing) => { existing.Weight = weight; return existing; });

            _logger?.LogInformation("Set weight for transport {Transport} to {Weight}", transportType, weight);
        }

        /// <summary>
        /// Round-robin load balancing.
        /// </summary>
        private ITransport SelectRoundRobin(IList<ITransport> transports)
        {
            var key = string.Join(",", transports.Select(t => t.Type.ToString()).OrderBy(x => x));
            var counter = _roundRobinCounters.AddOrUpdate(key, 0, (_, current) => (current + 1) % transports.Count);
            return transports[counter];
        }

        /// <summary>
        /// Least connections load balancing.
        /// </summary>
        private ITransport SelectLeastConnections(IList<ITransport> transports, ConnectionTable? connectionTable)
        {
            if (connectionTable == null)
            {
                _logger?.LogWarning("ConnectionTable not provided for least connections strategy, falling back to round-robin");
                return SelectRoundRobin(transports);
            }

            var transportConnections = new Dictionary<ITransport, int>();
            var stats = connectionTable.GetStatistics();

            foreach (var transport in transports)
            {
                // Count active connections for this transport
                var connectionCount = stats.ConnectionsByTransport.GetValueOrDefault(transport.Type.ToString(), 0);
                transportConnections[transport] = connectionCount;
            }

            var selected = transportConnections.OrderBy(kvp => kvp.Value).First().Key;
            _logger?.LogDebug("Selected transport {Transport} with {Connections} active connections",
                selected.Name, transportConnections[selected]);

            return selected;
        }

        /// <summary>
        /// Random load balancing.
        /// </summary>
        private ITransport SelectRandom(IList<ITransport> transports)
        {
            var index = _random.Next(transports.Count);
            return transports[index];
        }

        /// <summary>
        /// Weighted round-robin load balancing using smooth weighted round-robin algorithm.
        /// </summary>
        private ITransport SelectWeightedRoundRobin(IList<ITransport> transports)
        {
            var weights = new List<(ITransport Transport, TransportWeight Weight)>();

            // Get or create weights for all transports
            foreach (var transport in transports)
            {
                var key = transport.Type.ToString();
                var weight = _transportWeights.GetOrAdd(key, new TransportWeight { Weight = 1, CurrentWeight = 0 });
                weights.Add((transport, weight));
            }

            // Smooth weighted round-robin algorithm
            var totalWeight = weights.Sum(w => w.Weight.Weight);
            if (totalWeight == 0)
            {
                return SelectRoundRobin(transports); // Fallback if all weights are 0
            }

            // Increase current weights
            foreach (var (_, weight) in weights)
            {
                weight.CurrentWeight += weight.Weight;
            }

            // Select the transport with highest current weight
            var selected = weights.OrderByDescending(w => w.Weight.CurrentWeight).First();

            // Decrease the selected transport's current weight
            selected.Weight.CurrentWeight -= totalWeight;

            return selected.Transport;
        }

        /// <summary>
        /// IP hash load balancing for sticky sessions.
        /// </summary>
        private ITransport SelectIpHash(IList<ITransport> transports, ConnectionInfo? connectionInfo)
        {
            if (connectionInfo?.SourceEndpoint?.Address == null)
            {
                _logger?.LogWarning("No remote IP available for IP hash strategy, falling back to round-robin");
                return SelectRoundRobin(transports);
            }

            // Create a hash from the client IP
            var ipBytes = connectionInfo.SourceEndpoint.Address.GetAddressBytes();
            using var sha1 = SHA1.Create();
            var hashBytes = sha1.ComputeHash(ipBytes);
            var hash = BitConverter.ToUInt32(hashBytes, 0);

            var index = (int)(hash % (uint)transports.Count);
            var selected = transports[index];

            _logger?.LogDebug("Selected transport {Transport} for IP {IP} using hash {Hash}",
                selected.Name, connectionInfo.SourceEndpoint.Address, hash);

            return selected;
        }

        /// <summary>
        /// Gets load balancing statistics.
        /// </summary>
        /// <returns>Load balancing statistics</returns>
        public LoadBalancingStatistics GetStatistics()
        {
            return new LoadBalancingStatistics
            {
                RoundRobinCounters = _roundRobinCounters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                TransportWeights = _transportWeights.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)new { Weight = kvp.Value.Weight, CurrentWeight = kvp.Value.CurrentWeight })
            };
        }

        /// <summary>
        /// Throws an exception if the load balancer has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransportLoadBalancer));
        }

        /// <summary>
        /// Disposes the load balancer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _logger?.LogInformation("Transport load balancer disposed");
        }
    }

    /// <summary>
    /// Weight information for weighted round-robin load balancing.
    /// </summary>
    internal class TransportWeight
    {
        /// <summary>
        /// Gets or sets the configured weight.
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// Gets or sets the current weight for the smooth weighted round-robin algorithm.
        /// </summary>
        public int CurrentWeight { get; set; }
    }

    /// <summary>
    /// Load balancing statistics.
    /// </summary>
    public class LoadBalancingStatistics
    {
        /// <summary>
        /// Gets or sets the round-robin counters by transport group.
        /// </summary>
        public Dictionary<string, int> RoundRobinCounters { get; set; } = new();

        /// <summary>
        /// Gets or sets the transport weights.
        /// </summary>
        public Dictionary<string, object> TransportWeights { get; set; } = new();
    }
}