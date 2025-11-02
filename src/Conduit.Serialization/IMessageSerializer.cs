using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Serialization
{
    /// <summary>
    /// Core serialization interface for converting objects to/from various formats.
    /// </summary>
    public interface IMessageSerializer
    {
        /// <summary>
        /// Gets the serialization format supported by this serializer.
        /// </summary>
        SerializationFormat Format { get; }

        /// <summary>
        /// Gets the MIME type for this serialization format.
        /// </summary>
        string MimeType { get; }

        /// <summary>
        /// Gets the file extension for this serialization format.
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Serializes an object to a byte array.
        /// </summary>
        byte[] Serialize<T>(T obj);

        /// <summary>
        /// Serializes an object to a byte array asynchronously.
        /// </summary>
        Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes an object to a stream.
        /// </summary>
        void Serialize<T>(T obj, Stream stream);

        /// <summary>
        /// Serializes an object to a stream asynchronously.
        /// </summary>
        Task SerializeAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserializes an object from a byte array.
        /// </summary>
        T Deserialize<T>(byte[] data);

        /// <summary>
        /// Deserializes an object from a byte array asynchronously.
        /// </summary>
        Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserializes an object from a stream.
        /// </summary>
        T Deserialize<T>(Stream stream);

        /// <summary>
        /// Deserializes an object from a stream asynchronously.
        /// </summary>
        Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if this serializer can handle the given format.
        /// </summary>
        bool Supports(SerializationFormat format);

        /// <summary>
        /// Checks if this serializer can handle the given MIME type.
        /// </summary>
        bool Supports(string mimeType);

        /// <summary>
        /// Validates that the given data is valid for this format.
        /// </summary>
        bool IsValid(byte[] data);

        /// <summary>
        /// Gets the encoding used by this serializer (if applicable).
        /// </summary>
        Encoding? Encoding { get; }
    }

    /// <summary>
    /// Exception thrown when serialization fails.
    /// </summary>
    public class SerializationException : Exception
    {
        public SerializationFormat Format { get; }
        public string? Operation { get; }

        public SerializationException(string message, SerializationFormat format, string? operation = null)
            : base(message)
        {
            Format = format;
            Operation = operation;
        }

        public SerializationException(string message, Exception innerException, SerializationFormat format, string? operation = null)
            : base(message, innerException)
        {
            Format = format;
            Operation = operation;
        }
    }

    /// <summary>
    /// Options for serialization.
    /// </summary>
    public class SerializationOptions
    {
        /// <summary>
        /// Gets or sets whether to use pretty printing (for text formats).
        /// </summary>
        public bool PrettyPrint { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to include null values.
        /// </summary>
        public bool IncludeNulls { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to include type information.
        /// </summary>
        public bool IncludeTypeInformation { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to use camel case naming.
        /// </summary>
        public bool UseCamelCase { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to fail on unknown properties during deserialization.
        /// </summary>
        public bool FailOnUnknownProperties { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to compress the output.
        /// </summary>
        public bool UseCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to validate input.
        /// </summary>
        public bool ValidateInput { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate output.
        /// </summary>
        public bool ValidateOutput { get; set; } = false;

        /// <summary>
        /// Gets or sets the encoding to use (for text formats).
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Gets or sets the maximum depth for nested objects.
        /// </summary>
        public int MaxDepth { get; set; } = 64;

        /// <summary>
        /// Creates default options.
        /// </summary>
        public static SerializationOptions Default() => new();

        /// <summary>
        /// Creates options for compact output.
        /// </summary>
        public static SerializationOptions Compact() => new()
        {
            PrettyPrint = false,
            IncludeNulls = false,
            UseCompression = false
        };

        /// <summary>
        /// Creates options for readable output.
        /// </summary>
        public static SerializationOptions Readable() => new()
        {
            PrettyPrint = true,
            IncludeNulls = true,
            UseCompression = false
        };

        /// <summary>
        /// Creates options for compressed output.
        /// </summary>
        public static SerializationOptions Compressed() => new()
        {
            PrettyPrint = false,
            IncludeNulls = false,
            UseCompression = true
        };
    }

    /// <summary>
    /// Metadata about a serialization format.
    /// </summary>
    public class SerializationMetadata
    {
        public SerializationFormat Format { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Version { get; init; } = "";
        public bool SupportsStreaming { get; init; }
        public bool SupportsSchema { get; init; }
        public bool SupportsCompression { get; init; }
        public bool BinaryFormat { get; init; }
        public string[] SupportedMimeTypes { get; init; } = Array.Empty<string>();
        public string[] SupportedExtensions { get; init; } = Array.Empty<string>();
    }
}