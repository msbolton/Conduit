using System;
using System.Text;
using Apache.NMS;
using Conduit.Transports.Core;
using NmsPrimitiveMap = Apache.NMS.IPrimitiveMap;

namespace Conduit.Transports.ActiveMq
{
    /// <summary>
    /// Converts between Conduit TransportMessage and NMS IMessage.
    /// </summary>
    public class ActiveMqMessageConverter
    {
        /// <summary>
        /// Converts a TransportMessage to an NMS message.
        /// </summary>
        /// <param name="transportMessage">The transport message</param>
        /// <param name="session">The NMS session</param>
        /// <returns>An NMS message</returns>
        public IMessage ToNmsMessage(TransportMessage transportMessage, ISession session)
        {
            if (transportMessage == null)
                throw new ArgumentNullException(nameof(transportMessage));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            // Create bytes message with payload
            var message = session.CreateBytesMessage(transportMessage.Payload ?? Array.Empty<byte>());

            // Set standard properties
            message.NMSMessageId = transportMessage.MessageId;
            message.NMSCorrelationID = transportMessage.CorrelationId;
            message.NMSTimestamp = transportMessage.Timestamp.UtcDateTime;
            message.NMSPriority = MapPriority(transportMessage.Priority);
            message.NMSDeliveryMode = transportMessage.Persistent ? MsgDeliveryMode.Persistent : MsgDeliveryMode.NonPersistent;

            if (transportMessage.Expiration.HasValue)
            {
                var timeToLive = transportMessage.Expiration.Value - DateTimeOffset.UtcNow;
                if (timeToLive.TotalMilliseconds > 0)
                {
                    message.NMSTimeToLive = TimeSpan.FromMilliseconds(timeToLive.TotalMilliseconds);
                }
            }

            if (!string.IsNullOrEmpty(transportMessage.ReplyTo))
            {
                message.NMSReplyTo = session.GetQueue(transportMessage.ReplyTo);
            }

            // Set custom properties
            message.Properties.SetString("ContentType", transportMessage.ContentType);

            if (!string.IsNullOrEmpty(transportMessage.ContentEncoding))
            {
                message.Properties.SetString("ContentEncoding", transportMessage.ContentEncoding);
            }

            if (!string.IsNullOrEmpty(transportMessage.MessageType))
            {
                message.Properties.SetString("MessageType", transportMessage.MessageType);
            }

            if (!string.IsNullOrEmpty(transportMessage.CausationId))
            {
                message.Properties.SetString("CausationId", transportMessage.CausationId);
            }

            message.Properties.SetInt("DeliveryAttempts", transportMessage.DeliveryAttempts);

            // Set headers as properties
            foreach (var header in transportMessage.Headers)
            {
                SetProperty(message.Properties, $"Header_{header.Key}", header.Value);
            }

            // Set transport properties
            foreach (var prop in transportMessage.TransportProperties)
            {
                SetProperty(message.Properties, $"Transport_{prop.Key}", prop.Value);
            }

            return message;
        }

