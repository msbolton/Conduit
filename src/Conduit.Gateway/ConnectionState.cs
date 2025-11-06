using System;
using System.Net.Sockets;
using Conduit.Transports.Core;

namespace Conduit.Gateway
{
    /// <summary>
    /// Represents the state of an active network connection.
    /// </summary>
    public class ConnectionState
    {
        /// <summary>
        /// Gets or sets the unique connection identifier.
        /// </summary>
        public string ConnectionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the connection information.
        /// </summary>
        public ConnectionInfo ConnectionInfo { get; set; } = new();

        /// <summary>
        /// Gets or sets the current status of the connection.
        /// </summary>
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Connecting;

        /// <summary>
        /// Gets or sets the transport assigned to handle this connection.
        /// </summary>
        public ITransport? AssignedTransport { get; set; }

        /// <summary>
        /// Gets or sets the route entry that matched this connection.
        /// </summary>
        public RouteEntry? MatchedRoute { get; set; }

        /// <summary>
        /// Gets or sets when the connection was established.
        /// </summary>
        public DateTime EstablishedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when the connection was last active.
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the total number of bytes sent.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// Gets or sets the total number of bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Gets or sets the number of messages sent.
        /// </summary>
        public long MessagesSent { get; set; }

        /// <summary>
        /// Gets or sets the number of messages received.
        /// </summary>
        public long MessagesReceived { get; set; }

        /// <summary>
        /// Gets or sets the underlying socket (if applicable).
        /// </summary>
        public Socket? Socket { get; set; }

        /// <summary>
        /// Gets or sets custom metadata for this connection.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets the connection duration.
        /// </summary>
        public TimeSpan Duration => DateTime.UtcNow - EstablishedTime;

        /// <summary>
        /// Gets the time since last activity.
        /// </summary>
        public TimeSpan IdleTime => DateTime.UtcNow - LastActivity;

        /// <summary>
        /// Updates the activity timestamp.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Records bytes sent.
        /// </summary>
        /// <param name="bytes">Number of bytes sent</param>
        public void RecordBytesSent(long bytes)
        {
            BytesSent += bytes;
            UpdateActivity();
        }

        /// <summary>
        /// Records bytes received.
        /// </summary>
        /// <param name="bytes">Number of bytes received</param>
        public void RecordBytesReceived(long bytes)
        {
            BytesReceived += bytes;
            UpdateActivity();
        }

        /// <summary>
        /// Records a message sent.
        /// </summary>
        public void RecordMessageSent()
        {
            MessagesSent++;
            UpdateActivity();
        }

        /// <summary>
        /// Records a message received.
        /// </summary>
        public void RecordMessageReceived()
        {
            MessagesReceived++;
            UpdateActivity();
        }

        /// <summary>
        /// Returns a string representation of the connection state.
        /// </summary>
        public override string ToString()
        {
            return $"{ConnectionId}: {Status} {ConnectionInfo} [{Duration:hh\\:mm\\:ss}]";
        }
    }

    /// <summary>
    /// Status of a network connection.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Connection is being established.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connection is active and established.
        /// </summary>
        Connected,

        /// <summary>
        /// Connection is idle (no recent activity).
        /// </summary>
        Idle,

        /// <summary>
        /// Connection is being closed.
        /// </summary>
        Closing,

        /// <summary>
        /// Connection has been closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Connection failed or encountered an error.
        /// </summary>
        Failed
    }
}