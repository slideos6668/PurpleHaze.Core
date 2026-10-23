using System;
using System.Threading;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Text.Json;
#endif
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.SamplePlugin;

/// <summary>
/// Working sample plugin for Milestone 1.
/// Registers several capabilities and operates against a simulated game state.
/// Does NOT modify any real game.
/// </summary>
public sealed class ExamplePlugin : IPlugin, IPluginActionHandler
{
    private IPluginContext? _context;
    private bool _exampleFeatureEnabled;
    private double _movementMultiplier = 1.0;
    private string _exampleMode = "Normal";
    private int _actionCount;

    public PluginMetadata Metadata { get; } = new()
    {
        Id = "example.plugin",
        Name = "Example Plugin",
        Author = "Purple Haze Team",
        Version = "1.0.0",
        Description = "Sample plugin demonstrating capability registration and lifecycle.",
        RequiredApiVersion = "0.1.0",
        Dependencies = Array.Empty<string>()
    };

    public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        context.Logger.Information("ExamplePlugin initializing...");

        // Load persisted values if any
        _exampleFeatureEnabled = context.Configuration.GetBool("exampleFeature", false);
        _movementMultiplier = context.Configuration.GetDouble("movementMultiplier", 1.0);
        _exampleMode = context.Configuration.GetString("exampleMode", "Normal");

        RegisterCapabilities(context);

        context.Logger.Information("ExamplePlugin initialized successfully.");
        await context.PublishEventAsync("plugin.started", new { pluginId = Metadata.Id }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_context is null) return;

