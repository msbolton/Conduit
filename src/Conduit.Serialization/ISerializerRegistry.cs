namespace Conduit.Serialization;

/// <summary>
/// Interface for managing and selecting message serializers.
/// </summary>
public interface ISerializerRegistry
{
    /// <summary>
    /// Gets or sets the default serialization format.
    /// </summary>
    SerializationFormat DefaultFormat { get; set; }

    /// <summary>
    /// Registers a serializer for a specific format.
    /// </summary>
    void Register(IMessageSerializer serializer);

    /// <summary>
    /// Registers a serializer for a specific format.
    /// </summary>
    void Register(SerializationFormat format, IMessageSerializer serializer);

    /// <summary>
    /// Unregisters a serializer for a specific format.
    /// </summary>
    bool Unregister(SerializationFormat format);

    /// <summary>
    /// Gets a serializer for the specified format.
    /// </summary>
    IMessageSerializer? GetSerializer(SerializationFormat format);

    /// <summary>
    /// Gets a serializer for the specified MIME type.
    /// </summary>
    IMessageSerializer? GetSerializer(string mimeType);

    /// <summary>
    /// Gets the default serializer.
    /// </summary>
    IMessageSerializer GetDefaultSerializer();

    /// <summary>
    /// Gets a serializer by format, or the default if not found.
    /// </summary>
    IMessageSerializer GetOrDefault(SerializationFormat format);

    /// <summary>
    /// Gets a serializer by MIME type, or the default if not found.
    /// </summary>
    IMessageSerializer GetOrDefault(string mimeType);

    /// <summary>
    /// Checks if a serializer is registered for the specified format.
    /// </summary>
    bool IsRegistered(SerializationFormat format);

    /// <summary>
    /// Checks if a serializer is registered for the specified MIME type.
    /// </summary>
    bool IsRegistered(string mimeType);

    /// <summary>
    /// Gets all registered formats.
    /// </summary>
    IEnumerable<SerializationFormat> GetRegisteredFormats();

    /// <summary>
    /// Gets all registered serializers.
    /// </summary>
    IEnumerable<IMessageSerializer> GetAllSerializers();

    /// <summary>
    /// Clears all registered serializers.
    /// </summary>
    void Clear();

    /// <summary>
    /// Detects the format from byte data.
    /// </summary>
    SerializationFormat? DetectFormat(byte[] data);

    /// <summary>
    /// Detects the format from a file extension.
    /// </summary>
    SerializationFormat? DetectFormatFromExtension(string extension);

    /// <summary>
    /// Detects the format from a MIME type.
    /// </summary>
    SerializationFormat? DetectFormatFromMimeType(string mimeType);

    /// <summary>
    /// Gets statistics about registered serializers.
    /// </summary>
    SerializerRegistryStatistics GetStatistics();
}