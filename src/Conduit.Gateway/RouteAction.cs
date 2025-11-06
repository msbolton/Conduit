namespace Conduit.Gateway
{
    /// <summary>
    /// Specifies the action to take when a routing rule matches.
    /// </summary>
    public enum RouteAction
    {
        /// <summary>
        /// Accept the connection and route to the specified transport.
        /// </summary>
        Accept,

        /// <summary>
        /// Reject the connection (close it).
        /// </summary>
        Reject,

        /// <summary>
        /// Connect to the specified destination (for outbound connections).
        /// </summary>
        Connect,

        /// <summary>
        /// Forward the connection to another gateway or proxy.
        /// </summary>
        Forward,

        /// <summary>
        /// Drop the connection silently (no response).
        /// </summary>
        Drop
    }
}