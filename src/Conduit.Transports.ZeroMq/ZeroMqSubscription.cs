using Conduit.Transports.Core;

namespace Conduit.Transports.ZeroMq;

/// <summary>
/// ZeroMQ-specific transport subscription implementation
/// </summary>
public class ZeroMqSubscription : ITransportSubscription
{
    private readonly ZeroMqTransport _transport;
    private bool _disposed;
    private long _messagesReceived;

    /// <inheritdoc />
    public string SubscriptionId { get; }

    /// <inheritdoc />
    public string Source { get; }

    /// <inheritdoc />
    public bool IsActive { get; private set; } = true;

    /// <inheritdoc />
    public long MessagesReceived => _messagesReceived;

    /// <summary>
    /// Gets the message handler function
    /// </summary>
    internal Func<TransportMessage, Task> Handler { get; }

    /// <summary>
    /// Increments the message received counter
    /// </summary>
    internal void IncrementMessagesReceived()
    {
        Interlocked.Increment(ref _messagesReceived);
    }

    /// <summary>
    /// Initializes a new instance of the ZeroMqSubscription class
    /// </summary>
    public ZeroMqSubscription(
        string subscriptionId,
        string? source,
        Func<TransportMessage, Task> handler,
        ZeroMqTransport transport)
    {
        SubscriptionId = subscriptionId ?? throw new ArgumentNullException(nameof(subscriptionId));
        Source = source ?? "default";
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    public async Task PauseAsync()
    {
        await Task.Run(() =>
        {
            IsActive = false;
        });
    }

    /// <inheritdoc />
    public async Task ResumeAsync()
    {
        await Task.Run(() =>
        {
            if (!_disposed)
            {
                IsActive = true;
            }
        });
    }

    /// <summary>
    /// Unsubscribes from the transport
    /// </summary>
    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsActive)
            return;

        await Task.Run(() =>
        {
            try
            {
                IsActive = false;
                _transport.RemoveSubscriptionInternal(SubscriptionId);
            }
            catch (Exception)
            {
                // Let the transport base class handle error logging
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            UnsubscribeAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Swallow exceptions during disposal to prevent finalizer issues
        }
        finally
        {
            _disposed = true;
        }
    }
}