using System.Reflection;
using System.Runtime.Loader;

namespace PurpleHaze.Loader.Lifecycle;

/// <summary>
/// Collectible <see cref="AssemblyLoadContext"/> used to load a single plugin assembly
/// so it can later be unloaded (best-effort) without tearing down the host process.
/// Shared SDK / runtime assemblies are resolved from the default context.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(isCollectible: true)
    {
        if (string.IsNullOrWhiteSpace(pluginAssemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(pluginAssemblyPath));
        if (!File.Exists(pluginAssemblyPath))
            throw new FileNotFoundException("Plugin assembly not found.", pluginAssemblyPath);

        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prefer shared host assemblies for the SDK and BCL so plugins and host share the same types.
        if (IsSharedAssembly(assemblyName))
            return null; // fall through to default context

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is not null)
            return LoadFromAssemblyPath(path);

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path is not null)
            return LoadUnmanagedDllFromPath(path);
        return IntPtr.Zero;
    }

    private static bool IsSharedAssembly(AssemblyName name)
    {
        var n = name.Name ?? string.Empty;
        return n.Equals("PurpleHaze.SDK", StringComparison.OrdinalIgnoreCase)
            || n.Equals("PurpleHaze.Protocol", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            || n.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
            || n.Equals("mscorlib", StringComparison.OrdinalIgnoreCase);
    }
}
