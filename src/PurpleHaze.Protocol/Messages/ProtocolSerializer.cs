using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurpleHaze.Protocol.Messages;

/// <summary>
/// Length-prefixed JSON framing over a stream.
/// Frame layout: 4-byte big-endian length + UTF-8 JSON body.
/// </summary>
public static class ProtocolSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static byte[] Serialize(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var json = JsonSerializer.SerializeToUtf8Bytes(message, Options);
        if (json.Length > ProtocolConstants.MaxMessageSizeBytes)
            throw new InvalidOperationException(
                $"Message size {json.Length} exceeds limit of {ProtocolConstants.MaxMessageSizeBytes} bytes.");

        var frame = new byte[4 + json.Length];
        // big-endian length
        frame[0] = (byte)((json.Length >> 24) & 0xFF);
        frame[1] = (byte)((json.Length >> 16) & 0xFF);
        frame[2] = (byte)((json.Length >> 8) & 0xFF);
        frame[3] = (byte)(json.Length & 0xFF);
        Buffer.BlockCopy(json, 0, frame, 4, json.Length);
        return frame;
    }

    public static async Task WriteAsync(Stream stream, ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        var frame = Serialize(message);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ProtocolMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var lengthBuf = new byte[4];
        if (!await ReadExactAsync(stream, lengthBuf, cancellationToken).ConfigureAwait(false))
            return null; // EOF

        var length = (lengthBuf[0] << 24) | (lengthBuf[1] << 16) | (lengthBuf[2] << 8) | lengthBuf[3];
        if (length <= 0 || length > ProtocolConstants.MaxMessageSizeBytes)
            throw new InvalidDataException($"Invalid message length: {length}");

        var body = new byte[length];
        if (!await ReadExactAsync(stream, body, cancellationToken).ConfigureAwait(false))
            throw new EndOfStreamException("Unexpected EOF while reading message body.");

        var message = JsonSerializer.Deserialize<ProtocolMessage>(body, Options);
        return message ?? throw new InvalidDataException("Deserialized message was null.");
    }

    public static ProtocolMessage DeserializeJson(string json)
    {
        var message = JsonSerializer.Deserialize<ProtocolMessage>(json, Options);
        return message ?? throw new InvalidDataException("Deserialized message was null.");
    }

    public static string ToJson(ProtocolMessage message) =>
        JsonSerializer.Serialize(message, Options);

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                return false; // EOF before the buffer was filled
            offset += read;
        }
        return true;
    }
}
