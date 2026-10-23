using PurpleHaze.Bridge.Session;
using PurpleHaze.Loader.Lifecycle;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;
using PurpleHaze.SDK.Abstractions;
using PurpleHaze.SDK.Capabilities;
using PurpleHaze.SDK.Models;

namespace PurpleHaze.Bridge.Hosting;

/// <summary>
/// Wires <see cref="PluginLifecycleManager"/> to a <see cref="BridgeServer"/>:
/// publishes capability registrations when a manager connects, and dispatches
/// validated <c>action.execute</c> messages to plugin action handlers.
/// </summary>
public sealed class PluginBridgeCoordinator : IAsyncDisposable
{
    private readonly PluginLifecycleManager _lifecycle;
    private readonly BridgeServer _server;
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    public PluginBridgeCoordinator(PluginLifecycleManager lifecycle, BridgeServer server)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _server = server ?? throw new ArgumentNullException(nameof(server));

        _server.ClientConnected += OnClientConnected;
        _server.MessageReceived += OnMessageReceived;
        _server.ClientDisconnected += _ => { /* session cleared by server */ };
    }

    /// <summary>
    /// Publishes all currently running plugins' capabilities to the connected client.
    /// Called automatically on connect; can also be invoked manually.
    /// </summary>
    public async Task PublishAllCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var session = _server.Session;
        if (session is not { IsAuthenticated: true })
            return;

        foreach (var plugin in _lifecycle.Plugins.Where(p => p.State == PluginState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PublishPluginCapabilitiesAsync(session, plugin, cancellationToken).ConfigureAwait(false);
            await session.SendAsync(ProtocolMessage.Create(
                MessageTypes.PluginState,
                new PluginStatePayload { State = plugin.State.ToString() },
                pluginId: plugin.Metadata?.Id),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnClientConnected(BridgeSession session)
    {
        // Fire-and-forget publish; errors are logged via best-effort
        _ = Task.Run(async () =>
        {
            try
            {
                await PublishAllCapabilitiesAsync().ConfigureAwait(false);
            }
            catch
            {
                // Client may have disconnected mid-publish
            }
        });
    }

    private void OnMessageReceived(BridgeSession session, ProtocolMessage message)
    {
        if (message.MessageType == MessageTypes.ActionExecute)
        {
            _ = HandleActionExecuteAsync(session, message);
            return;
        }

        if (message.MessageType == MessageTypes.StatusRequest)
        {
            _ = HandleStatusRequestAsync(session, message);
        }
    }

    private async Task HandleActionExecuteAsync(BridgeSession session, ProtocolMessage message)
    {
        var requestId = message.RequestId;
        var pluginId = message.PluginId;

        try
        {
            var validation = MessageValidator.ValidateActionExecute(message.GetPayload<ActionExecutePayload>());
            if (!validation.IsValid)
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false, error: validation.Error)
                    .ConfigureAwait(false);
                return;
            }

            var payload = message.GetPayload<ActionExecutePayload>()!;
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false, error: "pluginId is required.")
                    .ConfigureAwait(false);
                return;
            }

            var plugin = _lifecycle.GetPlugin(pluginId);
            if (plugin is null || plugin.State != PluginState.Running)
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false,
                    error: $"Plugin '{pluginId}' is not running.").ConfigureAwait(false);
                return;
            }

            // Capability must be registered by this plugin
            var caps = plugin.Capabilities.GetAll();
            if (!caps.Any(c => c.Id.Equals(payload.CapabilityId, StringComparison.OrdinalIgnoreCase)))
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false,
                    error: $"Capability '{payload.CapabilityId}' is not registered by plugin '{pluginId}'.")
                    .ConfigureAwait(false);
                return;
            }

            if (plugin.Instance is not IPluginActionHandler handler)
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false,
                    error: "Plugin does not implement IPluginActionHandler.").ConfigureAwait(false);
                return;
            }

            object? result;
            try
            {
                result = await handler.HandleActionAsync(payload.CapabilityId, payload.Argument)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false, error: ex.Message)
                    .ConfigureAwait(false);
                return;
            }

            await SendActionResultAsync(session, requestId, pluginId, success: true, result: result)
                .ConfigureAwait(false);

            // Publish capability value updates for toggles/sliders/dropdowns when possible
            await TryPublishCapabilityUpdateAsync(session, plugin, payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                await SendActionResultAsync(session, requestId, pluginId, success: false,
                    error: "Internal error handling action.").ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
            _ = ex; // avoid unused in non-debug
        }
    }

    private async Task HandleStatusRequestAsync(BridgeSession session, ProtocolMessage message)
    {
        var plugins = _lifecycle.Plugins.Select(p => new
        {
            id = p.Metadata?.Id,
            state = p.State.ToString(),
            capabilities = p.Capabilities.GetAll().Select(CapabilityMapper.ToDto).ToList(),
            failure = p.FailureReason
        }).ToList();

        await session.SendAsync(ProtocolMessage.Create(
            MessageTypes.StatusResponse,
            new { plugins },
            requestId: message.RequestId)).ConfigureAwait(false);
    }

    private static async Task PublishPluginCapabilitiesAsync(
        BridgeSession session,
        LoadedPlugin plugin,
        CancellationToken cancellationToken)
    {
        var pluginId = plugin.Metadata?.Id;
        if (pluginId is null) return;

        foreach (var cap in plugin.Capabilities.GetAll())
        {
            await session.SendAsync(ProtocolMessage.Create(
                MessageTypes.CapabilityRegister,
                new CapabilityRegisterPayload { Capability = CapabilityMapper.ToDto(cap) },
                pluginId: pluginId),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task TryPublishCapabilityUpdateAsync(
        BridgeSession session,
        LoadedPlugin plugin,
        ActionExecutePayload payload)
    {
        // After a successful action, re-read capabilities and send an update for the changed id if present.
        var cap = plugin.Capabilities.GetAll()
            .FirstOrDefault(c => c.Id.Equals(payload.CapabilityId, StringComparison.OrdinalIgnoreCase));
        if (cap is null) return;

        // For value-bearing controls, prefer the argument as the new value.
        object? value = payload.Argument ?? cap.InitialValue;
        await session.SendAsync(ProtocolMessage.Create(
            MessageTypes.CapabilityUpdate,
            new CapabilityUpdatePayload
            {
                CapabilityId = cap.Id,
                Value = value,
                IsEnabled = cap.IsEnabled,
                Label = cap.Label
            },
            pluginId: plugin.Metadata?.Id)).ConfigureAwait(false);
    }

    private static Task SendActionResultAsync(
        BridgeSession session,
        string? requestId,
        string? pluginId,
        bool success,
        object? result = null,
        string? error = null)
    {
        return session.SendAsync(ProtocolMessage.Create(
            MessageTypes.ActionResult,
            new ActionResultPayload { Success = success, Result = result, Error = error },
            requestId: requestId,
            pluginId: pluginId));
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _server.ClientConnected -= OnClientConnected;
        _server.MessageReceived -= OnMessageReceived;
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        return ValueTask.CompletedTask;
    }
}
