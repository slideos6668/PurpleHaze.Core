using PurpleHaze.Bridge.Session;
using PurpleHaze.Bridge.Transport;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Bridge;

/// <summary>
/// Game-side (or host-side) bridge server. Accepts one authenticated local connection,
/// negotiates protocol version, and exposes a <see cref="BridgeSession"/>.
/// </summary>
public sealed class BridgeServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly BridgeSessionOptions _options;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;
    private BridgeSession? _session;
    private bool _disposed;

    public BridgeServer(string pipeName, BridgeSessionOptions options)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? ProtocolConstants.DefaultPipeName
            : pipeName;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ExpectedPairingToken))
            throw new ArgumentException("ExpectedPairingToken is required for authentication.", nameof(options));
    }

    public BridgeSession? Session => _session;
    public bool IsClientConnected => _session?.IsConnected == true && _session.IsAuthenticated;

    /// <summary>Raised after a successful handshake.</summary>
    public event Action<BridgeSession>? ClientConnected;

    /// <summary>Raised when the client disconnects.</summary>
    public event Action<string>? ClientDisconnected;

    /// <summary>Raised for application messages after handshake.</summary>
    public event Action<BridgeSession, ProtocolMessage>? MessageReceived;

    /// <summary>
    /// Starts accepting a single client. Returns when the listen loop is cancelled or disposed.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = ListenLoopAsync(_listenCts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits until a client has completed handshake, or until timeout/cancellation.
    /// </summary>
    public async Task<BridgeSession> WaitForClientAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_session is { IsAuthenticated: true })
                return _session;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("No authenticated client connected within the timeout.");
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeTransport? transport = null;
            BridgeSession? session = null;
            try
            {
                transport = await NamedPipeTransport.CreateServerAsync(_pipeName, cancellationToken)
                    .ConfigureAwait(false);

                session = new BridgeSession(transport, _options);
                session.MessageReceived += msg => OnSessionMessage(session, msg);
                session.Disconnected += reason =>
                {
                    ClientDisconnected?.Invoke(reason);
                    if (ReferenceEquals(_session, session))
                        _session = null;
                };
                session.Start();

                // Wait for handshake as the first meaningful message (with timeout).
                var handshakeCompleted = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                void Handler(ProtocolMessage msg)
                {
                    if (msg.MessageType != MessageTypes.Handshake)
                        return;
                    try
                    {
                        HandleHandshake(session, msg);
                        handshakeCompleted.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        handshakeCompleted.TrySetException(ex);
                    }
                }

                session.MessageReceived += Handler;
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                    await handshakeCompleted.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                finally
                {
                    session.MessageReceived -= Handler;
                }

                _session = session;
                ClientConnected?.Invoke(session);

                // Stay until disconnect
                while (session.IsConnected && !cancellationToken.IsCancellationRequested)
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Accept loop continues after a failed connection attempt.
            }
            finally
            {
                if (session is not null)
                    await session.DisposeAsync().ConfigureAwait(false);
                else if (transport is not null)
                    await transport.DisposeAsync().ConfigureAwait(false);
                _session = null;
            }
        }
    }

    private void HandleHandshake(BridgeSession session, ProtocolMessage message)
    {
        var request = message.GetPayload<HandshakeRequest>();
        var validation = MessageValidator.ValidateHandshake(request);
        if (!validation.IsValid)
        {
            _ = session.SendAsync(ProtocolMessage.Create(
                MessageTypes.HandshakeAck,
                new HandshakeAck
                {
                    Accepted = false,
                    RejectReason = validation.Error
                },
                requestId: message.RequestId));
            throw new InvalidOperationException(validation.Error);
        }

        // Constant-time-ish token compare
        if (!FixedTimeEquals(request!.PairingToken, _options.ExpectedPairingToken))
        {
            _ = session.SendAsync(ProtocolMessage.Create(
                MessageTypes.HandshakeAck,
                new HandshakeAck
                {
                    Accepted = false,
                    RejectReason = "Invalid pairing token."
                },
                requestId: message.RequestId));
            throw new UnauthorizedAccessException("Invalid pairing token.");
        }

        if (!request.SupportedProtocolVersions.Contains(ProtocolConstants.ProtocolVersion))
        {
            _ = session.SendAsync(ProtocolMessage.Create(
                MessageTypes.HandshakeAck,
                new HandshakeAck
                {
                    Accepted = false,
                    RejectReason = $"No common protocol version. Server supports {ProtocolConstants.ProtocolVersion}."
                },
                requestId: message.RequestId));
            throw new InvalidOperationException("Protocol version mismatch.");
        }

        var sessionId = Guid.NewGuid().ToString("N");
        session.MarkAuthenticated(sessionId, ProtocolConstants.ProtocolVersion);

        _ = session.SendAsync(ProtocolMessage.Create(
            MessageTypes.HandshakeAck,
            new HandshakeAck
            {
                Accepted = true,
                NegotiatedProtocolVersion = ProtocolConstants.ProtocolVersion,
                ServerName = _options.ServerName,
                ServerVersion = _options.ServerVersion,
                HeartbeatIntervalMs = _options.HeartbeatIntervalMs,
                SessionId = sessionId
            },
            requestId: message.RequestId));
    }

    private void OnSessionMessage(BridgeSession session, ProtocolMessage message)
    {
        // Handshake is handled separately during accept.
        if (message.MessageType == MessageTypes.Handshake)
            return;
        if (message.MessageType == MessageTypes.Heartbeat)
            return; // traffic updates last-received; no app handler needed

        MessageReceived?.Invoke(session, message);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            // still walk to avoid short-circuit timing on length alone for typical tokens
            var dummy = 0;
            for (var i = 0; i < a.Length; i++) dummy |= a[i];
            return false;
        }
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _listenCts?.Cancel();
        if (_listenTask is not null)
        {
            try { await Task.WhenAny(_listenTask, Task.Delay(3000)).ConfigureAwait(false); }
            catch { /* ignore */ }
        }
        if (_session is not null)
            await _session.DisposeAsync().ConfigureAwait(false);
        _listenCts?.Dispose();
    }
}
