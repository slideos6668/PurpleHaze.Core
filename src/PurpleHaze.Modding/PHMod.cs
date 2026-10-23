using PurpleHaze.SDK.Logging;

namespace PurpleHaze.Modding;

/// <summary>
/// MelonLoader-style base class. Prefer this for new mods.
/// <code>
/// [PHPlugin("author.mod", "My Mod", "1.0.0")]
/// public class MyMod : PHMod
/// {
///     public override void OnInitialize() { }
/// }
/// </code>
/// </summary>
public abstract class PHMod : PurpleHazePluginBase
{
    /// <summary>Shortcut logger (like MelonLogger).</summary>
    protected IPluginLogger Log => Context.Logger;

    /// <summary>
    /// Called when the mod is loaded (after Harmony PatchAll).
    /// Same idea as MelonLoader's OnInitializeMelon.
    /// </summary>
    public virtual void OnInitialize() { }

    /// <summary>Called when the mod is unloading. Like OnDeinitializeMelon.</summary>
    public virtual void OnDeinitialize() { }

    protected sealed override void OnLoaded()
    {
        OnInitialize();
    }

    protected sealed override void OnUnloading()
    {
        OnDeinitialize();
    }
}

/// <summary>Alias for <see cref="PHMod"/>.</summary>
public abstract class PHPlugin : PHMod
{
}
