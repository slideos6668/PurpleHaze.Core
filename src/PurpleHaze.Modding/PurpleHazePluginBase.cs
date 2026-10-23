using System.Linq;
using System.Reflection;
using HarmonyLib;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Attributes;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Modding;

/// <summary>
/// BepInEx-like base class: attribute metadata, Harmony instance, Load/Unload hooks.
/// </summary>
public abstract class PurpleHazePluginBase : IPlugin
{
    private Harmony? _harmony;
    private IPluginContext? _context;

    public PluginMetadata Metadata { get; }

    protected IPluginContext Context =>
        _context ?? throw new InvalidOperationException("Plugin not initialized.");

    /// <summary>Harmony instance for this plugin (created on init).</summary>
    protected Harmony Harmony =>
        _harmony ?? throw new InvalidOperationException("Harmony not ready.");

    protected PurpleHazePluginBase()
    {
        var attr = GetType().GetCustomAttribute<PurpleHazePluginAttribute>()
            ?? GetType().GetCustomAttributes(false).OfType<PurpleHazePluginAttribute>().FirstOrDefault();
        // PHPluginAttribute derives from PurpleHazePluginAttribute — also accept by name
        if (attr == null)
        {
            foreach (var a in GetType().GetCustomAttributes(false))
            {
                if (a is PurpleHazePluginAttribute pha) { attr = pha; break; }
            }
        }
        if (attr != null)
        {
            Metadata = new PluginMetadata
            {
                Id = attr.Id,
                Name = attr.Name,
                Version = attr.Version,
                Author = attr.Author ?? "",
                Description = attr.Description ?? "",
                RequiredApiVersion = attr.RequiredApiVersion ?? "0.1.0"
            };
        }
        else
        {
            Metadata = new PluginMetadata
            {
                Id = GetType().FullName ?? "unknown.plugin",
                Name = GetType().Name,
                Version = "0.0.0",
                Author = "",
                Description = "",
                RequiredApiVersion = "0.1.0"
            };
        }
    }

    public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        try
        {
            _harmony = new Harmony(Metadata.Id);
            context.Logger.Information($"[{Metadata.Id}] Harmony ready. Applying patches...");
            _harmony.PatchAll(GetType().Assembly);
            context.Logger.Information($"[{Metadata.Id}] PatchAll complete.");
        }
        catch (Exception ex)
        {
            // Surface full chain — type initializer failures hide the real missing DLL in Message alone
            var detail = ex.ToString();
            context.Logger.Error($"[{Metadata.Id}] Harmony init/patch failed: {detail}", ex);
            throw new InvalidOperationException("Harmony failed: " + ex.GetBaseException().Message, ex);
        }

        OnLoaded();
        await OnLoadedAsync(cancellationToken).ConfigureAwait(false);
        context.Logger.Information($"[{Metadata.Id}] Loaded.");
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            OnUnloading();
            await OnUnloadingAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { _harmony?.UnpatchSelf(); }
            catch { /* ignore */ }
            _harmony = null;
            _context = null;
        }
    }

    /// <summary>Called after Harmony PatchAll. Override for setup.</summary>
    protected virtual void OnLoaded() { }

    /// <summary>Async variant of OnLoaded.</summary>
    protected virtual Task OnLoadedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called before Harmony unpatch.</summary>
    protected virtual void OnUnloading() { }

    protected virtual Task OnUnloadingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
