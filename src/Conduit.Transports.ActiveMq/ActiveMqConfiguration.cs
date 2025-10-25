using System;
using Conduit.Transports.Core;

namespace Conduit.Transports.ActiveMq
{
    /// <summary>
    /// Configuration for ActiveMQ Artemis transport.
    /// </summary>
    public class ActiveMqConfiguration : TransportConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the ActiveMqConfiguration class.
        /// </summary>
        public ActiveMqConfiguration()
        {
            Type = TransportType.Amqp;
            Name = "ActiveMQ";
        }

        /// <summary>
        /// Gets or sets the broker URI (e.g., "amqp://localhost:5672").
        /// </summary>
        public string BrokerUri { get; set; } = "amqp://localhost:5672";

        /// <summary>
        /// Gets or sets the username for authentication.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the password for authentication.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the client ID for durable subscriptions.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets whether to use asynchronous send.
        /// </summary>
        public bool AsyncSend { get; set; } = true;

        /// <summary>
        /// Gets or sets the send timeout in milliseconds.
        /// </summary>
        public int SendTimeout { get; set; } = 30000; // 30 seconds

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int RequestTimeout { get; set; } = 60000; // 60 seconds

        /// <summary>
        /// Gets or sets the close timeout in milliseconds.
        /// </summary>
        public int CloseTimeout { get; set; } = 15000; // 15 seconds

        /// <summary>
        /// Gets or sets the acknowledgement mode.
        /// </summary>
        public AcknowledgementMode AcknowledgementMode { get; set; } = AcknowledgementMode.AutoAcknowledge;

        /// <summary>
        /// Gets or sets the message priority.
        /// </summary>
        public byte DefaultMessagePriority { get; set; } = 4; // Normal priority (0-9)

        /// <summary>
        /// Gets or sets the default time-to-live for messages in milliseconds.
        /// </summary>
        public long DefaultTimeToLive { get; set; } = 0; // 0 = no expiration

        /// <summary>
        /// Gets or sets whether messages are persistent by default.
        /// </summary>
        public bool PersistentDelivery { get; set; } = true;

        /// <summary>
        /// Gets or sets the prefetch policy (number of messages to prefetch).
        /// </summary>
        public int PrefetchPolicy { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to use compression.
        /// </summary>
        public bool UseCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of redelivery attempts.
        /// </summary>
        public int MaxRedeliveryAttempts { get; set; } = 6;

        /// <summary>
        /// Gets or sets the redelivery delay in milliseconds.
        /// </summary>
        public long RedeliveryDelay { get; set; } = 1000; // 1 second

        /// <summary>
        /// Gets or sets whether to use exponential backoff for redelivery.
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Gets or sets the backoff multiplier for exponential redelivery.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the maximum redelivery delay in milliseconds.
        /// </summary>
        public long MaxRedeliveryDelay { get; set; } = 60000; // 60 seconds

        /// <summary>
        /// Gets or sets whether to create temporary queues.
        /// </summary>
        public bool AllowTemporaryQueues { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to create temporary topics.
        /// </summary>
        public bool AllowTemporaryTopics { get; set; } = true;

        /// <summary>
        /// Creates a connection URI string from the configuration.
        /// </summary>
        /// <returns>The connection URI</returns>
        public string BuildConnectionUri()
        {
            var uri = BrokerUri;

            // Add query parameters for additional settings
            var separator = uri.Contains('?') ? '&' : '?';

            if (AsyncSend)
            {
                uri += $"{separator}nms.AsyncSend=true";
                separator = '&';
            }

            if (SendTimeout > 0)
            {
                uri += $"{separator}nms.SendTimeout={SendTimeout}";
                separator = '&';
            }

            if (RequestTimeout > 0)
            {
                uri += $"{separator}nms.RequestTimeout={RequestTimeout}";
                separator = '&';
            }

            if (PrefetchPolicy > 0)
            {
                uri += $"{separator}nms.PrefetchPolicy.All={PrefetchPolicy}";
                separator = '&';
            }

            return uri;
        }
    }

    /// <summary>
    /// Acknowledgement modes for message consumption.
    /// </summary>
    public enum AcknowledgementMode
    {
        /// <summary>
        /// Messages are automatically acknowledged when received.
        /// </summary>
        AutoAcknowledge = 1,

        /// <summary>
        /// Client explicitly acknowledges messages.
        /// </summary>
        ClientAcknowledge = 2,

        /// <summary>
        /// Messages are acknowledged in batches (duplicates allowed).
        /// </summary>
        DupsOkAcknowledge = 3,

        /// <summary>
        /// Transactional acknowledgement.
        /// </summary>
        Transactional = 0,

        /// <summary>
        /// Individual acknowledgement (ActiveMQ extension).
        /// </summary>
        IndividualAcknowledge = 4
    }
}
