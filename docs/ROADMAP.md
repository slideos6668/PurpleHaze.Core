# Roadmap

## Milestone 1 — Core SDK  ✅

- [x] Solution and project structure
- [x] `IPlugin`, metadata, manifests
- [x] Manifest and ID validation
- [x] Capability model
- [x] Sample plugin
- [x] Discovery
- [x] Development host (simulated)
- [x] Automated tests
- [x] Documentation skeleton

## Milestone 2 — Plugin lifecycle  ✅

- [x] Collectible `AssemblyLoadContext` loading (`PluginLoadContext`)
- [x] Full initialize / shutdown orchestration (`PluginLifecycleManager`)
- [x] Dependency ordering with missing-dep and cycle detection
- [x] Structured host logging (`HostLogger` + per-plugin scopes)
- [x] Failure isolation (one bad plugin does not block others)
- [x] Default-context activation fallback for dev iteration
- [x] Optional `IPluginActionHandler` for cross-ALC action invocation
- [x] Lifecycle unit tests
- [x] PluginHost exercises real ALC load of the sample DLL

## Milestone 3 — Communication bridge  ✅

- [x] Named-pipe transport (`System.IO.Pipes`, Windows + Unix)
- [x] Length-prefixed JSON framing + size limits
- [x] Handshake with pairing-token authentication
- [x] Protocol version negotiation
- [x] Request/response correlation (`requestId`) + timeouts
- [x] Heartbeats + receive timeout
- [x] Message validation + known-type allowlist
- [x] Soft rate limiting
- [x] Controlled handling of malformed frames / disconnects
- [x] Integration tests (server ↔ client over real pipes)

## Milestone 4 — Dynamic capabilities & test client  ✅

- [x] `PluginBridgeCoordinator` publishes capabilities on connect
- [x] Dispatches validated `action.execute` to `IPluginActionHandler`
- [x] Capability updates pushed after successful actions
- [x] `PurpleHaze.TestClient` diagnostic console (list / invoke / demo)
- [x] PluginHost `--bridge` mode with pairing token
- [x] End-to-end demo: client → bridge → sample plugin → result
- [x] Coordinator integration tests

## Milestone 5 — Target-game integration  ◐ (Unity family started)

- [x] User chose Unity as the first runtime family
- [x] Document Mono vs IL2CPP (not interchangeable)
- [x] `UnityGameDetector` + `PluginHost --detect`
- [x] `UnityMonoAdapter` bootstrap plan (Doorstop-oriented)
- [x] `UnityIl2CppAdapter` explicit unsupported stub
- [x] `PurpleHaze.Unity.Mono` Doorstop entry assembly (netstandard2.1)
- [x] Target title selected: **Muck** (Unity 2019.4.19f1 Mono)
- [x] BepInEx 5 host project + game profile
- [x] Option B selected: Doorstop-only entry (no BepInEx)
- [x] MonoHost in-process plugin load via Assembly.LoadFrom
- [ ] Validate bootstrap inside a **live Muck process** on the developer machine
- [ ] In-process lifecycle host on the game’s Mono BCL
- [ ] IL2CPP interop host (separate milestone)

## Milestone 6 — Purple Haze WPF integration

- [ ] Document the stable bridge API
- [ ] Add a game-specific tab in the existing manager
- [ ] Keep core independent and testable in isolation

## Explicit non-goals for early milestones

- Claiming support for every engine
- Bypassing anti-cheat or DRM
- Treating Unity Mono, IL2CPP, and Unreal as interchangeable
