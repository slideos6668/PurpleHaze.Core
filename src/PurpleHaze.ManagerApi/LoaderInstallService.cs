using PurpleHaze.Adapters;
using PurpleHaze.Adapters.Unity;

namespace PurpleHaze.ManagerApi;

/// <summary>
/// Core operations the WPF "Install mod loader" button should call.
/// Does not depend on WPF — pure library.
/// </summary>
public sealed class LoaderInstallService
{
    private readonly GameProfileCatalog _catalog;

    public LoaderInstallService(GameProfileCatalog? catalog = null)
    {
        _catalog = catalog ?? GameProfileCatalog.CreateDefault();
    }

    public GameProfileCatalog Catalog => _catalog;

    /// <summary>Detect game folder + whether we can install.</summary>
    public DetectInfo Detect(string gameFolder)
    {
        gameFolder = Path.GetFullPath(gameFolder);
        var d = UnityGameDetector.Detect(gameFolder);
        var profile = _catalog.MatchFolder(gameFolder);
        var installed = Directory.Exists(Path.Combine(gameFolder, "PurpleHaze", "core"))
                        && File.Exists(Path.Combine(gameFolder, "doorstop_config.ini"));

        var can = d.IsUnity && d.Backend == UnityScriptingBackend.Mono;
        var backend = d.Backend.ToString();
        if (d.Backend == UnityScriptingBackend.Il2Cpp)
            can = false;

        return new DetectInfo
        {
            Path = gameFolder,
            IsUnity = d.IsUnity,
            Backend = backend,
            ManagedDirectory = d.ManagedDirectory,
            ExecutablePath = d.ExecutablePath,
            LoaderCanInstall = can,
            LoaderAlreadyInstalled = installed,
            MatchedProfileId = profile?.GameId ?? (can ? "unity.mono.generic" : null),
            Summary = can
                ? (installed ? "Purple Haze loader already present." : "Unity Mono — loader can be installed.")
                : d.Backend == UnityScriptingBackend.Il2Cpp
                    ? "Unity IL2CPP — not supported yet."
                    : "Not a supported Unity Mono install.",
            Notes = d.Notes.ToList()
        };
    }

