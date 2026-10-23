using System.Text.Json.Serialization;

namespace PurpleHaze.Protocol.Messages.Payloads;

public sealed class ActionExecutePayload
{
    [JsonPropertyName("capabilityId")]
    public string CapabilityId { get; set; } = string.Empty;

    [JsonPropertyName("argument")]
    public object? Argument { get; set; }
}

public sealed class ActionResultPayload
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class LogPayload
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "Information";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class EventPayload
{
    [JsonPropertyName("eventName")]
    public string EventName { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public sealed class PluginStatePayload
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
