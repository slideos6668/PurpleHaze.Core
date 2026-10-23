namespace PurpleHaze.SDK.Models;

/// <summary>
/// Immutable metadata describing a plugin.
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// Unique plugin identifier. Must be lowercase, use dots as separators (e.g. "example.plugin").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Plugin author or team.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Semantic version of the plugin (e.g. "1.0.0").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Short description of what the plugin does.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Minimum Purple Haze Core API version this plugin requires (e.g. "0.1.0").
    /// </summary>
    public required string RequiredApiVersion { get; init; }

    /// <summary>
    /// Optional list of plugin IDs this plugin depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional website or repository URL.
    /// </summary>
    public string? Homepage { get; init; }
}
