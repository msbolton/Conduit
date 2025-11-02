using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// Core interface for message transport implementations.
    /// Provides abstraction for different transport protocols and technologies.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// Gets the transport type.
        /// </summary>
        TransportType Type { get; }

        /// <summary>
        /// Gets the transport name/identifier.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether the transport is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects the transport.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when connected</returns>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects the transport.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when disconnected</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message through this transport.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when message is sent</returns>
        Task SendAsync(IMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to a specific destination.
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="destination">The destination address/queue/topic</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when message is sent</returns>
        Task SendAsync(IMessage message, string destination, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to receive messages from this transport.
        /// </summary>
        /// <param name="handler">The message handler to invoke for received messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A subscription that can be disposed to unsubscribe</returns>
        Task<ITransportSubscription> SubscribeAsync(
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to receive messages from a specific source.
        /// </summary>
        /// <param name="source">The source address/queue/topic</param>
        /// <param name="handler">The message handler to invoke for received messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A subscription that can be disposed to unsubscribe</returns>
        Task<ITransportSubscription> SubscribeAsync(
            string source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets transport statistics and metrics.
        /// </summary>
        /// <returns>The transport statistics</returns>
        TransportStatistics GetStatistics();
    }
}
