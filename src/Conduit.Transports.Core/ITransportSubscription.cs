using System;
using System.Threading.Tasks;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Represents a subscription to a transport message source.
    /// Dispose to unsubscribe.
    /// </summary>
    public interface ITransportSubscription : IDisposable
    {
        /// <summary>
        /// Gets the subscription identifier.
        /// </summary>
        string SubscriptionId { get; }

        /// <summary>
        /// Gets the source this subscription is listening to.
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Gets whether this subscription is active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets the number of messages received through this subscription.
        /// </summary>
        long MessagesReceived { get; }

        /// <summary>
        /// Pauses the subscription temporarily.
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Resumes a paused subscription.
        /// </summary>
        Task ResumeAsync();
    }
}
