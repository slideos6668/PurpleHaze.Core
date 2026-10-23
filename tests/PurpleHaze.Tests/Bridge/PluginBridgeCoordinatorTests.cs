using System.Collections.Concurrent;
using PurpleHaze.Bridge;
using PurpleHaze.Bridge.Hosting;
using PurpleHaze.Bridge.Session;
using PurpleHaze.Loader.Lifecycle;
using PurpleHaze.Loader.Logging;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Tests.Bridge;

public class PluginBridgeCoordinatorTests : IAsyncLifetime
{
    private readonly string _tempRoot;
    private PluginLifecycleManager? _lifecycle;
    private BridgeServer? _server;
    private PluginBridgeCoordinator? _coordinator;

    public PluginBridgeCoordinatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PH_Coord_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_coordinator is not null) await _coordinator.DisposeAsync();
        if (_server is not null) await _server.DisposeAsync();
        if (_lifecycle is not null) await _lifecycle.DisposeAsync();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    private static string UniquePipe() => "ph-m4-" + Guid.NewGuid().ToString("N")[..12];

    private async Task<(string pipe, string token)> StartHostWithSampleAsync()
    {
        var dir = Path.Combine(_tempRoot, "example.plugin");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "plugin.manifest.json"), """
        {
          "manifestVersion": 1,
          "id": "example.plugin",
          "name": "Example Plugin",
          "author": "test",
          "version": "1.0.0",
          "requiredApiVersion": "0.1.0",
          "dependencies": [],
          "entryType": "PurpleHaze.SamplePlugin.ExamplePlugin"
        }
        """);

        // Ensure the sample assembly is loadable: prefer copying the DLL next to the test host,
        // and also touch the type so the default context has it as a fallback.
        _ = typeof(PurpleHaze.SamplePlugin.ExamplePlugin);
        var sampleDll = Path.Combine(AppContext.BaseDirectory, "PurpleHaze.SamplePlugin.dll");
        if (File.Exists(sampleDll))
            File.Copy(sampleDll, Path.Combine(dir, "PurpleHaze.SamplePlugin.dll"), overwrite: true);

        _lifecycle = new PluginLifecycleManager(new HostLogger());
        await _lifecycle.LoadAllAsync(_tempRoot);

        var loaded = _lifecycle.GetPlugin("example.plugin");
        if (loaded is null || loaded.State != PurpleHaze.SDK.Models.PluginState.Running)
            throw new InvalidOperationException(
                $"Sample plugin not running after load. State={loaded?.State} Reason={loaded?.FailureReason}");

        var pipe = UniquePipe();
        var token = PairingToken.Generate();
        _server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        _coordinator = new PluginBridgeCoordinator(_lifecycle, _server);
        await _server.StartAsync();
        return (pipe, token);
    }

    [Fact]
    public async Task Connect_ReceivesCapabilityRegistrations()
    {
        var (pipe, token) = await StartHostWithSampleAsync();

        var registered = new ConcurrentBag<(string PluginId, string CapId)>();
        await using var client = new BridgeClient(pipe, token, clientName: "test");
        client.MessageReceived += msg =>
        {
            if (msg.MessageType != MessageTypes.CapabilityRegister) return;
            var payload = msg.GetPayload<CapabilityRegisterPayload>();
            if (payload?.Capability is not null)
                registered.Add((msg.PluginId ?? "", payload.Capability.Id));
        };

        await client.ConnectAsync();
        await _server!.WaitForClientAsync(TimeSpan.FromSeconds(5));

        // Allow publish to complete
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline && registered.Count < 6)
            await Task.Delay(50);

        Assert.Contains(registered, x => x.CapId == "example.button.testAction");
        Assert.Contains(registered, x => x.CapId == "example.toggle.feature");
        Assert.Contains(registered, x => x.CapId == "example.slider.movement");
        Assert.Contains(registered, x => x.CapId == "example.dropdown.mode");
        Assert.Contains(registered, x => x.CapId == "example.command.status");
        Assert.Contains(registered, x => x.CapId == "example.status.indicator");
    }

    [Fact]
    public async Task ExecuteAction_RoundTripsThroughCoordinator()
    {
        var (pipe, token) = await StartHostWithSampleAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        await _server!.WaitForClientAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200); // capabilities publish

        var result = await client.ExecuteActionAsync(
            "example.plugin",
            "example.button.testAction",
            argument: null,
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Result);

        var toggle = await client.ExecuteActionAsync(
            "example.plugin",
            "example.toggle.feature",
            argument: true,
            timeout: TimeSpan.FromSeconds(5));
        Assert.True(toggle.Success, toggle.Error);

        var bad = await client.ExecuteActionAsync(
            "example.plugin",
            "example.slider.movement",
            argument: 99.0,
            timeout: TimeSpan.FromSeconds(5));
        Assert.False(bad.Success);
        Assert.False(string.IsNullOrWhiteSpace(bad.Error));
    }

    [Fact]
    public async Task ExecuteAction_UnknownCapability_FailsCleanly()
    {
        var (pipe, token) = await StartHostWithSampleAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        await _server!.WaitForClientAsync(TimeSpan.FromSeconds(5));

        var result = await client.ExecuteActionAsync(
            "example.plugin",
            "does.not.exist",
            timeout: TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
