using Conduit.Api;

namespace Conduit.Messaging;

/// <summary>
/// Interface for managing message correlation for tracking related messages and conversations.
/// </summary>
public interface IMessageCorrelator : IDisposable
{
    /// <summary>
    /// Gets the number of active correlations.
    /// </summary>
    int ActiveCorrelations { get; }

    /// <summary>
    /// Gets the number of active conversations.
    /// </summary>
    int ActiveConversations { get; }

    /// <summary>
    /// Gets or creates a correlation ID for a message.
    /// </summary>
    string GetOrCreateCorrelationId(IMessage message);

    /// <summary>
    /// Starts a new correlation for a message.
    /// </summary>
    void StartCorrelation(string correlationId, IMessage message);

    /// <summary>
    /// Adds a message to an existing correlation.
    /// </summary>
    void AddMessage(string correlationId, IMessage message);

    /// <summary>
    /// Marks a correlation as complete.
    /// </summary>
    void CompleteCorrelation(string correlationId);

    /// <summary>
    /// Marks a correlation as failed.
    /// </summary>
    void MarkAsFailed(string correlationId, Exception exception);

    /// <summary>
    /// Gets a correlation context by ID.
    /// </summary>
    CorrelationContext? GetCorrelation(string correlationId);

    /// <summary>
    /// Removes a correlation from tracking.
    /// </summary>
    bool RemoveCorrelation(string correlationId);

    /// <summary>
    /// Gets or creates a conversation ID for a message.
    /// </summary>
    string GetOrCreateConversationId(IMessage message);

    /// <summary>
    /// Starts a new conversation for a message.
    /// </summary>
    void StartConversation(string conversationId, IMessage message);

    /// <summary>
    /// Adds a correlation to an existing conversation.
    /// </summary>
    void AddToConversation(string conversationId, string correlationId, IMessage message);

    /// <summary>
    /// Gets a conversation context by ID.
    /// </summary>
    ConversationContext? GetConversation(string conversationId);
}