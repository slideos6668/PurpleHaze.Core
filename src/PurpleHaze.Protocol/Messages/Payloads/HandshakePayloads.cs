using System.Text.Json.Serialization;

namespace PurpleHaze.Protocol.Messages.Payloads;

public sealed class HandshakeRequest
{
    [JsonPropertyName("clientName")]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyName("clientVersion")]
    public string ClientVersion { get; set; } = "0.1.0";

    /// <summary>Shared pairing token established out-of-band (config file / manager UI).</summary>
    [JsonPropertyName("pairingToken")]
    public string PairingToken { get; set; } = string.Empty;

    [JsonPropertyName("supportedProtocolVersions")]
    public int[] SupportedProtocolVersions { get; set; } = { ProtocolConstants.ProtocolVersion };
}

public sealed class HandshakeAck
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("negotiatedProtocolVersion")]
    public int NegotiatedProtocolVersion { get; set; }

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = "PurpleHaze.Bridge";

    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = "0.1.0";

    [JsonPropertyName("heartbeatIntervalMs")]
    public int HeartbeatIntervalMs { get; set; } = ProtocolConstants.HeartbeatIntervalMs;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("rejectReason")]
    public string? RejectReason { get; set; }
}

public sealed class HeartbeatPayload
{
    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }
}

public sealed class ErrorPayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "error";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
