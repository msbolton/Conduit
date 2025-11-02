namespace Conduit.Messaging;

/// <summary>
/// Interface for managing event subscriptions for the message bus.
/// </summary>
public interface ISubscriptionManager : IDisposable
{
    /// <summary>
    /// Adds a subscription for a specific message type.
    /// </summary>
    void AddSubscription(Type messageType, IMessageSubscription subscription);

    /// <summary>
    /// Removes a subscription for a specific message type.
    /// </summary>
    bool RemoveSubscription(Type messageType, IMessageSubscription subscription);

    /// <summary>
    /// Gets all subscriptions for a specific message type.
    /// </summary>
    IEnumerable<IMessageSubscription> GetSubscriptions(Type messageType);

    /// <summary>
    /// Gets all active subscriptions for a specific message type.
    /// </summary>
    IEnumerable<IMessageSubscription> GetActiveSubscriptions(Type messageType);

    /// <summary>
    /// Gets the count of active subscriptions.
    /// </summary>
    int GetActiveSubscriptionCount();

    /// <summary>
    /// Gets the count of all subscriptions.
    /// </summary>
    int GetTotalSubscriptionCount();

    /// <summary>
    /// Gets subscription statistics.
    /// </summary>
    SubscriptionStatistics GetStatistics();

    /// <summary>
    /// Clears all subscriptions.
    /// </summary>
    void Clear();
}