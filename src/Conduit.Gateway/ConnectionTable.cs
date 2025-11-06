using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Manages active network connections for the gateway.
    /// Provides connection tracking, statistics, and lifecycle management.
    /// </summary>
    public class ConnectionTable : IDisposable
    {
        private readonly ConcurrentDictionary<string, ConnectionState> _connections;
        private readonly ConcurrentDictionary<string, List<string>> _connectionsByEndpoint;
        private readonly ILogger<ConnectionTable>? _logger;
        private readonly Timer? _cleanupTimer;
        private readonly TimeSpan _idleTimeout;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ConnectionTable class.
        /// </summary>
        /// <param name="idleTimeout">Timeout for idle connections</param>
        /// <param name="logger">Optional logger instance</param>
        public ConnectionTable(TimeSpan idleTimeout = default, ILogger<ConnectionTable>? logger = null)
        {
            _connections = new ConcurrentDictionary<string, ConnectionState>();
            _connectionsByEndpoint = new ConcurrentDictionary<string, List<string>>();
            _logger = logger;
            _idleTimeout = idleTimeout == default ? TimeSpan.FromMinutes(30) : idleTimeout;

            // Start cleanup timer (run every 5 minutes)
            _cleanupTimer = new Timer(CleanupIdleConnections, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Gets the total number of active connections.
        /// </summary>
        public int ActiveConnectionCount => _connections.Count;

        /// <summary>
        /// Adds a new connection to the table.
        /// </summary>
        /// <param name="connectionState">The connection state to add</param>
        /// <returns>True if added successfully, false if connection ID already exists</returns>
        public bool AddConnection(ConnectionState connectionState)
        {
            if (connectionState == null)
                throw new ArgumentNullException(nameof(connectionState));

            if (_disposed)
                return false;

            if (_connections.TryAdd(connectionState.ConnectionId, connectionState))
            {
                // Index by endpoint for quick lookup
                var endpointKey = GetEndpointKey(connectionState.ConnectionInfo);
                if (!string.IsNullOrEmpty(endpointKey))
                {
                    _connectionsByEndpoint.AddOrUpdate(
                        endpointKey,
                        new List<string> { connectionState.ConnectionId },
                        (key, existing) =>
                        {
                            lock (existing)
                            {
                                existing.Add(connectionState.ConnectionId);
                                return existing;
                            }
                        });
                }

                _logger?.LogDebug("Added connection {ConnectionId}: {Connection}",
                    connectionState.ConnectionId, connectionState.ConnectionInfo);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a connection by its ID.
        /// </summary>
        /// <param name="connectionId">The connection ID</param>
        /// <returns>The connection state if found, null otherwise</returns>
        public ConnectionState? GetConnection(string connectionId)
        {
            return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
        }

        /// <summary>
        /// Updates an existing connection.
        /// </summary>
        /// <param name="connectionState">The updated connection state</param>
        /// <returns>True if updated successfully, false if connection not found</returns>
        public bool UpdateConnection(ConnectionState connectionState)
        {
            if (connectionState == null)
                throw new ArgumentNullException(nameof(connectionState));

            if (_disposed)
                return false;

            return _connections.TryUpdate(connectionState.ConnectionId, connectionState,
                _connections.GetValueOrDefault(connectionState.ConnectionId) ?? connectionState);
        }

        /// <summary>
        /// Removes a connection from the table.
        /// </summary>
        /// <param name="connectionId">The connection ID to remove</param>
        /// <returns>The removed connection state, or null if not found</returns>
        public ConnectionState? RemoveConnection(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId) || _disposed)
                return null;

            if (_connections.TryRemove(connectionId, out var connection))
            {
                // Remove from endpoint index
                var endpointKey = GetEndpointKey(connection.ConnectionInfo);
                if (!string.IsNullOrEmpty(endpointKey) &&
                    _connectionsByEndpoint.TryGetValue(endpointKey, out var connectionIds))
                {
                    lock (connectionIds)
                    {
                        connectionIds.Remove(connectionId);
                        if (connectionIds.Count == 0)
                        {
                            _connectionsByEndpoint.TryRemove(endpointKey, out _);
                        }
                    }
                }

                _logger?.LogDebug("Removed connection {ConnectionId}: {Connection}",
                    connectionId, connection.ConnectionInfo);

                return connection;
            }

            return null;
        }

        /// <summary>
        /// Gets all connections, optionally filtered by status.
        /// </summary>
        /// <param name="status">Optional status filter</param>
        /// <returns>List of connections</returns>
        public List<ConnectionState> GetConnections(ConnectionStatus? status = null)
        {
            var connections = _connections.Values.ToList();

            if (status.HasValue)
            {
                connections = connections.Where(c => c.Status == status.Value).ToList();
            }

            return connections;
        }

        /// <summary>
        /// Gets connections by endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint to search for</param>
        /// <param name="includeSource">Whether to match source endpoints</param>
        /// <param name="includeDestination">Whether to match destination endpoints</param>
        /// <returns>List of connections for the endpoint</returns>
        public List<ConnectionState> GetConnectionsByEndpoint(IPEndPoint endpoint,
            bool includeSource = true, bool includeDestination = true)
        {
            var result = new List<ConnectionState>();
            var endpointKey = $"{endpoint.Address}:{endpoint.Port}";

            if (_connectionsByEndpoint.TryGetValue(endpointKey, out var connectionIds))
            {
                lock (connectionIds)
                {
                    foreach (var connectionId in connectionIds)
                    {
                        if (_connections.TryGetValue(connectionId, out var connection))
                        {
                            bool matches = false;

                            if (includeSource && connection.ConnectionInfo.SourceEndpoint?.Equals(endpoint) == true)
                                matches = true;

                            if (includeDestination && connection.ConnectionInfo.DestinationEndpoint?.Equals(endpoint) == true)
                                matches = true;

                            if (matches)
                                result.Add(connection);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Closes all connections matching the specified criteria.
        /// </summary>
        /// <param name="predicate">Optional predicate to filter connections</param>
        /// <returns>Number of connections closed</returns>
        public Task<int> CloseConnectionsAsync(Func<ConnectionState, bool>? predicate = null)
        {
            var connectionsToClose = _connections.Values
                .Where(c => predicate?.Invoke(c) ?? true)
                .ToList();

            int closedCount = 0;

            foreach (var connection in connectionsToClose)
            {
                try
                {
                    connection.Status = ConnectionStatus.Closing;
                    connection.Socket?.Close();
                    RemoveConnection(connection.ConnectionId);
                    closedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error closing connection {ConnectionId}", connection.ConnectionId);
                }
            }

            _logger?.LogInformation("Closed {Count} connections", closedCount);
            return Task.FromResult(closedCount);
        }

        /// <summary>
        /// Gets statistics about the connection table.
        /// </summary>
        /// <returns>Connection table statistics</returns>
        public ConnectionTableStatistics GetStatistics()
        {
            var connections = _connections.Values.ToList();

            var stats = new ConnectionTableStatistics
            {
                TotalConnections = connections.Count,
                ConnectionsByStatus = connections.GroupBy(c => c.Status)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TotalBytesTransferred = connections.Sum(c => c.BytesSent + c.BytesReceived),
                TotalMessagesTransferred = connections.Sum(c => c.MessagesSent + c.MessagesReceived),
                AverageConnectionDuration = connections.Count > 0
                    ? TimeSpan.FromTicks((long)connections.Average(c => c.Duration.Ticks))
                    : TimeSpan.Zero,
                OldestConnection = connections.OrderBy(c => c.EstablishedTime).FirstOrDefault()?.EstablishedTime,
                NewestConnection = connections.OrderByDescending(c => c.EstablishedTime).FirstOrDefault()?.EstablishedTime,
                ConnectionsByTransport = connections
                    .Where(c => c.AssignedTransport != null)
                    .GroupBy(c => c.AssignedTransport!.Type.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }

        /// <summary>
        /// Cleans up idle connections.
        /// </summary>
        private void CleanupIdleConnections(object? state)
        {
            if (_disposed)
                return;

            try
            {
                var cutoffTime = DateTime.UtcNow - _idleTimeout;
                var idleConnections = _connections.Values
                    .Where(c => c.LastActivity < cutoffTime && c.Status != ConnectionStatus.Closed)
                    .ToList();

                foreach (var connection in idleConnections)
                {
                    connection.Status = ConnectionStatus.Idle;
                    _logger?.LogDebug("Marking connection {ConnectionId} as idle", connection.ConnectionId);
                }

                // Optionally close very old idle connections
                var veryOldCutoff = DateTime.UtcNow - TimeSpan.FromHours(2);
                var veryOldConnections = _connections.Values
                    .Where(c => c.LastActivity < veryOldCutoff && c.Status == ConnectionStatus.Idle)
                    .ToList();

                foreach (var connection in veryOldConnections)
                {
                    try
                    {
                        connection.Socket?.Close();
                        RemoveConnection(connection.ConnectionId);
                        _logger?.LogDebug("Closed very old idle connection {ConnectionId}", connection.ConnectionId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error closing idle connection {ConnectionId}", connection.ConnectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during connection cleanup");
            }
        }

        /// <summary>
        /// Gets a key for indexing connections by endpoint.
        /// </summary>
        private string GetEndpointKey(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.SourceEndpoint != null)
                return $"{connectionInfo.SourceEndpoint.Address}:{connectionInfo.SourceEndpoint.Port}";

            if (connectionInfo.DestinationEndpoint != null)
                return $"{connectionInfo.DestinationEndpoint.Address}:{connectionInfo.DestinationEndpoint.Port}";

            return string.Empty;
        }

        /// <summary>
        /// Disposes the connection table.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cleanupTimer?.Dispose();

            // Close all connections
            _ = CloseConnectionsAsync().ConfigureAwait(false);

            _logger?.LogInformation("Connection table disposed");
        }
    }

    /// <summary>
    /// Statistics about the connection table.
    /// </summary>
    public class ConnectionTableStatistics
    {
        /// <summary>
        /// Gets or sets the total number of connections.
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of connections by status.
        /// </summary>
        public Dictionary<ConnectionStatus, int> ConnectionsByStatus { get; set; } = new();

        /// <summary>
        /// Gets or sets the total bytes transferred across all connections.
        /// </summary>
        public long TotalBytesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the total messages transferred across all connections.
        /// </summary>
        public long TotalMessagesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the average connection duration.
        /// </summary>
        public TimeSpan AverageConnectionDuration { get; set; }

        /// <summary>
        /// Gets or sets when the oldest connection was established.
        /// </summary>
        public DateTime? OldestConnection { get; set; }

        /// <summary>
        /// Gets or sets when the newest connection was established.
        /// </summary>
        public DateTime? NewestConnection { get; set; }

        /// <summary>
        /// Gets or sets the number of connections by transport type.
        /// </summary>
        public Dictionary<string, int> ConnectionsByTransport { get; set; } = new();
    }
}