using Conduit.Transports.Core;

namespace Conduit.Transports.ZeroMq;

/// <summary>
/// Configuration for ZeroMQ transport
/// </summary>
public class ZeroMqConfiguration : TransportConfiguration
{
    /// <summary>
    /// ZeroMQ socket pattern to use
    /// </summary>
    public ZeroMqPattern Pattern { get; set; } = ZeroMqPattern.PubSub;

    /// <summary>
    /// Address to bind to for server sockets (PUB, REP, PUSH, ROUTER, DEALER)
    /// </summary>
    public string? BindAddress { get; set; }

    /// <summary>
    /// Address to connect to for client sockets (SUB, REQ, PULL, ROUTER, DEALER)
    /// </summary>
    public string? ConnectAddress { get; set; }

    /// <summary>
    /// List of addresses to connect to for multi-endpoint scenarios
    /// </summary>
    public List<string> ConnectAddresses { get; set; } = new();

    /// <summary>
    /// Subscription topics for SUB sockets
    /// </summary>
    public List<string> SubscriptionTopics { get; set; } = new();

    /// <summary>
    /// High Water Mark for sending (number of messages to queue)
    /// </summary>
    public int SendHighWaterMark { get; set; } = 1000;

    /// <summary>
    /// High Water Mark for receiving (number of messages to queue)
    /// </summary>
    public int ReceiveHighWaterMark { get; set; } = 1000;

    /// <summary>
    /// Send timeout in milliseconds
    /// </summary>
    public int SendTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Receive timeout in milliseconds
    /// </summary>
    public int ReceiveTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Linger period in milliseconds (how long to wait for pending messages on close)
    /// </summary>
    public int LingerMs { get; set; } = 1000;

    /// <summary>
    /// Whether to enable TCP keep-alive
    /// </summary>
    public bool TcpKeepAlive { get; set; } = true;

    /// <summary>
    /// TCP keep-alive interval in seconds
    /// </summary>
    public int TcpKeepAliveInterval { get; set; } = 1;

    /// <summary>
    /// Maximum message size in bytes (0 = unlimited)
    /// </summary>
    public long MaxMessageSize { get; set; } = 0;

    /// <summary>
    /// Whether to use message framing (multi-part messages)
    /// </summary>
    public bool UseFraming { get; set; } = true;

    /// <summary>
    /// Socket identity for ROUTER/DEALER patterns
    /// </summary>
    public string? SocketIdentity { get; set; }

    /// <summary>
    /// Whether to enable immediate delivery (no Nagle algorithm)
    /// </summary>
    public bool Immediate { get; set; } = true;

    /// <summary>
    /// Reconnection interval in milliseconds
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Maximum reconnection interval in milliseconds
    /// </summary>
    public int MaxReconnectIntervalMs { get; set; } = 30000;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Transport name must be specified");

        if (string.IsNullOrWhiteSpace(BindAddress) &&
            string.IsNullOrWhiteSpace(ConnectAddress) &&
            ConnectAddresses.Count == 0)
        {
            throw new InvalidOperationException("Either BindAddress, ConnectAddress, or ConnectAddresses must be specified");
        }

        if (SendTimeoutMs < 0)
            throw new ArgumentOutOfRangeException(nameof(SendTimeoutMs), "Send timeout must be non-negative");

        if (ReceiveTimeoutMs < 0)
            throw new ArgumentOutOfRangeException(nameof(ReceiveTimeoutMs), "Receive timeout must be non-negative");

        if (SendHighWaterMark < 0)
            throw new ArgumentOutOfRangeException(nameof(SendHighWaterMark), "Send high water mark must be non-negative");

        if (ReceiveHighWaterMark < 0)
            throw new ArgumentOutOfRangeException(nameof(ReceiveHighWaterMark), "Receive high water mark must be non-negative");
    }
}

/// <summary>
/// ZeroMQ messaging patterns
/// </summary>
public enum ZeroMqPattern
{
    /// <summary>
    /// Publisher-Subscriber pattern (one-to-many)
    /// </summary>
    PubSub,

    /// <summary>
    /// Request-Reply pattern (synchronous RPC)
    /// </summary>
    RequestReply,

    /// <summary>
    /// Push-Pull pattern (load-balanced pipeline)
    /// </summary>
    PushPull,

    /// <summary>
    /// Pair pattern (exclusive pair communication)
    /// </summary>
    Pair,

    /// <summary>
    /// Router-Dealer pattern (asynchronous RPC)
    /// </summary>
    RouterDealer
}