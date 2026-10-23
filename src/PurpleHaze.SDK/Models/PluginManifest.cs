namespace PurpleHaze.SDK.Models;

/// <summary>
/// Manifest that ships alongside a plugin DLL.
/// Property names match JSON keys (camelCase) for desktop tooling;
/// the in-game Mono host parses JSON without System.Text.Json.
/// </summary>
public sealed class PluginManifest
{
    public int ManifestVersion { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequiredApiVersion { get; set; } = "0.1.0";
    public List<string> Dependencies { get; set; } = new List<string>();
    public string? Homepage { get; set; }
    public string? EntryType { get; set; }

    public PluginMetadata ToMetadata()
    {
        return new PluginMetadata
        {
            Id = Id,
            Name = Name,
            Author = Author,
            Version = Version,
            Description = Description,
            RequiredApiVersion = RequiredApiVersion,
            Dependencies = Dependencies.AsReadOnly(),
            Homepage = Homepage
        };
    }
}
