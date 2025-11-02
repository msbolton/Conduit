using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Serialization
{
    /// <summary>
    /// Registry for managing and selecting message serializers.
    /// </summary>
    public class SerializerRegistry : ISerializerRegistry
    {
        private readonly ConcurrentDictionary<SerializationFormat, IMessageSerializer> _serializers;
        private readonly ConcurrentDictionary<string, SerializationFormat> _mimeTypeMap;
        private readonly ILogger? _logger;
        private SerializationFormat _defaultFormat;

        /// <summary>
        /// Gets the default serialization format.
        /// </summary>
        public SerializationFormat DefaultFormat
        {
            get => _defaultFormat;
            set
            {
                if (!_serializers.ContainsKey(value))
                {
                    throw new InvalidOperationException(
                        $"Cannot set default format to {value} - no serializer registered for this format");
                }
                _defaultFormat = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the SerializerRegistry class.
        /// </summary>
        public SerializerRegistry(ILogger<SerializerRegistry>? logger = null)
        {
            _serializers = new ConcurrentDictionary<SerializationFormat, IMessageSerializer>();
            _mimeTypeMap = new ConcurrentDictionary<string, SerializationFormat>();
            _logger = logger;
            _defaultFormat = SerializationFormat.Json;

            // Register default serializers
            RegisterDefaults();
        }

        /// <summary>
        /// Registers a serializer for a specific format.
        /// </summary>
        public void Register(IMessageSerializer serializer)
        {
            Guard.AgainstNull(serializer, nameof(serializer));

            if (_serializers.TryAdd(serializer.Format, serializer))
            {
                // Register MIME type mapping
                _mimeTypeMap[serializer.MimeType] = serializer.Format;

                _logger?.LogInformation("Registered serializer for format {Format} with MIME type {MimeType}",
                    serializer.Format, serializer.MimeType);
            }
            else
            {
                _logger?.LogWarning("Serializer for format {Format} already registered, replacing",
                    serializer.Format);
                _serializers[serializer.Format] = serializer;
                _mimeTypeMap[serializer.MimeType] = serializer.Format;
            }
        }

        /// <summary>
        /// Registers a serializer for a specific format.
        /// </summary>
        public void Register(SerializationFormat format, IMessageSerializer serializer)
        {
            Guard.AgainstNull(serializer, nameof(serializer));

            _serializers[format] = serializer;
            _mimeTypeMap[serializer.MimeType] = format;

            _logger?.LogInformation("Registered serializer for format {Format}", format);
        }

        /// <summary>
        /// Unregisters a serializer for a specific format.
        /// </summary>
        public bool Unregister(SerializationFormat format)
        {
            if (_serializers.TryRemove(format, out var serializer))
            {
                _mimeTypeMap.TryRemove(serializer.MimeType, out _);
                _logger?.LogInformation("Unregistered serializer for format {Format}", format);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a serializer for the specified format.
        /// </summary>
        public IMessageSerializer? GetSerializer(SerializationFormat format)
        {
            return _serializers.TryGetValue(format, out var serializer) ? serializer : null;
        }

        /// <summary>
        /// Gets a serializer for the specified MIME type.
        /// </summary>
        public IMessageSerializer? GetSerializer(string mimeType)
        {
            Guard.AgainstNullOrEmpty(mimeType, nameof(mimeType));

            if (_mimeTypeMap.TryGetValue(mimeType, out var format))
            {
                return GetSerializer(format);
            }

            // Try to find by MIME type support
            return _serializers.Values.FirstOrDefault(s => s.Supports(mimeType));
        }

        /// <summary>
        /// Gets the default serializer.
        /// </summary>
        public IMessageSerializer GetDefaultSerializer()
        {
            var serializer = GetSerializer(_defaultFormat);
            if (serializer == null)
            {
                throw new InvalidOperationException(
                    $"No serializer registered for default format {_defaultFormat}");
            }

            return serializer;
        }

        /// <summary>
        /// Gets a serializer by format, or the default if not found.
        /// </summary>
        public IMessageSerializer GetOrDefault(SerializationFormat format)
        {
            return GetSerializer(format) ?? GetDefaultSerializer();
        }

        /// <summary>
        /// Gets a serializer by MIME type, or the default if not found.
        /// </summary>
        public IMessageSerializer GetOrDefault(string mimeType)
        {
            return GetSerializer(mimeType) ?? GetDefaultSerializer();
        }

        /// <summary>
        /// Checks if a serializer is registered for the specified format.
        /// </summary>
        public bool IsRegistered(SerializationFormat format)
        {
            return _serializers.ContainsKey(format);
        }

        /// <summary>
        /// Checks if a serializer is registered for the specified MIME type.
        /// </summary>
        public bool IsRegistered(string mimeType)
        {
            return _mimeTypeMap.ContainsKey(mimeType) ||
                   _serializers.Values.Any(s => s.Supports(mimeType));
        }

        /// <summary>
        /// Gets all registered formats.
        /// </summary>
        public IEnumerable<SerializationFormat> GetRegisteredFormats()
        {
            return _serializers.Keys;
        }

        /// <summary>
        /// Gets all registered serializers.
        /// </summary>
        public IEnumerable<IMessageSerializer> GetAllSerializers()
        {
            return _serializers.Values;
        }

        /// <summary>
        /// Clears all registered serializers.
        /// </summary>
        public void Clear()
        {
            _serializers.Clear();
            _mimeTypeMap.Clear();
            _logger?.LogInformation("Cleared all registered serializers");
        }

        /// <summary>
        /// Detects the format from byte data.
        /// </summary>
        public SerializationFormat? DetectFormat(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            foreach (var serializer in _serializers.Values)
            {
                if (serializer.IsValid(data))
                {
                    return serializer.Format;
                }
            }

            return null;
        }

        /// <summary>
        /// Detects the format from a file extension.
        /// </summary>
        public SerializationFormat? DetectFormatFromExtension(string extension)
        {
            return SerializationFormatExtensions.FromExtension(extension);
        }

        /// <summary>
        /// Detects the format from a MIME type.
        /// </summary>
        public SerializationFormat? DetectFormatFromMimeType(string mimeType)
        {
            if (_mimeTypeMap.TryGetValue(mimeType, out var format))
            {
                return format;
            }

            return SerializationFormatExtensions.FromMimeType(mimeType);
        }

        /// <summary>
        /// Gets statistics about registered serializers.
        /// </summary>
        public SerializerRegistryStatistics GetStatistics()
        {
            return new SerializerRegistryStatistics
            {
                RegisteredFormats = _serializers.Count,
                DefaultFormat = _defaultFormat,
                Formats = _serializers.Keys.ToList(),
                MimeTypes = _mimeTypeMap.Keys.ToList()
            };
        }

        private void RegisterDefaults()
        {
            // Register JSON serializer by default
            Register(new JsonMessageSerializer());

            // Register MessagePack serializer
            Register(new MessagePackSerializer());

            _logger?.LogInformation("Registered default serializers: JSON, MessagePack");
        }
    }

    /// <summary>
    /// Statistics about the serializer registry.
    /// </summary>
    public class SerializerRegistryStatistics
    {
        public int RegisteredFormats { get; set; }
        public SerializationFormat DefaultFormat { get; set; }
        public List<SerializationFormat> Formats { get; set; } = new();
        public List<string> MimeTypes { get; set; } = new();
    }

    /// <summary>
    /// Content type negotiation helper.
    /// </summary>
    public static class ContentNegotiation
    {
        /// <summary>
        /// Selects the best serialization format based on Accept header.
        /// </summary>
        public static SerializationFormat SelectFormat(
            string acceptHeader,
            SerializerRegistry registry,
            SerializationFormat defaultFormat = SerializationFormat.Json)
        {
            if (string.IsNullOrWhiteSpace(acceptHeader))
            {
                return defaultFormat;
            }

            // Parse Accept header (simplified - doesn't handle q values)
            var mimeTypes = acceptHeader
                .Split(',')
                .Select(mt => mt.Trim().Split(';')[0])
                .Where(mt => !string.IsNullOrWhiteSpace(mt));

            foreach (var mimeType in mimeTypes)
            {
                var format = registry.DetectFormatFromMimeType(mimeType);
                if (format.HasValue && registry.IsRegistered(format.Value))
                {
                    return format.Value;
                }
            }

            return defaultFormat;
        }

        /// <summary>
        /// Parses Content-Type header to get serialization format.
        /// </summary>
        public static SerializationFormat? ParseContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }

            // Extract MIME type (ignore charset and other parameters)
            var mimeType = contentType.Split(';')[0].Trim();
            return SerializationFormatExtensions.FromMimeType(mimeType);
        }
    }
}