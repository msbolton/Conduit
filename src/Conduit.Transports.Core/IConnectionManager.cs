using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Interface for managing transport connections with pooling support.
    /// </summary>
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// Gets a connection to the specified destination.
        /// Creates a new connection if none are available in the pool.
        /// </summary>
        /// <param name="destination">The destination address</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A connection to the destination</returns>
        Task<ITransportConnection> GetConnectionAsync(string destination, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a connection to the pool for reuse.
        /// </summary>
        /// <param name="connection">The connection to return</param>
        Task ReturnConnectionAsync(ITransportConnection connection);

        /// <summary>
        /// Removes a connection from the pool.
        /// Used when a connection becomes invalid or fails.
        /// </summary>
        /// <param name="connection">The connection to remove</param>
        Task RemoveConnectionAsync(ITransportConnection connection);

        /// <summary>
        /// Gets all active connections.
        /// </summary>
        /// <returns>List of active connections</returns>
        IEnumerable<ITransportConnection> GetActiveConnections();

        /// <summary>
        /// Gets all available (idle) connections.
        /// </summary>
        /// <returns>List of available connections</returns>
        IEnumerable<ITransportConnection> GetAvailableConnections();

        /// <summary>
        /// Gets the total number of connections in the pool.
        /// </summary>
        int TotalConnections { get; }

        /// <summary>
        /// Gets the number of active (in-use) connections.
        /// </summary>
        int ActiveConnections { get; }

        /// <summary>
        /// Gets the number of available (idle) connections.
        /// </summary>
        int AvailableConnections { get; }

        /// <summary>
        /// Closes all connections and shuts down the manager.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets connection pool statistics.
        /// </summary>
        /// <returns>The pool statistics</returns>
        ConnectionPoolStatistics GetStatistics();
    }

    /// <summary>
    /// Represents a transport connection.
    /// </summary>
    public interface ITransportConnection : IDisposable
    {
        /// <summary>
        /// Gets the connection identifier.
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Gets the destination address.
        /// </summary>
        string Destination { get; }

        /// <summary>
        /// Gets whether the connection is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets the connection creation time.
        /// </summary>
        DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the last used timestamp.
        /// </summary>
        DateTimeOffset LastUsedAt { get; }

        /// <summary>
        /// Gets the number of times this connection has been used.
        /// </summary>
        long UseCount { get; }

        /// <summary>
        /// Opens the connection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task OpenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CloseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the connection is still valid/healthy.
        /// </summary>
        /// <returns>True if the connection is valid</returns>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Resets the connection to a clean state.
        /// </summary>
        Task ResetAsync();
    }

    /// <summary>
    /// Connection pool statistics.
    /// </summary>
    public class ConnectionPoolStatistics
    {
        /// <summary>
        /// Gets or sets the total number of connections.
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of available connections.
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// Gets or sets the total number of connections created.
        /// </summary>
        public long ConnectionsCreated { get; set; }

        /// <summary>
        /// Gets or sets the total number of connections destroyed.
        /// </summary>
        public long ConnectionsDestroyed { get; set; }

        /// <summary>
        /// Gets or sets the total number of connection requests.
        /// </summary>
        public long ConnectionRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of times a connection was reused from the pool.
        /// </summary>
        public long ConnectionReuses { get; set; }

        /// <summary>
        /// Gets or sets the average connection age in seconds.
        /// </summary>
        public double AverageConnectionAgeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the connection pool utilization rate (0.0 to 1.0).
        /// </summary>
        public double UtilizationRate { get; set; }

        /// <summary>
        /// Gets or sets the average wait time for a connection in milliseconds.
        /// </summary>
        public double AverageWaitTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when statistics started being collected.
        /// </summary>
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Calculates the connection reuse rate.
        /// </summary>
        public double ReuseRate
        {
            get
            {
                return ConnectionRequests > 0
                    ? (double)ConnectionReuses / ConnectionRequests
                    : 0.0;
            }
        }

        /// <summary>
        /// Calculates the pool efficiency (reuse rate).
        /// </summary>
        public double Efficiency => ReuseRate * 100.0;
    }
}
