using System.Reflection;
using PurpleHaze.Loader.Discovery;
using PurpleHaze.Loader.Logging;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Loader.Lifecycle;

/// <summary>
/// Orchestrates discovery, dependency ordering, assembly loading, initialization, and shutdown
/// for plugins in the development host (and later, game-side adapters).
/// Failures in one plugin are isolated; they do not abort the whole manager.
/// </summary>
public sealed class PluginLifecycleManager : IAsyncDisposable
{
    private readonly PluginDiscoverer _discoverer = new();
    private readonly HostLogger _logger;
    private readonly Dictionary<string, LoadedPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    public PluginLifecycleManager(HostLogger? logger = null)
    {
        _logger = logger ?? new HostLogger(sink: e =>
        {
            var prefix = e.PluginId is { Length: > 0 } ? $"[{e.PluginId}] " : "";
            Console.WriteLine($"{e.Timestamp:HH:mm:ss.fff} {e.Level,-11} {prefix}{e.Message}");
            if (e.Exception is not null)
                Console.WriteLine(e.Exception);
        });
    }

    public HostLogger Logger => _logger;

    /// <summary>Snapshot of all tracked plugins.</summary>
    public IReadOnlyList<LoadedPlugin> Plugins
    {
        get { lock (_lock) return _plugins.Values.ToList(); }
    }

    public LoadedPlugin? GetPlugin(string pluginId)
    {
        lock (_lock) return _plugins.TryGetValue(pluginId, out var p) ? p : null;
    }

    /// <summary>
    /// Discover plugins under <paramref name="pluginsRoot"/>, resolve dependencies,
    /// load assemblies, and initialize each plugin. Already-tracked IDs are skipped.
    /// </summary>
    public async Task LoadAllAsync(string pluginsRoot, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);

        _logger.Log(LogLevel.Information, "host", $"Discovering plugins in '{pluginsRoot}'...");
        var discovered = _discoverer.Discover(pluginsRoot);
        _logger.Log(LogLevel.Information, "host", $"Found {discovered.Count} candidate(s).");

        // Track invalid / incompatible first.
        foreach (var d in discovered.Where(x => !x.IsValid || x.Metadata is null))
        {
            var id = d.Manifest?.Id ?? Path.GetFileName(d.DirectoryPath) ?? "unknown";
            var loaded = new LoadedPlugin { Discovery = d };
            loaded.SetState(
                d.State == PluginState.Incompatible ? PluginState.Incompatible : PluginState.Failed,
                string.Join("; ", d.Errors));
            TryAdd(loaded);
            _logger.Log(LogLevel.Warning, id, $"Skipped: {string.Join("; ", d.Errors)}");
        }

        var validDiscoveries = discovered
            .Where(d => d.IsValid && d.Metadata is not null)
            .ToList();

        var validMeta = validDiscoveries.Select(d => d.Metadata!).ToList();

        // Duplicate ID detection
        var duplicates = validMeta
            .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (duplicates.Count > 0)
        {
            foreach (var dup in duplicates)
                _logger.Log(LogLevel.Error, "host", $"Duplicate plugin ID '{dup}'. All instances with this ID will be marked failed.");

            foreach (var d in validDiscoveries.Where(x => duplicates.Contains(x.Metadata!.Id)))
            {
                var loaded = new LoadedPlugin { Discovery = d };
                loaded.MarkFailed($"Duplicate plugin ID '{d.Metadata!.Id}'.");
                TryAdd(loaded);
            }

            validDiscoveries = validDiscoveries
                .Where(d => !duplicates.Contains(d.Metadata!.Id))
                .ToList();
            validMeta = validDiscoveries.Select(d => d.Metadata!).ToList();
        }

        var sort = DependencySorter.Sort(validMeta);
        foreach (var miss in sort.MissingDependencies)
            _logger.Log(LogLevel.Error, "host", $"Missing dependency: {miss}");
        foreach (var cycle in sort.Cycles)
            _logger.Log(LogLevel.Error, "host", $"Dependency cycle involving '{cycle}'.");

