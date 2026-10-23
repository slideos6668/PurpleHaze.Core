namespace PurpleHaze.SDK.Logging;

/// <summary>
/// Minimal structured logging abstraction for plugins.
/// Implementations forward to the host logger and the bridge.
/// </summary>
public interface IPluginLogger
{
    void Trace(string message);
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    void Critical(string message, Exception? exception = null);
}
