using System.Reflection;
using HarmonyLib;
using PurpleHaze.Modding;
using PurpleHaze.SDK.Attributes;

namespace MuckModTemplate;

/// <summary>
/// MelonLoader-style mod entry for Purple Haze.
/// Drop the built DLL into PurpleHaze/plugins/ (no JSON required).
/// </summary>
[PHPlugin("muckmod.template", "Muck Mod Template", "1.0.0",
    Author = "You",
    Description = "Template — Harmony + OnInitialize")]
public class Plugin : PHMod
{
    public override void OnInitialize()
    {
        Log.Information("Muck mod template initialized (Purple Haze).");
    }
}

[HarmonyPatch]
internal static class DemoLogPatch
{
    static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("GameManager")
                   ?? AccessTools.TypeByName("PlayerMovement");
        if (type == null) return null;
        return AccessTools.Method(type, "Update")
               ?? AccessTools.Method(type, "Start")
               ?? AccessTools.Method(type, "Awake");
    }

    static void Prefix() { }
}
