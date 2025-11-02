using System;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Transport statistics and metrics.
    /// </summary>
    public class TransportStatistics
    {
        /// <summary>
        /// Gets or sets the total number of messages sent.
        /// </summary>
        public long MessagesSent { get; set; }

        /// <summary>
        /// Gets or sets the total number of messages received.
        /// </summary>
        public long MessagesReceived { get; set; }

        /// <summary>
        /// Gets or sets the total bytes sent.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// Gets or sets the total bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Gets or sets the number of send failures.
        /// </summary>
        public long SendFailures { get; set; }

        /// <summary>
        /// Gets or sets the number of receive failures.
        /// </summary>
        public long ReceiveFailures { get; set; }

        /// <summary>
        /// Gets or sets the number of connection attempts.
        /// </summary>
        public long ConnectionAttempts { get; set; }

        /// <summary>
        /// Gets or sets the number of successful connections.
        /// </summary>
        public long SuccessfulConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of connection failures.
        /// </summary>
        public long ConnectionFailures { get; set; }

        /// <summary>
        /// Gets or sets the number of disconnections.
        /// </summary>
        public long Disconnections { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of active subscriptions.
        /// </summary>
        public int ActiveSubscriptions { get; set; }

        /// <summary>
        /// Gets or sets the average message send time in milliseconds.
        /// </summary>
        public double AverageSendTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average message receive time in milliseconds.
        /// </summary>
        public double AverageReceiveTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when statistics started being collected.
        /// </summary>
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Calculates the send success rate.
        /// </summary>
        public double SendSuccessRate
        {
            get
            {
                var total = MessagesSent + SendFailures;
                return total > 0 ? (double)MessagesSent / total : 0.0;
            }
        }

        /// <summary>
        /// Calculates the receive success rate.
        /// </summary>
        public double ReceiveSuccessRate
        {
            get
            {
                var total = MessagesReceived + ReceiveFailures;
                return total > 0 ? (double)MessagesReceived / total : 0.0;
            }
        }

        /// <summary>
        /// Calculates the connection success rate.
        /// </summary>
        public double ConnectionSuccessRate
        {
            get
            {
                return ConnectionAttempts > 0
                    ? (double)SuccessfulConnections / ConnectionAttempts
                    : 0.0;
            }
        }

        /// <summary>
        /// Calculates the uptime duration.
        /// </summary>
        public TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;

        /// <summary>
        /// Calculates messages per second throughput.
        /// </summary>
        public double MessagesPerSecond
        {
            get
            {
                var uptimeSeconds = Uptime.TotalSeconds;
                return uptimeSeconds > 0 ? (MessagesSent + MessagesReceived) / uptimeSeconds : 0.0;
            }
        }

        /// <summary>
        /// Calculates bytes per second throughput.
        /// </summary>
        public double BytesPerSecond
        {
            get
            {
                var uptimeSeconds = Uptime.TotalSeconds;
                return uptimeSeconds > 0 ? (BytesSent + BytesReceived) / uptimeSeconds : 0.0;
            }
        }

        /// <summary>
        /// Resets all statistics to zero.
        /// </summary>
        public void Reset()
        {
            MessagesSent = 0;
            MessagesReceived = 0;
            BytesSent = 0;
            BytesReceived = 0;
            SendFailures = 0;
            ReceiveFailures = 0;
            ConnectionAttempts = 0;
            SuccessfulConnections = 0;
            ConnectionFailures = 0;
            Disconnections = 0;
            ActiveConnections = 0;
            ActiveSubscriptions = 0;
            AverageSendTimeMs = 0;
            AverageReceiveTimeMs = 0;
            StartTime = DateTimeOffset.UtcNow;
            LastUpdated = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a snapshot of the current statistics.
        /// </summary>
        /// <returns>A new statistics object with copied values</returns>
        public TransportStatistics Snapshot()
        {
            return new TransportStatistics
            {
                MessagesSent = MessagesSent,
                MessagesReceived = MessagesReceived,
                BytesSent = BytesSent,
                BytesReceived = BytesReceived,
                SendFailures = SendFailures,
                ReceiveFailures = ReceiveFailures,
                ConnectionAttempts = ConnectionAttempts,
                SuccessfulConnections = SuccessfulConnections,
                ConnectionFailures = ConnectionFailures,
                Disconnections = Disconnections,
                ActiveConnections = ActiveConnections,
                ActiveSubscriptions = ActiveSubscriptions,
                AverageSendTimeMs = AverageSendTimeMs,
                AverageReceiveTimeMs = AverageReceiveTimeMs,
                StartTime = StartTime,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }
    }
}
