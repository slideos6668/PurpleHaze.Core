using PurpleHaze.SDK.Capabilities;

namespace PurpleHaze.Loader.Capabilities;

/// <summary>
/// Per-plugin capability registry used by the host context.
/// </summary>
public sealed class PluginCapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDefinition> _caps = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event Action<CapabilityDefinition>? CapabilityRegistered;
    public event Action<string, CapabilityDefinition>? CapabilityUpdated;
    public event Action<string>? CapabilityUnregistered;

    public void Register(CapabilityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new ArgumentException("Capability ID is required.", nameof(definition));

        lock (_lock)
        {
            if (_caps.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Capability '{definition.Id}' is already registered.");
            _caps[definition.Id] = definition;
        }
        CapabilityRegistered?.Invoke(definition);
    }

    public void Update(string capabilityId, Action<CapabilityDefinitionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        CapabilityDefinition updated;
        lock (_lock)
        {
            if (!_caps.TryGetValue(capabilityId, out var current))
                throw new KeyNotFoundException($"Capability '{capabilityId}' is not registered.");
            var builder = new CapabilityDefinitionBuilder(current);
            configure(builder);
            updated = builder.Current;
            _caps[capabilityId] = updated;
        }
        CapabilityUpdated?.Invoke(capabilityId, updated);
    }

    public void Unregister(string capabilityId)
    {
        lock (_lock)
        {
            if (!_caps.Remove(capabilityId))
                return;
        }
        CapabilityUnregistered?.Invoke(capabilityId);
    }

    public IReadOnlyList<CapabilityDefinition> GetAll()
    {
        lock (_lock) return _caps.Values.ToList();
    }
}
