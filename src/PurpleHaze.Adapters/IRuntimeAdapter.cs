namespace PurpleHaze.Adapters;

/// <summary>
/// Contract for a game-runtime-specific bootstrap. Adapters are not interchangeable:
/// Unity Mono, Unity IL2CPP, Unreal, and external processes require different mechanisms.
/// </summary>
public interface IRuntimeAdapter
{
    /// <summary>Stable adapter id (e.g. "unity.mono").</summary>
    string AdapterId { get; }

    /// <summary>Human-readable name.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Returns true if this adapter believes it can host the given game install path.
    /// Detection only — does not inject or modify files.
    /// </summary>
    bool CanHandle(string gameInstallPath);

    /// <summary>
    /// Describes what would be required to bootstrap into this game.
    /// Does not perform injection.
    /// </summary>
    RuntimeBootstrapPlan CreatePlan(string gameInstallPath);
}

/// <summary>
/// Non-destructive description of how Purple Haze would attach to a game.
/// </summary>
public sealed class RuntimeBootstrapPlan
{
    public required string AdapterId { get; init; }
    public required string GamePath { get; init; }
    public required UnityScriptingBackend Backend { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnsupportedReasons { get; init; } = Array.Empty<string>();
    public bool IsSupported { get; init; }
}

/// <summary>
/// Unity scripting backend as detected from the install folder.
/// </summary>
public enum UnityScriptingBackend
{
    Unknown = 0,
    Mono = 1,
    Il2Cpp = 2,
    NotUnity = 3
}
