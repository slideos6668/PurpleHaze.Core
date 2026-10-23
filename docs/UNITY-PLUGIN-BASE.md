# “Official Unity plugin base” (what BepInEx means)

BepInEx `BaseUnityPlugin` **inherits Unity’s `MonoBehaviour`**.
Unity then calls `Awake`, `Start`, `Update`, etc. on a component on a GameObject.

## Path A — Simple (no Unity reference) — default

```csharp
[PHPlugin("author.mod", "My Mod", "1.0.0")]
public class MyMod : PHMod
{
    public override void OnInitialize() { /* like OnInitializeMelon */ }
}
```

Harmony patches do the gameplay work. **No** Unity `Update` loop on your class.

## Path B — True Unity component (BepInEx-like)

1. Reference the game’s Unity assemblies (Private=false):

```xml
<Reference Include="UnityEngine.CoreModule">
  <HintPath>C:\...\Muck\Muck_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
  <Private>false</Private>
</Reference>
```

2. Your plugin class must **both** implement the PH plugin contract **and** inherit `MonoBehaviour`
   (advanced: often two classes — one `PHMod` for load, one `MonoBehaviour` for ticks).

Purple Haze host creates `PurpleHaze.Host` GameObject. If your **plugin type** is a `MonoBehaviour`,
the loader calls `AddComponent` on that host so Unity drives `Update`.

Practical pattern:

```csharp
[PHPlugin("author.mod", "My Mod", "1.0.0")]
public class MyMod : PHMod
{
    public override void OnInitialize()
    {
        // Harmony PatchAll already done by base
        Log.Information("Init");
    }
}

// Separate behaviour if you need Update — add via your own AddComponent using Unity APIs
// once you reference UnityEngine, or rely on host AddComponent when type is MonoBehaviour.
```

Full `BaseUnityPlugin` clone (inherits MonoBehaviour + BepInEx config) needs UnityEngine refs and
is optional; most Muck mods only need `PHMod` + Harmony.

## Summary

| Feature | How |
|---------|-----|
| Melon-style init | `PHMod.OnInitialize()` |
| Harmony | Built into `PHMod` / `PurpleHazePluginBase` |
| Unity `Update` | Reference UnityEngine + MonoBehaviour on host GO |
| No game DLL at compile time | `AccessTools.TypeByName` |