        var orderedIds = new HashSet<string>(sort.Ordered.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);

        // Mark plugins that lost the sort (missing deps / cycles)
        foreach (var d in validDiscoveries.Where(x => !orderedIds.Contains(x.Metadata!.Id)))
        {
            var loaded = new LoadedPlugin { Discovery = d };
            loaded.MarkFailed("Unresolved dependency or cycle.");
            TryAdd(loaded);
            _logger.Log(LogLevel.Error, d.Metadata!.Id, loaded.FailureReason!);
        }

        // Load + initialize in dependency order
        foreach (var meta in sort.Ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discovery = validDiscoveries.First(d =>
                d.Metadata!.Id.Equals(meta.Id, StringComparison.OrdinalIgnoreCase));
            await LoadAndInitializeAsync(discovery, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Load and initialize a single discovered plugin. Failures are recorded on the
    /// <see cref="LoadedPlugin"/> and do not throw to the caller.
    /// </summary>
    public async Task<LoadedPlugin> LoadAndInitializeAsync(
        PluginDiscoveryResult discovery,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(discovery);

        var loaded = new LoadedPlugin { Discovery = discovery };
        if (!discovery.IsValid || discovery.Metadata is null)
        {
            loaded.SetState(
                discovery.State == PluginState.Incompatible ? PluginState.Incompatible : PluginState.Failed,
                string.Join("; ", discovery.Errors));
            TryAdd(loaded);
            return loaded;
        }

        var id = discovery.Metadata.Id;

        // Prefer a real DLL when present.
        if (!string.IsNullOrWhiteSpace(discovery.AssemblyPath) && File.Exists(discovery.AssemblyPath))
        {
            try
            {
                var fullPath = Path.GetFullPath(discovery.AssemblyPath);
                var alc = new PluginLoadContext(fullPath);
                var assembly = alc.LoadFromAssemblyPath(fullPath);
                var instance = CreatePluginInstance(assembly, discovery);
                loaded.AttachInstance(instance, assembly, alc);
                _logger.Log(LogLevel.Information, id, $"Assembly loaded from '{fullPath}'.");
            }
            catch (Exception ex)
            {
                loaded.MarkFailed($"Failed to load assembly: {ex.Message}", ex);
                _logger.Log(LogLevel.Error, id, loaded.FailureReason!, ex);
                TryAdd(loaded);
                return loaded;
            }
        }
        else if (TryActivateFromDefaultContext(discovery, loaded))
        {
            _logger.Log(LogLevel.Information, id,
                "Activated from default load context (development host project reference).");
        }
        else
        {
            loaded.MarkFailed("No plugin assembly (.dll) found and entry type not available in the default context.");
            _logger.Log(LogLevel.Error, id, loaded.FailureReason!);
            TryAdd(loaded);
            return loaded;
        }

        await InitializeSafeAsync(loaded, cancellationToken).ConfigureAwait(false);
        TryAdd(loaded);
        return loaded;
    }

    /// <summary>
    /// Shut down all running plugins in reverse initialization order (best-effort).
    /// </summary>
    public async Task ShutdownAllAsync(CancellationToken cancellationToken = default)
    {
        List<LoadedPlugin> snapshot;
        lock (_lock) snapshot = _plugins.Values.ToList();

        foreach (var plugin in snapshot
                     .Where(p => p.State is PluginState.Running or PluginState.Initialized or PluginState.Loaded)
                     .OrderByDescending(p => p.InitializedAt ?? DateTimeOffset.MinValue))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ShutdownOneAsync(plugin, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ShutdownOneAsync(LoadedPlugin plugin, CancellationToken cancellationToken = default)
    {
        if (plugin.Instance is null || plugin.State is PluginState.Shutdown or PluginState.Failed or PluginState.Incompatible)
            return;

        var id = plugin.Metadata?.Id ?? "unknown";
        try
        {
            _logger.Log(LogLevel.Information, id, "Shutting down...");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            await plugin.Instance.ShutdownAsync(linked.Token).ConfigureAwait(false);
            plugin.MarkShutdown();
            _logger.Log(LogLevel.Information, id, "Shutdown complete.");
        }
        catch (Exception ex)
        {
            plugin.MarkFailed($"Shutdown failed: {ex.Message}", ex);
            _logger.Log(LogLevel.Error, id, plugin.FailureReason!, ex);
        }
    }

    private async Task InitializeSafeAsync(LoadedPlugin loaded, CancellationToken cancellationToken)
    {
        var id = loaded.Metadata?.Id ?? "unknown";
        if (loaded.Instance is null)
        {
            loaded.MarkFailed("No plugin instance to initialize.");
            return;
        }

        var context = new HostPluginContext(id, _logger, loaded.Capabilities);
        try
        {
            _logger.Log(LogLevel.Information, id, "Initializing...");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(30));
            await loaded.Instance.InitializeAsync(context, linked.Token).ConfigureAwait(false);
            loaded.MarkInitialized();
            _logger.Log(LogLevel.Information, id,
                $"Initialized. Capabilities: {loaded.Capabilities.GetAll().Count}");
        }
        catch (Exception ex)
        {
            loaded.MarkFailed($"Initialize failed: {ex.Message}", ex);
            _logger.Log(LogLevel.Error, id, loaded.FailureReason!, ex);
        }
    }

    private static IPlugin CreatePluginInstance(Assembly assembly, PluginDiscoveryResult discovery)
    {
        Type? type = null;
        var entry = discovery.Manifest?.EntryType;
        if (!string.IsNullOrWhiteSpace(entry))
        {
            type = assembly.GetType(entry, throwOnError: false, ignoreCase: false);
            if (type is null)
                throw new InvalidOperationException(
                    $"Entry type '{entry}' not found in assembly '{assembly.GetName().Name}'.");
        }
        else
        {
            // Prefer GetExportedTypes to avoid ReflectionTypeLoadException noise from private types.
            try
            {
                type = assembly.GetExportedTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });
            }
            catch (ReflectionTypeLoadException ex)
            {
                type = ex.Types
                    .Where(t => t is not null)
                    .Cast<Type>()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });
            }

            if (type is null)
                throw new InvalidOperationException(
                    $"No IPlugin implementation found in '{assembly.GetName().Name}'.");
        }

