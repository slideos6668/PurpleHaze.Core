using PurpleHaze.Protocol.Messages.Payloads;
using PurpleHaze.SDK.Capabilities;

namespace PurpleHaze.Bridge.Hosting;

/// <summary>
/// Maps SDK capability definitions to protocol DTOs and back.
/// </summary>
public static class CapabilityMapper
{
    public static CapabilityDto ToDto(CapabilityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new CapabilityDto
        {
            Id = definition.Id,
            Label = definition.Label,
            ControlType = definition.ControlType.ToString(),
            Description = definition.Description,
            InitialValue = definition.InitialValue,
            Min = definition.Min,
            Max = definition.Max,
            Step = definition.Step,
            Options = definition.Options?.ToList(),
            Placeholder = definition.Placeholder,
            IsEnabled = definition.IsEnabled
        };
    }

    public static bool TryParseControlType(string? value, out ControlType controlType)
    {
        return Enum.TryParse(value, ignoreCase: true, out controlType);
    }
}
