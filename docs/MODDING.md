# Gameplay modding (Harmony + attributes)

## Features

| Feature | How |
|--------|-----|
| **Easy attributes** | `[PurpleHazePlugin("id", "Name", "1.0.0")]` on a class inheriting `PurpleHazePluginBase` |
| **Harmony** | `HarmonyX` via `PurpleHaze.Modding` — `PatchAll` on load |
| **Auto Assembly-CSharp / Unity** | Host probes `*_Data/Managed` and resolves assemblies for plugins |

## Minimal plugin

```csharp
using PurpleHaze.Modding;
using PurpleHaze.SDK.Attributes;
using HarmonyLib;

[PurpleHazePlugin("author.my_mod", "My Mod", "1.0.0", Author = "You")]
public class MyMod : PurpleHazePluginBase
{
    protected override void OnLoaded()
    {
        Context.Logger.Information("Hello from Muck");
    }
}

[HarmonyPatch]
class MyPatch
{
    static System.Reflection.MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName("PlayerMovement");
        return t == null ? null : AccessTools.Method(t, "Update");
    }

    static void Prefix() { /* your code */ }
}
```

## Template

See `templates/MuckModTemplate/`.

## Deploy

Plugin folder must include Harmony DLLs (`0Harmony.dll`, etc.) from the build output, not only your mod DLL.
