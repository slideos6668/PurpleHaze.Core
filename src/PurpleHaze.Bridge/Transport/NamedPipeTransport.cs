using System.IO.Pipes;

namespace PurpleHaze.Bridge.Transport;

/// <summary>
/// Named-pipe transport. On Windows this is a classic named pipe; on Linux/macOS
/// .NET maps it to a Unix domain socket under a well-known path.
/// </summary>
public sealed class NamedPipeTransport : IBridgeTransport
{
    private readonly PipeStream _pipe;
    private readonly string _description;

    private NamedPipeTransport(PipeStream pipe, string description)
    {
        _pipe = pipe;
        _description = description;
    }

    public bool IsConnected => _pipe.IsConnected;
    public Stream Stream => _pipe;
    public string Description => _description;

    /// <summary>
    /// Creates a server pipe and waits for a single client connection.
    /// </summary>
    public static async Task<NamedPipeTransport> CreateServerAsync(
        string pipeName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        // Byte mode, message-agnostic; we do our own length-prefix framing.
        var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        return new NamedPipeTransport(server, $"pipe-server:{pipeName}");
    }

    /// <summary>
    /// Connects a client to an existing server pipe.
    /// </summary>
    public static async Task<NamedPipeTransport> CreateClientAsync(
        string pipeName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        await client.ConnectAsync(timeout: 10_000, cancellationToken).ConfigureAwait(false);
        return new NamedPipeTransport(client, $"pipe-client:{pipeName}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_pipe.IsConnected)
            {
                // Best-effort graceful close
            }
        }
        catch
        {
            // ignore
        }

        await _pipe.DisposeAsync().ConfigureAwait(false);
    }
}
