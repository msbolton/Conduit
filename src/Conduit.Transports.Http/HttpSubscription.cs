using Conduit.Api;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Http;

/// <summary>
/// HTTP subscription for handling incoming messages
/// </summary>
public class HttpSubscription : ITransportSubscription
{
    private readonly string _topic;
    private readonly Func<TransportMessage, Task> _messageHandler;
    private readonly HttpConfiguration _configuration;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private bool _disposed;
    private long _messagesReceived;

    /// <summary>
    /// Initializes a new instance of the HttpSubscription class
    /// </summary>
    public HttpSubscription(
        string topic,
        Func<TransportMessage, Task> messageHandler,
        HttpConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        SubscriptionId = Guid.NewGuid().ToString();
        Source = topic;
        IsActive = true;
    }

    /// <summary>
    /// Gets the subscription identifier (required by interface)
    /// </summary>
    public string SubscriptionId { get; }

    /// <summary>
    /// Gets the source this subscription listens to (required by interface)
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets whether the subscription is active
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the number of messages received (required by interface)
    /// </summary>
    public long MessagesReceived => _messagesReceived;

    /// <summary>
    /// Processes an incoming transport message
    /// </summary>
    public async Task ProcessTransportMessageAsync(TransportMessage transportMessage)
    {
        if (_disposed || !IsActive)
        {
            _logger?.LogWarning("Received message on inactive HTTP subscription for topic: {Topic}", _topic);
            return;
        }

        try
        {
            _logger?.LogDebug("Processing HTTP transport message for topic: {Topic}, MessageId: {MessageId}",
                _topic, transportMessage.Id);

            await _messageHandler(transportMessage);

            Interlocked.Increment(ref _messagesReceived);
            _logger?.LogDebug("Successfully processed HTTP transport message: {MessageId}", transportMessage.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing HTTP transport message: {MessageId}", transportMessage.Id);
            throw;
        }
    }

    /// <summary>
    /// Pauses the subscription temporarily
    /// </summary>
    public Task PauseAsync()
    {
        if (!_disposed && IsActive)
        {
            IsActive = false;
            _logger?.LogInformation("Paused HTTP subscription for topic: {Topic}", _topic);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes a paused subscription
    /// </summary>
    public Task ResumeAsync()
    {
        if (!_disposed && !IsActive)
        {
            IsActive = true;
            _logger?.LogInformation("Resumed HTTP subscription for topic: {Topic}", _topic);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the subscription
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            IsActive = false;
            _disposed = true;
            _logger?.LogDebug("Disposed HTTP subscription for topic: {Topic}", _topic);
        }
    }
}