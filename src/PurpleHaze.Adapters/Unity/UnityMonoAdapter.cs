namespace PurpleHaze.Adapters.Unity;

/// <summary>
/// Adapter for Unity games that use the Mono scripting backend.
/// Bootstrap is designed around Unity Doorstop (managed entry before Unity starts).
/// This adapter does not inject by itself — it produces a plan and validates layout.
/// </summary>
public sealed class UnityMonoAdapter : IRuntimeAdapter
{
    public string AdapterId => "unity.mono";
    public string DisplayName => "Unity Mono (Doorstop-oriented)";

    public bool CanHandle(string gameInstallPath)
    {
        var d = UnityGameDetector.Detect(gameInstallPath);
        return d.IsUnity && d.Backend == UnityScriptingBackend.Mono;
    }

    public RuntimeBootstrapPlan CreatePlan(string gameInstallPath)
    {
        var d = UnityGameDetector.Detect(gameInstallPath);
        if (!d.IsUnity)
        {
            return new RuntimeBootstrapPlan
            {
                AdapterId = AdapterId,
                GamePath = gameInstallPath,
                Backend = d.Backend,
                IsSupported = false,
                Summary = "Not a Unity install (or unrecognized layout).",
                UnsupportedReasons = d.Notes
            };
        }

        if (d.Backend == UnityScriptingBackend.Il2Cpp)
        {
            return new RuntimeBootstrapPlan
            {
                AdapterId = AdapterId,
                GamePath = gameInstallPath,
                Backend = d.Backend,
                IsSupported = false,
                Summary = "This install looks like Unity IL2CPP. UnityMonoAdapter cannot load managed plugins into IL2CPP.",
                UnsupportedReasons = new[]
                {
                    "IL2CPP compiles game C# to native code (GameAssembly.dll).",
                    "A separate unity.il2cpp adapter (interop + native host / CoreCLR side-by-side) is required.",
                    "Do not place net8 / netstandard plugins next to IL2CPP and expect them to load like Mono."
                },
                Risks = new[]
                {
                    "Forcing Mono tooling onto an IL2CPP game will fail or crash."
                },
                Steps = d.Notes
            };
        }

        if (d.Backend != UnityScriptingBackend.Mono)
        {
            return new RuntimeBootstrapPlan
            {
                AdapterId = AdapterId,
                GamePath = gameInstallPath,
                Backend = d.Backend,
                IsSupported = false,
                Summary = "Unity backend could not be classified as Mono.",
                UnsupportedReasons = d.Notes
            };
        }

        return new RuntimeBootstrapPlan
        {
            AdapterId = AdapterId,
            GamePath = gameInstallPath,
            Backend = UnityScriptingBackend.Mono,
            IsSupported = true,
            Summary = "Unity Mono detected. Bootstrap via Doorstop-compatible entry assembly (PurpleHaze.Unity.Mono).",
            Steps = new[]
            {
                "Confirm the game has no anti-cheat / ToS prohibition against third-party code.",
                "Install Unity Doorstop 4 (winhttp.dll / version.dll proxy or Linux equivalent) for this Unity version.",
                "Point Doorstop target_assembly at PurpleHaze.Unity.Mono.dll.",
                "Doorstop invokes Doorstop.Entrypoint.Start() early in the Mono domain.",
                "Entrypoint starts PluginLifecycleManager + BridgeServer on a local pipe.",
                "Place Purple Haze plugins under <Game>/PurpleHaze/plugins/.",
                "Connect Purple Haze manager / TestClient with the printed pairing token."
            },
            Risks = new[]
            {
                "In-process plugins can access the game process memory and APIs.",
                "Game updates may change Mono version or strip corlib; Doorstop dll_search_path_override may be required.",
                "Online games with anti-cheat must not be targeted.",
                "This repository does not ship Doorstop binaries; install them from upstream."
            },
            UnsupportedReasons = Array.Empty<string>()
        };
    }
}
