using System.Collections.Concurrent;
using PurpleHaze.Bridge.Transport;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Bridge.Session;

/// <summary>
/// One authenticated connection. Handles framing, validation, request/response
/// correlation, heartbeats, and rate limiting. Does not execute arbitrary payloads.
/// </summary>
public sealed class BridgeSession : IAsyncDisposable
{
    private readonly IBridgeTransport _transport;
    private readonly BridgeSessionOptions _options;
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _rateLock = new();
    private readonly Queue<DateTimeOffset> _recentMessages = new();

    private Task? _receiveLoop;
    private Task? _heartbeatLoop;
    private long _heartbeatSequence;
    private DateTimeOffset _lastReceived = DateTimeOffset.UtcNow;
    private bool _disposed;

    public BridgeSession(IBridgeTransport transport, BridgeSessionOptions options)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string? SessionId { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool IsConnected => !_disposed && _transport.IsConnected;
    public int NegotiatedProtocolVersion { get; private set; } = ProtocolConstants.ProtocolVersion;

    /// <summary>Raised for every validated inbound message after handshake (and for handshake itself).</summary>
    public event Action<ProtocolMessage>? MessageReceived;

    /// <summary>Raised when the session ends (disconnect, timeout, error).</summary>
    public event Action<string>? Disconnected;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        if (_options.EnableHeartbeats)
            _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Sends a request and waits for a correlated response (same requestId).
    /// </summary>
    public async Task<ProtocolMessage> SendRequestAsync(
        ProtocolMessage request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RequestId))
            request.RequestId = Guid.NewGuid().ToString("N");

        if (_pending.Count >= ProtocolConstants.MaxInFlightRequests)
            throw new InvalidOperationException("Too many in-flight requests.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        linked.CancelAfter(timeout ?? TimeSpan.FromMilliseconds(ProtocolConstants.DefaultRequestTimeoutMs));

        var pending = new PendingRequest(request.RequestId, linked.Token);
        if (!_pending.TryAdd(request.RequestId, pending))
            throw new InvalidOperationException($"Duplicate requestId '{request.RequestId}'.");

        try
        {
            await WriteAsync(request, linked.Token).ConfigureAwait(false);
            return await pending.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(request.RequestId, out _);
            pending.Dispose();
        }
    }

    /// <summary>Fire-and-forget send (notifications, heartbeats, responses).</summary>
    public Task SendAsync(ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return WriteAsync(message, cancellationToken);
    }

    private async Task WriteAsync(ProtocolMessage message, CancellationToken cancellationToken)
    {
        var validation = MessageValidator.ValidateEnvelope(message);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Cannot send invalid message: {validation.Error}");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ProtocolSerializer.WriteAsync(_transport.Stream, message, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _transport.IsConnected)
            {
                ProtocolMessage? message;
                try
                {
                    message = await ProtocolSerializer.ReadAsync(_transport.Stream, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
                catch (InvalidDataException ex)
                {
                    // Malformed frame – notify and disconnect; do not crash the host.
                    try
                    {
                        await SendAsync(ProtocolMessage.Create(
                            MessageTypes.Error,
                            new ErrorPayload { Code = "malformed", Message = ex.Message }),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* ignore */ }
                    break;
                }

                if (message is null)
                    break; // clean EOF

                _lastReceived = DateTimeOffset.UtcNow;

                if (!TryAcceptRate())
                {
                    try
                    {
                        await SendAsync(ProtocolMessage.Create(
                            MessageTypes.Error,
                            new ErrorPayload { Code = "rate_limited", Message = "Message rate exceeded." }),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* ignore */ }
                    continue;
                }

                var validation = MessageValidator.ValidateEnvelope(message);
                if (!validation.IsValid || !MessageValidator.IsKnownMessageType(message.MessageType))
                {
                    try
                    {
                        await SendAsync(ProtocolMessage.Create(
                            MessageTypes.Error,
                            new ErrorPayload
                            {
                                Code = "invalid_message",
                                Message = validation.Error ?? $"Unknown messageType '{message.MessageType}'."
                            },
                            requestId: message.RequestId),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* ignore */ }
                    continue;
                }

                // Correlate responses
                if (message.RequestId is not null &&
                    _pending.TryGetValue(message.RequestId, out var pending) &&
                    IsResponseType(message.MessageType))
                {
                    pending.Complete(message);
                    continue;
                }

                MessageReceived?.Invoke(message);
            }
        }
        finally
        {
            FailAllPending(new IOException("Session disconnected."));
            Disconnected?.Invoke("receive_loop_ended");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(
            Math.Max(1000, _options.HeartbeatIntervalMs));
        var timeout = TimeSpan.FromMilliseconds(
            Math.Max(_options.HeartbeatIntervalMs * 2, ProtocolConstants.HeartbeatTimeoutMs));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

                if (DateTimeOffset.UtcNow - _lastReceived > timeout)
                {
                    Disconnected?.Invoke("heartbeat_timeout");
                    break;
                }

                if (!_transport.IsConnected)
                    break;

                var seq = Interlocked.Increment(ref _heartbeatSequence);
                try
                {
                    await SendAsync(ProtocolMessage.Create(
                        MessageTypes.Heartbeat,
                        new HeartbeatPayload { Sequence = seq }),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    internal void MarkAuthenticated(string sessionId, int protocolVersion)
    {
        SessionId = sessionId;
        NegotiatedProtocolVersion = protocolVersion;
        IsAuthenticated = true;
    }

    private bool TryAcceptRate()
    {
        var now = DateTimeOffset.UtcNow;
        lock (_rateLock)
        {
            while (_recentMessages.Count > 0 && now - _recentMessages.Peek() > TimeSpan.FromSeconds(1))
                _recentMessages.Dequeue();

            if (_recentMessages.Count >= ProtocolConstants.MaxMessagesPerSecond)
                return false;

            _recentMessages.Enqueue(now);
            return true;
        }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var pending))
            {
                pending.Fail(ex);
                pending.Dispose();
            }
        }
    }

    private static bool IsResponseType(string messageType) =>
        messageType is MessageTypes.HandshakeAck
            or MessageTypes.ActionResult
            or MessageTypes.StatusResponse
            or MessageTypes.Error;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();

        try
        {
            if (_receiveLoop is not null)
                await Task.WhenAny(_receiveLoop, Task.Delay(2000)).ConfigureAwait(false);
            if (_heartbeatLoop is not null)
                await Task.WhenAny(_heartbeatLoop, Task.Delay(1000)).ConfigureAwait(false);
        }
        catch { /* ignore */ }

        FailAllPending(new ObjectDisposedException(nameof(BridgeSession)));
        await _transport.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
        _cts.Dispose();
    }
}

public sealed class BridgeSessionOptions
{
    public bool EnableHeartbeats { get; init; } = true;
    public int HeartbeatIntervalMs { get; init; } = ProtocolConstants.HeartbeatIntervalMs;
    public string ExpectedPairingToken { get; init; } = string.Empty;
    public string ServerName { get; init; } = "PurpleHaze.Bridge";
    public string ServerVersion { get; init; } = "0.1.0";
}
