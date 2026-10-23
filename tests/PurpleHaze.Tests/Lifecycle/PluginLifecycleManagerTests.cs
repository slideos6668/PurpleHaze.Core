using PurpleHaze.Loader.Lifecycle;
using PurpleHaze.Loader.Logging;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Tests.Lifecycle;

public class PluginLifecycleManagerTests : IAsyncLifetime
{
    private readonly string _tempRoot;
    private PluginLifecycleManager? _manager;

    public PluginLifecycleManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PH_Lifecycle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_manager is not null)
            await _manager.DisposeAsync();
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    private static void WriteManifest(string dir, string id, string? entryType = null, string[]? deps = null)
    {
        Directory.CreateDirectory(dir);
        var depsJson = deps is { Length: > 0 }
            ? "[" + string.Join(",", deps.Select(d => $"\"{d}\"")) + "]"
            : "[]";
        var entry = entryType is null ? "" : $",\n  \"entryType\": \"{entryType}\"";
        File.WriteAllText(Path.Combine(dir, "plugin.manifest.json"), $$"""
        {
          "manifestVersion": 1,
          "id": "{{id}}",
          "name": "{{id}}",
          "author": "test",
          "version": "1.0.0",
          "description": "test",
          "requiredApiVersion": "0.1.0",
          "dependencies": {{depsJson}}{{entry}}
        }
        """);
    }

    [Fact]
    public async Task LoadAll_InvalidManifest_MarkedFailedOrIncompatible()
    {
        var dir = Path.Combine(_tempRoot, "bad");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.manifest.json"), """
        {
          "manifestVersion": 1,
          "id": "Bad.Id",
          "name": "Bad",
          "author": "test",
          "version": "1.0.0",
          "requiredApiVersion": "0.1.0"
        }
        """);

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        var plugin = _manager.Plugins.Single();
        Assert.True(plugin.State is PluginState.Incompatible or PluginState.Failed);
        Assert.NotNull(plugin.FailureReason);
    }

    [Fact]
    public async Task LoadAll_MissingAssembly_WithoutDefaultContext_MarkedFailed()
    {
        // Unknown entry type, no DLL → must fail (cannot activate from default context).
        var dir = Path.Combine(_tempRoot, "orphan");
        WriteManifest(dir, "orphan.plugin", entryType: "Does.Not.Exist.Type");

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        var plugin = _manager.GetPlugin("orphan.plugin");
        Assert.NotNull(plugin);
        Assert.Equal(PluginState.Failed, plugin!.State);
        Assert.Contains("assembly", plugin.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAll_SamplePlugin_FromDefaultContext_Initializes()
    {
        _ = typeof(PurpleHaze.SamplePlugin.ExamplePlugin);
        var dir = Path.Combine(_tempRoot, "example.plugin");
        WriteManifest(dir, "example.plugin", entryType: "PurpleHaze.SamplePlugin.ExamplePlugin");

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        var plugin = _manager.GetPlugin("example.plugin");
        Assert.NotNull(plugin);
        Assert.Equal(PluginState.Running, plugin!.State);
        Assert.True(plugin.IsDefaultContext);
        Assert.True(plugin.Capabilities.GetAll().Count >= 6);

        await _manager.ShutdownAllAsync();
        Assert.Equal(PluginState.Shutdown, plugin.State);
    }

    [Fact]
    public async Task LoadAll_MissingDependency_DoesNotInitializeDependent()
    {
        var dir = Path.Combine(_tempRoot, "needs.dep");
        WriteManifest(dir, "needs.dep", entryType: "PurpleHaze.SamplePlugin.ExamplePlugin", deps: new[] { "missing.base" });

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        var plugin = _manager.GetPlugin("needs.dep");
        Assert.NotNull(plugin);
        Assert.Equal(PluginState.Failed, plugin!.State);
        Assert.Contains("dependency", plugin.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailureIsolation_OneBadPlugin_DoesNotBlockGoodPlugin()
    {
        _ = typeof(PurpleHaze.SamplePlugin.ExamplePlugin);
        WriteManifest(Path.Combine(_tempRoot, "good"), "example.plugin",
            entryType: "PurpleHaze.SamplePlugin.ExamplePlugin");
        WriteManifest(Path.Combine(_tempRoot, "bad"), "bad.plugin",
            entryType: "Totally.Missing.Type");
        var sampleDll = Path.Combine(AppContext.BaseDirectory, "PurpleHaze.SamplePlugin.dll");
        if (File.Exists(sampleDll))
            File.Copy(sampleDll, Path.Combine(_tempRoot, "good", "PurpleHaze.SamplePlugin.dll"), overwrite: true);

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        var good = _manager.GetPlugin("example.plugin");
        var bad = _manager.GetPlugin("bad.plugin");

        Assert.NotNull(good);
        Assert.True(good!.State == PluginState.Running,
            $"Expected good plugin Running but was {good.State}: {good.FailureReason}");
        Assert.NotNull(bad);
        Assert.Equal(PluginState.Failed, bad!.State);
    }

    [Fact]
    public async Task DuplicateIds_AllMarkedFailed()
    {
        WriteManifest(Path.Combine(_tempRoot, "p1"), "same.id",
            entryType: "PurpleHaze.SamplePlugin.ExamplePlugin");
        WriteManifest(Path.Combine(_tempRoot, "p2"), "same.id",
            entryType: "PurpleHaze.SamplePlugin.ExamplePlugin");

        _manager = new PluginLifecycleManager(new HostLogger());
        await _manager.LoadAllAsync(_tempRoot);

        // Only one slot per ID is tracked; the duplicate handling marks them failed before load.
        var all = _manager.Plugins.Where(p =>
            (p.Metadata?.Id ?? p.Discovery.Manifest?.Id) == "same.id").ToList();
        Assert.NotEmpty(all);
        Assert.All(all, p => Assert.Equal(PluginState.Failed, p.State));
    }
}
