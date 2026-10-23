using PurpleHaze.SDK.Models;

namespace PurpleHaze.Loader.Discovery;

/// <summary>
/// Result of discovering a single plugin candidate.
/// </summary>
public sealed class PluginDiscoveryResult
{
    public required string DirectoryPath { get; init; }
    public required string? AssemblyPath { get; init; }
    public required string? ManifestPath { get; init; }
    public PluginManifest? Manifest { get; init; }
    public PluginMetadata? Metadata { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public PluginState State { get; init; } = PluginState.Discovered;
}
