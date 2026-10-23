namespace PurpleHaze.Adapters.Unity;

/// <summary>
/// Placeholder adapter for Unity IL2CPP games.
/// IL2CPP is not interchangeable with Mono.
/// </summary>
public sealed class UnityIl2CppAdapter : IRuntimeAdapter
{
    public string AdapterId => "unity.il2cpp";
    public string DisplayName => "Unity IL2CPP (not implemented)";

    public bool CanHandle(string gameInstallPath)
    {
        var d = UnityGameDetector.Detect(gameInstallPath);
        return d.IsUnity && d.Backend == UnityScriptingBackend.Il2Cpp;
    }

    public RuntimeBootstrapPlan CreatePlan(string gameInstallPath)
    {
        var d = UnityGameDetector.Detect(gameInstallPath);
        return new RuntimeBootstrapPlan
        {
            AdapterId = AdapterId,
            GamePath = gameInstallPath,
            Backend = UnityScriptingBackend.Il2Cpp,
            IsSupported = false,
            Summary = "Unity IL2CPP detected. Purple Haze Core does not yet implement an IL2CPP bootstrap.",
            Steps = new[]
            {
                "Future work: generate or consume interop assemblies for the game.",
                "Future work: host CoreCLR or use an established IL2CPP interop stack.",
                "Until then, use an existing loader that already supports this title, or pick a Mono game."
            },
            Risks = new[]
            {
                "IL2CPP interop is game- and Unity-version specific.",
                "Anti-cheat titles are out of scope."
            },
            UnsupportedReasons = d.Notes.Concat(new[]
            {
                "No IL2CPP runtime host is shipped in Milestone 5."
            }).ToList()
        };
    }
}
