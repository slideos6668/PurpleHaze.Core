namespace PurpleHaze.Adapters.Unity;

/// <summary>
/// Detects whether a folder looks like a Unity game and which scripting backend it uses.
/// Detection is read-only: no files are modified.
/// </summary>
public static class UnityGameDetector
{
    public sealed record DetectionResult(
        bool IsUnity,
        UnityScriptingBackend Backend,
        string? DataDirectory,
        string? ManagedDirectory,
        string? ExecutablePath,
        string? GameAssemblyPath,
        IReadOnlyList<string> Notes);

    public static DetectionResult Detect(string gameInstallPath)
    {
        if (string.IsNullOrWhiteSpace(gameInstallPath) || !Directory.Exists(gameInstallPath))
        {
            return new DetectionResult(
                false, UnityScriptingBackend.Unknown, null, null, null, null,
                new[] { "Path does not exist." });
        }

        var notes = new List<string>();
        var full = Path.GetFullPath(gameInstallPath);

        var dataDirs = Directory.GetDirectories(full, "*_Data", SearchOption.TopDirectoryOnly)
            .OrderBy(d => d.Length)
            .ToList();

        if (full.EndsWith("_Data", StringComparison.OrdinalIgnoreCase) && Directory.Exists(full))
            dataDirs.Insert(0, full);

        string? dataDir = dataDirs.FirstOrDefault();
        string? managedDir = null;
        string? gameAssembly = null;
        string? exe = null;

        var root = dataDir is not null && dataDir.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(dataDir) ?? full
            : full;

        var exes = Directory.GetFiles(root, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(e => !Path.GetFileName(e).StartsWith("UnityCrash", StringComparison.OrdinalIgnoreCase))
            .ToList();
        exe = exes.FirstOrDefault();

        if (dataDir is not null)
        {
            notes.Add($"Found data directory: {Path.GetFileName(dataDir)}");
            var managed = Path.Combine(dataDir, "Managed");
            if (Directory.Exists(managed) &&
                Directory.EnumerateFiles(managed, "*.dll").Any())
            {
                managedDir = managed;
                notes.Add("Managed/*.dll present → Unity Mono (or hybrid) layout.");
            }

            var ga = Path.Combine(root, "GameAssembly.dll");
            if (File.Exists(ga))
            {
                gameAssembly = ga;
                notes.Add("GameAssembly.dll present → IL2CPP native game code.");
            }

            var ggm = Path.Combine(dataDir, "globalgamemanagers");
            var ggmAssets = Path.Combine(dataDir, "globalgamemanagers.assets");
            if (File.Exists(ggm) || File.Exists(ggmAssets))
                notes.Add("globalgamemanagers found (Unity player data).");
        }
        else
        {
            notes.Add("No *_Data directory found at top level.");
        }

        var isUnity = dataDir is not null || gameAssembly is not null;
        UnityScriptingBackend backend;
        if (!isUnity)
            backend = UnityScriptingBackend.NotUnity;
        else if (gameAssembly is not null && managedDir is null)
            backend = UnityScriptingBackend.Il2Cpp;
        else if (managedDir is not null && gameAssembly is null)
            backend = UnityScriptingBackend.Mono;
        else if (managedDir is not null && gameAssembly is not null)
        {
            backend = UnityScriptingBackend.Il2Cpp;
            notes.Add("Both Managed and GameAssembly present; classifying as IL2CPP.");
        }
        else
            backend = UnityScriptingBackend.Unknown;

        return new DetectionResult(isUnity, backend, dataDir, managedDir, exe, gameAssembly, notes);
    }
}
