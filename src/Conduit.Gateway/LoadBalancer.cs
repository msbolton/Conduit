using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Load balancer for selecting upstream servers.
    /// </summary>
    public class LoadBalancer
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, UpstreamState> _upstreamStates;
        private readonly ConcurrentDictionary<string, int> _roundRobinCounters;
        private readonly Random _random;

        /// <summary>
        /// Initializes a new instance of the LoadBalancer class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public LoadBalancer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _upstreamStates = new ConcurrentDictionary<string, UpstreamState>();
            _roundRobinCounters = new ConcurrentDictionary<string, int>();
            _random = new Random();
        }

        /// <summary>
        /// Selects an upstream server using the specified strategy.
        /// </summary>
        /// <param name="upstreams">The list of upstream servers</param>
        /// <param name="strategy">The load balancing strategy</param>
        /// <param name="clientIp">The client IP address (for IP hash strategy)</param>
        /// <returns>The selected upstream server, or null if none available</returns>
        public string? SelectUpstream(List<string> upstreams, LoadBalancingStrategy strategy, string? clientIp = null)
        {
            if (upstreams == null || upstreams.Count == 0)
            {
                _logger.LogWarning("No upstreams available");
                return null;
            }

            // Filter out unhealthy upstreams
            var healthyUpstreams = upstreams
                .Where(u => IsHealthy(u))
                .ToList();

            if (healthyUpstreams.Count == 0)
            {
                _logger.LogWarning("No healthy upstreams available");
                return null;
            }

            if (healthyUpstreams.Count == 1)
            {
                return healthyUpstreams[0];
            }

            return strategy switch
            {
                LoadBalancingStrategy.RoundRobin => SelectRoundRobin(healthyUpstreams),
                LoadBalancingStrategy.LeastConnections => SelectLeastConnections(healthyUpstreams),
                LoadBalancingStrategy.Random => SelectRandom(healthyUpstreams),
                LoadBalancingStrategy.IpHash => SelectIpHash(healthyUpstreams, clientIp),
                _ => SelectRoundRobin(healthyUpstreams)
            };
        }

        /// <summary>
        /// Records a request start for an upstream.
        /// </summary>
        /// <param name="upstream">The upstream server</param>
        public void RecordRequestStart(string upstream)
        {
            var state = GetOrCreateUpstreamState(upstream);
            state.IncrementConnections();
        }

        /// <summary>
        /// Records a request completion for an upstream.
        /// </summary>
        /// <param name="upstream">The upstream server</param>
        /// <param name="success">Whether the request was successful</param>
        public void RecordRequestComplete(string upstream, bool success)
        {
            var state = GetOrCreateUpstreamState(upstream);
            state.DecrementConnections();

            if (success)
            {
                state.RecordSuccess();
            }
            else
            {
                state.RecordFailure();
            }
        }

        /// <summary>
        /// Marks an upstream as unhealthy.
        /// </summary>
        /// <param name="upstream">The upstream server</param>
        public void MarkUnhealthy(string upstream)
        {
            var state = GetOrCreateUpstreamState(upstream);
            state.MarkUnhealthy();
            _logger.LogWarning("Upstream {Upstream} marked as unhealthy", upstream);
        }

        /// <summary>
        /// Marks an upstream as healthy.
        /// </summary>
        /// <param name="upstream">The upstream server</param>
        public void MarkHealthy(string upstream)
        {
            var state = GetOrCreateUpstreamState(upstream);
            state.MarkHealthy();
            _logger.LogInformation("Upstream {Upstream} marked as healthy", upstream);
        }

        /// <summary>
        /// Gets the state of an upstream server.
        /// </summary>
        /// <param name="upstream">The upstream server</param>
        /// <returns>The upstream state</returns>
        public UpstreamState GetUpstreamState(string upstream)
        {
            return GetOrCreateUpstreamState(upstream);
        }

        /// <summary>
        /// Checks if an upstream is healthy.
        /// </summary>
        private bool IsHealthy(string upstream)
        {
            var state = GetOrCreateUpstreamState(upstream);
            return state.IsHealthy;
        }

        /// <summary>
        /// Gets or creates the state for an upstream.
        /// </summary>
        private UpstreamState GetOrCreateUpstreamState(string upstream)
        {
            return _upstreamStates.GetOrAdd(upstream, _ => new UpstreamState());
        }

        /// <summary>
        /// Selects an upstream using round-robin strategy.
        /// </summary>
        private string SelectRoundRobin(List<string> upstreams)
        {
            var key = string.Join(",", upstreams.OrderBy(u => u));
            var counter = _roundRobinCounters.AddOrUpdate(key, 0, (_, current) => (current + 1) % upstreams.Count);
            return upstreams[counter];
        }

        /// <summary>
        /// Selects an upstream using least connections strategy.
        /// </summary>
        private string SelectLeastConnections(List<string> upstreams)
        {
            var selected = upstreams
                .OrderBy(u => GetOrCreateUpstreamState(u).ActiveConnections)
                .ThenBy(_ => _random.Next()) // Random tiebreaker
                .First();

            return selected;
        }

        /// <summary>
        /// Selects an upstream using random strategy.
        /// </summary>
        private string SelectRandom(List<string> upstreams)
        {
            var index = _random.Next(upstreams.Count);
            return upstreams[index];
        }

        /// <summary>
        /// Selects an upstream using IP hash strategy (sticky sessions).
        /// </summary>
        private string SelectIpHash(List<string> upstreams, string? clientIp)
        {
            if (string.IsNullOrEmpty(clientIp))
            {
                _logger.LogWarning("IP hash strategy requires client IP, falling back to round-robin");
                return SelectRoundRobin(upstreams);
            }

            // Hash the client IP
            var hash = ComputeHash(clientIp);
            var index = Math.Abs(hash) % upstreams.Count;
            return upstreams[index];
        }

        /// <summary>
        /// Computes a hash for a string.
        /// </summary>
        private int ComputeHash(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var hashBytes = MD5.HashData(bytes);
            return BitConverter.ToInt32(hashBytes, 0);
        }
    }

    /// <summary>
    /// Represents the state of an upstream server.
    /// </summary>
    public class UpstreamState
    {
        private int _activeConnections;
        private int _totalRequests;
        private int _successfulRequests;
        private int _failedRequests;
        private volatile bool _isHealthy = true;

        /// <summary>
        /// Gets the number of active connections.
        /// </summary>
        public int ActiveConnections => _activeConnections;

        /// <summary>
        /// Gets the total number of requests.
        /// </summary>
        public int TotalRequests => _totalRequests;

        /// <summary>
        /// Gets the number of successful requests.
        /// </summary>
        public int SuccessfulRequests => _successfulRequests;

        /// <summary>
        /// Gets the number of failed requests.
        /// </summary>
        public int FailedRequests => _failedRequests;

        /// <summary>
        /// Gets a value indicating whether the upstream is healthy.
        /// </summary>
        public bool IsHealthy => _isHealthy;

        /// <summary>
        /// Gets the success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate =>
            _totalRequests > 0 ? (double)_successfulRequests / _totalRequests : 1.0;

        /// <summary>
        /// Increments the active connection count.
        /// </summary>
        public void IncrementConnections()
        {
            System.Threading.Interlocked.Increment(ref _activeConnections);
            System.Threading.Interlocked.Increment(ref _totalRequests);
        }

        /// <summary>
        /// Decrements the active connection count.
        /// </summary>
        public void DecrementConnections()
        {
            System.Threading.Interlocked.Decrement(ref _activeConnections);
        }

        /// <summary>
        /// Records a successful request.
        /// </summary>
        public void RecordSuccess()
        {
            System.Threading.Interlocked.Increment(ref _successfulRequests);
        }

        /// <summary>
        /// Records a failed request.
        /// </summary>
        public void RecordFailure()
        {
            System.Threading.Interlocked.Increment(ref _failedRequests);
        }

        /// <summary>
        /// Marks the upstream as unhealthy.
        /// </summary>
        public void MarkUnhealthy()
        {
            _isHealthy = false;
        }

        /// <summary>
        /// Marks the upstream as healthy.
        /// </summary>
        public void MarkHealthy()
        {
            _isHealthy = true;
        }
    }
}
