using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Messaging
{
    /// <summary>
    /// Context for message processing, providing metadata and tracking information.
    /// </summary>
    public class MessageContext
    {
        private readonly ConcurrentDictionary<string, object> _items;

        /// <summary>
        /// Gets the message being processed.
        /// </summary>
        public IMessage Message { get; }

        /// <summary>
        /// Gets or sets the unique identifier for the message.
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for tracking related messages.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the causation ID indicating what caused this message.
        /// </summary>
        public string? CausationId { get; set; }

        /// <summary>
        /// Gets or sets the conversation ID for tracking message conversations.
        /// </summary>
        public string? ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the session ID for tracking user sessions.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when processing started.
        /// </summary>
        public DateTimeOffset? ProcessingStartedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when processing completed.
        /// </summary>
        public DateTimeOffset? ProcessingCompletedAt { get; set; }

        /// <summary>
        /// Gets the processing duration.
        /// </summary>
        public TimeSpan? ProcessingDuration =>
            ProcessingStartedAt.HasValue && ProcessingCompletedAt.HasValue
                ? ProcessingCompletedAt.Value - ProcessingStartedAt.Value
                : null;

        /// <summary>
        /// Gets or sets the message headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets the user claims principal.
        /// </summary>
        public ClaimsPrincipal? User { get; set; }

        /// <summary>
        /// Gets or sets the originating endpoint.
        /// </summary>
        public string? OriginatingEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the destination endpoint.
        /// </summary>
        public string? DestinationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the retry count.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum retries allowed.
        /// </summary>
        public int? MaxRetries { get; set; }

        /// <summary>
        /// Gets or sets whether this is a retry attempt.
        /// </summary>
        public bool IsRetry => RetryCount > 0;

        /// <summary>
        /// Gets or sets whether the message processing failed.
        /// </summary>
        public bool IsFaulted { get; set; }

        /// <summary>
        /// Gets or sets the exception if the message processing failed.
        /// </summary>
        public Exception? FaultException { get; set; }

        /// <summary>
        /// Gets or sets the fault reason if the message processing failed.
        /// </summary>
        public string? FaultReason { get; set; }

        /// <summary>
        /// Gets or sets whether the message has been acknowledged.
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// Gets or sets the message priority.
        /// </summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>
        /// Gets or sets the message expiration time.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// Gets whether the message has expired.
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// Gets or sets the message delivery count.
        /// </summary>
        public int DeliveryCount { get; set; }

        /// <summary>
        /// Gets or sets custom items associated with the context.
        /// </summary>
        public ConcurrentDictionary<string, object> Items => _items;

        /// <summary>
        /// Gets or sets the parent context for nested message processing.
        /// </summary>
        public MessageContext? ParentContext { get; set; }

        /// <summary>
        /// Gets or sets the depth of nested message processing.
        /// </summary>
        public int NestingDepth => ParentContext?.NestingDepth + 1 ?? 0;

        /// <summary>
        /// Gets or sets tracing information.
        /// </summary>
        public MessageTracing? Tracing { get; set; }

        /// <summary>
        /// Initializes a new instance of the MessageContext class.
        /// </summary>
        public MessageContext(IMessage message)
        {
            Guard.AgainstNull(message, nameof(message));

            Message = message;
            MessageId = message.MessageId ?? Guid.NewGuid().ToString();
            Timestamp = DateTimeOffset.UtcNow;
            Headers = new Dictionary<string, string>(message.Headers ?? new Dictionary<string, string>());
            _items = new ConcurrentDictionary<string, object>();

            // Extract standard headers
            ExtractStandardHeaders();
        }

        /// <summary>
        /// Creates a child context for nested message processing.
        /// </summary>
        public MessageContext CreateChildContext(IMessage childMessage)
        {
            var childContext = new MessageContext(childMessage)
            {
                ParentContext = this,
                CorrelationId = CorrelationId,
                CausationId = MessageId,
                ConversationId = ConversationId,
                SessionId = SessionId,
                User = User,
                OriginatingEndpoint = OriginatingEndpoint
            };

            // Copy parent headers with child prefix
            foreach (var header in Headers)
            {
                childContext.Headers[$"Parent.{header.Key}"] = header.Value;
            }

            return childContext;
        }

        /// <summary>
        /// Marks the processing as started.
        /// </summary>
        public void MarkProcessingStarted()
        {
            ProcessingStartedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Marks the processing as completed.
        /// </summary>
        public void MarkProcessingCompleted()
        {
            ProcessingCompletedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Marks the message as faulted.
        /// </summary>
        public void MarkAsFaulted(Exception exception, string? reason = null)
        {
            IsFaulted = true;
            FaultException = exception;
            FaultReason = reason ?? exception.Message;
            MarkProcessingCompleted();
        }

        /// <summary>
        /// Acknowledges the message.
        /// </summary>
        public void Acknowledge()
        {
            IsAcknowledged = true;
            if (!ProcessingCompletedAt.HasValue)
            {
                MarkProcessingCompleted();
            }
        }

        /// <summary>
        /// Gets a typed item from the context.
        /// </summary>
        public T? GetItem<T>(string key) where T : class
        {
            return Items.TryGetValue(key, out var value) ? value as T : null;
        }

        /// <summary>
        /// Sets a typed item in the context.
        /// </summary>
        public void SetItem<T>(string key, T value) where T : class
        {
            Items[key] = value;
        }

        /// <summary>
        /// Removes an item from the context.
        /// </summary>
        public bool RemoveItem(string key)
        {
            return Items.TryRemove(key, out _);
        }

        /// <summary>
        /// Adds or updates a header.
        /// </summary>
        public void SetHeader(string key, string value)
        {
            Headers[key] = value;
        }

        /// <summary>
        /// Gets a header value.
        /// </summary>
        public string? GetHeader(string key)
        {
            return Headers.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Prepares the context for retry.
        /// </summary>
        public void PrepareForRetry()
        {
            RetryCount++;
            ProcessingStartedAt = null;
            ProcessingCompletedAt = null;
            IsFaulted = false;
            FaultException = null;
            FaultReason = null;
            IsAcknowledged = false;

            SetHeader("RetryCount", RetryCount.ToString());
            SetHeader("RetryTimestamp", DateTimeOffset.UtcNow.ToString("O"));
        }

        /// <summary>
        /// Creates a new context for a response message.
        /// </summary>
        public MessageContext CreateResponseContext(IMessage response)
        {
            var responseContext = new MessageContext(response)
            {
                CorrelationId = CorrelationId,
                CausationId = MessageId,
                ConversationId = ConversationId,
                SessionId = SessionId,
                User = User,
                DestinationEndpoint = OriginatingEndpoint // Swap endpoints for response
            };

            // Copy relevant headers
            if (Headers.TryGetValue("ReplyTo", out var replyTo))
            {
                responseContext.DestinationEndpoint = replyTo;
            }

            return responseContext;
        }

        /// <summary>
        /// Clones the context for parallel processing.
        /// </summary>
        public MessageContext Clone()
        {
            var clone = new MessageContext(Message)
            {
                MessageId = MessageId,
                CorrelationId = CorrelationId,
                CausationId = CausationId,
                ConversationId = ConversationId,
                SessionId = SessionId,
                Timestamp = Timestamp,
                ProcessingStartedAt = ProcessingStartedAt,
                ProcessingCompletedAt = ProcessingCompletedAt,
                User = User,
                OriginatingEndpoint = OriginatingEndpoint,
                DestinationEndpoint = DestinationEndpoint,
                RetryCount = RetryCount,
                MaxRetries = MaxRetries,
                IsFaulted = IsFaulted,
                FaultException = FaultException,
                FaultReason = FaultReason,
                IsAcknowledged = IsAcknowledged,
                Priority = Priority,
                ExpiresAt = ExpiresAt,
                DeliveryCount = DeliveryCount,
                ParentContext = ParentContext,
                Tracing = Tracing?.Clone()
            };

            // Deep copy headers
            clone.Headers = new Dictionary<string, string>(Headers);

            // Deep copy items
            foreach (var item in Items)
            {
                clone.Items[item.Key] = item.Value;
            }

            return clone;
        }

        private void ExtractStandardHeaders()
        {
            if (Headers.TryGetValue("CorrelationId", out var correlationId))
                CorrelationId = correlationId;

            if (Headers.TryGetValue("CausationId", out var causationId))
                CausationId = causationId;

            if (Headers.TryGetValue("ConversationId", out var conversationId))
                ConversationId = conversationId;

            if (Headers.TryGetValue("SessionId", out var sessionId))
                SessionId = sessionId;

            if (Headers.TryGetValue("Priority", out var priority) &&
                Enum.TryParse<MessagePriority>(priority, out var parsedPriority))
                Priority = parsedPriority;

            if (Headers.TryGetValue("ExpiresAt", out var expiresAt) &&
                DateTimeOffset.TryParse(expiresAt, out var parsedExpiry))
                ExpiresAt = parsedExpiry;

            if (Headers.TryGetValue("RetryCount", out var retryCount) &&
                int.TryParse(retryCount, out var parsedRetryCount))
                RetryCount = parsedRetryCount;

            if (Headers.TryGetValue("DeliveryCount", out var deliveryCount) &&
                int.TryParse(deliveryCount, out var parsedDeliveryCount))
                DeliveryCount = parsedDeliveryCount;

            if (Headers.TryGetValue("OriginatingEndpoint", out var origEndpoint))
                OriginatingEndpoint = origEndpoint;

            if (Headers.TryGetValue("DestinationEndpoint", out var destEndpoint))
                DestinationEndpoint = destEndpoint;
        }
    }

    /// <summary>
    /// Message priority levels.
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Tracing information for message processing.
    /// </summary>
    public class MessageTracing
    {
        public string TraceId { get; set; } = Guid.NewGuid().ToString();
        public string? SpanId { get; set; }
        public string? ParentSpanId { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<MessageTraceEvent> Events { get; } = new();

        public void AddEvent(string name, string? description = null)
        {
            Events.Add(new MessageTraceEvent
            {
                Name = name,
                Description = description,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        public MessageTracing Clone()
        {
            return new MessageTracing
            {
                TraceId = TraceId,
                SpanId = SpanId,
                ParentSpanId = ParentSpanId,
                Tags = new Dictionary<string, string>(Tags)
            };
        }
    }

    /// <summary>
    /// Represents a trace event in message processing.
    /// </summary>
    public class MessageTraceEvent
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<string, object>? Attributes { get; set; }
    }
}