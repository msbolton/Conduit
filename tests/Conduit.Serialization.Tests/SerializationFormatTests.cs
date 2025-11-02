using FluentAssertions;
using Conduit.Serialization;

namespace Conduit.Serialization.Tests;

public class SerializationFormatTests
{
    [Fact]
    public void SerializationFormat_Enumeration_ShouldHaveCorrectValues()
    {
        // Assert - verify all expected formats exist
        SerializationFormat.Json.Should().Be(SerializationFormat.Json);
        SerializationFormat.Xml.Should().Be(SerializationFormat.Xml);
        SerializationFormat.MessagePack.Should().Be(SerializationFormat.MessagePack);
        SerializationFormat.Protobuf.Should().Be(SerializationFormat.Protobuf);
        SerializationFormat.Avro.Should().Be(SerializationFormat.Avro);
        SerializationFormat.Yaml.Should().Be(SerializationFormat.Yaml);
    }

    [Fact]
    public void SerializationFormat_Extensions_GetExtension_ShouldReturnCorrectValues()
    {
        // Assert
        SerializationFormat.Json.GetExtension().Should().Be("json");
        SerializationFormat.Xml.GetExtension().Should().Be("xml");
        SerializationFormat.MessagePack.GetExtension().Should().Be("msgpack");
        SerializationFormat.Protobuf.GetExtension().Should().Be("proto");
        SerializationFormat.Avro.GetExtension().Should().Be("avro");
        SerializationFormat.Yaml.GetExtension().Should().Be("yaml");
    }

    [Fact]
    public void SerializationFormat_Extensions_GetMimeType_ShouldReturnCorrectValues()
    {
        // Assert
        SerializationFormat.Json.GetMimeType().Should().Be("application/json");
        SerializationFormat.Xml.GetMimeType().Should().Be("application/xml");
        SerializationFormat.MessagePack.GetMimeType().Should().Be("application/x-msgpack");
        SerializationFormat.Protobuf.GetMimeType().Should().Be("application/x-protobuf");
        SerializationFormat.Avro.GetMimeType().Should().Be("application/avro");
        SerializationFormat.Yaml.GetMimeType().Should().Be("application/x-yaml");
    }

    [Fact]
    public void SerializationFormat_Extensions_IsBinary_ShouldReturnCorrectValues()
    {
        // Assert - text formats
        SerializationFormat.Json.IsBinary().Should().BeFalse();
        SerializationFormat.Xml.IsBinary().Should().BeFalse();
        SerializationFormat.Yaml.IsBinary().Should().BeFalse();

        // Assert - binary formats
        SerializationFormat.MessagePack.IsBinary().Should().BeTrue();
        SerializationFormat.Protobuf.IsBinary().Should().BeTrue();
        SerializationFormat.Avro.IsBinary().Should().BeTrue();
    }

    [Fact]
    public void SerializationFormat_Extensions_IsText_ShouldReturnCorrectValues()
    {
        // Assert - text formats
        SerializationFormat.Json.IsText().Should().BeTrue();
        SerializationFormat.Xml.IsText().Should().BeTrue();
        SerializationFormat.Yaml.IsText().Should().BeTrue();

        // Assert - binary formats
        SerializationFormat.MessagePack.IsText().Should().BeFalse();
        SerializationFormat.Protobuf.IsText().Should().BeFalse();
        SerializationFormat.Avro.IsText().Should().BeFalse();
    }

    [Fact]
    public void SerializationFormat_Extensions_GetDescription_ShouldReturnValidDescriptions()
    {
        // Assert
        SerializationFormat.Json.GetDescription().Should().NotBeNullOrEmpty();
        SerializationFormat.Xml.GetDescription().Should().NotBeNullOrEmpty();
        SerializationFormat.MessagePack.GetDescription().Should().NotBeNullOrEmpty();
        SerializationFormat.Protobuf.GetDescription().Should().NotBeNullOrEmpty();
        SerializationFormat.Avro.GetDescription().Should().NotBeNullOrEmpty();
        SerializationFormat.Yaml.GetDescription().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("json", SerializationFormat.Json)]
    [InlineData("xml", SerializationFormat.Xml)]
    [InlineData("msgpack", SerializationFormat.MessagePack)]
    [InlineData("proto", SerializationFormat.Protobuf)]
    [InlineData("avro", SerializationFormat.Avro)]
    [InlineData("yaml", SerializationFormat.Yaml)]
    public void SerializationFormat_FromExtension_ShouldReturnCorrectFormat(string extension, SerializationFormat expected)
    {
        // Act
        var result = SerializationFormatExtensions.FromExtension(extension);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".json", SerializationFormat.Json)]
    [InlineData(".xml", SerializationFormat.Xml)]
    [InlineData(".msgpack", SerializationFormat.MessagePack)]
    public void SerializationFormat_FromExtension_WithDot_ShouldReturnCorrectFormat(string extension, SerializationFormat expected)
    {
        // Act
        var result = SerializationFormatExtensions.FromExtension(extension);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("application/json", SerializationFormat.Json)]
    [InlineData("application/xml", SerializationFormat.Xml)]
    [InlineData("application/x-msgpack", SerializationFormat.MessagePack)]
    [InlineData("application/x-protobuf", SerializationFormat.Protobuf)]
    [InlineData("application/avro", SerializationFormat.Avro)]
    [InlineData("application/x-yaml", SerializationFormat.Yaml)]
    public void SerializationFormat_FromMimeType_ShouldReturnCorrectFormat(string mimeType, SerializationFormat expected)
    {
        // Act
        var result = SerializationFormatExtensions.FromMimeType(mimeType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("JSON", SerializationFormat.Json)]
    [InlineData("xml", SerializationFormat.Xml)]
    [InlineData("MessagePack", SerializationFormat.MessagePack)]
    [InlineData("PROTOBUF", SerializationFormat.Protobuf)]
    public void SerializationFormat_FromName_ShouldReturnCorrectFormat(string name, SerializationFormat expected)
    {
        // Act
        var result = SerializationFormatExtensions.FromName(name);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void SerializationFormat_FromExtension_WithEmptyInput_ShouldReturnNull(string? input)
    {
        // Act
        var result = SerializationFormatExtensions.FromExtension(input!);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("unknown")]
    public void SerializationFormat_FromExtension_WithInvalidInput_ShouldReturnDefault(string input)
    {
        // Act
        var result = SerializationFormatExtensions.FromExtension(input);

        // Assert - FirstOrDefault returns the default enum value when no match found
        result.Should().Be(default(SerializationFormat));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void SerializationFormat_FromMimeType_WithEmptyInput_ShouldReturnNull(string? input)
    {
        // Act
        var result = SerializationFormatExtensions.FromMimeType(input!);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("invalid/type")]
    [InlineData("application/unknown")]
    public void SerializationFormat_FromMimeType_WithInvalidInput_ShouldReturnDefault(string input)
    {
        // Act
        var result = SerializationFormatExtensions.FromMimeType(input);

        // Assert - FirstOrDefault returns the default enum value when no match found
        result.Should().Be(default(SerializationFormat));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("unknown")]
    public void SerializationFormat_FromName_WithInvalidInput_ShouldReturnNull(string? input)
    {
        // Act
        var result = SerializationFormatExtensions.FromName(input!);

        // Assert
        result.Should().BeNull();
    }
}