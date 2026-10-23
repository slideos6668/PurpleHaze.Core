namespace PurpleHaze.SDK;

/// <summary>
/// Shared constants for the Purple Haze Core SDK.
/// </summary>
public static class Constants
{
    /// <summary>Current SDK / host API version.</summary>
    public const string ApiVersion = "0.1.0";

    /// <summary>Current plugin manifest schema version.</summary>
    public const int ManifestVersion = 1;

    /// <summary>Default relative plugins directory name.</summary>
    public const string DefaultPluginsDirectory = "plugins";

    /// <summary>Manifest file name preferred when co-located with the DLL.</summary>
    public const string ManifestFileName = "plugin.manifest.json";
}
