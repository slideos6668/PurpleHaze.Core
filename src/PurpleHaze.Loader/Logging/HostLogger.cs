using PurpleHaze.SDK.Logging;

namespace PurpleHaze.Loader.Logging;

/// <summary>
/// Simple structured host logger. Writes to an optional sink and keeps an in-memory ring buffer
/// for diagnostics. Plugins receive a scoped <see cref="PluginScopedLogger"/>.
/// </summary>
public sealed class HostLogger
{
    private readonly object _lock = new();
    private readonly List<LogEntry> _entries = new();
    private readonly int _maxEntries;
    private readonly Action<LogEntry>? _sink;

    public HostLogger(int maxEntries = 2000, Action<LogEntry>? sink = null)
    {
        _maxEntries = Math.Max(100, maxEntries);
        _sink = sink;
    }

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public void Log(LogLevel level, string pluginId, string message, Exception? exception = null)
    {
        var entry = new LogEntry(DateTimeOffset.UtcNow, level, pluginId, message, exception?.ToString());
        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > _maxEntries)
                _entries.RemoveRange(0, _entries.Count - _maxEntries);
        }
        _sink?.Invoke(entry);
    }

    public IPluginLogger ForPlugin(string pluginId) => new PluginScopedLogger(this, pluginId);
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string PluginId,
    string Message,
    string? Exception);

internal sealed class PluginScopedLogger : IPluginLogger
{
    private readonly HostLogger _host;
    private readonly string _pluginId;

    public PluginScopedLogger(HostLogger host, string pluginId)
    {
        _host = host;
        _pluginId = pluginId;
    }

    public void Trace(string message) => _host.Log(LogLevel.Trace, _pluginId, message);
    public void Debug(string message) => _host.Log(LogLevel.Debug, _pluginId, message);
    public void Information(string message) => _host.Log(LogLevel.Information, _pluginId, message);
    public void Warning(string message) => _host.Log(LogLevel.Warning, _pluginId, message);
    public void Error(string message, Exception? exception = null) => _host.Log(LogLevel.Error, _pluginId, message, exception);
    public void Critical(string message, Exception? exception = null) => _host.Log(LogLevel.Critical, _pluginId, message, exception);
}
