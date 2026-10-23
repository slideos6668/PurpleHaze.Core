namespace PurpleHaze.ManagerApi;

/// <summary>Machine-readable result for the external Purple Haze client.</summary>
public sealed class ApiResult
{
    public bool Success { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public Dictionary<string, string> Data { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class GameProfile
{
    public string GameId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? SteamAppId { get; set; }
    public string ScriptingBackend { get; set; } = "mono"; // mono | il2cpp | unknown
    public string AdapterId { get; set; } = "unity.mono";
    public string? DataFolderName { get; set; }
    public List<string> ExecutableNames { get; set; } = new();
    public string RecommendedHost { get; set; } = "doorstop";
    public bool LoaderSupported { get; set; }
    public string SupportNotes { get; set; } = "";
}

public sealed class DetectInfo
{
    public string Path { get; set; } = "";
    public bool IsUnity { get; set; }
    public string Backend { get; set; } = "Unknown";
    public string? ManagedDirectory { get; set; }
    public string? ExecutablePath { get; set; }
    public bool LoaderCanInstall { get; set; }
    public bool LoaderAlreadyInstalled { get; set; }
    public string? MatchedProfileId { get; set; }
    public string Summary { get; set; } = "";
    public List<string> Notes { get; set; } = new();
}
