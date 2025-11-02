using FluentAssertions;
using Conduit.Serialization;

namespace Conduit.Serialization.Tests;

public class SimpleSerializationTests
{
    public class TestMessage
    {
        public string? Name { get; set; }
        public int Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    [Fact]
    public void SerializationFormat_Enumeration_ShouldHaveCorrectCount()
    {
        // Act
        var values = Enum.GetValues<SerializationFormat>();

        // Assert
        values.Should().HaveCount(6);
        values.Should().Contain(SerializationFormat.Json);
        values.Should().Contain(SerializationFormat.Xml);
        values.Should().Contain(SerializationFormat.MessagePack);
        values.Should().Contain(SerializationFormat.Protobuf);
        values.Should().Contain(SerializationFormat.Avro);
        values.Should().Contain(SerializationFormat.Yaml);
    }

    [Theory]
    [InlineData(SerializationFormat.Json)]
    [InlineData(SerializationFormat.Xml)]
    [InlineData(SerializationFormat.MessagePack)]
    [InlineData(SerializationFormat.Protobuf)]
    [InlineData(SerializationFormat.Avro)]
    [InlineData(SerializationFormat.Yaml)]
    public void SerializationFormat_AllValues_ShouldBeValid(SerializationFormat format)
    {
        // Act & Assert - verify each format value is defined
        Enum.IsDefined(typeof(SerializationFormat), format).Should().BeTrue();
    }

    [Fact]
    public void SerializationFormat_ToString_ShouldReturnCorrectNames()
    {
        // Assert
        SerializationFormat.Json.ToString().Should().Be("Json");
        SerializationFormat.Xml.ToString().Should().Be("Xml");
        SerializationFormat.MessagePack.ToString().Should().Be("MessagePack");
        SerializationFormat.Protobuf.ToString().Should().Be("Protobuf");
        SerializationFormat.Avro.ToString().Should().Be("Avro");
        SerializationFormat.Yaml.ToString().Should().Be("Yaml");
    }

    [Fact]
    public void SerializationFormat_Parse_ShouldWorkCorrectly()
    {
        // Act & Assert
        Enum.Parse<SerializationFormat>("Json").Should().Be(SerializationFormat.Json);
        Enum.Parse<SerializationFormat>("Xml").Should().Be(SerializationFormat.Xml);
        Enum.Parse<SerializationFormat>("MessagePack").Should().Be(SerializationFormat.MessagePack);
        Enum.Parse<SerializationFormat>("Protobuf").Should().Be(SerializationFormat.Protobuf);
        Enum.Parse<SerializationFormat>("Avro").Should().Be(SerializationFormat.Avro);
        Enum.Parse<SerializationFormat>("Yaml").Should().Be(SerializationFormat.Yaml);
    }

    [Fact]
    public void TestMessage_Creation_ShouldWork()
    {
        // Act
        var message = new TestMessage
        {
            Name = "Test",
            Value = 42,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.Should().NotBeNull();
        message.Name.Should().Be("Test");
        message.Value.Should().Be(42);
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("json", "JSON")]
    [InlineData("xml", "XML")]
    [InlineData("msgpack", "MessagePack")]
    [InlineData("proto", "Protocol Buffers")]
    [InlineData("avro", "Apache Avro")]
    [InlineData("yaml", "YAML")]
    public void SerializationFormat_Extensions_DescriptionShouldContainExpectedText(string extension, string expectedInDescription)
    {
        // Act
        var format = SerializationFormatExtensions.FromExtension(extension);
        var description = format?.GetDescription();

        // Assert
        format.Should().NotBeNull();
        description.Should().NotBeNullOrEmpty();
        description.Should().ContainEquivalentOf(expectedInDescription);
    }

    [Fact]
    public void SerializationFormat_BinaryVsText_ShouldBeCorrectlyClassified()
    {
        // Assert - exactly 3 text formats
        var textFormats = Enum.GetValues<SerializationFormat>()
            .Where(f => f.IsText())
            .ToArray();
        textFormats.Should().HaveCount(3);

        // Assert - exactly 3 binary formats
        var binaryFormats = Enum.GetValues<SerializationFormat>()
            .Where(f => f.IsBinary())
            .ToArray();
        binaryFormats.Should().HaveCount(3);

        // Assert - no overlap
        textFormats.Intersect(binaryFormats).Should().BeEmpty();
    }

    [Fact]
    public void SerializationFormat_MimeTypes_ShouldBeUnique()
    {
        // Act
        var mimeTypes = Enum.GetValues<SerializationFormat>()
            .Select(f => f.GetMimeType())
            .ToArray();

        // Assert
        mimeTypes.Should().OnlyHaveUniqueItems();
        mimeTypes.Should().AllSatisfy(mt => mt.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void SerializationFormat_Extensions_ShouldBeUnique()
    {
        // Act
        var extensions = Enum.GetValues<SerializationFormat>()
            .Select(f => f.GetExtension())
            .ToArray();

        // Assert
        extensions.Should().OnlyHaveUniqueItems();
        extensions.Should().AllSatisfy(ext => ext.Should().NotBeNullOrEmpty());
    }
}