        /// <summary>
        /// Converts an NMS message to a TransportMessage.
        /// </summary>
        /// <param name="nmsMessage">The NMS message</param>
        /// <returns>A transport message</returns>
        public TransportMessage FromNmsMessage(IMessage nmsMessage)
        {
            if (nmsMessage == null)
                throw new ArgumentNullException(nameof(nmsMessage));

            var transportMessage = new TransportMessage
            {
                MessageId = nmsMessage.NMSMessageId ?? Guid.NewGuid().ToString(),
                CorrelationId = nmsMessage.NMSCorrelationID,
                Timestamp = new DateTimeOffset(nmsMessage.NMSTimestamp, TimeSpan.Zero),
                Priority = MapPriority(nmsMessage.NMSPriority),
                Persistent = nmsMessage.NMSDeliveryMode == MsgDeliveryMode.Persistent
            };

            // Extract payload
            if (nmsMessage is IBytesMessage bytesMessage)
            {
                transportMessage.Payload = bytesMessage.Content;
            }
            else if (nmsMessage is ITextMessage textMessage)
            {
                transportMessage.Payload = Encoding.UTF8.GetBytes(textMessage.Text ?? string.Empty);
            }

            // Set reply-to
            if (nmsMessage.NMSReplyTo != null)
            {
                transportMessage.ReplyTo = GetDestinationName(nmsMessage.NMSReplyTo);
            }

            // Set destination
            if (nmsMessage.NMSDestination != null)
            {
                transportMessage.Destination = GetDestinationName(nmsMessage.NMSDestination);
            }

            // Extract expiration
            if (nmsMessage.NMSTimeToLive.TotalMilliseconds > 0)
            {
                transportMessage.Expiration = DateTimeOffset.UtcNow.Add(nmsMessage.NMSTimeToLive);
            }

            // Extract custom properties
            var properties = nmsMessage.Properties;

            transportMessage.ContentType = properties.GetString("ContentType") ?? "application/octet-stream";
            transportMessage.ContentEncoding = properties.GetString("ContentEncoding");
            transportMessage.MessageType = properties.GetString("MessageType");
            transportMessage.CausationId = properties.GetString("CausationId");

            if (properties.Contains("DeliveryAttempts"))
            {
                transportMessage.DeliveryAttempts = properties.GetInt("DeliveryAttempts");
            }

            // Extract headers (properties starting with "Header_")
            foreach (var key in properties.Keys)
            {
                var keyStr = key.ToString() ?? string.Empty;
                if (keyStr.StartsWith("Header_"))
                {
                    var headerKey = keyStr.Substring(7); // Remove "Header_" prefix
                    transportMessage.Headers[headerKey] = properties[keyStr];
                }
                else if (keyStr.StartsWith("Transport_"))
                {
                    var propKey = keyStr.Substring(10); // Remove "Transport_" prefix
                    transportMessage.TransportProperties[propKey] = properties[keyStr];
                }
            }

            // Increment delivery attempts for redelivered messages
            if (nmsMessage.NMSRedelivered)
            {
                transportMessage.DeliveryAttempts++;
            }

            return transportMessage;
        }

        private void SetProperty(NmsPrimitiveMap properties, string key, object value)
        {
            switch (value)
            {
                case string s:
                    properties.SetString(key, s);
                    break;
                case int i:
                    properties.SetInt(key, i);
                    break;
                case long l:
                    properties.SetLong(key, l);
                    break;
                case bool b:
                    properties.SetBool(key, b);
                    break;
                case double d:
                    properties.SetDouble(key, d);
                    break;
                case float f:
                    properties.SetFloat(key, f);
                    break;
                case byte by:
                    properties.SetByte(key, by);
                    break;
                case short sh:
                    properties.SetShort(key, sh);
                    break;
                default:
                    // Convert to string for unsupported types
                    properties.SetString(key, value.ToString() ?? string.Empty);
                    break;
            }
        }

        private MsgPriority MapPriority(int priority)
        {
            // Conduit uses 0-10, NMS uses enum
            return priority switch
            {
                <= 1 => MsgPriority.Lowest,
                2 or 3 => MsgPriority.VeryLow,
                4 => MsgPriority.Low,
                5 or 6 => MsgPriority.Normal,
                7 or 8 => MsgPriority.High,
                9 => MsgPriority.VeryHigh,
                _ => MsgPriority.Highest
            };
        }

        private int MapPriority(MsgPriority priority)
        {
            return priority switch
            {
                MsgPriority.Lowest => 0,
                MsgPriority.VeryLow => 2,
                MsgPriority.Low => 4,
                MsgPriority.Normal => 5,
                MsgPriority.High => 7,
                MsgPriority.VeryHigh => 9,
                MsgPriority.Highest => 10,
                _ => 5
            };
        }

        private string GetDestinationName(IDestination destination)
        {
            if (destination is IQueue queue)
            {
                return $"queue://{queue.QueueName}";
            }
            else if (destination is ITopic topic)
            {
                return $"topic://{topic.TopicName}";
            }
            else if (destination is ITemporaryQueue tempQueue)
            {
                return $"temp-queue://{tempQueue.QueueName}";
            }
            else if (destination is ITemporaryTopic tempTopic)
            {
                return $"temp-topic://{tempTopic.TopicName}";
            }

            return destination.ToString() ?? string.Empty;
        }
    }
}
