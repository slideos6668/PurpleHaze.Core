# Plugin Developer Guide (Milestone 1)

## Quick start

1. Create a class library targeting `net8.0`.
2. Reference `PurpleHaze.SDK`.
3. Implement `IPlugin`.
4. Ship a `plugin.manifest.json` next to your DLL.

### Minimal plugin

```csharp
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Models;

public sealed class MyPlugin : IPlugin
{
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "my.author.plugin",
        Name = "My Plugin",
        Author = "Me",
        Version = "1.0.0",
        RequiredApiVersion = "0.1.0"
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        context.Logger.Information("Hello from MyPlugin");
        // Register capabilities via context.Capabilities.Register(...)
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
```

### Manifest (`plugin.manifest.json`)

```json
{
  "manifestVersion": 1,
  "id": "my.author.plugin",
  "name": "My Plugin",
  "author": "Me",
  "version": "1.0.0",
  "description": "Does something cool",
  "requiredApiVersion": "0.1.0",
  "dependencies": [],
  "entryType": "MyNamespace.MyPlugin"
}
```

### Plugin ID rules

- Lowercase letters, digits, and dots only.
- Must start with a letter.
- No consecutive dots, no leading/trailing dots.
- Example: `example.plugin`, `purple.haze.utils`.

### Capabilities

Register during `InitializeAsync`:

```csharp
context.Capabilities.Register(new CapabilityDefinition
{
    Id = "my.button.doThing",
    Label = "Do Thing",
    ControlType = ControlType.Button
});
```

Supported control types: `Button`, `Toggle`, `Slider`, `Dropdown`, `TextInput`, `StatusIndicator`, `DeveloperCommand`.

### Validation

Always validate incoming values in your action handlers. The sample plugin shows patterns for bool, double range, and enum-like dropdown checks.

### Testing

Use the development host (`PurpleHaze.PluginHost`) or write unit tests with a fake `IPluginContext` (see `ExamplePluginTests`).
