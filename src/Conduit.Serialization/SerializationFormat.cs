using System;
using System.Collections.Generic;
using System.Linq;

namespace Conduit.Serialization
{
    /// <summary>
    /// Enumeration of supported serialization formats.
    /// </summary>
    public enum SerializationFormat
    {
        /// <summary>
        /// JavaScript Object Notation (JSON).
        /// </summary>
        Json,

        /// <summary>
        /// Extensible Markup Language (XML).
        /// </summary>
        Xml,

        /// <summary>
        /// MessagePack binary format.
        /// </summary>
        MessagePack,

        /// <summary>
        /// Protocol Buffers (protobuf) for gRPC.
        /// </summary>
        Protobuf,

        /// <summary>
        /// Apache Avro binary format.
        /// </summary>
        Avro,

        /// <summary>
        /// YAML Ain't Markup Language.
        /// </summary>
        Yaml
    }

    /// <summary>
    /// Extension methods for SerializationFormat.
    /// </summary>
    public static class SerializationFormatExtensions
    {
        private static readonly Dictionary<SerializationFormat, FormatInfo> FormatInfoMap = new()
        {
            [SerializationFormat.Json] = new FormatInfo
            {
                Extension = "json",
                MimeType = "application/json",
                Description = "JSON serialization format",
                IsBinary = false
            },
            [SerializationFormat.Xml] = new FormatInfo
            {
                Extension = "xml",
                MimeType = "application/xml",
                Description = "XML serialization format",
                IsBinary = false
            },
            [SerializationFormat.MessagePack] = new FormatInfo
            {
                Extension = "msgpack",
                MimeType = "application/x-msgpack",
                Description = "MessagePack binary format",
                IsBinary = true
            },
            [SerializationFormat.Protobuf] = new FormatInfo
            {
                Extension = "proto",
                MimeType = "application/x-protobuf",
                Description = "Protocol Buffers binary format",
                IsBinary = true
            },
            [SerializationFormat.Avro] = new FormatInfo
            {
                Extension = "avro",
                MimeType = "application/avro",
                Description = "Apache Avro binary format",
                IsBinary = true
            },
            [SerializationFormat.Yaml] = new FormatInfo
            {
                Extension = "yaml",
                MimeType = "application/x-yaml",
                Description = "YAML serialization format",
                IsBinary = false
            }
        };

        /// <summary>
        /// Gets the file extension for this format.
        /// </summary>
        public static string GetExtension(this SerializationFormat format)
        {
            return FormatInfoMap[format].Extension;
        }

        /// <summary>
        /// Gets the MIME type for this format.
        /// </summary>
        public static string GetMimeType(this SerializationFormat format)
        {
            return FormatInfoMap[format].MimeType;
        }

        /// <summary>
        /// Gets the description of this format.
        /// </summary>
        public static string GetDescription(this SerializationFormat format)
        {
            return FormatInfoMap[format].Description;
        }

        /// <summary>
        /// Checks if this format is binary.
        /// </summary>
        public static bool IsBinary(this SerializationFormat format)
        {
            return FormatInfoMap[format].IsBinary;
        }

        /// <summary>
        /// Checks if this format is text-based.
        /// </summary>
        public static bool IsText(this SerializationFormat format)
        {
            return !IsBinary(format);
        }

        /// <summary>
        /// Gets a format by its file extension.
        /// </summary>
        public static SerializationFormat? FromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }

            var cleanExtension = extension.StartsWith(".") ? extension.Substring(1) : extension;

            return FormatInfoMap
                .FirstOrDefault(kvp => kvp.Value.Extension.Equals(cleanExtension, StringComparison.OrdinalIgnoreCase))
                .Key;
        }

        /// <summary>
        /// Gets a format by its MIME type.
        /// </summary>
        public static SerializationFormat? FromMimeType(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                return null;
            }

            return FormatInfoMap
                .FirstOrDefault(kvp => kvp.Value.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                .Key;
        }

        /// <summary>
        /// Gets a format by its name (case-insensitive).
        /// </summary>
        public static SerializationFormat? FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Enum.TryParse<SerializationFormat>(name, ignoreCase: true, out var format))
            {
                return format;
            }

            return null;
        }

        private class FormatInfo
        {
            public string Extension { get; init; } = "";
            public string MimeType { get; init; } = "";
            public string Description { get; init; } = "";
            public bool IsBinary { get; init; }
        }
    }
}