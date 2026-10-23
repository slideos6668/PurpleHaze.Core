namespace PurpleHaze.SDK.Capabilities;

/// <summary>
/// Describes a single capability (UI control or command) exposed by a plugin.
/// </summary>
public sealed class CapabilityDefinition
{
    /// <summary>
    /// Unique identifier within the plugin (e.g. "example.toggle").
    /// Combined with plugin ID forms a globally unique key.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable label shown in the manager UI.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Type of control.
    /// </summary>
    public required ControlType ControlType { get; init; }

    /// <summary>
    /// Optional longer description or tooltip.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Initial value (type depends on ControlType: bool for Toggle, double for Slider, string for Dropdown/TextInput).
    /// </summary>
    public object? InitialValue { get; init; }

    /// <summary>
    /// Minimum value for Slider.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value for Slider.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Step size for Slider.
    /// </summary>
    public double? Step { get; init; }

    /// <summary>
    /// Available options for Dropdown.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>
    /// Optional placeholder or validation regex for TextInput.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// Whether the control is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
