namespace PurpleHaze.Bridge.Transport;

/// <summary>
/// Abstraction over the underlying IPC stream (named pipe or future transports).
/// </summary>
public interface IBridgeTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    Stream Stream { get; }
    string Description { get; }
}
