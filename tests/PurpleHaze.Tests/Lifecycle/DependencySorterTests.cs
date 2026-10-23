using PurpleHaze.Loader.Lifecycle;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Tests.Lifecycle;

public class DependencySorterTests
{
    private static PluginMetadata Meta(string id, params string[] deps) => new()
    {
        Id = id,
        Name = id,
        Author = "test",
        Version = "1.0.0",
        RequiredApiVersion = "0.1.0",
        Dependencies = deps
    };

    [Fact]
    public void Sort_NoDependencies_PreservesAll()
    {
        var plugins = new[] { Meta("a"), Meta("b"), Meta("c") };
        var result = DependencySorter.Sort(plugins);
        Assert.Equal(3, result.Ordered.Count);
        Assert.Empty(result.MissingDependencies);
        Assert.Empty(result.Cycles);
    }

    [Fact]
    public void Sort_LinearChain_OrdersDependenciesFirst()
    {
        var plugins = new[]
        {
            Meta("c", "b"),
            Meta("b", "a"),
            Meta("a")
        };
        var result = DependencySorter.Sort(plugins);
        Assert.Equal(3, result.Ordered.Count);
        var ids = result.Ordered.Select(p => p.Id).ToList();
        Assert.True(ids.IndexOf("a") < ids.IndexOf("b"));
        Assert.True(ids.IndexOf("b") < ids.IndexOf("c"));
    }

    [Fact]
    public void Sort_MissingDependency_ReportedAndExcluded()
    {
        var plugins = new[] { Meta("a", "missing.dep") };
        var result = DependencySorter.Sort(plugins);
        Assert.Empty(result.Ordered);
        Assert.Contains(result.MissingDependencies, m => m.Contains("missing.dep"));
    }

    [Fact]
    public void Sort_Cycle_Detected()
    {
        var plugins = new[]
        {
            Meta("a", "b"),
            Meta("b", "a")
        };
        var result = DependencySorter.Sort(plugins);
        // Both participate in a cycle; ordered may be empty or partial depending on visit order.
        Assert.NotEmpty(result.Cycles);
    }
}
