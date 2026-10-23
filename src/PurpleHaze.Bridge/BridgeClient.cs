using PurpleHaze.Bridge.Session;
using PurpleHaze.Bridge.Transport;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Bridge;

/// <summary>
/// Manager-side (or test-side) bridge client. Connects to a <see cref="BridgeServer"/>,
/// performs handshake with a pairing token, and exposes request/response helpers.
/// </summary>
public sealed class BridgeClient : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly string _pairingToken;
    private readonly string _clientName;
    private readonly string _clientVersion;
    private BridgeSession? _session;
    private bool _disposed;

    public BridgeClient(
        string pipeName,
        string pairingToken,
        string clientName = "PurpleHaze.Manager",
        string clientVersion = "0.1.0")
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? ProtocolConstants.DefaultPipeName
            : pipeName;
        _pairingToken = pairingToken ?? throw new ArgumentNullException(nameof(pairingToken));
        _clientName = clientName;
        _clientVersion = clientVersion;
    }

    public BridgeSession? Session => _session;
    public bool IsConnected => _session?.IsConnected == true && _session.IsAuthenticated;

    public event Action<ProtocolMessage>? MessageReceived;
    public event Action<string>? Disconnected;

    /// <summary>
    /// Connects, performs handshake, and starts the session receive loop.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is not null)
            throw new InvalidOperationException("Already connected.");

        var transport = await NamedPipeTransport.CreateClientAsync(_pipeName, cancellationToken)
            .ConfigureAwait(false);

        var options = new BridgeSessionOptions
        {
            EnableHeartbeats = true,
            ExpectedPairingToken = _pairingToken // not used on client for validation
        };

        var session = new BridgeSession(transport, options);
        session.MessageReceived += msg => MessageReceived?.Invoke(msg);
        session.Disconnected += reason => Disconnected?.Invoke(reason);
        session.Start();

        var requestId = Guid.NewGuid().ToString("N");
        var handshake = ProtocolMessage.Create(
            MessageTypes.Handshake,
            new HandshakeRequest
            {
                ClientName = _clientName,
                ClientVersion = _clientVersion,
                PairingToken = _pairingToken,
                SupportedProtocolVersions = new[] { ProtocolConstants.ProtocolVersion }
            },
            requestId: requestId);

        ProtocolMessage response;
        try
        {
            response = await session.SendRequestAsync(
                handshake,
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        if (response.MessageType != MessageTypes.HandshakeAck)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Expected handshake.ack, got '{response.MessageType}'.");
        }

        var ack = response.GetPayload<HandshakeAck>()
            ?? throw new InvalidOperationException("Handshake ack payload missing.");

        if (!ack.Accepted)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new UnauthorizedAccessException(ack.RejectReason ?? "Handshake rejected.");
        }

        session.MarkAuthenticated(ack.SessionId, ack.NegotiatedProtocolVersion);
        _session = session;
    }

    /// <summary>
    /// Sends an action.execute request and waits for action.result (or error).
    /// </summary>
    public async Task<ActionResultPayload> ExecuteActionAsync(
        string pluginId,
        string capabilityId,
        object? argument = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var requestId = Guid.NewGuid().ToString("N");
        var msg = ProtocolMessage.Create(
            MessageTypes.ActionExecute,
            new ActionExecutePayload { CapabilityId = capabilityId, Argument = argument },
            requestId: requestId,
            pluginId: pluginId);

        var response = await _session!.SendRequestAsync(msg, timeout, cancellationToken)
            .ConfigureAwait(false);

        if (response.MessageType == MessageTypes.Error)
        {
            var err = response.GetPayload<ErrorPayload>();
            return new ActionResultPayload
            {
                Success = false,
                Error = err?.Message ?? "Unknown error"
            };
        }

        return response.GetPayload<ActionResultPayload>()
            ?? new ActionResultPayload { Success = false, Error = "Empty result payload." };
    }

    public Task SendAsync(ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        return _session!.SendAsync(message, cancellationToken);
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is not { IsAuthenticated: true, IsConnected: true })
            throw new InvalidOperationException("Not connected.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session is not null)
            await _session.DisposeAsync().ConfigureAwait(false);
        _session = null;
    }
}
