using PurpleHaze.Loader.Capabilities;
using PurpleHaze.Loader.Configuration;
using PurpleHaze.Loader.Logging;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Configuration;
using PurpleHaze.SDK.Logging;

namespace PurpleHaze.Loader.Lifecycle;

/// <summary>
/// Host-provided <see cref="IPluginContext"/> for a single loaded plugin.
/// </summary>
internal sealed class HostPluginContext : IPluginContext
{
    private readonly string _pluginId;
    private readonly HostLogger _hostLogger;
    private readonly List<(string Name, object? Payload)> _publishedEvents = new();

    public HostPluginContext(string pluginId, HostLogger hostLogger, PluginCapabilityRegistry capabilities)
    {
        _pluginId = pluginId;
        _hostLogger = hostLogger;
        Logger = hostLogger.ForPlugin(pluginId);
        Configuration = new InMemoryPluginConfiguration();
        Capabilities = capabilities;
    }

    public IPluginLogger Logger { get; }
    public IPluginConfiguration Configuration { get; }
    public ICapabilityRegistry Capabilities { get; }

    /// <summary>Events published by the plugin during this session (for diagnostics / tests).</summary>
    public IReadOnlyList<(string Name, object? Payload)> PublishedEvents
    {
        get { lock (_publishedEvents) return _publishedEvents.ToList(); }
    }

    public void ReportError(string message, Exception? exception = null)
    {
        _hostLogger.Log(LogLevel.Error, _pluginId, message, exception);
    }

    public Task PublishEventAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name is required.", nameof(eventName));

        lock (_publishedEvents)
            _publishedEvents.Add((eventName, payload));

        _hostLogger.Log(LogLevel.Debug, _pluginId, $"Event published: {eventName}");
        return Task.CompletedTask;
    }
}
