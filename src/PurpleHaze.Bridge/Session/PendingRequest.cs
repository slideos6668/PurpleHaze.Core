using PurpleHaze.Protocol.Messages;

namespace PurpleHaze.Bridge.Session;

internal sealed class PendingRequest : IDisposable
{
    private readonly TaskCompletionSource<ProtocolMessage> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;

    public string RequestId { get; }
    public Task<ProtocolMessage> Task => _tcs.Task;

    public PendingRequest(string requestId, CancellationToken cancellationToken)
    {
        RequestId = requestId;
        _registration = cancellationToken.Register(() =>
            _tcs.TrySetCanceled(cancellationToken));
    }

    public void Complete(ProtocolMessage response) => _tcs.TrySetResult(response);

    public void Fail(Exception ex) => _tcs.TrySetException(ex);

    public void Dispose() => _registration.Dispose();
}
