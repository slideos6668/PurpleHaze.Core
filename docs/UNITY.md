# Unity support (Milestone 5)

## Critical distinction: Mono vs IL2CPP

| | **Unity Mono** | **Unity IL2CPP** |
|--|----------------|------------------|
| Game code | .NET assemblies in `<Game>_Data/Managed` | Native code in `GameAssembly.dll` |
| How to spot | `Managed/*.dll`, no `GameAssembly.dll` | `GameAssembly.dll` present |
| Mod execution | Load managed DLLs into the game’s Mono domain | Need interop + side-by-side runtime |
| Purple Haze status | **Adapter + Doorstop entry stub** | **Detected, not implemented** |

These are **not interchangeable**. A plugin built for Mono will not load into an IL2CPP game as a normal assembly.

## What this repo implements now

1. **Read-only detection** (`UnityGameDetector`, `PluginHost --detect <path>`).
2. **`UnityMonoAdapter`** — bootstrap *plan* for Doorstop-oriented Mono installs.
3. **`UnityIl2CppAdapter`** — explicit “not supported yet” plan.
4. **`PurpleHaze.Unity.Mono`** — Doorstop-compatible `Doorstop.Entrypoint.Start()` assembly targeting `netstandard2.1`.

## What is **not** claimed

- We have **not** validated injection inside a real Unity player process in this environment.
- We do **not** ship Unity Doorstop binaries (use upstream Doorstop 4).
- We do **not** bypass anti-cheat, DRM, or integrity systems.
- IL2CPP bootstrap is **not** implemented.

## Recommended path for a specific Mono game

1. Confirm ToS / no anti-cheat.
2. Run detection:
   ```bash
   dotnet run --project src/PurpleHaze.PluginHost -- --detect "C:\Path\To\Game"
   ```
3. Install Doorstop for that Unity version (upstream).
4. Set Doorstop `target_assembly` to `PurpleHaze.Unity.Mono.dll`.
5. Launch the game; check `PurpleHaze/logs/unity-mono-bootstrap.log` and `session.marker`.
6. Run external `PluginHost --bridge` + TestClient against plugins (current bridge is net8 external).
7. Later: port or dual-target the in-process lifecycle host to the game’s Mono BCL so plugins run *inside* the player.

## BepInEx / MelonLoader coexistence

Purple Haze can remain an external manager talking over the bridge while an existing loader (BepInEx, MelonLoader) hosts in-game plugins. A thin BepInEx plugin that only starts our bridge is a valid compatibility path and does not require replacing those loaders on day one.

## Security

Plugins loaded **inside** a game process can touch that process’s memory and APIs. There is no complete isolation between in-process plugins.


## First concrete title: Muck

See [games/MUCK.md](games/MUCK.md).

- Unity **2019.4.19f1**, **Mono**
- Host path: **BepInEx 5** plugin (`PurpleHaze.BepInExHost`)
