using System;
using System.Collections.Generic;
using Conduit.Api;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Wrapper for messages being transported across the network.
    /// Contains the message payload and transport-specific metadata.
    /// </summary>
    public class TransportMessage
    {
        /// <summary>
        /// Gets or sets the message identifier.
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the correlation identifier for request/response patterns.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the causation identifier (message that caused this one).
        /// </summary>
        public string? CausationId { get; set; }

        /// <summary>
        /// Gets or sets the message payload (serialized message).
        /// </summary>
        public byte[]? Payload { get; set; }

        /// <summary>
        /// Gets or sets the content type of the payload.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the content encoding (e.g., "utf-8", "gzip").
        /// </summary>
        public string? ContentEncoding { get; set; }

        /// <summary>
        /// Gets or sets the message type name.
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// Gets or sets the source address/queue/topic.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the destination address/queue/topic.
        /// </summary>
        public string? Destination { get; set; }

        /// <summary>
        /// Gets or sets the reply-to address for responses.
        /// </summary>
        public string? ReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the message expiration time (TTL).
        /// </summary>
        public DateTimeOffset? Expiration { get; set; }

        /// <summary>
        /// Gets or sets the message priority (0 = lowest, 10 = highest).
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether the message should be persisted (durable).
        /// </summary>
        public bool Persistent { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of delivery attempts.
        /// </summary>
        public int DeliveryAttempts { get; set; }

        /// <summary>
        /// Gets or sets custom headers/properties.
        /// </summary>
        public Dictionary<string, object> Headers { get; set; } = new();

        /// <summary>
        /// Gets or sets transport-specific properties.
        /// </summary>
        public Dictionary<string, object> TransportProperties { get; set; } = new();

        /// <summary>
        /// Checks if the message has expired.
        /// </summary>
        public bool IsExpired => Expiration.HasValue && DateTimeOffset.UtcNow > Expiration.Value;

        /// <summary>
        /// Creates a TransportMessage from an IMessage.
        /// </summary>
        /// <param name="message">The message to wrap</param>
        /// <param name="payload">The serialized payload</param>
        /// <param name="contentType">The content type</param>
        /// <returns>A new transport message</returns>
        public static TransportMessage FromMessage(IMessage message, byte[] payload, string contentType = "application/json")
        {
            var transportMessage = new TransportMessage
            {
                MessageId = message.Id,
                CorrelationId = message.CorrelationId,
                Payload = payload,
                ContentType = contentType,
                MessageType = message.GetType().FullName,
                Timestamp = message.Timestamp,
                Priority = 5
            };

            // Copy headers
            foreach (var header in message.Headers)
            {
                transportMessage.Headers[header.Key] = header.Value;
            }

            return transportMessage;
        }

        /// <summary>
        /// Gets a header value.
        /// </summary>
        /// <typeparam name="T">The header value type</typeparam>
        /// <param name="key">The header key</param>
        /// <returns>The header value, or default if not found</returns>
        public T? GetHeader<T>(string key)
        {
            if (Headers.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a header value.
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value</param>
        public void SetHeader(string key, object value)
        {
            Headers[key] = value;
        }

        /// <summary>
        /// Gets a transport property value.
        /// </summary>
        /// <typeparam name="T">The property value type</typeparam>
        /// <param name="key">The property key</param>
        /// <returns>The property value, or default if not found</returns>
        public T? GetTransportProperty<T>(string key)
        {
            if (TransportProperties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Sets a transport property value.
        /// </summary>
        /// <param name="key">The property key</param>
        /// <param name="value">The property value</param>
        public void SetTransportProperty(string key, object value)
        {
            TransportProperties[key] = value;
        }

        /// <summary>
        /// Creates a clone of this transport message.
        /// </summary>
        /// <returns>A new transport message with copied values</returns>
        public TransportMessage Clone()
        {
            return new TransportMessage
            {
                MessageId = MessageId,
                CorrelationId = CorrelationId,
                CausationId = CausationId,
                Payload = Payload?.ToArray(),
                ContentType = ContentType,
                ContentEncoding = ContentEncoding,
                MessageType = MessageType,
                Source = Source,
                Destination = Destination,
                ReplyTo = ReplyTo,
                Timestamp = Timestamp,
                Expiration = Expiration,
                Priority = Priority,
                Persistent = Persistent,
                DeliveryAttempts = DeliveryAttempts,
                Headers = new Dictionary<string, object>(Headers),
                TransportProperties = new Dictionary<string, object>(TransportProperties)
            };
        }
    }
}
