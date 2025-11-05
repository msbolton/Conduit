namespace Conduit.Gateway
{
    /// <summary>
    /// Network protocols supported by the gateway.
    /// </summary>
    public enum Protocol
    {
        /// <summary>
        /// Any protocol (wildcard).
        /// </summary>
        Any,

        /// <summary>
        /// Transmission Control Protocol.
        /// </summary>
        TCP,

        /// <summary>
        /// User Datagram Protocol.
        /// </summary>
        UDP,

        /// <summary>
        /// Internet Control Message Protocol.
        /// </summary>
        ICMP,

        /// <summary>
        /// Raw socket protocol.
        /// </summary>
        Raw
    }
}