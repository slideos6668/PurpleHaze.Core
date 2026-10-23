using System;
using System.IO;
using System.Reflection;

namespace PurpleHaze.Unity.Mono;

internal static class PathResolver
{
    /// <summary>
    /// Finds the game root (folder with Muck.exe / Muck_Data).
    /// Doorstop usually loads our DLL from under the game directory.
    /// </summary>
    public static string ResolveGameRoot()
    {
        try
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(loc))
            {
                var current = new DirectoryInfo(Path.GetDirectoryName(loc)!);
                for (var i = 0; i < 8 && current != null; i++)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, "Muck_Data")))
                        return current.FullName;
                    if (File.Exists(Path.Combine(current.FullName, "Muck.exe")))
                        return current.FullName;
                    var dataDirs = current.GetDirectories("*_Data");
                    if (dataDirs.Length > 0)
                        return current.FullName;
                    current = current.Parent;
                }
            }
        }
        catch { }

        // Doorstop often sets cwd to the game root.
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            if (Directory.Exists(Path.Combine(cwd, "Muck_Data")) || File.Exists(Path.Combine(cwd, "Muck.exe")))
                return cwd;
        }
        catch { }

        return Directory.GetCurrentDirectory();
    }
}
