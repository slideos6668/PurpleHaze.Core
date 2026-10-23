using PurpleHaze.SDK.Models;

namespace PurpleHaze.Loader.Lifecycle;

/// <summary>
/// Topological sort of plugins by declared dependencies.
/// Detects missing dependencies and cycles.
/// </summary>
public static class DependencySorter
{
    public sealed record SortResult(
        IReadOnlyList<PluginMetadata> Ordered,
        IReadOnlyList<string> MissingDependencies,
        IReadOnlyList<string> Cycles);

    /// <summary>
    /// Returns plugins in an order safe for initialization (dependencies first).
    /// Plugins with missing deps or that participate in a cycle are omitted from <see cref="SortResult.Ordered"/>
    /// and reported in the corresponding lists.
    /// </summary>
    public static SortResult Sort(IReadOnlyList<PluginMetadata> plugins)
    {
        var byId = plugins.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var permanent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var temporary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<PluginMetadata>();
        var cycles = new List<string>();

        foreach (var plugin in plugins)
        {
            foreach (var dep in plugin.Dependencies)
            {
                if (!byId.ContainsKey(dep))
                    missing.Add($"{plugin.Id} -> {dep}");
            }
        }

        bool Visit(string id)
        {
            if (permanent.Contains(id)) return true;
            if (temporary.Contains(id))
            {
                cycles.Add(id);
                return false;
            }

            if (!byId.TryGetValue(id, out var plugin))
                return false; // missing dep already recorded

            temporary.Add(id);
            foreach (var dep in plugin.Dependencies)
            {
                if (!Visit(dep))
                {
                    // cycle or missing – do not mark permanent
                }
            }
            temporary.Remove(id);
            permanent.Add(id);
            ordered.Add(plugin);
            return true;
        }

        foreach (var plugin in plugins)
        {
            // Skip plugins that have any missing dependency.
            if (plugin.Dependencies.Any(d => !byId.ContainsKey(d)))
                continue;
            Visit(plugin.Id);
        }

        return new SortResult(ordered, missing.Distinct().ToList(), cycles.Distinct().ToList());
    }
}
