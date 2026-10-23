# Bootstrap Purple Haze into Muck (Doorstop only)

## One-command install (recommended)

```bash
# From the PurpleHaze.Core folder
dotnet build src/PurpleHaze.Unity.Mono -c Release
dotnet run --project src/PurpleHaze.Installer -- install "C:\Steam\steamapps\common\Muck" --doorstop "C:\path\to\Doorstop-Release"
```

Download Doorstop 4 first: https://github.com/NeighTools/UnityDoorstop/releases

## What gets installed in the game folder

```
Muck/
  Muck.exe
  winhttp.dll                 ← Doorstop (from upstream)
  doorstop_config.ini         ← points at our entry DLL
  Muck_Data/
  PurpleHaze/
    core/
      PurpleHaze.Unity.Mono.dll   ← OUR entry (Doorstop.Entrypoint.Start)
      PurpleHaze.SDK.dll
    plugins/                      ← drop plugin folders here
    logs/bootstrap.log
    state/ready
    session.marker
```

## Boot sequence inside the game

1. Windows loads `winhttp.dll` (Doorstop) with the game  
2. Doorstop loads `PurpleHaze/core/PurpleHaze.Unity.Mono.dll`  
3. Calls `Doorstop.Entrypoint.Start()`  
4. **GameBootstrap** runs on a background thread:
   - Creates `PurpleHaze/` layout  
   - Waits for `UnityEngine` to appear in the Mono domain  
   - Tries `DontDestroyOnLoad(new GameObject("PurpleHaze.Host"))` via reflection  
   - Loads every `PurpleHaze/plugins/*/manifest.json` + DLL  
   - Writes `session.marker`, `state/ready`, heartbeat file  

## Verify

After launching Muck:

| File | Meaning |
|------|---------|
| `PurpleHaze/logs/bootstrap.log` | Full bootstrap log |
| `PurpleHaze/state/phase` | `ready` when done |
| `PurpleHaze/state/ready` | Token + plugin count |
| `PurpleHaze/session.marker` | `host=doorstop`, `bepinex=false` |
| `%TEMP%\purplehaze-doorstop.log` | Fallback log |

## Uninstall

```bash
dotnet run --project src/PurpleHaze.Installer -- uninstall "C:\Steam\steamapps\common\Muck"
```

Then remove `winhttp.dll` if you want Doorstop gone completely.

## No BepInEx

This path does **not** use BepInEx. Do not install BepInExPack if you want a clean Option B stack (or remove it first).
