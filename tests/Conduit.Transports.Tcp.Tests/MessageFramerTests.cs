using FluentAssertions;
using Conduit.Transports.Tcp;
using System.Text;

namespace Conduit.Transports.Tcp.Tests;

public class MessageFramerTests
{
    [Fact]
    public void MessageFramer_Constructor_WithLengthPrefixed_ShouldSucceed()
    {
        // Act
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);

        // Assert
        framer.Should().NotBeNull();
    }

    [Fact]
    public void MessageFramer_Constructor_WithNewlineDelimited_ShouldSucceed()
    {
        // Act
        var framer = new MessageFramer(FramingProtocol.NewlineDelimited, 1024);

        // Assert
        framer.Should().NotBeNull();
    }

    [Fact]
    public void MessageFramer_Constructor_WithCustomDelimiterButNoDelimiter_ShouldThrow()
    {
        // Act & Assert
        var act = () => new MessageFramer(FramingProtocol.CustomDelimiter, 1024);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("customDelimiter")
            .WithMessage("*Custom delimiter must be provided for CustomDelimiter protocol*");
    }

    [Fact]
    public void MessageFramer_Constructor_WithCustomDelimiterAndDelimiter_ShouldSucceed()
    {
        // Arrange
        var delimiter = Encoding.UTF8.GetBytes("|END|");

        // Act
        var framer = new MessageFramer(FramingProtocol.CustomDelimiter, 1024, delimiter);

        // Assert
        framer.Should().NotBeNull();
    }

    [Fact]
    public async Task MessageFramer_WriteMessageAsync_WithNullData_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        using var stream = new MemoryStream();

        // Act & Assert
        var act = async () => await framer.WriteMessageAsync(stream, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public async Task MessageFramer_WriteMessageAsync_WithOversizedData_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 100);
        using var stream = new MemoryStream();
        var data = new byte[200]; // Exceeds max size

        // Act & Assert
        var act = async () => await framer.WriteMessageAsync(stream, data);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("data")
            .WithMessage("*Message size 200 exceeds maximum 100*");
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_WriteAndRead_ShouldRoundTrip()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, originalData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read
        var receivedData = await framer.ReadMessageAsync(stream);

        // Assert
        receivedData.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task MessageFramer_NewlineDelimited_WriteAndRead_ShouldRoundTrip()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.NewlineDelimited, 1024);
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, originalData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read
        var receivedData = await framer.ReadMessageAsync(stream);

        // Assert
        receivedData.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task MessageFramer_CrlfDelimited_WriteAndRead_ShouldRoundTrip()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.CrlfDelimited, 1024);
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, originalData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read
        var receivedData = await framer.ReadMessageAsync(stream);

        // Assert
        receivedData.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task MessageFramer_CustomDelimited_WriteAndRead_ShouldRoundTrip()
    {
        // Arrange
        var delimiter = Encoding.UTF8.GetBytes("|END|");
        var framer = new MessageFramer(FramingProtocol.CustomDelimiter, 1024, delimiter);
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, originalData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read
        var receivedData = await framer.ReadMessageAsync(stream);

        // Assert
        receivedData.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public async Task MessageFramer_NewlineDelimited_WithDataContainingNewline_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.NewlineDelimited, 1024);
        var dataWithNewline = Encoding.UTF8.GetBytes("Hello\nWorld!");
        using var stream = new MemoryStream();

        // Act & Assert
        var act = async () => await framer.WriteMessageAsync(stream, dataWithNewline);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("data")
            .WithMessage("*Message data contains delimiter sequence*");
    }

    [Fact]
    public async Task MessageFramer_CustomDelimited_WithDataContainingDelimiter_ShouldThrow()
    {
        // Arrange
        var delimiter = Encoding.UTF8.GetBytes("|END|");
        var framer = new MessageFramer(FramingProtocol.CustomDelimiter, 1024, delimiter);
        var dataWithDelimiter = Encoding.UTF8.GetBytes("Hello|END|World!");
        using var stream = new MemoryStream();

        // Act & Assert
        var act = async () => await framer.WriteMessageAsync(stream, dataWithDelimiter);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("data")
            .WithMessage("*Message data contains delimiter sequence*");
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_WithEmptyData_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        var emptyData = Array.Empty<byte>();
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, emptyData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act & Assert - The implementation doesn't support zero-length messages
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Invalid message length: 0*");
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_WithLargeData_ShouldRoundTrip()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 10000);
        var largeData = new byte[5000];
        Random.Shared.NextBytes(largeData); // Fill with random data
        using var stream = new MemoryStream();

        // Act - Write
        await framer.WriteMessageAsync(stream, largeData);

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read
        var receivedData = await framer.ReadMessageAsync(stream);

        // Assert
        receivedData.Should().BeEquivalentTo(largeData);
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_ReadFromIncompleteStream_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        using var stream = new MemoryStream();

        // Write only partial length header (2 bytes instead of 4)
        stream.Write(new byte[] { 0x00, 0x10 }, 0, 2);
        stream.Position = 0;

        // Act & Assert
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<EndOfStreamException>()
            .WithMessage("*Connection closed while reading length header*");
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_ReadWithInvalidLength_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        using var stream = new MemoryStream();

        // Write length header indicating message too large
        var lengthBytes = BitConverter.GetBytes(2000); // Exceeds max size
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        stream.Write(lengthBytes, 0, 4);
        stream.Position = 0;

        // Act & Assert
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Invalid message length: 2000*");
    }

    [Fact]
    public async Task MessageFramer_LengthPrefixed_ReadWithNegativeLength_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        using var stream = new MemoryStream();

        // Write negative length header
        var lengthBytes = BitConverter.GetBytes(-1);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        stream.Write(lengthBytes, 0, 4);
        stream.Position = 0;

        // Act & Assert
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Invalid message length: -1*");
    }

    [Fact]
    public async Task MessageFramer_NewlineDelimited_ReadFromIncompleteStream_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.NewlineDelimited, 1024);
        using var stream = new MemoryStream();

        // Write data without delimiter, then close stream
        var data = Encoding.UTF8.GetBytes("Hello");
        stream.Write(data, 0, data.Length);
        stream.Position = 0;

        // Act & Assert
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<EndOfStreamException>()
            .WithMessage("*Connection closed while reading delimited message*");
    }

    [Fact]
    public async Task MessageFramer_NewlineDelimited_ReadOversizedMessage_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.NewlineDelimited, 10); // Small max size
        using var stream = new MemoryStream();

        // Write message larger than max size without delimiter
        var largeData = new byte[20];
        Array.Fill<byte>(largeData, (byte)'A');
        stream.Write(largeData, 0, largeData.Length);
        stream.Position = 0;

        // Act & Assert
        var act = async () => await framer.ReadMessageAsync(stream);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Message exceeds maximum size 10*");
    }

    [Fact]
    public async Task MessageFramer_UnsupportedProtocol_ShouldThrow()
    {
        // Arrange
        var framer = new MessageFramer((FramingProtocol)999, 1024); // Invalid protocol
        var data = Encoding.UTF8.GetBytes("test");
        using var stream = new MemoryStream();

        // Act & Assert
        var act = async () => await framer.WriteMessageAsync(stream, data);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Framing protocol 999 is not supported*");
    }

    [Fact]
    public async Task MessageFramer_MultipleMessages_ShouldRoundTripCorrectly()
    {
        // Arrange
        var framer = new MessageFramer(FramingProtocol.LengthPrefixed, 1024);
        var messages = new[]
        {
            Encoding.UTF8.GetBytes("First message"),
            Encoding.UTF8.GetBytes("Second message"),
            Encoding.UTF8.GetBytes("Third message")
        };
        using var stream = new MemoryStream();

        // Act - Write all messages
        foreach (var message in messages)
        {
            await framer.WriteMessageAsync(stream, message);
        }

        // Reset stream position for reading
        stream.Position = 0;

        // Act - Read all messages
        var receivedMessages = new List<byte[]>();
        for (int i = 0; i < messages.Length; i++)
        {
            var receivedData = await framer.ReadMessageAsync(stream);
            receivedMessages.Add(receivedData);
        }

        // Assert
        receivedMessages.Should().HaveCount(3);
        receivedMessages[0].Should().BeEquivalentTo(messages[0]);
        receivedMessages[1].Should().BeEquivalentTo(messages[1]);
        receivedMessages[2].Should().BeEquivalentTo(messages[2]);
    }
}