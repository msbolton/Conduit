using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;

namespace Conduit.Serialization
{
    /// <summary>
    /// JSON serializer implementation using System.Text.Json.
    /// </summary>
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options;
        private readonly SerializationOptions _serializationOptions;

        public SerializationFormat Format => SerializationFormat.Json;
        public string MimeType => "application/json";
        public string FileExtension => "json";
        public Encoding? Encoding => _serializationOptions.Encoding;

        /// <summary>
        /// Initializes a new instance of the JsonMessageSerializer class.
        /// </summary>
        public JsonMessageSerializer(SerializationOptions? options = null)
        {
            _serializationOptions = options ?? SerializationOptions.Default();
            _options = CreateJsonSerializerOptions(_serializationOptions);
        }

        public byte[] Serialize<T>(T obj)
        {
            try
            {
                ValidateInput(obj);

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj, _options);

                if (_serializationOptions.UseCompression)
                {
                    jsonBytes = Compress(jsonBytes);
                }

                ValidateOutput(jsonBytes);
                return jsonBytes;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to JSON: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "serialize");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during JSON serialization: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    await JsonSerializer.SerializeAsync(gzipStream, obj, _options, cancellationToken);
                    await gzipStream.FlushAsync(cancellationToken);
                }
                else
                {
                    await JsonSerializer.SerializeAsync(stream, obj, _options, cancellationToken);
                }

                var jsonBytes = stream.ToArray();
                ValidateOutput(jsonBytes);
                return jsonBytes;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to JSON: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "serializeAsync");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during JSON serialization: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    JsonSerializer.Serialize(gzipStream, obj, _options);
                    gzipStream.Flush();
                }
                else
                {
                    JsonSerializer.Serialize(stream, obj, _options);
                }
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to JSON stream: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    await JsonSerializer.SerializeAsync(gzipStream, obj, _options, cancellationToken);
                    await gzipStream.FlushAsync(cancellationToken);
                }
                else
                {
                    await JsonSerializer.SerializeAsync(stream, obj, _options, cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object to JSON stream: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "serializeAsync");
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            try
            {
                ValidateInputData(data);

                var jsonBytes = data;
                if (_serializationOptions.UseCompression)
                {
                    jsonBytes = Decompress(data);
                }

                var result = JsonSerializer.Deserialize<T>(jsonBytes, _options);
                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.Json,
                        "deserialize");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize JSON to object: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "deserialize");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during JSON deserialization: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    result = await JsonSerializer.DeserializeAsync<T>(gzipStream, _options, cancellationToken);
                }
                else
                {
                    result = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.Json,
                        "deserializeAsync");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize JSON to object: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "deserializeAsync");
            }
            catch (Exception ex) when (ex is not SerializationException)
            {
                throw new SerializationException(
                    $"Unexpected error during JSON deserialization: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    result = JsonSerializer.Deserialize<T>(gzipStream, _options);
                }
                else
                {
                    result = JsonSerializer.Deserialize<T>(stream, _options);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.Json,
                        "deserialize");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize JSON stream to object: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
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
                    result = await JsonSerializer.DeserializeAsync<T>(gzipStream, _options, cancellationToken);
                }
                else
                {
                    result = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
                }

                if (result == null)
                {
                    throw new SerializationException(
                        "Deserialization returned null",
                        SerializationFormat.Json,
                        "deserializeAsync");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to deserialize JSON stream to object: {ex.Message}",
                    ex,
                    SerializationFormat.Json,
                    "deserializeAsync");
            }
        }

        public bool Supports(SerializationFormat format)
        {
            return format == SerializationFormat.Json;
        }

        public bool Supports(string mimeType)
        {
            return string.Equals(mimeType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mimeType, "text/json", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValid(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            try
            {
                var jsonBytes = data;
                if (_serializationOptions.UseCompression)
                {
                    jsonBytes = Decompress(data);
                }

                using var document = JsonDocument.Parse(jsonBytes);
                return document.RootElement.ValueKind != JsonValueKind.Undefined;
            }
            catch
            {
                return false;
            }
        }

        private static JsonSerializerOptions CreateJsonSerializerOptions(SerializationOptions options)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = options.PrettyPrint,
                DefaultIgnoreCondition = options.IncludeNulls
                    ? JsonIgnoreCondition.Never
                    : JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = options.UseCamelCase
                    ? JsonNamingPolicy.CamelCase
                    : null,
                UnknownTypeHandling = options.FailOnUnknownProperties
                    ? JsonUnknownTypeHandling.JsonElement
                    : JsonUnknownTypeHandling.JsonElement,
                MaxDepth = options.MaxDepth,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            // Add converters for common types
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            return jsonOptions;
        }

        private void ValidateInput<T>(T obj)
        {
            if (_serializationOptions.ValidateInput && obj == null)
            {
                throw new SerializationException(
                    "Input object cannot be null",
                    SerializationFormat.Json,
                    "validation");
            }
        }

        private void ValidateInputData(byte[] data)
        {
            if (_serializationOptions.ValidateInput && (data == null || data.Length == 0))
            {
                throw new SerializationException(
                    "Input data cannot be null or empty",
                    SerializationFormat.Json,
                    "validation");
            }
        }

        private void ValidateOutput(byte[] output)
        {
            if (_serializationOptions.ValidateOutput && (output == null || output.Length == 0))
            {
                throw new SerializationException(
                    "Serialization produced null or empty output",
                    SerializationFormat.Json,
                    "validation");
            }
        }

        private static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return data;
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
                return compressedData;
            }

            using var inputStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}