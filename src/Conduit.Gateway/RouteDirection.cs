namespace Conduit.Gateway
{
    /// <summary>
    /// Specifies the direction of network traffic for routing rules.
    /// </summary>
    public enum RouteDirection
    {
        /// <summary>
        /// Route applies to incoming connections (external clients connecting to this system).
        /// </summary>
        Inbound,

        /// <summary>
        /// Route applies to outgoing connections (this system connecting to external services).
        /// </summary>
        Outbound,

        /// <summary>
        /// Route applies to both incoming and outgoing connections.
        /// </summary>
        Both
    }
}