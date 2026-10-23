namespace PurpleHaze.SDK.Configuration;

/// <summary>
/// Key-value configuration scoped to a single plugin.
/// Values are persisted by the host when possible.
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Gets a string value or the supplied default.
    /// </summary>
    string GetString(string key, string defaultValue = "");

    /// <summary>
    /// Gets a boolean value or the supplied default.
    /// </summary>
    bool GetBool(string key, bool defaultValue = false);

    /// <summary>
    /// Gets an integer value or the supplied default.
    /// </summary>
    int GetInt(string key, int defaultValue = 0);

    /// <summary>
    /// Gets a double value or the supplied default.
    /// </summary>
    double GetDouble(string key, double defaultValue = 0.0);

    /// <summary>
    /// Sets a value. Persisted asynchronously by the host.
    /// </summary>
    void Set(string key, object value);

    /// <summary>
    /// Returns true if the key exists.
    /// </summary>
    bool ContainsKey(string key);
}
