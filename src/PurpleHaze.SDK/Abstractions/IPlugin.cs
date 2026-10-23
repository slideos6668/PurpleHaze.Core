using PurpleHaze.SDK.Models;

namespace PurpleHaze.SDK.Abstractions;

/// <summary>
/// Core contract that every Purple Haze plugin must implement.
/// Plugins are loaded by the host and receive an <see cref="IPluginContext"/> for services.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Static metadata for this plugin. Must match the manifest.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Called once after the plugin assembly is loaded and dependencies are satisfied.
    /// Register capabilities and subscribe to events here.
    /// </summary>
    /// <param name="context">Host-provided services (logging, config, capability registry).</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A task that completes when initialization finishes.</returns>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the host is shutting down or the plugin is being unloaded.
    /// Release resources and unsubscribe from events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
