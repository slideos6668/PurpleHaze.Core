using System.Text.Json;
using PurpleHaze.ManagerApi;

// Purple Haze Installer CLI — callable from WPF client
//   detect|install|uninstall|profiles|install-mod  [args]  [--json]

var json = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
var list = args.Where(a => !a.Equals("--json", StringComparison.OrdinalIgnoreCase)).ToList();
if (list.Count == 0) return PrintUsage(json);

var cmd = list[0].ToLowerInvariant();
var svc = new LoaderInstallService();
var profilesDir = FindProfilesDir();
if (profilesDir != null) svc.Catalog.LoadFromDirectory(profilesDir);

return cmd switch
{
    "profiles" => CmdProfiles(svc, json),
    "detect" when list.Count >= 2 => CmdDetect(svc, list[1], json),
    "install" when list.Count >= 2 => CmdInstall(svc, list, json),
    "uninstall" when list.Count >= 2 => CmdUninstall(svc, list[1], json),
    "install-mod" when list.Count >= 3 => CmdInstallMod(svc, list[1], list[2], json),
    _ => PrintUsage(json)
};

static int PrintUsage(bool json)
{
    if (json)
    {
        Console.WriteLine("""{"success":false,"code":"usage","message":"See docs"}""");
        return 1;
    }
    Console.WriteLine("""
        Purple Haze Loader Installer (multi-game Unity Mono)

          profiles
          detect     <gameFolder> [--json]
          install    <gameFolder> --payload <payloadRoot> [--doorstop <dir>] [--json]
          uninstall  <gameFolder> [--json]
          install-mod <gameFolder> <modFolder> [--json]

        Payload layout:
          payload/core/*.dll     (PurpleHaze.Unity.Mono.dll, SDK)
          payload/libs/*.dll     (optional shared Harmony)
          payload/doorstop/      (optional Doorstop extract)

        WPF client: run with --json and parse stdout.
        """);
    return 1;
}

static int CmdProfiles(LoaderInstallService svc, bool json)
{
    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(svc.Catalog.All, JsonOpts()));
        return 0;
    }
    foreach (var p in svc.Catalog.All)
        Console.WriteLine($"{p.GameId,-24} {(p.LoaderSupported ? "OK" : "NO"),-3} {p.DisplayName} — {p.SupportNotes}");
    return 0;
}

static int CmdDetect(LoaderInstallService svc, string game, bool json)
{
    var info = svc.Detect(game);
    if (json) { Console.WriteLine(JsonSerializer.Serialize(info, JsonOpts())); return info.LoaderCanInstall ? 0 : 2; }
    Console.WriteLine($"Path       : {info.Path}");
    Console.WriteLine($"Unity      : {info.IsUnity}  Backend={info.Backend}");
    Console.WriteLine($"Can install: {info.LoaderCanInstall}");
    Console.WriteLine($"Installed  : {info.LoaderAlreadyInstalled}");
    Console.WriteLine($"Profile    : {info.MatchedProfileId}");
    Console.WriteLine($"Summary    : {info.Summary}");
    foreach (var n in info.Notes) Console.WriteLine("  - " + n);
    return info.LoaderCanInstall ? 0 : 2;
}

static int CmdInstall(LoaderInstallService svc, List<string> list, bool json)
{
    var game = list[1];
    string? payload = null;
    string? doorstop = null;
    for (var i = 2; i < list.Count - 1; i++)
    {
        if (list[i].Equals("--payload", StringComparison.OrdinalIgnoreCase)) payload = list[i + 1];
        if (list[i].Equals("--doorstop", StringComparison.OrdinalIgnoreCase)) doorstop = list[i + 1];
    }
    payload ??= FindDefaultPayload();
    if (payload == null)
    {
        var fail = new ApiResult { Success = false, Code = "no_payload", Message = "Pass --payload <dir> with core/ DLLs" };
        Console.WriteLine(json ? JsonSerializer.Serialize(fail, JsonOpts()) : fail.Message);
        return 3;
    }
    var r = svc.Install(game, payload, doorstop);
    Console.WriteLine(json ? JsonSerializer.Serialize(r, JsonOpts()) : Format(r));
    return r.Success ? 0 : 4;
}

static int CmdUninstall(LoaderInstallService svc, string game, bool json)
{
    var r = svc.Uninstall(game);
    Console.WriteLine(json ? JsonSerializer.Serialize(r, JsonOpts()) : Format(r));
    return r.Success ? 0 : 5;
}

static int CmdInstallMod(LoaderInstallService svc, string game, string mod, bool json)
{
    var r = svc.InstallMod(game, mod);
    Console.WriteLine(json ? JsonSerializer.Serialize(r, JsonOpts()) : Format(r));
    return r.Success ? 0 : 6;
}

static string Format(ApiResult r)
{
    var s = (r.Success ? "OK" : "FAIL") + " [" + r.Code + "] " + r.Message;
    foreach (var step in r.Steps) s += "\n  * " + step;
    foreach (var w in r.Warnings) s += "\n  ! " + w;
    return s;
}

static JsonSerializerOptions JsonOpts() => new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

static string? FindProfilesDir()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8 && dir != null; i++)
    {
        var p = Path.Combine(dir, "src", "PurpleHaze.GameProfiles");
        if (Directory.Exists(p)) return p;
        p = Path.Combine(dir, "GameProfiles");
        if (Directory.Exists(p)) return p;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}

static string? FindDefaultPayload()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8 && dir != null; i++)
    {
        var p = Path.Combine(dir, "payload");
        if (Directory.Exists(Path.Combine(p, "core"))) return p;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}
