using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Configuration;
using PurpleHaze.SDK.Logging;
using PurpleHaze.SDK.Models;
using PurpleHaze.SDK.Validation;

namespace PurpleHaze.Unity.Mono;

/// <summary>
/// Loads plugins inside Unity Mono. No System.Text.Json.
/// Supports:
///   plugins/MyMod.dll                    — drop-in, no JSON
///   plugins/MyMod/MyMod.dll              — folder, no JSON
///   plugins/MyMod/manifest.json + dll    — optional manifest
/// Metadata from [PurpleHazePlugin] or IPlugin.Metadata when no manifest.
/// </summary>
internal static class PluginLoader
{
    private static readonly List<IPlugin> Loaded = new List<IPlugin>();
    private static string[] _resolveRoots = Array.Empty<string>();

    private static readonly string[] DependencyDllPrefixes =
    {
        "0Harmony", "HarmonyLib", "MonoMod.", "Mono.Cecil", "System.", "Microsoft.",
        "netstandard", "Newtonsoft.", "PurpleHaze.SDK", "PurpleHaze.Modding"
    };

    public static int LoadAll(string phRoot, Action<string> log)
    {
        var root = Path.Combine(phRoot, "plugins");
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
            log("plugins folder empty.");
            return 0;
        }

        AppDomain.CurrentDomain.AssemblyResolve -= ResolvePluginDeps;
        AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginDeps;

        var resolveList = new List<string>
        {
            Path.Combine(phRoot, "core"),
            Path.Combine(phRoot, "libs"),
            root
        };
        try
        {
            foreach (var sub in Directory.GetDirectories(root))
                resolveList.Add(sub);
        }
        catch { }
        _resolveRoots = resolveList.ToArray();
        Directory.CreateDirectory(Path.Combine(phRoot, "libs"));

        // 1) Subfolders
        foreach (var dir in Directory.GetDirectories(root))
        {
            try { LoadFromDirectory(dir, phRoot, log); }
            catch (Exception ex) { log("Plugin error in " + dir + ": " + ex.Message); }
        }

        // 2) Loose DLLs directly in plugins/
        foreach (var dll in Directory.GetFiles(root, "*.dll"))
        {
            if (IsDependencyDll(Path.GetFileName(dll))) continue;
            try { LoadFromDll(dll, phRoot, log, manifest: null); }
            catch (Exception ex) { log("Plugin error " + Path.GetFileName(dll) + ": " + ex.Message); }
        }