        _context.Logger.Information("ExamplePlugin shutting down...");
        await _context.PublishEventAsync("plugin.stopped", new { pluginId = Metadata.Id }, cancellationToken)
            .ConfigureAwait(false);
        _context = null;
    }

    private void RegisterCapabilities(IPluginContext context)
    {
        var caps = context.Capabilities;

        caps.Register(new CapabilityDefinition
        {
            Id = "example.button.testAction",
            Label = "Test Action",
            ControlType = ControlType.Button,
            Description = "Increments a counter and logs an event (simulated)."
        });

        caps.Register(new CapabilityDefinition
        {
            Id = "example.toggle.feature",
            Label = "Example Feature",
            ControlType = ControlType.Toggle,
            Description = "Enables or disables the example feature (simulated).",
            InitialValue = _exampleFeatureEnabled
        });

        caps.Register(new CapabilityDefinition
        {
            Id = "example.slider.movement",
            Label = "Movement Multiplier",
            ControlType = ControlType.Slider,
            Description = "Simulated movement speed multiplier.",
            InitialValue = _movementMultiplier,
            Min = 0.1,
            Max = 5.0,
            Step = 0.1
        });

        caps.Register(new CapabilityDefinition
        {
            Id = "example.dropdown.mode",
            Label = "Example Mode",
            ControlType = ControlType.Dropdown,
            Description = "Selects a simulated operating mode.",
            InitialValue = _exampleMode,
            Options = new[] { "Normal", "Aggressive", "Stealth", "Debug" }
        });

        caps.Register(new CapabilityDefinition
        {
            Id = "example.command.status",
            Label = "example.status",
            ControlType = ControlType.DeveloperCommand,
            Description = "Returns current simulated state of the example plugin."
        });

        caps.Register(new CapabilityDefinition
        {
            Id = "example.status.indicator",
            Label = "Plugin Status",
            ControlType = ControlType.StatusIndicator,
            Description = "Shows whether the example feature is active.",
            InitialValue = _exampleFeatureEnabled ? "Active" : "Inactive"
        });
    }

    /// <summary>
    /// Handles an action invoked by the manager against a registered capability.
    /// Called by the host / bridge (not part of the public IPlugin contract yet).
    /// </summary>
    public Task<object?> HandleActionAsync(string capabilityId, object? argument, CancellationToken cancellationToken = default)
    {
        if (_context is null)
            throw new InvalidOperationException("Plugin is not initialized.");

        return capabilityId switch
        {
            "example.button.testAction" => HandleTestActionAsync(cancellationToken),
            "example.toggle.feature" => HandleToggleAsync(argument, cancellationToken),
            "example.slider.movement" => HandleSliderAsync(argument, cancellationToken),
            "example.dropdown.mode" => HandleDropdownAsync(argument, cancellationToken),
            "example.command.status" => HandleStatusCommandAsync(cancellationToken),
            _ => throw new ArgumentException($"Unknown capability '{capabilityId}'.", nameof(capabilityId))
        };
    }

    private async Task<object?> HandleTestActionAsync(CancellationToken ct)
    {
        _actionCount++;
        _context!.Logger.Information($"Test Action invoked. Count = {_actionCount}");
        await _context.PublishEventAsync("example.testAction", new { count = _actionCount }, ct).ConfigureAwait(false);
        return new { success = true, count = _actionCount };
    }

    private async Task<object?> HandleToggleAsync(object? argument, CancellationToken ct)
    {
        if (argument is not bool value && !TryConvertToBool(argument, out value))
            throw new ArgumentException("Toggle expects a boolean value.");

        _exampleFeatureEnabled = value;
        _context!.Configuration.Set("exampleFeature", value);
        _context.Capabilities.Update("example.status.indicator", b => b.WithValue(value ? "Active" : "Inactive"));
        _context.Logger.Information($"Example Feature set to {value}");
        await _context.PublishEventAsync("example.featureChanged", new { enabled = value }, ct).ConfigureAwait(false);
        return new { success = true, value };
    }

    private async Task<object?> HandleSliderAsync(object? argument, CancellationToken ct)
    {
        if (!TryConvertToDouble(argument, out var value))
            throw new ArgumentException("Slider expects a numeric value.");

        if (value < 0.1 || value > 5.0)
            throw new ArgumentOutOfRangeException(nameof(argument), "Movement Multiplier must be between 0.1 and 5.0.");

        _movementMultiplier = value;
        _context!.Configuration.Set("movementMultiplier", value);
        _context.Logger.Information($"Movement Multiplier set to {value:F2}");
        await _context.PublishEventAsync("example.movementChanged", new { multiplier = value }, ct).ConfigureAwait(false);
        return new { success = true, value };
    }

    private async Task<object?> HandleDropdownAsync(object? argument, CancellationToken ct)
    {
        var mode = argument switch
        {
#if NET8_0_OR_GREATER
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
#endif
            null => string.Empty,
            _ => argument.ToString() ?? string.Empty
        };
        var allowed = new[] { "Normal", "Aggressive", "Stealth", "Debug" };
        if (!allowed.Contains(mode, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid mode '{mode}'. Allowed: {string.Join(", ", allowed)}");

        _exampleMode = mode;
        _context!.Configuration.Set("exampleMode", mode);
        _context.Logger.Information($"Example Mode set to {mode}");
        await _context.PublishEventAsync("example.modeChanged", new { mode }, ct).ConfigureAwait(false);
        return new { success = true, mode };
    }

    private Task<object?> HandleStatusCommandAsync(CancellationToken ct)
    {
        var status = new
        {
            pluginId = Metadata.Id,
            featureEnabled = _exampleFeatureEnabled,
            movementMultiplier = _movementMultiplier,
            mode = _exampleMode,
            actionCount = _actionCount
        };
        _context!.Logger.Information("Status command executed.");
        return Task.FromResult<object?>(status);
    }

    private static bool TryConvertToBool(object? arg, out bool value)
    {
        value = false;
        if (arg is bool b) { value = b; return true; }
#if NET8_0_OR_GREATER
        if (arg is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (je.ValueKind == JsonValueKind.False) { value = false; return true; }
            if (je.ValueKind == JsonValueKind.String && bool.TryParse(je.GetString(), out var fromStr))
            { value = fromStr; return true; }
            return false;
        }
#endif
        if (arg is string s && bool.TryParse(s, out var parsed)) { value = parsed; return true; }
        return false;
    }

    private static bool TryConvertToDouble(object? arg, out double value)
    {
        value = 0;
        switch (arg)
        {
            case double d: value = d; return true;
            case float f: value = f; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
#if NET8_0_OR_GREATER
            case JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var jd):
                value = jd; return true;
            case JsonElement je2 when je2.ValueKind == JsonValueKind.String && double.TryParse(je2.GetString(), out var js):
                value = js; return true;
#endif
            case string s when double.TryParse(s, out var parsed): value = parsed; return true;
            default: return false;
        }
    }
}
