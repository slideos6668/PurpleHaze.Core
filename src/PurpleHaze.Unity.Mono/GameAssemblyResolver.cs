using System;
using System.IO;
using System.Reflection;

namespace PurpleHaze.Unity.Mono;

/// <summary>
/// Makes Assembly-CSharp, UnityEngine.*, etc. resolvable for plugins
/// by probing &lt;Game&gt;/*_Data/Managed.
/// </summary>
internal static class GameAssemblyResolver
{
    private static string? _managedDir;
    private static bool _hooked;

    public static void Install(string gameRoot, Action<string> log)
    {
        if (_hooked) return;

        _managedDir = FindManaged(gameRoot);
        if (_managedDir == null)
        {
            log("WARN: no *_Data/Managed folder — game assembly auto-resolve disabled.");
            return;
        }

        log("Managed folder: " + _managedDir);
        AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        _hooked = true;

        // Eager-load common assemblies so plugins can use them immediately
        TryLoad("UnityEngine", log);
        TryLoad("UnityEngine.CoreModule", log);
        TryLoad("Assembly-CSharp", log);
    }

    public static string? ManagedDirectory => _managedDir;

    private static string? FindManaged(string gameRoot)
    {
        try
        {
            foreach (var data in Directory.GetDirectories(gameRoot, "*_Data"))
            {
                var managed = Path.Combine(data, "Managed");
                if (Directory.Exists(managed))
                    return managed;
            }
        }
        catch { }
        return null;
    }

    private static void TryLoad(string simpleName, Action<string> log)
    {
        try
        {
            if (_managedDir == null) return;
            var path = Path.Combine(_managedDir, simpleName + ".dll");
            if (!File.Exists(path)) return;
            Assembly.LoadFrom(path);
            log("Preloaded: " + simpleName);
        }
        catch (Exception ex)
        {
            log("Preload " + simpleName + " failed: " + ex.Message);
        }
    }

    private static Assembly? OnResolve(object sender, ResolveEventArgs args)
    {
        try
        {
            if (_managedDir == null) return null;
            var name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name)) return null;

            var path = Path.Combine(_managedDir, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }
        catch { }
        return null;
    }
}
