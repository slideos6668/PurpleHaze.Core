using PurpleHaze.SamplePlugin;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Configuration;
using PurpleHaze.SDK.Logging;

namespace PurpleHaze.Tests.SamplePlugin;

public class ExamplePluginTests
{
    private sealed class FakeLogger : IPluginLogger
    {
        public List<string> Messages { get; } = new();
        public void Trace(string m) => Messages.Add("T:" + m);
        public void Debug(string m) => Messages.Add("D:" + m);
        public void Information(string m) => Messages.Add("I:" + m);
        public void Warning(string m) => Messages.Add("W:" + m);
        public void Error(string m, Exception? e = null) => Messages.Add("E:" + m);
        public void Critical(string m, Exception? e = null) => Messages.Add("C:" + m);
    }

    private sealed class FakeConfig : IPluginConfiguration
    {
        private readonly Dictionary<string, object> _store = new();
        public string GetString(string key, string defaultValue = "") =>
            _store.TryGetValue(key, out var v) ? v?.ToString() ?? defaultValue : defaultValue;
        public bool GetBool(string key, bool defaultValue = false) =>
            _store.TryGetValue(key, out var v) && v is bool b ? b : defaultValue;
        public int GetInt(string key, int defaultValue = 0) =>
            _store.TryGetValue(key, out var v) && v is int i ? i : defaultValue;
        public double GetDouble(string key, double defaultValue = 0.0) =>
            _store.TryGetValue(key, out var v) && v is double d ? d : defaultValue;
        public void Set(string key, object value) => _store[key] = value;
        public bool ContainsKey(string key) => _store.ContainsKey(key);
    }

    private sealed class FakeCapabilities : ICapabilityRegistry
    {
        private readonly Dictionary<string, CapabilityDefinition> _caps = new();
        public void Register(CapabilityDefinition d) => _caps[d.Id] = d;
        public void Update(string id, Action<CapabilityDefinitionBuilder> configure)
        {
            if (!_caps.TryGetValue(id, out var current)) return;
            var builder = new CapabilityDefinitionBuilder(current);
            configure(builder);
        }
        public void Unregister(string id) => _caps.Remove(id);
        public IReadOnlyList<CapabilityDefinition> GetAll() => _caps.Values.ToList();
    }

    private sealed class FakeContext : IPluginContext
    {
        public IPluginLogger Logger { get; } = new FakeLogger();
        public IPluginConfiguration Configuration { get; } = new FakeConfig();
        public ICapabilityRegistry Capabilities { get; } = new FakeCapabilities();
        public List<(string Name, object? Payload)> Events { get; } = new();
        public void ReportError(string message, Exception? exception = null) { }
        public Task PublishEventAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default)
        {
            Events.Add((eventName, payload));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Initialize_RegistersExpectedCapabilities()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();

        await plugin.InitializeAsync(ctx);

        var caps = ctx.Capabilities.GetAll();
        Assert.Contains(caps, c => c.Id == "example.button.testAction" && c.ControlType == ControlType.Button);
        Assert.Contains(caps, c => c.Id == "example.toggle.feature" && c.ControlType == ControlType.Toggle);
        Assert.Contains(caps, c => c.Id == "example.slider.movement" && c.ControlType == ControlType.Slider);
        Assert.Contains(caps, c => c.Id == "example.dropdown.mode" && c.ControlType == ControlType.Dropdown);
        Assert.Contains(caps, c => c.Id == "example.command.status" && c.ControlType == ControlType.DeveloperCommand);
        Assert.Contains(caps, c => c.Id == "example.status.indicator" && c.ControlType == ControlType.StatusIndicator);

        Assert.Contains(ctx.Events, e => e.Name == "plugin.started");
    }

    [Fact]
    public async Task HandleAction_TestButton_IncrementsCount()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();
        await plugin.InitializeAsync(ctx);

        var result1 = await plugin.HandleActionAsync("example.button.testAction", null);
        var result2 = await plugin.HandleActionAsync("example.button.testAction", null);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
    }

    [Fact]
    public async Task HandleAction_Toggle_ValidatesBoolean()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();
        await plugin.InitializeAsync(ctx);

        await plugin.HandleActionAsync("example.toggle.feature", true);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            plugin.HandleActionAsync("example.toggle.feature", "not-a-bool"));
    }

    [Fact]
    public async Task HandleAction_Slider_ValidatesRange()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();
        await plugin.InitializeAsync(ctx);

        await plugin.HandleActionAsync("example.slider.movement", 2.5);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            plugin.HandleActionAsync("example.slider.movement", 99.0));
    }

    [Fact]
    public async Task HandleAction_Dropdown_ValidatesOptions()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();
        await plugin.InitializeAsync(ctx);

        await plugin.HandleActionAsync("example.dropdown.mode", "Stealth");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            plugin.HandleActionAsync("example.dropdown.mode", "InvalidMode"));
    }

    [Fact]
    public async Task Shutdown_PublishesStoppedEvent()
    {
        var plugin = new ExamplePlugin();
        var ctx = new FakeContext();
        await plugin.InitializeAsync(ctx);
        await plugin.ShutdownAsync();

        Assert.Contains(ctx.Events, e => e.Name == "plugin.stopped");
    }
}
