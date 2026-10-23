namespace PurpleHaze.SDK.Capabilities;

/// <summary>
/// Allows a plugin to register, update, and unregister capabilities.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Registers a new capability. Throws if the ID is already registered by this plugin.
    /// </summary>
    void Register(CapabilityDefinition definition);

    /// <summary>
    /// Updates an existing capability (value, enabled state, options, etc.).
    /// </summary>
    void Update(string capabilityId, Action<CapabilityDefinitionBuilder> configure);

    /// <summary>
    /// Unregisters a capability.
    /// </summary>
    void Unregister(string capabilityId);

    /// <summary>
    /// Returns all capabilities currently registered by this plugin.
    /// </summary>
    IReadOnlyList<CapabilityDefinition> GetAll();
}

/// <summary>
/// Fluent builder used when updating a capability.
/// </summary>
public sealed class CapabilityDefinitionBuilder
{
    public CapabilityDefinition Current { get; private set; }

    public CapabilityDefinitionBuilder(CapabilityDefinition current)
    {
        Current = current;
    }

    public CapabilityDefinitionBuilder WithLabel(string label)
    {
        Current = Clone(label: label);
        return this;
    }

    public CapabilityDefinitionBuilder WithValue(object? value)
    {
        Current = Clone(initialValue: value);
        return this;
    }

    public CapabilityDefinitionBuilder WithEnabled(bool enabled)
    {
        Current = Clone(isEnabled: enabled);
        return this;
    }

    public CapabilityDefinitionBuilder WithOptions(IReadOnlyList<string> options)
    {
        Current = Clone(options: options);
        return this;
    }

    private CapabilityDefinition Clone(
        string? label = null,
        object? initialValue = null,
        bool? isEnabled = null,
        IReadOnlyList<string>? options = null)
    {
        return new CapabilityDefinition
        {
            Id = Current.Id,
            Label = label ?? Current.Label,
            ControlType = Current.ControlType,
            Description = Current.Description,
            InitialValue = initialValue ?? Current.InitialValue,
            Min = Current.Min,
            Max = Current.Max,
            Step = Current.Step,
            Options = options ?? Current.Options,
            Placeholder = Current.Placeholder,
            IsEnabled = isEnabled ?? Current.IsEnabled
        };
    }
}
