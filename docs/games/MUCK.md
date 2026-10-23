# Target game: Muck

| Field | Value |
|-------|--------|
| Title | Muck |
| Steam App ID | 1625450 |
| Unity | **2019.4.19f1** |
| Backend | **Mono** (`Muck_Data/Managed`) |
| **Chosen entry** | **Option B — Doorstop only (no BepInEx)** |

## Option B (active choice)

```
Muck.exe
  → Doorstop (winhttp.dll / version.dll)
    → PurpleHaze.Unity.Mono.dll   (Doorstop.Entrypoint.Start)
      → MonoHost loads PurpleHaze/plugins/*
```

Full install steps: **[deploy/muck-doorstop/INSTALL.md](../../deploy/muck-doorstop/INSTALL.md)**

Config template: `deploy/muck-doorstop/doorstop_config.ini`

## What BepInEx is used for

**Nothing in Option B.** The BepInEx host project remains in the repo only as an optional alternate path for users who already run BepInEx mods.

## Detect (read-only)

```bash
dotnet run --project src/PurpleHaze.PluginHost -- --detect "C:\Steam\steamapps\common\Muck"
```

Expect `Backend: Mono`, `Adapter: unity.mono`.

## Status

| Feature | Status |
|---------|--------|
| Doorstop entry source | Implemented |
| In-game plugin folder load | Implemented |
| Live Muck validation in this environment | Not possible (no game install) |
| In-process named-pipe bridge on Mono BCL | Not yet |

Profile: `src/PurpleHaze.GameProfiles/muck.json`