    /// <summary>
    /// Install Doorstop + Purple Haze core into the game folder.
    /// <paramref name="payloadRoot"/> must contain:
    ///   core/*.dll (Unity.Mono + SDK)
    ///   doorstop/   optional pre-extracted Doorstop (winhttp.dll etc.)
    /// </summary>
    public ApiResult Install(string gameFolder, string payloadRoot, string? doorstopDir = null)
    {
        var result = new ApiResult { Code = "install" };
        gameFolder = Path.GetFullPath(gameFolder);
        payloadRoot = Path.GetFullPath(payloadRoot);

        var info = Detect(gameFolder);
        if (!info.LoaderCanInstall)
        {
            result.Success = false;
            result.Message = info.Summary;
            result.Code = "unsupported";
            return result;
        }

        try
        {
            var ph = Path.Combine(gameFolder, "PurpleHaze");
            var core = Path.Combine(ph, "core");
            var plugins = Path.Combine(ph, "plugins");
            var logs = Path.Combine(ph, "logs");
            var state = Path.Combine(ph, "state");
            var libs = Path.Combine(ph, "libs");
            Directory.CreateDirectory(core);
            Directory.CreateDirectory(plugins);
            Directory.CreateDirectory(logs);
            Directory.CreateDirectory(state);
            Directory.CreateDirectory(libs);
            result.Steps.Add("Created PurpleHaze folders");

            // Copy core payload
            var payloadCore = Path.Combine(payloadRoot, "core");
            if (!Directory.Exists(payloadCore))
            {
                result.Success = false;
                result.Message = "Payload missing core/ folder: " + payloadCore;
                result.Code = "missing_payload";
                return result;
            }

            foreach (var f in Directory.GetFiles(payloadCore, "*.dll"))
            {
                var dest = Path.Combine(core, Path.GetFileName(f));
                File.Copy(f, dest, overwrite: true);
            }
            result.Steps.Add("Copied core DLLs");

            // Optional shared libs (Harmony pack)
            var payloadLibs = Path.Combine(payloadRoot, "libs");
            if (Directory.Exists(payloadLibs))
            {
                foreach (var f in Directory.GetFiles(payloadLibs, "*.dll"))
                    File.Copy(f, Path.Combine(libs, Path.GetFileName(f)), true);
                result.Steps.Add("Copied shared libs");
            }

            // Doorstop config
            var entryRel = "PurpleHaze/core/PurpleHaze.Unity.Mono.dll";
            var ini = Path.Combine(gameFolder, "doorstop_config.ini");
            File.WriteAllText(ini, BuildDoorstopIni(entryRel));
            result.Steps.Add("Wrote doorstop_config.ini");

            // Doorstop proxy DLL
            var ds = doorstopDir ?? Path.Combine(payloadRoot, "doorstop");
            if (Directory.Exists(ds))
            {
                var copied = 0;
                foreach (var name in new[] { "winhttp.dll", "version.dll", "winhttp.dll.disabled" })
                {
                    foreach (var f in Directory.GetFiles(ds, name, SearchOption.AllDirectories))
                    {
                        File.Copy(f, Path.Combine(gameFolder, Path.GetFileName(f)), true);
                        copied++;
                        break;
                    }
                }
                // also copy any .dll at top of doorstop extract
                foreach (var f in Directory.GetFiles(ds, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(f, Path.Combine(gameFolder, Path.GetFileName(f)), true);
                    copied++;
                }
                result.Steps.Add(copied > 0 ? "Copied Doorstop proxy DLL(s)" : "Doorstop folder present but no proxy DLL found");
                if (copied == 0)
                    result.Warnings.Add("Place Doorstop winhttp.dll next to the game exe or pass doorstop folder.");
            }
            else
            {
                result.Warnings.Add("No Doorstop folder — copy winhttp.dll from UnityDoorstop releases next to the game exe.");
            }

            // Marker for client
            File.WriteAllText(Path.Combine(ph, "loader.installed"), DateTime.UtcNow.ToString("o") + "\n" + (info.MatchedProfileId ?? ""));
            result.Success = true;
            result.Message = "Purple Haze loader installed.";
            result.Code = "ok";
            result.Data["gameFolder"] = gameFolder;
            result.Data["profile"] = info.MatchedProfileId ?? "";
            result.Data["core"] = core;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Code = "error";
            result.Message = ex.Message;
            return result;
        }
    }

    public ApiResult Uninstall(string gameFolder)
    {
        var result = new ApiResult { Code = "uninstall" };
        gameFolder = Path.GetFullPath(gameFolder);
        try
        {
            var ini = Path.Combine(gameFolder, "doorstop_config.ini");
            if (File.Exists(ini)) { File.Delete(ini); result.Steps.Add("Removed doorstop_config.ini"); }

            foreach (var proxy in new[] { "winhttp.dll", "version.dll" })
            {
                var p = Path.Combine(gameFolder, proxy);
                if (File.Exists(p))
                {
                    // only remove if our install — soft: rename aside
                    try { File.Move(p, p + ".purplehaze.bak"); result.Steps.Add("Disabled " + proxy); }
                    catch { result.Warnings.Add("Could not move " + proxy); }
                }
            }

            var ph = Path.Combine(gameFolder, "PurpleHaze");
            if (Directory.Exists(ph))
            {
                // Keep plugins/ by default; remove core marker only? User asked uninstall loader.
                var core = Path.Combine(ph, "core");
                if (Directory.Exists(core))
                {
                    Directory.Delete(core, true);
                    result.Steps.Add("Removed PurpleHaze/core");
                }
                var marker = Path.Combine(ph, "loader.installed");
                if (File.Exists(marker)) File.Delete(marker);
            }

            result.Success = true;
            result.Message = "Loader uninstalled (plugins folder left intact if present).";
            result.Code = "ok";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Code = "error";
            result.Message = ex.Message;
            return result;
        }
    }

    /// <summary>Install a mod folder (manifest + dlls) into PurpleHaze/plugins/{id}.</summary>
    public ApiResult InstallMod(string gameFolder, string modSourceFolder)
    {
        var result = new ApiResult { Code = "install_mod" };
        gameFolder = Path.GetFullPath(gameFolder);
        modSourceFolder = Path.GetFullPath(modSourceFolder);

        var manifest = Path.Combine(modSourceFolder, "manifest.json");
        if (!File.Exists(manifest))
        {
            result.Success = false;
            result.Message = "manifest.json required in mod folder";
            result.Code = "no_manifest";
            return result;
        }

        var id = "mod";
        try
        {
            var text = File.ReadAllText(manifest);
            var idx = text.IndexOf("\"id\"", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var colon = text.IndexOf(':', idx);
                var q1 = text.IndexOf('"', colon + 1);
                var q2 = text.IndexOf('"', q1 + 1);
                if (q1 >= 0 && q2 > q1) id = text.Substring(q1 + 1, q2 - q1 - 1);
            }
        }
        catch { /* default id */ }

        var dest = Path.Combine(gameFolder, "PurpleHaze", "plugins", id);
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(modSourceFolder))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);

        result.Success = true;
        result.Code = "ok";
        result.Message = "Mod installed to " + dest;
        result.Data["pluginId"] = id;
        result.Data["path"] = dest;
        return result;
    }

    private static string BuildDoorstopIni(string targetAssembly) =>
        $"""
        [General]
        enabled=true
        target_assembly={targetAssembly}
        redirect_output_log=false
        ignore_disable_switch=false

        [UnityDll]
        # left default

        [Multiplayer]
        enabled=false
        """;
}
