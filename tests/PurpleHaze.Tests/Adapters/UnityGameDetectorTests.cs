using PurpleHaze.Adapters;
using PurpleHaze.Adapters.Unity;

namespace PurpleHaze.Tests.Adapters;

public class UnityGameDetectorTests : IDisposable
{
    private readonly string _root;

    public UnityGameDetectorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PH_UnityDet_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void Detect_EmptyFolder_NotUnity()
    {
        var d = UnityGameDetector.Detect(_root);
        Assert.False(d.IsUnity);
        Assert.Equal(UnityScriptingBackend.NotUnity, d.Backend);
    }

    [Fact]
    public void Detect_MonoLayout()
    {
        var data = Path.Combine(_root, "SampleGame_Data");
        var managed = Path.Combine(data, "Managed");
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "Assembly-CSharp.dll"), "fake");
        File.WriteAllText(Path.Combine(data, "globalgamemanagers"), "fake");

        var d = UnityGameDetector.Detect(_root);
        Assert.True(d.IsUnity);
        Assert.Equal(UnityScriptingBackend.Mono, d.Backend);
        Assert.NotNull(d.ManagedDirectory);

        var plan = new UnityMonoAdapter().CreatePlan(_root);
        Assert.True(plan.IsSupported);
        Assert.Equal("unity.mono", plan.AdapterId);
    }

    [Fact]
    public void Detect_Il2CppLayout()
    {
        var data = Path.Combine(_root, "SampleGame_Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "globalgamemanagers"), "fake");
        File.WriteAllText(Path.Combine(_root, "GameAssembly.dll"), "fake");

        var d = UnityGameDetector.Detect(_root);
        Assert.True(d.IsUnity);
        Assert.Equal(UnityScriptingBackend.Il2Cpp, d.Backend);

        var monoPlan = new UnityMonoAdapter().CreatePlan(_root);
        Assert.False(monoPlan.IsSupported);

        var il2cppPlan = new UnityIl2CppAdapter().CreatePlan(_root);
        Assert.False(il2cppPlan.IsSupported); // intentionally not implemented
        Assert.Equal("unity.il2cpp", il2cppPlan.AdapterId);
    }

    [Fact]
    public void Registry_SelectsMonoAdapter()
    {
        var data = Path.Combine(_root, "G_Data");
        Directory.CreateDirectory(Path.Combine(data, "Managed"));
        File.WriteAllText(Path.Combine(data, "Managed", "UnityEngine.dll"), "x");

        var registry = new RuntimeAdapterRegistry();
        var handler = registry.FindHandler(_root);
        Assert.NotNull(handler);
        Assert.Equal("unity.mono", handler!.AdapterId);
    }
}
