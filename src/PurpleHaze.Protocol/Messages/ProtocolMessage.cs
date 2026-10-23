using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurpleHaze.Protocol.Messages;

/// <summary>
/// Envelope for every message exchanged over the bridge.
/// Payload is stored as <see cref="JsonElement"/> so it can be re-deserialized
/// to a strongly typed DTO after the message type is known.
/// </summary>
public sealed class ProtocolMessage
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Unique request identifier. Responses echo the same ID.
    /// Notifications (events, logs, heartbeats) may leave this null.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("pluginId")]
    public string? PluginId { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    public static ProtocolMessage Create(string messageType, object? payload = null, string? requestId = null, string? pluginId = null)
    {
        JsonElement? element = null;
        if (payload is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, ProtocolSerializer.Options);
            element = JsonSerializer.Deserialize<JsonElement>(bytes, ProtocolSerializer.Options);
        }

        return new ProtocolMessage
        {
            ProtocolVersion = ProtocolConstants.ProtocolVersion,
            MessageType = messageType,
            RequestId = requestId,
            PluginId = pluginId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = element
        };
    }

    public T? GetPayload<T>()
    {
        if (Payload is null || Payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;
        return Payload.Value.Deserialize<T>(ProtocolSerializer.Options);
    }
}
