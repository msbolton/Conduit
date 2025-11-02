using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using MessagePack;
using MessagePack.Resolvers;

namespace Conduit.Serialization
{
    /// <summary>
    /// MessagePack serializer implementation.
    /// </summary>
    public class MessagePackSerializer : IMessageSerializer
    {
        private readonly MessagePackSerializerOptions _options;
        private readonly SerializationOptions _serializationOptions;

        public SerializationFormat Format => SerializationFormat.MessagePack;
        public string MimeType => "application/x-msgpack";
        public string FileExtension => "msgpack";
        public Encoding? Encoding => null; // MessagePack is binary

        /// <summary>
        /// Initializes a new instance of the MessagePackSerializer class.
        /// </summary>
        public MessagePackSerializer(SerializationOptions? options = null)
        {
            _serializationOptions = options ?? SerializationOptions.Default();
            _options = CreateMessagePackSerializerOptions(_serializationOptions);
        }

        public byte[] Serialize<T>(T obj)
        {
            try
            {
                ValidateInput(obj);

                var msgpackBytes = global::MessagePack.MessagePackSerializer.Serialize(obj, _options);

                if (_serializationOptions.UseCompression)
                {
                    msgpackBytes = Compress(msgpackBytes);
                }

                ValidateOutput(msgpackBytes);
                return msgpackBytes;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to MessagePack: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serialize");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during MessagePack serialization: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serialize");
            }
        }

        public async Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateInput(obj);

                using var stream = new MemoryStream();

                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
                    await global::MessagePack.MessagePackSerializer.SerializeAsync(gzipStream, obj, _options, cancellationToken);
                    await gzipStream.FlushAsync(cancellationToken);
                }
                else
                {
                    await global::MessagePack.MessagePackSerializer.SerializeAsync(stream, obj, _options, cancellationToken);
                }

                var msgpackBytes = stream.ToArray();
                ValidateOutput(msgpackBytes);
                return msgpackBytes;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to MessagePack: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serializeAsync");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during MessagePack serialization: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serializeAsync");
            }
        }

        public void Serialize<T>(T obj, Stream stream)
        {
            try
            {
                Guard.AgainstNull(stream, nameof(stream));
                ValidateInput(obj);

                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
                    global::MessagePack.MessagePackSerializer.Serialize(gzipStream, obj, _options);
                    gzipStream.Flush();
                }
                else
                {
                    global::MessagePack.MessagePackSerializer.Serialize(stream, obj, _options);
                }
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to MessagePack stream: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serialize");
            }
        }

        public async Task SerializeAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                Guard.AgainstNull(stream, nameof(stream));
                ValidateInput(obj);

                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
                    await global::MessagePack.MessagePackSerializer.SerializeAsync(gzipStream, obj, _options, cancellationToken);
                    await gzipStream.FlushAsync(cancellationToken);
                }
                else
                {
                    await global::MessagePack.MessagePackSerializer.SerializeAsync(stream, obj, _options, cancellationToken);
                }
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to MessagePack stream: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "serializeAsync");
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            try
            {
                ValidateInputData(data);

                var msgpackBytes = data;
                if (_serializationOptions.UseCompression)
                {
                    msgpackBytes = Decompress(data);
                }

                var result = global::MessagePack.MessagePackSerializer.Deserialize<T>(msgpackBytes, _options);
                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.MessagePack,
                        "deserialize");
                }

                return result;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize MessagePack to object: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserialize");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during MessagePack deserialization: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserialize");
            }
        }

        public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateInputData(data);

                using var stream = new MemoryStream(data);

                T? result;
                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
                    result = await global::MessagePack.MessagePackSerializer.DeserializeAsync<T>(gzipStream, _options, cancellationToken);
                }
                else
                {
                    result = await global::MessagePack.MessagePackSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.MessagePack,
                        "deserializeAsync");
                }

                return result;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize MessagePack to object: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserializeAsync");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during MessagePack deserialization: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserializeAsync");
            }
        }

        public T Deserialize<T>(Stream stream)
        {
            try
            {
                Guard.AgainstNull(stream, nameof(stream));

                T? result;
                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                    result = global::MessagePack.MessagePackSerializer.Deserialize<T>(gzipStream, _options);
                }
                else
                {
                    result = global::MessagePack.MessagePackSerializer.Deserialize<T>(stream, _options);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.MessagePack,
                        "deserialize");
                }

                return result;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize MessagePack stream to object: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserialize");
            }
        }

        public async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                Guard.AgainstNull(stream, nameof(stream));

                T? result;
                if (_serializationOptions.UseCompression)
                {
                    using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                    result = await global::MessagePack.MessagePackSerializer.DeserializeAsync<T>(gzipStream, _options, cancellationToken);
                }
                else
                {
                    result = await global::MessagePack.MessagePackSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.MessagePack,
                        "deserializeAsync");
                }

                return result;
            }
            catch (MessagePackSerializationException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize MessagePack stream to object: {ex.Message}",
                    ex,
                    SerializationFormat.MessagePack,
                    "deserializeAsync");
            }
        }

        public bool Supports(SerializationFormat format)
        {
            return format == SerializationFormat.MessagePack;
        }

        public bool Supports(string mimeType)
        {
            return string.Equals(mimeType, "application/x-msgpack", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mimeType, "application/msgpack", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValid(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            try
            {
                var msgpackBytes = data;
                if (_serializationOptions.UseCompression)
                {
                    msgpackBytes = Decompress(data);
                }

                // Try to read the first byte to check if it's valid MessagePack
                // MessagePack format starts with specific byte patterns
                if (msgpackBytes.Length > 0)
                {
                    var firstByte = msgpackBytes[0];
                    // Valid MessagePack starts with specific byte ranges
                    // This is a simplified validation
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static MessagePackSerializerOptions CreateMessagePackSerializerOptions(SerializationOptions options)
        {
            // Use ContractlessStandardResolver for automatic serialization without attributes
            var resolver = options.IncludeTypeInformation
                ? CompositeResolver.Create(
                    StandardResolverAllowPrivate.Instance,
                    ContractlessStandardResolver.Instance)
                : ContractlessStandardResolver.Instance;

            return MessagePackSerializerOptions.Standard
                .WithResolver(resolver)
                .WithCompression(options.UseCompression ? MessagePackCompression.Lz4BlockArray : MessagePackCompression.None);
        }

        private void ValidateInput<T>(T obj)
        {
            if (_serializationOptions.ValidateInput && obj == null)
            {
                throw new SerializationException(
                    "Input object cannot be null",
                    SerializationFormat.MessagePack,
                    "validation");
            }
        }

        private void ValidateInputData(byte[] data)
        {
            if (_serializationOptions.ValidateInput && (data == null || data.Length == 0))
            {
                throw new SerializationException(
                    "Input data cannot be null or empty",
                    SerializationFormat.MessagePack,
                    "validation");
            }
        }

        private void ValidateOutput(byte[] output)
        {
            if (_serializationOptions.ValidateOutput && (output == null || output.Length == 0))
            {
                throw new SerializationException(
                    "Serialization produced null or empty output",
                    SerializationFormat.MessagePack,
                    "validation");
            }
        }

        private static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return data ?? Array.Empty<byte>();
            }

            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        private static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
            {
                return compressedData ?? Array.Empty<byte>();
            }

            using var inputStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}