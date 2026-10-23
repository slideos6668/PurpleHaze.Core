using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Configuration;
using PurpleHaze.SDK.Logging;

namespace PurpleHaze.SDK.Abstractions;

/// <summary>
/// Services provided by the host to a plugin during its lifetime.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Structured logger scoped to this plugin.
    /// </summary>
    IPluginLogger Logger { get; }

    /// <summary>
    /// Configuration store for this plugin.
    /// </summary>
    IPluginConfiguration Configuration { get; }

    /// <summary>
    /// Registry used to expose UI controls and commands to the manager.
    /// </summary>
    ICapabilityRegistry Capabilities { get; }

    /// <summary>
    /// Reports a non-fatal error that should be surfaced to the manager.
    /// </summary>
    void ReportError(string message, Exception? exception = null);

    /// <summary>
    /// Publishes a named event that the external manager can observe.
    /// </summary>
    /// <param name="eventName">Dot-separated event name (e.g. "example.valueChanged").</param>
    /// <param name="payload">Optional structured payload (must be JSON-serializable).</param>
    Task PublishEventAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default);
}
