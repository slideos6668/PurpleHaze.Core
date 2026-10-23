using PurpleHaze.Adapters;
using PurpleHaze.Adapters.Unity;
using PurpleHaze.Bridge;
using PurpleHaze.Bridge.Hosting;
using PurpleHaze.Bridge.Session;
using PurpleHaze.Loader.Lifecycle;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.PluginHost;

/// <summary>
/// Development host for Milestone 4.
/// Loads plugins, optionally hosts the bridge server, and publishes capabilities
/// to a connected TestClient / future WPF manager.
/// This is NOT a real in-game loader.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // --detect <gamePath> : classify Unity Mono / IL2CPP without injecting
        var detectPath = GetArg(args, "--detect");
        if (detectPath is not null)
            return RunDetect(detectPath);

        var enableBridge = args.Any(a => a is "--bridge" or "-b");
        var pipeName = GetArg(args, "--pipe") ?? ProtocolConstants.DefaultPipeName;
        var token = GetArg(args, "--token") ?? PairingToken.Generate();
        var idleSeconds = int.TryParse(GetArg(args, "--idle"), out var idle) ? idle : 120;

        Console.WriteLine("=== Purple Haze PluginHost (Development Only) — Milestone 4 ===");
        Console.WriteLine("Loads plugins and optionally hosts the IPC bridge. Not a game injector.\n");

        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        Directory.CreateDirectory(pluginsDir);
        await StageSamplePluginAsync(pluginsDir).ConfigureAwait(false);

        await using var manager = new PluginLifecycleManager();
        await manager.LoadAllAsync(pluginsDir).ConfigureAwait(false);

        Console.WriteLine("\n--- Plugin summary ---");
        foreach (var p in manager.Plugins)
        {
            var id = p.Metadata?.Id ?? "(unknown)";
            Console.WriteLine($"  {id,-24} State={p.State,-12} Caps={p.Capabilities.GetAll().Count}");
            if (p.FailureReason is not null)
                Console.WriteLine($"      Failure: {p.FailureReason}");
        }

        if (!enableBridge)
        {
            Console.WriteLine("\nBridge disabled. Pass --bridge to host the IPC server for TestClient.");
            await manager.ShutdownAllAsync().ConfigureAwait(false);
            return 0;
        }

        Console.WriteLine("\n--- Bridge server ---");
        Console.WriteLine($"  Pipe : {pipeName}");
        Console.WriteLine($"  Token: {token}");
        Console.WriteLine();
        Console.WriteLine("  Connect with:");
        Console.WriteLine($"    dotnet run --project src/PurpleHaze.TestClient -- --token {token} --pipe {pipeName} --demo");
        Console.WriteLine();

        await using var server = new BridgeServer(pipeName, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = true,
            ServerName = "PurpleHaze.PluginHost",
            ServerVersion = "0.1.0"
        });

        await using var coordinator = new PluginBridgeCoordinator(manager, server);

        server.ClientConnected += s =>
            Console.WriteLine($"[bridge] Client connected. Session={s.SessionId}");
        server.ClientDisconnected += reason =>
            Console.WriteLine($"[bridge] Client disconnected: {reason}");
        server.MessageReceived += (_, msg) =>
            Console.WriteLine($"[bridge] ← {msg.MessageType} plugin={msg.PluginId} req={msg.RequestId}");

        await server.StartAsync().ConfigureAwait(false);
        Console.WriteLine($"Bridge listening. Idle timeout: {idleSeconds}s (Ctrl+C to stop).\n");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            // Wait until cancelled or idle timeout after last activity.
            var started = DateTimeOffset.UtcNow;
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(500, cts.Token).ConfigureAwait(false);
                if ((DateTimeOffset.UtcNow - started).TotalSeconds >= idleSeconds && !server.IsClientConnected)
                {
                    Console.WriteLine("Idle timeout with no client; shutting down.");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Shutdown requested.");
        }

        Console.WriteLine("\n--- Shutting down ---");
        await manager.ShutdownAllAsync().ConfigureAwait(false);
        Console.WriteLine("PluginHost finished.");
        return 0;
    }

    private static async Task StageSamplePluginAsync(string pluginsDir)
    {
        var sampleDir = Path.Combine(pluginsDir, "example.plugin");
        Directory.CreateDirectory(sampleDir);

        await File.WriteAllTextAsync(Path.Combine(sampleDir, "plugin.manifest.json"), """
            {
              "manifestVersion": 1,
              "id": "example.plugin",
              "name": "Example Plugin",
              "author": "Purple Haze Team",
              "version": "1.0.0",
              "description": "Sample plugin demonstrating capability registration and lifecycle.",
              "requiredApiVersion": "0.1.0",
              "dependencies": [],
              "entryType": "PurpleHaze.SamplePlugin.ExamplePlugin"
            }
            """).ConfigureAwait(false);

        var candidateDll = Path.Combine(AppContext.BaseDirectory, "PurpleHaze.SamplePlugin.dll");
        if (File.Exists(candidateDll))
        {
            File.Copy(candidateDll, Path.Combine(sampleDir, "PurpleHaze.SamplePlugin.dll"), overwrite: true);
            Console.WriteLine($"Staged sample DLL.");
        }
        else
        {
            Console.WriteLine("Sample DLL not beside host; default-context activation may be used.");
        }
    }

    private static int RunDetect(string gamePath)
    {
        Console.WriteLine("=== Purple Haze — Unity game detection (read-only) ===\n");
        var detection = UnityGameDetector.Detect(gamePath);
        Console.WriteLine($"Path     : {Path.GetFullPath(gamePath)}");
        Console.WriteLine($"IsUnity  : {detection.IsUnity}");
        Console.WriteLine($"Backend  : {detection.Backend}");
        Console.WriteLine($"Data     : {detection.DataDirectory ?? "(none)"}");
        Console.WriteLine($"Managed  : {detection.ManagedDirectory ?? "(none)"}");
        Console.WriteLine($"GameAsm  : {detection.GameAssemblyPath ?? "(none)"}");
        Console.WriteLine($"Exe      : {detection.ExecutablePath ?? "(none)"}");
        Console.WriteLine("Notes:");
        foreach (var n in detection.Notes)
            Console.WriteLine($"  - {n}");

        var registry = new RuntimeAdapterRegistry();
        var plan = registry.Plan(gamePath);
        Console.WriteLine();
        Console.WriteLine($"Adapter  : {plan.AdapterId}  Supported={plan.IsSupported}");
        Console.WriteLine($"Summary  : {plan.Summary}");
        if (plan.Steps.Count > 0)
        {
            Console.WriteLine("Plan steps:");
            foreach (var s in plan.Steps) Console.WriteLine($"  * {s}");
        }
        if (plan.Risks.Count > 0)
        {
            Console.WriteLine("Risks:");
            foreach (var s in plan.Risks) Console.WriteLine($"  ! {s}");
        }
        if (plan.UnsupportedReasons.Count > 0)
        {
            Console.WriteLine("Unsupported:");
            foreach (var s in plan.UnsupportedReasons) Console.WriteLine($"  x {s}");
        }
        Console.WriteLine();
        Console.WriteLine("No files were modified. Detection only.");
        return plan.IsSupported ? 0 : 2;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
