using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PurpleHaze.Unity.Mono;

/// <summary>
/// Full game bootstrap sequence for Unity Mono (Muck / Doorstop Option B).
/// Phases: resolve roots → wait for Unity → attach to player → load plugins → heartbeat.
/// </summary>
public static class GameBootstrap
{
    private static readonly object LogGate = new object();
    private static string? _gameRoot;
    private static string? _phRoot;
    private static string? _logPath;
    private static string? _token;
    private static int _unityAttached;

    public static void Run()
    {
        try
        {
            // Phase 0 — give Mono a moment after Doorstop injects us.
            Thread.Sleep(500);

            _gameRoot = PathResolver.ResolveGameRoot();
            _phRoot = Path.Combine(_gameRoot, "PurpleHaze");
            EnsureLayout(_phRoot);
            _logPath = Path.Combine(_phRoot, "logs", "bootstrap.log");
            _token = CreateToken();

            Log("========================================");
            Log("Purple Haze Game Bootstrap (Option B)");
            Log("Doorstop entry — no BepInEx");
            Log("========================================");
            Log("Game root : " + _gameRoot);
            Log("PH root   : " + _phRoot);
            Log("PID       : " + Process.GetCurrentProcess().Id);
            Log("Token     : " + _token);
            Log("Assembly  : " + Assembly.GetExecutingAssembly().FullName);

            WriteState("starting");
            WriteSessionMarker(unityReady: false, pluginCount: 0);

            // Phase 1 — wait until UnityEngine is loaded in this domain.
            Log("Installing game assembly resolver (Managed)...");
            GameAssemblyResolver.Install(_gameRoot!, Log);

            Log("Phase 1: waiting for UnityEngine...");
            var unity = UnityRuntime.WaitForUnity(TimeSpan.FromSeconds(60), Log);
            if (unity == null)
            {
                Log("WARN: UnityEngine not found within 60s. Continuing headless (plugins only).");
                WriteState("unity_timeout");
            }
            else
            {
                Log("Phase 1 OK: UnityEngine present (" + unity.GetName().Name + " v" + unity.GetName().Version + ")");
                WriteState("unity_loaded");

                // Phase 2 — attach a persistent object so we stay alive with the player.
                Log("Phase 2: attaching to Unity player...");
                if (UnityRuntime.TryAttachPersistentHost(unity, Log))
                {
                    Interlocked.Exchange(ref _unityAttached, 1);
                    Log("Phase 2 OK: persistent host attached (DontDestroyOnLoad).");
                    WriteState("attached");
                }
                else
                {
                    Log("Phase 2 WARN: could not attach GameObject; plugins still load.");
                    WriteState("attach_failed");
                }
            }

            // Phase 2.5 — shared Harmony / MonoMod in PurpleHaze/libs
            Log("Loading shared libs from PurpleHaze/libs ...");
            SharedLibLoader.LoadAll(_phRoot!, Log);

            // Phase 3 — load Purple Haze plugins from disk.
            Log("Phase 3: loading plugins from PurpleHaze/plugins ...");
            var loaded = PluginLoader.LoadAll(_phRoot, Log);
            Log("Phase 3 OK: " + loaded + " plugin(s) initialized.");

            WriteSessionMarker(unityReady: _unityAttached == 1, pluginCount: loaded);
            WriteState("ready");
            WriteReadyFile(loaded);

            Log("Bootstrap complete. Heartbeat every 30s.");
            HeartbeatLoop(loaded);
        }
        catch (ThreadAbortException)
        {
            // Normal when the game process exits — not a real failure.
            try { Log("Bootstrap thread ending (game exit)."); } catch { }
        }
        catch (Exception ex)
        {
            Log("FATAL bootstrap: " + ex);
            EmergencyLog(ex.ToString());
            try { WriteState("fatal"); } catch { }
        }
    }

    private static void HeartbeatLoop(int pluginCount)
    {
        while (true)
        {
            try
            {
                Thread.Sleep(30_000);
                var hb = Path.Combine(_phRoot!, "heartbeat");
                File.WriteAllText(hb,
                    "utc=" + DateTime.UtcNow.ToString("o") + "\n" +
                    "pid=" + Process.GetCurrentProcess().Id + "\n" +
                    "plugins=" + pluginCount + "\n" +
                    "unityAttached=" + (_unityAttached == 1) + "\n");
            }
            catch
            {
                // never tear down the game for heartbeat failures
            }
        }
    }

    private static void EnsureLayout(string phRoot)
    {
        Directory.CreateDirectory(phRoot);
        Directory.CreateDirectory(Path.Combine(phRoot, "core"));
        Directory.CreateDirectory(Path.Combine(phRoot, "plugins"));
        Directory.CreateDirectory(Path.Combine(phRoot, "libs"));
        Directory.CreateDirectory(Path.Combine(phRoot, "logs"));
        Directory.CreateDirectory(Path.Combine(phRoot, "config"));
        Directory.CreateDirectory(Path.Combine(phRoot, "state"));
    }

    private static void WriteSessionMarker(bool unityReady, int pluginCount)
    {
        var path = Path.Combine(_phRoot!, "session.marker");
        var sb = new StringBuilder();
        sb.AppendLine("gameId=muck");
        sb.AppendLine("host=doorstop");
        sb.AppendLine("backend=unity.mono");
        sb.AppendLine("unityVersion=2019.4.19f1");
        sb.AppendLine("pairingToken=" + _token);
        sb.AppendLine("gameRoot=" + _gameRoot);
        sb.AppendLine("plugins=" + Path.Combine(_phRoot!, "plugins"));
        sb.AppendLine("pid=" + Process.GetCurrentProcess().Id);
        sb.AppendLine("startedUtc=" + DateTime.UtcNow.ToString("o"));
        sb.AppendLine("unityAttached=" + unityReady.ToString().ToLowerInvariant());
        sb.AppendLine("pluginCount=" + pluginCount);
        sb.AppendLine("bepinex=false");
        sb.AppendLine("validatedInRealGame=false");
        File.WriteAllText(path, sb.ToString());
        Log("Wrote session.marker");
    }

    private static void WriteReadyFile(int pluginCount)
    {
        File.WriteAllText(Path.Combine(_phRoot!, "state", "ready"),
            "ready=true\n" +
            "token=" + _token + "\n" +
            "plugins=" + pluginCount + "\n" +
            "utc=" + DateTime.UtcNow.ToString("o") + "\n");
    }

    private static void WriteState(string state)
    {
        try
        {
            File.WriteAllText(Path.Combine(_phRoot!, "state", "phase"), state + "\n");
        }
        catch { }
    }

    private static string CreateToken()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        var sb = new StringBuilder(32);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static void Log(string msg)
    {
        lock (LogGate)
        {
            var line = DateTime.UtcNow.ToString("o") + " " + msg;
            if (_logPath != null)
            {
                try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
            }
            EmergencyLog(line);
        }
    }

    public static void EmergencyLog(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "purplehaze-doorstop.log"),
                DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
        }
        catch { }
    }
}
