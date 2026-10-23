using System.Text.Json.Serialization;

namespace PurpleHaze.Protocol.Messages.Payloads;

public sealed class CapabilityRegisterPayload
{
    [JsonPropertyName("capability")]
    public CapabilityDto Capability { get; set; } = new();
}

public sealed class CapabilityUpdatePayload
{
    [JsonPropertyName("capabilityId")]
    public string CapabilityId { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class CapabilityUnregisterPayload
{
    [JsonPropertyName("capabilityId")]
    public string CapabilityId { get; set; } = string.Empty;
}

public sealed class CapabilityDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("controlType")]
    public string ControlType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("initialValue")]
    public object? InitialValue { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("step")]
    public double? Step { get; set; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}
