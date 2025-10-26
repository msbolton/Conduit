using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Transports.Tcp
{
    /// <summary>
    /// Handles framing of messages over TCP streams.
    /// </summary>
    public class MessageFramer
    {
        private readonly FramingProtocol _protocol;
        private readonly int _maxMessageSize;
        private readonly byte[]? _customDelimiter;

        /// <summary>
        /// Initializes a new instance of the MessageFramer class.
        /// </summary>
        /// <param name="protocol">The framing protocol</param>
        /// <param name="maxMessageSize">The maximum message size</param>
        /// <param name="customDelimiter">The custom delimiter (for CustomDelimiter protocol)</param>
        public MessageFramer(FramingProtocol protocol, int maxMessageSize, byte[]? customDelimiter = null)
        {
            _protocol = protocol;
            _maxMessageSize = maxMessageSize;
            _customDelimiter = customDelimiter;

            if (protocol == FramingProtocol.CustomDelimiter && customDelimiter == null)
                throw new ArgumentException("Custom delimiter must be provided for CustomDelimiter protocol", nameof(customDelimiter));
        }

        /// <summary>
        /// Writes a framed message to a stream.
        /// </summary>
        /// <param name="stream">The network stream</param>
        /// <param name="data">The message data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task WriteMessageAsync(Stream stream, byte[] data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length > _maxMessageSize)
                throw new ArgumentException($"Message size {data.Length} exceeds maximum {_maxMessageSize}", nameof(data));

            switch (_protocol)
            {
                case FramingProtocol.LengthPrefixed:
                    await WriteLengthPrefixedAsync(stream, data, cancellationToken);
                    break;

                case FramingProtocol.NewlineDelimited:
                    await WriteDelimitedAsync(stream, data, Encoding.UTF8.GetBytes("\n"), cancellationToken);
                    break;

                case FramingProtocol.CrlfDelimited:
                    await WriteDelimitedAsync(stream, data, Encoding.UTF8.GetBytes("\r\n"), cancellationToken);
                    break;

                case FramingProtocol.CustomDelimiter:
                    await WriteDelimitedAsync(stream, data, _customDelimiter!, cancellationToken);
                    break;

                default:
                    throw new NotSupportedException($"Framing protocol {_protocol} is not supported");
            }

            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Reads a framed message from a stream.
        /// </summary>
        /// <param name="stream">The network stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The message data</returns>
        public async Task<byte[]> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            return _protocol switch
            {
                FramingProtocol.LengthPrefixed => await ReadLengthPrefixedAsync(stream, cancellationToken),
                FramingProtocol.NewlineDelimited => await ReadDelimitedAsync(stream, Encoding.UTF8.GetBytes("\n"), cancellationToken),
                FramingProtocol.CrlfDelimited => await ReadDelimitedAsync(stream, Encoding.UTF8.GetBytes("\r\n"), cancellationToken),
                FramingProtocol.CustomDelimiter => await ReadDelimitedAsync(stream, _customDelimiter!, cancellationToken),
                _ => throw new NotSupportedException($"Framing protocol {_protocol} is not supported")
            };
        }

        /// <summary>
        /// Writes a length-prefixed message.
        /// </summary>
        private async Task WriteLengthPrefixedAsync(Stream stream, byte[] data, CancellationToken cancellationToken)
        {
            // Write 4-byte length header (big-endian)
            var lengthBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            await stream.WriteAsync(lengthBytes, 0, 4, cancellationToken);

            // Write data
            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }

        /// <summary>
        /// Reads a length-prefixed message.
        /// </summary>
        private async Task<byte[]> ReadLengthPrefixedAsync(Stream stream, CancellationToken cancellationToken)
        {
            // Read 4-byte length header
            var lengthBytes = new byte[4];
            var bytesRead = 0;

            while (bytesRead < 4)
            {
                var read = await stream.ReadAsync(lengthBytes, bytesRead, 4 - bytesRead, cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException("Connection closed while reading length header");

                bytesRead += read;
            }

            // Convert to length (big-endian)
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);

            var length = BitConverter.ToInt32(lengthBytes, 0);

            if (length <= 0 || length > _maxMessageSize)
                throw new InvalidDataException($"Invalid message length: {length}");

            // Read message data
            var data = new byte[length];
            bytesRead = 0;

            while (bytesRead < length)
            {
                var read = await stream.ReadAsync(data, bytesRead, length - bytesRead, cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException("Connection closed while reading message data");

                bytesRead += read;
            }

            return data;
        }

        /// <summary>
        /// Writes a delimiter-terminated message.
        /// </summary>
        private async Task WriteDelimitedAsync(Stream stream, byte[] data, byte[] delimiter, CancellationToken cancellationToken)
        {
            // Check if data already contains delimiter
            if (ContainsDelimiter(data, delimiter))
                throw new ArgumentException("Message data contains delimiter sequence", nameof(data));

            // Write data
            await stream.WriteAsync(data, 0, data.Length, cancellationToken);

            // Write delimiter
            await stream.WriteAsync(delimiter, 0, delimiter.Length, cancellationToken);
        }

        /// <summary>
        /// Reads a delimiter-terminated message.
        /// </summary>
        private async Task<byte[]> ReadDelimitedAsync(Stream stream, byte[] delimiter, CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            var delimiterBuffer = new byte[delimiter.Length];
            var delimiterIndex = 0;

            try
            {
                while (true)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
                    if (bytesRead == 0)
                        throw new EndOfStreamException("Connection closed while reading delimited message");

                    var currentByte = buffer[0];

                    // Check if byte matches delimiter
                    if (currentByte == delimiter[delimiterIndex])
                    {
                        delimiterBuffer[delimiterIndex] = currentByte;
                        delimiterIndex++;

                        // Full delimiter found
                        if (delimiterIndex == delimiter.Length)
                        {
                            return memoryStream.ToArray();
                        }
                    }
                    else
                    {
                        // Not a match, write buffered delimiter bytes if any
                        if (delimiterIndex > 0)
                        {
                            memoryStream.Write(delimiterBuffer, 0, delimiterIndex);
                            delimiterIndex = 0;
                        }

                        // Write current byte
                        memoryStream.WriteByte(currentByte);

                        // Check message size limit
                        if (memoryStream.Length > _maxMessageSize)
                            throw new InvalidDataException($"Message exceeds maximum size {_maxMessageSize}");
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Checks if data contains delimiter sequence.
        /// </summary>
        private bool ContainsDelimiter(byte[] data, byte[] delimiter)
        {
            for (int i = 0; i <= data.Length - delimiter.Length; i++)
            {
                var match = true;
                for (int j = 0; j < delimiter.Length; j++)
                {
                    if (data[i + j] != delimiter[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }
    }
}
