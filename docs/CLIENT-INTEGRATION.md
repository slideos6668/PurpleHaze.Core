# Wire "Install mod loader" in the Purple Haze WPF client

## Architecture

```
┌─────────────────────────┐
│  Purple Haze WPF (UI)   │
│  "Install mod loader"   │
└───────────┬─────────────┘
            │ references OR Process.Start
            ▼
┌─────────────────────────┐
│  PurpleHaze.ManagerApi  │  ← preferred: project reference
│  LoaderInstallService   │
└───────────┬─────────────┘
            │ copies files into game folder
            ▼
┌─────────────────────────┐
│  Game folder            │
│  doorstop + PurpleHaze/ │
└─────────────────────────┘
```

## Option A — Reference the library (best)

In your WPF `.csproj`:

```xml
<ProjectReference Include="..\path\to\PurpleHaze.Core\src\PurpleHaze.ManagerApi\PurpleHaze.ManagerApi.csproj" />
```

Button click:

```csharp
var svc = new PurpleHaze.ManagerApi.LoaderInstallService();
var detect = svc.Detect(gamePath);
if (!detect.LoaderCanInstall) { /* show detect.Summary */ return; }

// payloadRoot = folder you ship with the manager containing core/*.dll
var result = svc.Install(gamePath, payloadRoot, doorstopDir);
MessageBox.Show(result.Success ? "Installed" : result.Message);
```

## Option B — Call the CLI with --json

```csharp
var psi = new ProcessStartInfo {
  FileName = "PurpleHaze.Installer.exe",
  Arguments = $"install \"{gamePath}\" --payload \"{payloadRoot}\" --json",
  RedirectStandardOutput = true,
  UseShellExecute = false,
  CreateNoWindow = true
};
var p = Process.Start(psi)!;
var json = p.StandardOutput.ReadToEnd();
p.WaitForExit();
// deserialize ApiResult
```

## Payload you must ship with the client

```
YourApp/
  payload/
    core/
      PurpleHaze.Unity.Mono.dll
      PurpleHaze.SDK.dll
    libs/          (optional: 0Harmony + MonoMod for all mods)
    doorstop/      (optional: extracted UnityDoorstop winhttp.dll)
```

Doorstop is **not** redistributed by us — download from  
https://github.com/NeighTools/UnityDoorstop/releases  
and either ship it in `payload/doorstop` or ask the user once.

## Multi-game

- **Supported install today:** any **Unity Mono** folder (`*_Data/Managed` present).
- **Validated:** Muck only.
- **IL2CPP:** detect returns `LoaderCanInstall = false`.
- Add games by dropping JSON into `GameProfiles/` or extending `GameProfileCatalog`.

## Suggested WPF flow

1. User picks game folder (or Steam library scan).
2. Call `Detect` → show backend + “Can install”.
3. User clicks **Install mod loader** → `Install`.
4. User clicks **Install mod** → `InstallMod(game, unzippedModFolder)`.
5. Launch game from Steam as usual.


## Shared Harmony (recommended)

Ship Harmony once with the loader payload:

```
payload/
  core/     PurpleHaze.Unity.Mono.dll, PurpleHaze.SDK.dll
  libs/     0Harmony.dll, MonoMod.*.dll, Mono.Cecil*.dll, ...
  doorstop/ winhttp.dll
```

On install, LoaderInstallService copies libs/ to Game/PurpleHaze/libs/.
At runtime the host preloads that folder so mods do not need to bundle Harmony.
