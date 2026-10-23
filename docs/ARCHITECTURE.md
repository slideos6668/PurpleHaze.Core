# Architecture — Purple Haze Core

## Goals

1. Provide a first-party plugin API so mod authors do not need BepInEx or MelonLoader for games we support.
2. Keep the core independent of the WPF manager; communicate only through a versioned local protocol.
3. Support game-specific adapters because Unity Mono, Unity IL2CPP, Unreal, and external processes are not interchangeable.

## High-level components

```
┌─────────────────────────┐         IPC (named pipe / loopback)        ┌──────────────────────────┐
│  Purple Haze (WPF)      │ ◄───────────────────────────────────────► │  PurpleHaze.Bridge       │
│  Manager application    │                                           │  (in-process or sidecar) │
└─────────────────────────┘                                           └────────────┬─────────────┘
                                                                                   │
                                                                          ┌────────▼────────┐
                                                                          │ PurpleHaze.Loader│
                                                                          │  discovery +     │
                                                                          │  lifecycle       │
                                                                          └────────┬────────┘
                                                                                   │
                                                                          ┌────────▼────────┐
                                                                          │  IPlugin (SDK)  │
                                                                          │  Sample / real  │
                                                                          │  game plugins   │
                                                                          └─────────────────┘
```

## Loading strategies (future)

| Runtime | Approach | Notes |
|---------|----------|-------|
| Unity Mono | Managed assembly load into the Mono domain, or a thin native bootstrap | Prefer official modding hooks when available |
| Unity IL2CPP | Native interop / generated bindings; cannot load arbitrary .NET 8 DLLs | Requires a different target framework or native component |
| Unreal Engine | Native module / plugin or external process | Completely different ABI |
| External process | No injection; communicate via the bridge only | Safest, limited access to game state |
| Existing loader (BepInEx etc.) | Optional adapter that runs on top of them | Compatibility layer, not a replacement |

**Important:** Milestone 1 does not implement any of these. It only defines the SDK and discovery so that later milestones can plug in the correct loader for a chosen game.

## Design principles

- **Contracts first** — plugins only reference `PurpleHaze.SDK`.
- **Validate everything** — IDs, versions, command arguments, message payloads.
- **No arbitrary code execution** over the bridge.
- **Failure isolation** — one bad plugin should not take down the host (best-effort inside a shared process).
- **Explicit simulation** — sample plugin actions are clearly labelled as simulated.


## Plugin lifecycle (Milestone 2)

```
plugins/                          PluginLifecycleManager
  example.plugin/                   │
    plugin.manifest.json  ──►  Discover ──► Validate ──► DependencySorter
    ExamplePlugin.dll     ──►  PluginLoadContext (collectible ALC)
                                    │
                                    ▼
                              Create IPlugin instance
                                    │
                                    ▼
                              InitializeAsync(HostPluginContext)
                                    │  registers capabilities
                                    ▼
                              Running  ←──  actions via IPluginActionHandler
                                    │
                                    ▼
                              ShutdownAsync ──► Unload ALC (best-effort)
```

Shared assemblies (`PurpleHaze.SDK`, BCL) always resolve from the default context so interface
casts such as `IPlugin` / `IPluginActionHandler` work across the ALC boundary.

Failure isolation: each plugin is loaded and initialized inside its own try/catch.
A bad plugin is marked `Failed` / `Incompatible` and the manager continues with the rest.
