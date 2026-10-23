using System.Collections.Concurrent;
using System.Text.Json;
using PurpleHaze.Bridge;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.TestClient;

/// <summary>
/// Diagnostic manager-side client for Milestone 4.
/// Connects to the PluginHost bridge, renders registered capabilities in the console,
/// and lets you invoke actions interactively or via command-line flags.
/// This is NOT the real Purple Haze WPF UI.
/// </summary>
internal static class Program
{
    private static readonly ConcurrentDictionary<string, TrackedCapability> Capabilities = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main(string[] args)
    {
        var pipeName = GetArg(args, "--pipe") ?? ProtocolConstants.DefaultPipeName;
        var token = GetArg(args, "--token") ?? Environment.GetEnvironmentVariable("PURPLEHAZE_TOKEN");
        var autoDemo = args.Any(a => a is "--demo" or "-d");
        var waitSeconds = int.TryParse(GetArg(args, "--wait"), out var w) ? w : 15;

        Console.WriteLine("=== Purple Haze TestClient (Diagnostic) — Milestone 4 ===");
        Console.WriteLine("Connects to PluginHost over the bridge. Not the real WPF manager.\n");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  PurpleHaze.TestClient --token <pairingToken> [--pipe <name>] [--demo] [--wait <seconds>]");
            Console.WriteLine();
            Console.WriteLine("Start PluginHost with --bridge first; it prints the pairing token.");
            return 1;
        }

        await using var client = new BridgeClient(pipeName, token, clientName: "PurpleHaze.TestClient");
        client.MessageReceived += OnMessage;

