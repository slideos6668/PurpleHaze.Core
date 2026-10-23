# PurpleHaze.BepInExHost

BepInEx **5** plugin host for **Muck** (Unity **2019.4.19f1**, **Mono**).

## Why BepInEx for Muck?

Muck’s community already standardizes on BepInEx 5 (Thunderstore `BepInExPack_Muck`).  
Purple Haze does not replace that ecosystem on day one — this plugin **rides** BepInEx and starts our session/bridge side.

## Build the real plugin (on a PC with Muck installed)

1. Install BepInEx 5 into the Muck folder (Thunderstore pack or manual).
2. Copy into `src/PurpleHaze.BepInExHost/libs/`:
   - `BepInEx/core/BepInEx.dll`
   - From `Muck_Data/Managed/`: `UnityEngine.dll`, `UnityEngine.CoreModule.dll` (and others if the compiler asks)
3. Rebuild:
   ```bash
   dotnet build src/PurpleHaze.BepInExHost -c Release
   ```
4. Copy `PurpleHaze.BepInExHost.dll` (+ dependencies if any) to `Muck/BepInEx/plugins/`.
5. Launch Muck. Check `Muck/PurpleHaze/logs/bepinex-host.log` and `session.marker`.

## Without game libs

The project still builds in CI (`PURPLEHAZE_STANDALONE_BUILD`). The BepInEx `BaseUnityPlugin` type is compiled only when `libs/BepInEx.dll` exists (`HAS_BEPINEX`).

## Status flags

| Claim | Status |
|-------|--------|
| Muck is Unity Mono 2019.4 | Confirmed by community layout (`Muck_Data/Managed`) |
| Detection / profile | Implemented |
| BepInEx plugin source | Implemented |
| Validated inside a live Muck process in this repo | **No** — requires your local install |

## Multiplayer fairness

Do not use advantage mods in public multiplayer without the other players’ consent. Purple Haze is a loader/manager framework, not a cheat suite.
