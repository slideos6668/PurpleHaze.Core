using PurpleHaze.Adapters.Unity;

namespace PurpleHaze.Adapters;

/// <summary>
/// Discovers which adapter applies to a game install path.
/// </summary>
public sealed class RuntimeAdapterRegistry
{
    private readonly IReadOnlyList<IRuntimeAdapter> _adapters;

    public RuntimeAdapterRegistry(IEnumerable<IRuntimeAdapter>? adapters = null)
    {
        _adapters = (adapters ?? DefaultAdapters()).ToList();
    }

    public IReadOnlyList<IRuntimeAdapter> Adapters => _adapters;

    public IRuntimeAdapter? FindHandler(string gameInstallPath) =>
        _adapters.FirstOrDefault(a => a.CanHandle(gameInstallPath));

    public RuntimeBootstrapPlan Plan(string gameInstallPath)
    {
        var handler = FindHandler(gameInstallPath);
        if (handler is not null)
            return handler.CreatePlan(gameInstallPath);

        var detection = UnityGameDetector.Detect(gameInstallPath);
        return new RuntimeBootstrapPlan
        {
            AdapterId = "none",
            GamePath = gameInstallPath,
            Backend = detection.Backend,
            IsSupported = false,
            Summary = "No adapter claimed this path.",
            UnsupportedReasons = detection.Notes
        };
    }

    private static IEnumerable<IRuntimeAdapter> DefaultAdapters()
    {
        yield return new UnityMonoAdapter();
        yield return new UnityIl2CppAdapter();
    }
}
