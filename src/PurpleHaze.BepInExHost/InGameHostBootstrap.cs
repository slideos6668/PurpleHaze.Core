using System;
using System.IO;
using System.Threading;

namespace PurpleHaze.BepInExHost;

/// <summary>
/// Starts the Purple Haze in-game side for Unity Mono / BepInEx hosts.
/// This bootstrap is deliberately lightweight: it writes session metadata and
/// prepares the plugins directory. Full lifecycle + named-pipe bridge wiring
/// can be attached when net472-compatible Loader/Bridge builds are available
/// or when the external PluginHost is used alongside the game.
/// </summary>
public static class InGameHostBootstrap
{
    private static int _started;

    public static string? LastSessionDirectory { get; private set; }
    public static string? LastPairingToken { get; private set; }

    /// <summary>
    /// Idempotent start. Safe to call from BepInEx Awake or Doorstop Start.
    /// </summary>
    public static void Start(string? gameRootHint = null)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        try
        {
            var root = ResolveGameRoot(gameRootHint);
            var phRoot = Path.Combine(root, "PurpleHaze");
            Directory.CreateDirectory(phRoot);
            Directory.CreateDirectory(Path.Combine(phRoot, "plugins"));
            Directory.CreateDirectory(Path.Combine(phRoot, "logs"));

            LastSessionDirectory = phRoot;
            LastPairingToken = GenerateToken();

            var logPath = Path.Combine(phRoot, "logs", "bepinex-host.log");
            Log(logPath, "PurpleHaze.BepInExHost starting.");
            Log(logPath, "Game root: " + root);
            Log(logPath, "Target game profile: Muck (Unity 2019.4.19f1 Mono) — primary.");
            Log(logPath, "Pairing token: " + LastPairingToken);
            Log(logPath, "STATUS: In-game session marker written.");
            Log(logPath, "Bridge: use external PluginHost --bridge with the same token,");
            Log(logPath, "or enable full in-process bridge when Loader is available on net472.");

            var marker = Path.Combine(phRoot, "session.marker");
            File.WriteAllText(marker,
                "gameId=muck\n" +
                "unityVersion=2019.4.19f1\n" +
                "backend=unity.mono\n" +
                "host=bepinex5\n" +
                "pairingToken=" + LastPairingToken + "\n" +
                "startedUtc=" + DateTime.UtcNow.ToString("o") + "\n" +
                "validatedInRealGame=false\n");

            Log(logPath, "Wrote session.marker under " + phRoot);
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "purplehaze-bepinex-host.log"),
                    DateTime.UtcNow.ToString("o") + " " + ex + Environment.NewLine);
            }
            catch
            {
                // never throw out of bootstrap into the game
            }
        }
    }

    private static string ResolveGameRoot(string? hint)
    {
        if (!string.IsNullOrEmpty(hint) && Directory.Exists(hint))
            return Path.GetFullPath(hint);

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var current = new DirectoryInfo(dir);
        for (var i = 0; i < 6 && current != null; i++)
        {
            var data = Path.Combine(current.FullName, "Muck_Data");
            if (Directory.Exists(data))
                return current.FullName;
            // generic Unity
            if (Directory.GetDirectories(current.FullName, "*_Data").Length > 0)
                return current.FullName;
            current = current.Parent;
        }
        return dir;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static void Log(string path, string message)
    {
        File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
    }
}