        if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract)
            throw new InvalidOperationException($"Type '{type.FullName}' does not implement IPlugin.");

        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create instance of '{type.FullName}'.");
        return (IPlugin)instance;
    }

    /// <summary>
    /// When the sample (or other) plugin is referenced by the host project, activate it from the
    /// default context so developers can iterate without copying DLLs into a plugins folder.
    /// </summary>
    private static bool TryActivateFromDefaultContext(PluginDiscoveryResult discovery, LoadedPlugin loaded)
    {
        var entry = discovery.Manifest?.EntryType;
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        Type? type = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = asm.GetType(entry, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }
            if (type is not null) break;
        }

        if (type is null || !typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract)
            return false;

        try
        {
            var instance = (IPlugin)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Activator returned null."));
            loaded.AttachInstance(instance, type.Assembly, context: null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryAdd(LoadedPlugin loaded)
    {
        var id = loaded.Discovery.Metadata?.Id
                 ?? loaded.Discovery.Manifest?.Id
                 ?? Path.GetFileName(loaded.Discovery.DirectoryPath)
                 ?? Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            if (_plugins.ContainsKey(id))
            {
                _logger.Log(LogLevel.Warning, id, "Plugin ID already tracked; ignoring duplicate registration.");
                return;
            }
            _plugins[id] = loaded;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await ShutdownAllAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (_lock) _plugins.Clear();
        }
    }
}
