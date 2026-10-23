using System;
using System.IO;
using System.Reflection;

namespace PurpleHaze.Unity.Mono;

/// <summary>
/// Loads shared libraries from PurpleHaze/libs (Harmony, MonoMod, etc.)
/// so individual mods do not need to ship them.
/// </summary>
internal static class SharedLibLoader
{
    public static void LoadAll(string phRoot, Action<string> log)
    {
        var libs = Path.Combine(phRoot, "libs");
        Directory.CreateDirectory(libs);

        // Prefer libs first on resolve
        AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        _libsDir = libs;

        var count = 0;
        foreach (var f in Directory.GetFiles(libs, "*.dll"))
        {
            try
            {
                Assembly.LoadFrom(f);
                log("  libs: " + Path.GetFileName(f));
                count++;
            }
            catch (Exception ex)
            {
                log("  libs skip " + Path.GetFileName(f) + ": " + ex.Message);
            }
        }

        if (count == 0)
            log("libs: (empty — Harmony mods must ship their own 0Harmony or install shared pack)");
        else
            log("libs: preloaded " + count + " shared assembl" + (count == 1 ? "y" : "ies"));
    }

    private static string? _libsDir;

    private static Assembly? Resolve(object sender, ResolveEventArgs args)
    {
        try
        {
            if (string.IsNullOrEmpty(_libsDir) || !Directory.Exists(_libsDir)) return null;
            var name = new AssemblyName(args.Name).Name + ".dll";
            var path = Path.Combine(_libsDir, name);
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }
        catch { }
        return null;
    }
}
