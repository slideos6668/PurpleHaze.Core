using System.Text.Json;
using PurpleHaze.SDK;
using PurpleHaze.SDK.Models;
using PurpleHaze.SDK.Validation;

namespace PurpleHaze.Loader.Discovery;

/// <summary>
/// Scans a plugins directory for plugin folders containing a manifest and optional assembly.
/// Does not load assemblies; only discovers and validates manifests.
/// </summary>
public sealed class PluginDiscoverer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Discovers all plugin candidates under <paramref name="pluginsRoot"/>.
    /// Each immediate subdirectory is treated as a potential plugin.
    /// </summary>
    public IReadOnlyList<PluginDiscoveryResult> Discover(string pluginsRoot)
    {
        if (string.IsNullOrWhiteSpace(pluginsRoot))
            throw new ArgumentException("Plugins root path is required.", nameof(pluginsRoot));

        if (!Directory.Exists(pluginsRoot))
            return Array.Empty<PluginDiscoveryResult>();

        var results = new List<PluginDiscoveryResult>();

        foreach (var dir in Directory.EnumerateDirectories(pluginsRoot))
        {
            results.Add(DiscoverSingle(dir));
        }

        // Also check for a single-plugin layout where the root itself contains the manifest.
        var rootManifest = Path.Combine(pluginsRoot, Constants.ManifestFileName);
        if (File.Exists(rootManifest) && results.Count == 0)
        {
            results.Add(DiscoverSingle(pluginsRoot));
        }

        return results;
    }

    private static PluginDiscoveryResult DiscoverSingle(string directory)
    {
        var errors = new List<string>();
        string? manifestPath = null;
        string? assemblyPath = null;
        PluginManifest? manifest = null;
        PluginMetadata? metadata = null;
        var isValid = false;
        var state = PluginState.Discovered;

        // Prefer plugin.manifest.json; also accept <foldername>.manifest.json
        var candidates = new[]
        {
            Path.Combine(directory, Constants.ManifestFileName),
            Path.Combine(directory, Path.GetFileName(directory) + ".manifest.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                manifestPath = candidate;
                break;
            }
        }

        if (manifestPath is null)
        {
            errors.Add("No plugin.manifest.json found.");
            return new PluginDiscoveryResult
            {
                DirectoryPath = directory,
                AssemblyPath = null,
                ManifestPath = null,
                IsValid = false,
                Errors = errors,
                State = PluginState.Failed
            };
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse manifest: {ex.Message}");
            return new PluginDiscoveryResult
            {
                DirectoryPath = directory,
                AssemblyPath = null,
                ManifestPath = manifestPath,
                IsValid = false,
                Errors = errors,
                State = PluginState.Failed
            };
        }

        var validation = ManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors);
            state = PluginState.Incompatible;
        }
        else
        {
            isValid = true;
            metadata = manifest!.ToMetadata();
            state = PluginState.Validated;
        }

        // Look for a .dll that matches the plugin id or is the only non-system dll.
        var dlls = Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(p => !Path.GetFileName(p).StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                     && !Path.GetFileName(p).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dlls.Count == 1)
            assemblyPath = dlls[0];
        else if (dlls.Count > 1 && metadata is not null)
        {
            var preferred = dlls.FirstOrDefault(d =>
                Path.GetFileNameWithoutExtension(d).Equals(metadata.Id.Replace('.', '.'), StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(d).Equals(metadata.Name.Replace(' ', '.'), StringComparison.OrdinalIgnoreCase));
            assemblyPath = preferred ?? dlls[0];
        }

        return new PluginDiscoveryResult
        {
            DirectoryPath = directory,
            AssemblyPath = assemblyPath,
            ManifestPath = manifestPath,
            Manifest = manifest,
            Metadata = metadata,
            IsValid = isValid,
            Errors = errors,
            State = state
        };
    }
}
