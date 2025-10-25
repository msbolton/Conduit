namespace Conduit.Transports.Core
{
    /// <summary>
    /// Enumeration of supported transport types.
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// In-memory transport for testing and local communication
        /// </summary>
        InMemory,

        /// <summary>
        /// TCP socket-based transport
        /// </summary>
        Tcp,

        /// <summary>
        /// AMQP protocol (RabbitMQ, ActiveMQ, etc.)
        /// </summary>
        Amqp,

        /// <summary>
        /// gRPC transport
        /// </summary>
        Grpc,

        /// <summary>
        /// Apache Kafka transport
        /// </summary>
        Kafka,

        /// <summary>
        /// Redis Pub/Sub transport
        /// </summary>
        Redis,

        /// <summary>
        /// WebSocket transport
        /// </summary>
        WebSocket,

        /// <summary>
        /// HTTP/REST transport
        /// </summary>
        Http,

        /// <summary>
        /// Named pipes transport
        /// </summary>
        NamedPipes,

        /// <summary>
        /// Azure Service Bus transport
        /// </summary>
        ServiceBus,

        /// <summary>
        /// AWS SQS transport
        /// </summary>
        Sqs,

        /// <summary>
        /// Custom transport implementation
        /// </summary>
        Custom
    }
}
