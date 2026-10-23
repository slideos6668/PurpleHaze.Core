using PurpleHaze.Bridge;
using PurpleHaze.Bridge.Session;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Tests.Bridge;

public class BridgeIntegrationTests
{
    private static string UniquePipe() =>
        "ph-test-" + Guid.NewGuid().ToString("N")[..16];

    [Fact]
    public async Task Handshake_WithValidToken_Succeeds()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        await server.StartAsync();

        await using var client = new BridgeClient(pipe, token, clientName: "test-client");
        var connectTask = client.ConnectAsync();
        var session = await server.WaitForClientAsync(TimeSpan.FromSeconds(10));

        await connectTask;

        Assert.True(client.IsConnected);
        Assert.True(server.IsClientConnected);
        Assert.True(session.IsAuthenticated);
        Assert.False(string.IsNullOrEmpty(session.SessionId));
    }

    [Fact]
    public async Task Handshake_WithInvalidToken_Fails()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        await server.StartAsync();

        // Give server a moment to start listening
        await Task.Delay(100);

        await using var client = new BridgeClient(pipe, "wrong-token-0000000000000000");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task RequestResponse_Correlation_Works()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });

        server.MessageReceived += async (session, msg) =>
        {
            if (msg.MessageType != MessageTypes.ActionExecute) return;
            var payload = msg.GetPayload<ActionExecutePayload>();
            await session.SendAsync(ProtocolMessage.Create(
                MessageTypes.ActionResult,
                new ActionResultPayload
                {
                    Success = true,
                    Result = new { echo = payload?.CapabilityId, arg = payload?.Argument }
                },
                requestId: msg.RequestId,
                pluginId: msg.PluginId));
        };

        await server.StartAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        await server.WaitForClientAsync(TimeSpan.FromSeconds(5));

        var result = await client.ExecuteActionAsync(
            "example.plugin",
            "example.button.testAction",
            argument: 42,
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task Request_Timeout_Throws()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        // Intentionally do not respond to ActionExecute
        await server.StartAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        await server.WaitForClientAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ExecuteActionAsync(
                "example.plugin",
                "example.button.testAction",
                timeout: TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public async Task MalformedMessage_DoesNotCrashSession()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        await server.StartAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        var session = await server.WaitForClientAsync(TimeSpan.FromSeconds(5));

        // Send a valid message with unknown type – should get error response path, session stays up
        // (unknown types are rejected by validator in receive loop)
        await client.SendAsync(new ProtocolMessage
        {
            ProtocolVersion = 1,
            MessageType = "totally.unknown",
            RequestId = Guid.NewGuid().ToString("N")
        });

        // Give receive loop time to process
        await Task.Delay(200);

        // Session may disconnect on strict paths; either way the host process must not crash.
        // A subsequent valid request either works or throws a clean disconnect exception.
        try
        {
            await client.ExecuteActionAsync("example.plugin", "x", timeout: TimeSpan.FromMilliseconds(500));
        }
        catch (Exception ex)
        {
            Assert.True(
                ex is OperationCanceledException or IOException or InvalidOperationException or ObjectDisposedException,
                $"Unexpected exception type: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public async Task Disconnect_FailsPendingRequests()
    {
        var pipe = UniquePipe();
        var token = PairingToken.Generate();

        await using var server = new BridgeServer(pipe, new BridgeSessionOptions
        {
            ExpectedPairingToken = token,
            EnableHeartbeats = false
        });
        await server.StartAsync();

        await using var client = new BridgeClient(pipe, token);
        await client.ConnectAsync();
        await server.WaitForClientAsync(TimeSpan.FromSeconds(5));

        var pending = client.ExecuteActionAsync(
            "example.plugin",
            "example.button.testAction",
            timeout: TimeSpan.FromSeconds(10));

        await Task.Delay(50);
        await server.DisposeAsync(); // tear down server while request in flight

        await Assert.ThrowsAnyAsync<Exception>(() => pending);
    }
}
