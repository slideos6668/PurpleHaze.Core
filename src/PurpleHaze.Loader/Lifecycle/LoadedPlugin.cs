using System.Reflection;
using PurpleHaze.Loader.Capabilities;
using PurpleHaze.Loader.Discovery;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Loader.Lifecycle;

/// <summary>
/// Tracks one plugin instance through its lifetime inside the host.
/// </summary>
public sealed class LoadedPlugin : IAsyncDisposable
{
    public required PluginDiscoveryResult Discovery { get; init; }

    public PluginMetadata? Metadata => Discovery.Metadata;

    public PluginState State { get; private set; } = PluginState.Discovered;
    public string? FailureReason { get; private set; }
    public Exception? FailureException { get; private set; }

    public IPlugin? Instance { get; private set; }
    public PluginLoadContext? LoadContext { get; private set; }
    public Assembly? Assembly { get; private set; }
    public PluginCapabilityRegistry Capabilities { get; } = new();
    public DateTimeOffset? InitializedAt { get; private set; }
    public DateTimeOffset? ShutdownAt { get; private set; }

    /// <summary>
    /// True when the instance was activated from the default load context (dev-host project reference)
    /// rather than a collectible <see cref="PluginLoadContext"/>.
    /// </summary>
    public bool IsDefaultContext { get; private set; }

    internal void SetState(PluginState state, string? reason = null, Exception? exception = null)
    {
        State = state;
        if (reason is not null) FailureReason = reason;
        if (exception is not null) FailureException = exception;
    }

    internal void AttachInstance(IPlugin instance, Assembly assembly, PluginLoadContext? context)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        LoadContext = context;
        IsDefaultContext = context is null;
        State = PluginState.Loaded;
    }

    internal void MarkInitialized()
    {
        InitializedAt = DateTimeOffset.UtcNow;
        State = PluginState.Running;
    }

    internal void MarkShutdown()
    {
        State = PluginState.Shutdown;
        ShutdownAt = DateTimeOffset.UtcNow;
    }

    internal void MarkFailed(string reason, Exception? exception = null)
    {
        State = PluginState.Failed;
        FailureReason = reason;
        FailureException = exception;
    }

    public async ValueTask DisposeAsync()
    {
        if (Instance is not null && State is PluginState.Running or PluginState.Initialized or PluginState.Loaded)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Instance.ShutdownAsync(cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort during dispose.
            }
        }

        Instance = null;
        LoadContext?.Unload();
        LoadContext = null;
        Assembly = null;
    }
}