        Console.WriteLine($"Connecting to pipe '{pipeName}'...");
        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connect failed: {ex.Message}");
            return 2;
        }

        Console.WriteLine("Connected and authenticated.\n");

        // Wait for capability registrations
        var deadline = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);
        while (DateTimeOffset.UtcNow < deadline && Capabilities.IsEmpty)
            await Task.Delay(100);

        // Allow a short burst for multiple capability messages
        await Task.Delay(300);

        RenderCapabilities();

        if (autoDemo)
        {
            await RunDemoAsync(client);
        }
        else if (!Console.IsInputRedirected)
        {
            await InteractiveLoopAsync(client);
        }
        else
        {
            Console.WriteLine("No TTY and no --demo; exiting after capability snapshot.");
        }

        Console.WriteLine("\nTestClient exiting.");
        return 0;
    }

    private static void OnMessage(ProtocolMessage message)
    {
        switch (message.MessageType)
        {
            case MessageTypes.CapabilityRegister:
            {
                var payload = message.GetPayload<CapabilityRegisterPayload>();
                if (payload?.Capability is null) return;
                var key = Key(message.PluginId, payload.Capability.Id);
                Capabilities[key] = new TrackedCapability(message.PluginId ?? "?", payload.Capability);
                Console.WriteLine($"  [+] capability {key} ({payload.Capability.ControlType})");
                break;
            }
            case MessageTypes.CapabilityUpdate:
            {
                var payload = message.GetPayload<CapabilityUpdatePayload>();
                if (payload is null) return;
                var key = Key(message.PluginId, payload.CapabilityId);
                if (Capabilities.TryGetValue(key, out var existing))
                {
                    existing.Dto.InitialValue = payload.Value ?? existing.Dto.InitialValue;
                    if (payload.IsEnabled is not null) existing.Dto.IsEnabled = payload.IsEnabled.Value;
                    if (payload.Label is not null) existing.Dto.Label = payload.Label;
                    Console.WriteLine($"  [~] updated {key} => {FormatValue(existing.Dto.InitialValue)}");
                }
                break;
            }
            case MessageTypes.CapabilityUnregister:
            {
                var payload = message.GetPayload<CapabilityUnregisterPayload>();
                if (payload is null) return;
                var key = Key(message.PluginId, payload.CapabilityId);
                Capabilities.TryRemove(key, out _);
                Console.WriteLine($"  [-] removed {key}");
                break;
            }
            case MessageTypes.Log:
            {
                var log = message.GetPayload<LogPayload>();
                Console.WriteLine($"  [log:{message.PluginId}] {log?.Level}: {log?.Message}");
                break;
            }
            case MessageTypes.Event:
            {
                var ev = message.GetPayload<EventPayload>();
                Console.WriteLine($"  [event:{message.PluginId}] {ev?.EventName} {FormatValue(ev?.Data)}");
                break;
            }
            case MessageTypes.PluginState:
            {
                var st = message.GetPayload<PluginStatePayload>();
                Console.WriteLine($"  [state:{message.PluginId}] {st?.State} {st?.Reason}");
                break;
            }
        }
    }

    private static void RenderCapabilities()
    {
        Console.WriteLine("\n=== Registered capabilities ===");
        if (Capabilities.IsEmpty)
        {
            Console.WriteLine("  (none received yet)");
            return;
        }

        var groups = Capabilities.Values.GroupBy(c => c.PluginId);
        foreach (var group in groups)
        {
            Console.WriteLine($"  Plugin: {group.Key}");
            foreach (var cap in group.OrderBy(c => c.Dto.Id))
            {
                Console.WriteLine(
                    $"    [{cap.Dto.ControlType,-16}] {cap.Dto.Id,-32} {cap.Dto.Label,-24} value={FormatValue(cap.Dto.InitialValue)}");
                if (cap.Dto.Options is { Count: > 0 })
                    Console.WriteLine($"         options: {string.Join(", ", cap.Dto.Options)}");
                if (cap.Dto.Min is not null || cap.Dto.Max is not null)
                    Console.WriteLine($"         range: {cap.Dto.Min} .. {cap.Dto.Max} step {cap.Dto.Step}");
            }
        }
        Console.WriteLine();
    }

    private static async Task RunDemoAsync(BridgeClient client)
    {
        Console.WriteLine("--- Auto demo ---");
        var pluginId = Capabilities.Values.Select(c => c.PluginId).FirstOrDefault() ?? "example.plugin";

        await Invoke(client, pluginId, "example.button.testAction", null);
        await Invoke(client, pluginId, "example.toggle.feature", true);
        await Invoke(client, pluginId, "example.slider.movement", 2.0);
        await Invoke(client, pluginId, "example.dropdown.mode", "Stealth");
        await Invoke(client, pluginId, "example.command.status", null);

        // Invalid value should fail cleanly
        Console.WriteLine("\nExpecting validation failure for out-of-range slider...");
        await Invoke(client, pluginId, "example.slider.movement", 99.0);

        RenderCapabilities();
    }

    private static async Task InteractiveLoopAsync(BridgeClient client)
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                         – show capabilities");
        Console.WriteLine("  invoke <pluginId> <capId> [jsonArg] – execute action");
        Console.WriteLine("  demo                         – run built-in demo");
        Console.WriteLine("  quit                         – exit");
        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line is "quit" or "exit" or "q")
                break;
            if (line is "list" or "ls")
            {
                RenderCapabilities();
                continue;
            }
            if (line is "demo")
            {
                await RunDemoAsync(client);
                continue;
            }

            if (line.StartsWith("invoke ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitArgs(line[7..]);
                if (parts.Count < 2)
                {
                    Console.WriteLine("Usage: invoke <pluginId> <capabilityId> [jsonArg]");
                    continue;
                }
                object? arg = null;
                if (parts.Count >= 3)
                {
                    try { arg = JsonSerializer.Deserialize<JsonElement>(parts[2]); }
                    catch { arg = parts[2]; }
                }
                await Invoke(client, parts[0], parts[1], arg);
                continue;
            }

            Console.WriteLine("Unknown command.");
        }
    }

    private static async Task Invoke(BridgeClient client, string pluginId, string capabilityId, object? argument)
    {
        Console.WriteLine($"  → {pluginId} / {capabilityId} arg={FormatValue(argument)}");
        try
        {
            var result = await client.ExecuteActionAsync(pluginId, capabilityId, argument, timeout: TimeSpan.FromSeconds(8));
            if (result.Success)
                Console.WriteLine($"  ← OK  {FormatValue(result.Result)}");
            else
                Console.WriteLine($"  ← FAIL  {result.Error}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ← ERROR  {ex.Message}");
        }
    }

    private static string Key(string? pluginId, string capabilityId) =>
        $"{pluginId ?? "?"}::{capabilityId}";

    private static string FormatValue(object? value)
    {
        if (value is null) return "null";
        if (value is JsonElement je) return je.ToString();
        try { return JsonSerializer.Serialize(value); }
        catch { return value.ToString() ?? ""; }
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static List<string> SplitArgs(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inJson = false;
        foreach (var ch in line)
        {
            if (ch == '{') inJson = true;
            if (ch == '}') { inJson = false; current.Append(ch); result.Add(current.ToString()); current.Clear(); continue; }
            if (char.IsWhiteSpace(ch) && !inJson)
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private sealed class TrackedCapability(string pluginId, CapabilityDto dto)
    {
        public string PluginId { get; } = pluginId;
        public CapabilityDto Dto { get; } = dto;
    }
}
