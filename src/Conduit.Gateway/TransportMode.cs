namespace Conduit.Gateway
{
    /// <summary>
    /// Specifies how a transport operates regarding connections.
    /// </summary>
    public enum TransportMode
    {
        /// <summary>
        /// Transport acts as a server, accepting incoming connections.
        /// </summary>
        Server,

        /// <summary>
        /// Transport acts as a client, initiating outgoing connections.
        /// </summary>
        Client,

        /// <summary>
        /// Transport can act as both server and client (bidirectional).
        /// </summary>
        Proxy
    }
}