        WriteSnapshot(phRoot, log);
        return Loaded.Count;
    }

    private static Assembly? ResolvePluginDeps(object sender, ResolveEventArgs args)
    {
        try
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            foreach (var root in _resolveRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var direct = Path.Combine(root, name);
                if (File.Exists(direct))
                    return Assembly.LoadFrom(direct);
            }
        }
        catch { }
        return null;
    }

    private static bool IsDependencyDll(string fileName)
    {
        foreach (var p in DependencyDllPrefixes)
        {
            if (fileName.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void LoadFromDirectory(string dir, string phRoot, Action<string> log)
    {
        PluginManifest? manifest = null;
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var sm = SimpleManifest.Parse(File.ReadAllText(manifestPath));
                if (sm != null && !string.IsNullOrWhiteSpace(sm.Id))
                {
                    manifest = new PluginManifest
                    {
                        ManifestVersion = sm.ManifestVersion,
                        Id = sm.Id,
                        Name = sm.Name,
                        Author = sm.Author,
                        Version = sm.Version,
                        Description = sm.Description,
                        RequiredApiVersion = sm.RequiredApiVersion,
                        EntryType = sm.EntryType,
                        Dependencies = sm.Dependencies
                    };
                    var v = ManifestValidator.Validate(manifest);
                    if (!v.IsValid)
                    {
                        log("Invalid manifest in " + Path.GetFileName(dir) + ": " + string.Join("; ", v.Errors));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                log("Bad manifest in " + Path.GetFileName(dir) + " (will try DLL-only): " + ex.Message);
                manifest = null;
            }
        }

        var dll = SelectPluginDll(dir);
        if (dll == null)
        {
            log("Skip " + Path.GetFileName(dir) + " (no plugin DLL)");
            return;
        }

        PreloadDependencyDlls(dir, log);
        LoadFromDll(dll, phRoot, log, manifest);
    }

    private static string? SelectPluginDll(string dir)
    {
        var files = Directory.GetFiles(dir, "*.dll");
        foreach (var f in files)
        {
            var n = Path.GetFileName(f);
            if (IsDependencyDll(n)) continue;
            var stem = Path.GetFileNameWithoutExtension(n);
            if (stem.IndexOf("Plugin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stem.IndexOf("Template", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stem.IndexOf("Mod", StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        }
        foreach (var f in files)
        {
            if (!IsDependencyDll(Path.GetFileName(f)))
                return f;
        }
        return null;
    }

    private static void PreloadDependencyDlls(string dir, Action<string> log)
    {
        foreach (var f in Directory.GetFiles(dir, "*.dll"))
        {
            var n = Path.GetFileName(f);
            if (!IsDependencyDll(n) &&
                (n.IndexOf("Plugin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Template", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 n.IndexOf("Mod", StringComparison.OrdinalIgnoreCase) >= 0))
                continue;
            if (!IsDependencyDll(n) && n.StartsWith("MuckMod", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                Assembly.LoadFrom(f);
                log("  preloaded dep: " + n);
            }
            catch (Exception ex)
            {
                log("  dep preload skip " + n + ": " + ex.Message);
            }
        }
    }

    private static void LoadFromDll(string dllPath, string phRoot, Action<string> log, PluginManifest? manifest)
    {
        var fileName = Path.GetFileName(dllPath);
        log("Loading " + (manifest?.Id ?? fileName) + " ← " + fileName);

        Assembly asm;
        try { asm = Assembly.LoadFrom(dllPath); }
        catch (Exception ex)
        {
            log("LoadFrom failed: " + ex.Message);
            return;
        }

        Type? type = FindPluginType(asm, manifest?.EntryType);
        if (type == null)
        {
            log("No IPlugin type in " + fileName + " (need class implementing IPlugin or [PurpleHazePlugin])");
            return;
        }

        IPlugin plugin;
        try { plugin = (IPlugin)Activator.CreateInstance(type)!; }
        catch (Exception ex)
        {
            log("CreateInstance: " + ex.Message);
            if (ex.InnerException != null)
                log("  inner: " + ex.InnerException.Message);
            return;
        }

        // BepInEx-style Unity base: if the plugin type is also a MonoBehaviour, attach it to host GO
        try
        {
            var mb = FindMonoBehaviourType();
            if (mb != null && mb.IsAssignableFrom(type))
                UnityRuntime.TryAddComponent(type, log);
        }
        catch (Exception ex) { log("Unity attach skip: " + ex.Message); }

        // Id for logs: manifest > attribute/plugin metadata > dll name
        var id = manifest?.Id;
        if (string.IsNullOrWhiteSpace(id))
            id = plugin.Metadata?.Id;
        if (string.IsNullOrWhiteSpace(id))
            id = Path.GetFileNameWithoutExtension(fileName);

        var logger = new FileLogger(Path.Combine(phRoot, "logs", SanitizeFileName(id!) + ".log"));
        var ctx = new HostContext(logger, new MemConfig(), new CapRegistry());
        try
        {
            plugin.InitializeAsync(ctx, CancellationToken.None).GetAwaiter().GetResult();
            Loaded.Add(plugin);
            log("OK " + id + " caps=" + ctx.Capabilities.GetAll().Count);
        }
        catch (Exception ex)
        {
            log("Initialize failed: " + ex.Message);
            log("  detail: " + ex);
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                log("  inner: " + inner.GetType().Name + ": " + inner.Message);
        }
    }

    private static Type? FindPluginType(Assembly asm, string? entryType)
    {
        if (!string.IsNullOrWhiteSpace(entryType))
        {
            var t = asm.GetType(entryType, throwOnError: false);
            if (t != null && typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                return t;
        }

        Type[] exported;
        try { exported = asm.GetExportedTypes(); }
        catch (ReflectionTypeLoadException rtle)
        {
            exported = rtle.Types.Where(x => x != null).Cast<Type>().ToArray();
        }

        // Prefer [PurpleHazePlugin]
        foreach (var t in exported)
        {
            if (t.IsAbstract || t.IsInterface) continue;
            if (!typeof(IPlugin).IsAssignableFrom(t)) continue;
            if (t.GetCustomAttributes(false).Any(a => ((a.GetType().Name == "PurpleHazePluginAttribute" || a.GetType().Name == "PHPluginAttribute") || a.GetType().Name == "PHPluginAttribute")))
                return t;
        }

        foreach (var t in exported)
        {
            if (t != null && typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                return t;
        }

        return null;
    }


    private static Type? FindMonoBehaviourType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEngine.MonoBehaviour", throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }

    private static string SanitizeFileName(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return id;
    }

    private static void WriteSnapshot(string phRoot, Action<string> log)
    {
        try
        {
            var path = Path.Combine(phRoot, "state", "plugins.json");
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("{");
                sw.WriteLine("  \"count\": " + Loaded.Count + ",");
                sw.WriteLine("  \"plugins\": [");
                for (var i = 0; i < Loaded.Count; i++)
                {
                    var p = Loaded[i];
                    var c = i < Loaded.Count - 1 ? "," : "";
                    sw.WriteLine("    {\"id\":\"" + Esc(p.Metadata.Id) + "\",\"name\":\"" + Esc(p.Metadata.Name) + "\"}" + c);
                }
                sw.WriteLine("  ]");
                sw.WriteLine("}");
            }
        }
        catch (Exception ex) { log("snapshot: " + ex.Message); }
    }

    private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}

internal sealed class SimpleManifest
{
    public int ManifestVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string RequiredApiVersion { get; set; } = "0.1.0";
    public string? EntryType { get; set; }
    public List<string> Dependencies { get; set; } = new List<string>();

    public static SimpleManifest Parse(string json)
    {
        var m = new SimpleManifest();
        m.ManifestVersion = ReadInt(json, "manifestVersion", 1);
        m.Id = ReadString(json, "id") ?? "";
        m.Name = ReadString(json, "name") ?? "";
        m.Author = ReadString(json, "author") ?? "";
        m.Version = ReadString(json, "version") ?? "";
        m.Description = ReadString(json, "description") ?? "";
        m.RequiredApiVersion = ReadString(json, "requiredApiVersion") ?? "0.1.0";
        m.EntryType = ReadString(json, "entryType");
        return m;
    }

    private static string? ReadString(string json, string key)
    {
        var pat = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"";
        var match = Regex.Match(json, pat, RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static int ReadInt(string json, string key, int defaultValue)
    {
        var pat = "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)";
        var match = Regex.Match(json, pat, RegexOptions.CultureInvariant);
        if (!match.Success) return defaultValue;
        return int.TryParse(match.Groups[1].Value, out var n) ? n : defaultValue;
    }
}

internal sealed class FileLogger : IPluginLogger
{
    private readonly string _path;
    private readonly object _lock = new object();
    public FileLogger(string path)
    {
        _path = path;
        var d = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
    }
    private void W(string lvl, string msg, Exception? ex = null)
    {
        lock (_lock)
        {
            var line = DateTime.UtcNow.ToString("o") + " [" + lvl + "] " + msg;
            if (ex != null) line += " " + ex;
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
    public void Trace(string message) => W("Trace", message);
    public void Debug(string message) => W("Debug", message);
    public void Information(string message) => W("Info", message);
    public void Warning(string message) => W("Warn", message);
    public void Error(string message, Exception? exception = null) => W("Error", message, exception);
    public void Critical(string message, Exception? exception = null) => W("Critical", message, exception);
}

internal sealed class MemConfig : IPluginConfiguration
{
    private readonly Dictionary<string, object> _d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    public string GetString(string key, string defaultValue = "") =>
        _d.TryGetValue(key, out var v) ? Convert.ToString(v) ?? defaultValue : defaultValue;
    public bool GetBool(string key, bool defaultValue = false) =>
        _d.TryGetValue(key, out var v) && v is bool b ? b : defaultValue;
    public int GetInt(string key, int defaultValue = 0) =>
        _d.TryGetValue(key, out var v) ? Convert.ToInt32(v) : defaultValue;
    public double GetDouble(string key, double defaultValue = 0.0) =>
        _d.TryGetValue(key, out var v) ? Convert.ToDouble(v) : defaultValue;
    public void Set(string key, object value) => _d[key] = value;
    public bool ContainsKey(string key) => _d.ContainsKey(key);
}

internal sealed class CapRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDefinition> _m =
        new Dictionary<string, CapabilityDefinition>(StringComparer.OrdinalIgnoreCase);
    public void Register(CapabilityDefinition definition) => _m[definition.Id] = definition;
    public void Update(string capabilityId, Action<CapabilityDefinitionBuilder> configure)
    {
        if (!_m.TryGetValue(capabilityId, out var cur))
            throw new InvalidOperationException("Unknown capability " + capabilityId);
        var b = new CapabilityDefinitionBuilder(cur);
        configure(b);
        _m[capabilityId] = b.Current;
    }
    public void Unregister(string capabilityId) => _m.Remove(capabilityId);
    public IReadOnlyList<CapabilityDefinition> GetAll() => new List<CapabilityDefinition>(_m.Values);
}

internal sealed class HostContext : IPluginContext
{
    public HostContext(IPluginLogger logger, IPluginConfiguration configuration, ICapabilityRegistry capabilities)
    {
        Logger = logger;
        Configuration = configuration;
        Capabilities = capabilities;
    }
    public IPluginLogger Logger { get; }
    public IPluginConfiguration Configuration { get; }
    public ICapabilityRegistry Capabilities { get; }
    public void ReportError(string message, Exception? exception = null) => Logger.Error(message, exception);
    public Task PublishEventAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default)
    {
        Logger.Debug("event " + eventName);
        return Task.CompletedTask;
    }
}
