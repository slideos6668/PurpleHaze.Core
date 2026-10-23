namespace PurpleHaze.SDK.Models;

/// <summary>
/// Lifecycle state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>Discovered but not yet loaded.</summary>
    Discovered = 0,

    /// <summary>Metadata validated; ready to load.</summary>
    Validated = 1,

    /// <summary>Assembly loaded into the host.</summary>
    Loaded = 2,

    /// <summary>Plugin.Initialize completed successfully.</summary>
    Initialized = 3,

    /// <summary>Plugin is running and capabilities are active.</summary>
    Running = 4,

    /// <summary>Plugin was deliberately disabled by the user or configuration.</summary>
    Disabled = 5,

    /// <summary>API version or dependency check failed.</summary>
    Incompatible = 6,

    /// <summary>Load or initialization threw an exception.</summary>
    Failed = 7,

    /// <summary>Plugin has been shut down cleanly.</summary>
    Shutdown = 8
}
