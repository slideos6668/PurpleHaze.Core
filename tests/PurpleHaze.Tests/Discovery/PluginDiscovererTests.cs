using PurpleHaze.Loader.Discovery;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Tests.Discovery;

public class PluginDiscovererTests : IDisposable
{
    private readonly string _tempRoot;

    public PluginDiscovererTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PurpleHazeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Discover_EmptyDirectory_ReturnsEmpty()
    {
        var discoverer = new PluginDiscoverer();
        var results = discoverer.Discover(_tempRoot);
        Assert.Empty(results);
    }

    [Fact]
    public void Discover_ValidManifest_ReturnsValidated()
    {
        var pluginDir = Path.Combine(_tempRoot, "example.plugin");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "plugin.manifest.json"), """
        {
          "manifestVersion": 1,
          "id": "example.plugin",
          "name": "Example",
          "author": "Test",
          "version": "1.0.0",
          "description": "Test",
          "requiredApiVersion": "0.1.0",
          "dependencies": []
        }
        """);

        var discoverer = new PluginDiscoverer();
        var results = discoverer.Discover(_tempRoot);

        Assert.Single(results);
        var r = results[0];
        Assert.True(r.IsValid);
        Assert.Equal(PluginState.Validated, r.State);
        Assert.NotNull(r.Metadata);
        Assert.Equal("example.plugin", r.Metadata!.Id);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Discover_InvalidId_ReturnsIncompatible()
    {
        var pluginDir = Path.Combine(_tempRoot, "bad");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "plugin.manifest.json"), """
        {
          "manifestVersion": 1,
          "id": "Bad.Id",
          "name": "Bad",
          "author": "Test",
          "version": "1.0.0",
          "requiredApiVersion": "0.1.0"
        }
        """);

        var discoverer = new PluginDiscoverer();
        var results = discoverer.Discover(_tempRoot);

        Assert.Single(results);
        Assert.False(results[0].IsValid);
        Assert.Equal(PluginState.Incompatible, results[0].State);
        Assert.NotEmpty(results[0].Errors);
    }

    [Fact]
    public void Discover_MissingManifest_ReturnsFailed()
    {
        var pluginDir = Path.Combine(_tempRoot, "empty");
        Directory.CreateDirectory(pluginDir);

        var discoverer = new PluginDiscoverer();
        var results = discoverer.Discover(_tempRoot);

        Assert.Single(results);
        Assert.False(results[0].IsValid);
        Assert.Equal(PluginState.Failed, results[0].State);
    }

    [Fact]
    public void Discover_DuplicateIds_BothReported()
    {
        // Two folders with same id – discovery reports both; uniqueness is checked later by loader.
        for (int i = 0; i < 2; i++)
        {
            var pluginDir = Path.Combine(_tempRoot, $"plugin{i}");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(Path.Combine(pluginDir, "plugin.manifest.json"), """
            {
              "manifestVersion": 1,
              "id": "same.id",
              "name": "Same",
              "author": "Test",
              "version": "1.0.0",
              "requiredApiVersion": "0.1.0"
            }
            """);
        }

        var discoverer = new PluginDiscoverer();
        var results = discoverer.Discover(_tempRoot);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsValid));
    }
}
