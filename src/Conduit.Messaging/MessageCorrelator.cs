using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Messaging
{
    /// <summary>
    /// Manages message correlation for tracking related messages and conversations.
    /// </summary>
    public class MessageCorrelator : IDisposable
    {
        private readonly ConcurrentDictionary<string, CorrelationContext> _correlations;
        private readonly ConcurrentDictionary<string, ConversationContext> _conversations;
        private readonly TimeSpan _defaultTimeout;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// Gets the number of active correlations.
        /// </summary>
        public int ActiveCorrelations => _correlations.Count;

        /// <summary>
        /// Gets the number of active conversations.
        /// </summary>
        public int ActiveConversations => _conversations.Count;

        /// <summary>
        /// Initializes a new instance of the MessageCorrelator class.
        /// </summary>
        public MessageCorrelator(TimeSpan? defaultTimeout = null)
        {
            _correlations = new ConcurrentDictionary<string, CorrelationContext>();
            _conversations = new ConcurrentDictionary<string, ConversationContext>();
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(5);

            // Start cleanup timer to remove expired correlations
            _cleanupTimer = new Timer(
                CleanupExpiredCorrelations,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Gets or creates a correlation ID for a message.
        /// </summary>
        public string GetOrCreateCorrelationId(IMessage message)
        {
            Guard.AgainstNull(message, nameof(message));

            // Check if message already has a correlation ID
            if (message.Headers?.TryGetValue("CorrelationId", out var existingIdObj) == true)
            {
                var existingId = existingIdObj?.ToString();
                if (!string.IsNullOrEmpty(existingId))
                {
                    return existingId;
                }
            }

            // Generate new correlation ID
            var correlationId = Guid.NewGuid().ToString();

            // Note: Cannot modify readonly headers - would need to be handled differently in implementation

            // Start tracking
            StartCorrelation(correlationId, message);

            return correlationId;
        }

        /// <summary>
        /// Starts tracking a correlation.
        /// </summary>
        public void StartCorrelation(string correlationId, IMessage message)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            Guard.AgainstNull(message, nameof(message));

            var context = new CorrelationContext
            {
                CorrelationId = correlationId,
                StartedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(_defaultTimeout),
                InitiatingMessage = message,
                InitiatingMessageType = message.GetType().Name
            };

            _correlations.AddOrUpdate(correlationId, context, (_, existing) =>
            {
                existing.MessageCount++;
                existing.LastActivity = DateTimeOffset.UtcNow;
                return existing;
            });

            // Check for conversation ID
            if (message.Headers?.TryGetValue("ConversationId", out var conversationIdObj) == true)
            {
                var conversationId = conversationIdObj?.ToString();
                if (!string.IsNullOrEmpty(conversationId))
                {
                    AddToConversation(conversationId, correlationId, message);
                }
            }
        }

        /// <summary>
        /// Adds a message to a correlation.
        /// </summary>
        public void AddMessage(string correlationId, IMessage message)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            Guard.AgainstNull(message, nameof(message));

            if (_correlations.TryGetValue(correlationId, out var context))
            {
                lock (context.MessagesLock)
                {
                    context.Messages.Add(new CorrelatedMessage
                    {
                        Message = message,
                        MessageType = message.GetType().Name,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                    context.MessageCount++;
                    context.LastActivity = DateTimeOffset.UtcNow;
                }
            }
        }

        /// <summary>
        /// Completes a correlation.
        /// </summary>
        public void CompleteCorrelation(string correlationId)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));

            if (_correlations.TryGetValue(correlationId, out var context))
            {
                context.CompletedAt = DateTimeOffset.UtcNow;
                context.State = CorrelationState.Completed;
                context.Duration = context.CompletedAt.Value - context.StartedAt;
            }
        }

        /// <summary>
        /// Marks a correlation as failed.
        /// </summary>
        public void MarkAsFailed(string correlationId, Exception exception)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            Guard.AgainstNull(exception, nameof(exception));

            if (_correlations.TryGetValue(correlationId, out var context))
            {
                context.CompletedAt = DateTimeOffset.UtcNow;
                context.State = CorrelationState.Failed;
                context.Duration = context.CompletedAt.Value - context.StartedAt;
                context.FailureReason = exception.Message;
                context.FailureException = exception;
            }
        }

        /// <summary>
        /// Gets a correlation context by ID.
        /// </summary>
        public CorrelationContext? GetCorrelation(string correlationId)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            return _correlations.TryGetValue(correlationId, out var context) ? context : null;
        }

        /// <summary>
        /// Removes a correlation from tracking.
        /// </summary>
        public bool RemoveCorrelation(string correlationId)
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            return _correlations.TryRemove(correlationId, out _);
        }

        /// <summary>
        /// Creates or gets a conversation.
        /// </summary>
        public string GetOrCreateConversationId(IMessage message)
        {
            Guard.AgainstNull(message, nameof(message));

            // Check if message already has a conversation ID
            if (message.Headers?.TryGetValue("ConversationId", out var existingIdObj) == true)
            {
                var existingId = existingIdObj?.ToString();
                if (!string.IsNullOrEmpty(existingId))
                {
                    return existingId;
                }
            }

            // Generate new conversation ID
            var conversationId = Guid.NewGuid().ToString();

            // Note: Cannot modify readonly headers - would need to be handled differently in implementation

            // Start tracking
            StartConversation(conversationId, message);

            return conversationId;
        }

        /// <summary>
        /// Starts tracking a conversation.
        /// </summary>
        public void StartConversation(string conversationId, IMessage message)
        {
            Guard.AgainstNullOrEmpty(conversationId, nameof(conversationId));
            Guard.AgainstNull(message, nameof(message));

            var context = new ConversationContext
            {
                ConversationId = conversationId,
                StartedAt = DateTimeOffset.UtcNow,
                InitiatingMessage = message
            };

            _conversations.AddOrUpdate(conversationId, context, (_, existing) =>
            {
                existing.MessageCount++;
                existing.LastActivity = DateTimeOffset.UtcNow;
                return existing;
            });
        }

        /// <summary>
        /// Adds a message to a conversation.
        /// </summary>
        public void AddToConversation(string conversationId, string correlationId, IMessage message)
        {
            Guard.AgainstNullOrEmpty(conversationId, nameof(conversationId));
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));
            Guard.AgainstNull(message, nameof(message));

            var context = _conversations.GetOrAdd(conversationId, id => new ConversationContext
            {
                ConversationId = id,
                StartedAt = DateTimeOffset.UtcNow,
                InitiatingMessage = message
            });

            lock (context.CorrelationsLock)
            {
                if (!context.CorrelationIds.Contains(correlationId))
                {
                    context.CorrelationIds.Add(correlationId);
                }
                context.MessageCount++;
                context.LastActivity = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Gets a conversation context by ID.
        /// </summary>
        public ConversationContext? GetConversation(string conversationId)
        {
            Guard.AgainstNullOrEmpty(conversationId, nameof(conversationId));
            return _conversations.TryGetValue(conversationId, out var context) ? context : null;
        }

        /// <summary>
        /// Gets all correlations for a conversation.
        /// </summary>
        public IEnumerable<CorrelationContext> GetConversationCorrelations(string conversationId)
        {
            Guard.AgainstNullOrEmpty(conversationId, nameof(conversationId));

            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                return conversation.CorrelationIds
                    .Select(id => GetCorrelation(id))
                    .Where(c => c != null)
                    .Cast<CorrelationContext>();
            }

            return Enumerable.Empty<CorrelationContext>();
        }

        /// <summary>
        /// Waits for a correlated response.
        /// </summary>
        public async Task<TResponse?> WaitForResponseAsync<TResponse>(
            string correlationId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            where TResponse : class, IMessage
        {
            Guard.AgainstNullOrEmpty(correlationId, nameof(correlationId));

            var effectiveTimeout = timeout ?? _defaultTimeout;
            var deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);

            while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                if (_correlations.TryGetValue(correlationId, out var context))
                {
                    lock (context.MessagesLock)
                    {
                        var response = context.Messages
                            .Where(m => m.Message is TResponse)
                            .Select(m => m.Message as TResponse)
                            .FirstOrDefault();

                        if (response != null)
                        {
                            return response;
                        }
                    }

                    if (context.State == CorrelationState.Failed)
                    {
                        throw new CorrelationFailedException(
                            $"Correlation {correlationId} failed: {context.FailureReason}",
                            context.FailureException);
                    }
                }

                await Task.Delay(100, cancellationToken);
            }

            throw new TimeoutException($"Timeout waiting for response with correlation ID {correlationId}");
        }

        /// <summary>
        /// Gets statistics about correlations.
        /// </summary>
        public CorrelationStatistics GetStatistics()
        {
            var stats = new CorrelationStatistics
            {
                TotalCorrelations = _correlations.Count,
                ActiveCorrelations = _correlations.Count(c => c.Value.State == CorrelationState.Active),
                CompletedCorrelations = _correlations.Count(c => c.Value.State == CorrelationState.Completed),
                FailedCorrelations = _correlations.Count(c => c.Value.State == CorrelationState.Failed),
                ExpiredCorrelations = _correlations.Count(c => c.Value.IsExpired),
                TotalConversations = _conversations.Count,
                TotalMessages = _correlations.Values.Sum(c => c.MessageCount)
            };

            if (_correlations.Any(c => c.Value.Duration.HasValue))
            {
                var durations = _correlations.Values
                    .Where(c => c.Duration.HasValue)
                    .Select(c => c.Duration!.Value.TotalMilliseconds)
                    .ToList();

                stats.AverageDurationMs = durations.Average();
                stats.MinDurationMs = durations.Min();
                stats.MaxDurationMs = durations.Max();
            }

            return stats;
        }

        private void CleanupExpiredCorrelations(object? state)
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _correlations
                .Where(kvp => kvp.Value.IsExpired ||
                            (kvp.Value.State == CorrelationState.Completed &&
                             kvp.Value.CompletedAt.HasValue &&
                             now - kvp.Value.CompletedAt.Value > TimeSpan.FromHours(1)))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _correlations.TryRemove(key, out _);
            }

            // Cleanup old conversations
            var expiredConversations = _conversations
                .Where(kvp => now - kvp.Value.LastActivity > TimeSpan.FromHours(24))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredConversations)
            {
                _conversations.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _correlations.Clear();
                _conversations.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Context for a correlation.
    /// </summary>
    public class CorrelationContext
    {
        public string CorrelationId { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset LastActivity { get; set; }
        public TimeSpan? Duration { get; set; }
        public CorrelationState State { get; set; } = CorrelationState.Active;
        public IMessage? InitiatingMessage { get; set; }
        public string InitiatingMessageType { get; set; } = "";
        public List<CorrelatedMessage> Messages { get; } = new();
        public int MessageCount { get; set; }
        public string? FailureReason { get; set; }
        public Exception? FailureException { get; set; }
        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
        public object MessagesLock { get; } = new();
    }

    /// <summary>
    /// Context for a conversation.
    /// </summary>
    public class ConversationContext
    {
        public string ConversationId { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset LastActivity { get; set; }
        public IMessage? InitiatingMessage { get; set; }
        public List<string> CorrelationIds { get; } = new();
        public int MessageCount { get; set; }
        public object CorrelationsLock { get; } = new();
    }

    /// <summary>
    /// Represents a correlated message.
    /// </summary>
    public class CorrelatedMessage
    {
        public IMessage Message { get; set; } = null!;
        public string MessageType { get; set; } = "";
        public DateTimeOffset Timestamp { get; set; }
    }

    /// <summary>
    /// State of a correlation.
    /// </summary>
    public enum CorrelationState
    {
        Active,
        Completed,
        Failed,
        Expired
    }

    /// <summary>
    /// Statistics about correlations.
    /// </summary>
    public class CorrelationStatistics
    {
        public int TotalCorrelations { get; set; }
        public int ActiveCorrelations { get; set; }
        public int CompletedCorrelations { get; set; }
        public int FailedCorrelations { get; set; }
        public int ExpiredCorrelations { get; set; }
        public int TotalConversations { get; set; }
        public int TotalMessages { get; set; }
        public double? AverageDurationMs { get; set; }
        public double? MinDurationMs { get; set; }
        public double? MaxDurationMs { get; set; }
    }

    /// <summary>
    /// Exception thrown when a correlation fails.
    /// </summary>
    public class CorrelationFailedException : Exception
    {
        public CorrelationFailedException(string message) : base(message) { }
        public CorrelationFailedException(string message, Exception? innerException)
            : base(message, innerException) { }
    }
}