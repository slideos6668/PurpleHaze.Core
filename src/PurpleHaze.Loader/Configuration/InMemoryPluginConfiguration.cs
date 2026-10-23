using PurpleHaze.SDK.Configuration;

namespace PurpleHaze.Loader.Configuration;

/// <summary>
/// In-memory configuration store scoped to one plugin. Persisted config is out of scope for M2.
/// </summary>
public sealed class InMemoryPluginConfiguration : IPluginConfiguration
{
    private readonly Dictionary<string, object> _store = new(StringComparer.OrdinalIgnoreCase);

    public string GetString(string key, string defaultValue = "") =>
        _store.TryGetValue(key, out var v) ? v?.ToString() ?? defaultValue : defaultValue;

    public bool GetBool(string key, bool defaultValue = false) =>
        _store.TryGetValue(key, out var v) && v is bool b ? b : defaultValue;

    public int GetInt(string key, int defaultValue = 0) =>
        _store.TryGetValue(key, out var v) && v is int i ? i : defaultValue;

    public double GetDouble(string key, double defaultValue = 0.0) =>
        _store.TryGetValue(key, out var v) && v is double d ? d : defaultValue;

    public void Set(string key, object value) => _store[key] = value;

    public bool ContainsKey(string key) => _store.ContainsKey(key);
}
