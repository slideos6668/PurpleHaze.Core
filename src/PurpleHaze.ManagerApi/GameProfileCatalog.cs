using System.Text.Json;

namespace PurpleHaze.ManagerApi;

/// <summary>
/// Loads game profiles from a folder of JSON files (and built-in defaults).
/// Multi-game support starts here: each game is a profile, not hard-coded ifs.
/// </summary>
public sealed class GameProfileCatalog
{
    private readonly List<GameProfile> _profiles = new();

    public IReadOnlyList<GameProfile> All => _profiles;

    public static GameProfileCatalog CreateDefault()
    {
        var c = new GameProfileCatalog();
        c.AddBuiltIns();
        return c;
    }

    public void AddBuiltIns()
    {
        _profiles.Add(new GameProfile
        {
            GameId = "muck",
            DisplayName = "Muck",
            SteamAppId = 1625450,
            ScriptingBackend = "mono",
            AdapterId = "unity.mono",
            DataFolderName = "Muck_Data",
            ExecutableNames = { "Muck.exe", "Muck" },
            RecommendedHost = "doorstop",
            LoaderSupported = true,
            SupportNotes = "Validated: Doorstop + Purple Haze + Harmony on Unity 2019.4 Mono."
        });

        // Generic fallback — any Unity Mono folder can try install; not "validated"
        _profiles.Add(new GameProfile
        {
            GameId = "unity.mono.generic",
            DisplayName = "Unity Mono (generic)",
            ScriptingBackend = "mono",
            AdapterId = "unity.mono",
            RecommendedHost = "doorstop",
            LoaderSupported = true,
            SupportNotes = "Experimental: any game with *_Data/Managed. Not validated per title."
        });

        _profiles.Add(new GameProfile
        {
            GameId = "unity.il2cpp",
            DisplayName = "Unity IL2CPP",
            ScriptingBackend = "il2cpp",
            AdapterId = "unity.il2cpp",
            RecommendedHost = "none",
            LoaderSupported = false,
            SupportNotes = "Not supported yet. Needs Il2CppInterop stack."
        });
    }

    public void LoadFromDirectory(string profilesDir)
    {
        if (!Directory.Exists(profilesDir)) return;
        foreach (var file in Directory.GetFiles(profilesDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var p = new GameProfile
                {
                    GameId = r.GetProperty("gameId").GetString() ?? Path.GetFileNameWithoutExtension(file),
                    DisplayName = r.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                    ScriptingBackend = r.TryGetProperty("scriptingBackend", out var sb) ? sb.GetString() ?? "mono" : "mono",
                    AdapterId = r.TryGetProperty("adapterId", out var ad) ? ad.GetString() ?? "unity.mono" : "unity.mono",
                    DataFolderName = r.TryGetProperty("dataFolderName", out var df) ? df.GetString() : null,
                    RecommendedHost = r.TryGetProperty("recommendedHost", out var rh) ? rh.GetString() ?? "doorstop" : "doorstop",
                    LoaderSupported = true
                };
                if (r.TryGetProperty("steamAppId", out var sid) && sid.TryGetInt32(out var appId))
                    p.SteamAppId = appId;
                if (r.TryGetProperty("executableNames", out var exs) && exs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in exs.EnumerateArray())
                    {
                        var s = e.GetString();
                        if (!string.IsNullOrEmpty(s)) p.ExecutableNames.Add(s);
                    }
                }
                // Replace built-in with same id
                _profiles.RemoveAll(x => x.GameId.Equals(p.GameId, StringComparison.OrdinalIgnoreCase));
                _profiles.Add(p);
            }
            catch
            {
                // skip bad profile files
            }
        }
    }

    public GameProfile? FindById(string gameId) =>
        _profiles.FirstOrDefault(p => p.GameId.Equals(gameId, StringComparison.OrdinalIgnoreCase));

    public GameProfile? MatchFolder(string gameFolder)
    {
        if (!Directory.Exists(gameFolder)) return null;
        var name = Path.GetFileName(gameFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        foreach (var p in _profiles.Where(x => x.GameId != "unity.mono.generic" && x.GameId != "unity.il2cpp"))
        {
            if (p.DataFolderName != null &&
                Directory.Exists(Path.Combine(gameFolder, p.DataFolderName)))
                return p;
            foreach (var exe in p.ExecutableNames)
            {
                if (File.Exists(Path.Combine(gameFolder, exe)))
                    return p;
            }
            if (name.Equals(p.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(p.GameId, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }
}
