# Modding Purple Haze (MelonLoader / BepInEx style)

## MelonLoader → Purple Haze

```csharp
// MelonLoader
public class MyMod : MelonMod {
    public override void OnInitializeMelon() { }
}

// Purple Haze
[PHPlugin("author.mymod", "My Mod", "1.0.0")]
public class MyMod : PHMod {
    public override void OnInitialize() { }
}
```

## BepInEx → Purple Haze

```csharp
// BepInEx
[BepInPlugin("author.mymod", "My Mod", "1.0.0")]
public class MyMod : BaseUnityPlugin { }

// Purple Haze
[PHPlugin("author.mymod", "My Mod", "1.0.0")]
public class MyMod : PHMod {
    public override void OnInitialize() { }
}
```

Harmony patches work the same (`[HarmonyPatch]`, `AccessTools`, etc.).

## Setup

1. Class library **netstandard2.0**
2. Reference `PurpleHaze.SDK` + `PurpleHaze.Modding`
3. Build → drop `YourMod.dll` into `Game/PurpleHaze/plugins/`
4. Harmony is in `Game/PurpleHaze/libs/` (loader install) — don't require players to ship it per mod

## API surface (keep it small)

| You use | Role |
|---------|------|
| `[PHPlugin(id, name, version)]` | Identity |
| `PHMod` | Base class |
| `OnInitialize()` | Startup |
| `OnDeinitialize()` | Shutdown |
| `Log` | Logging |
| `Context` | Config, capabilities (advanced) |
| `[HarmonyPatch]` | Game hooks |

Long names still work: `PurpleHazePluginBase`, `[PurpleHazePlugin]`, `OnLoaded()`.
