using System.Text;
using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Tests.Protocol;

public class ProtocolSerializerTests
{
    [Fact]
    public async Task RoundTrip_PreservesFields()
    {
        var original = ProtocolMessage.Create(
            MessageTypes.ActionExecute,
            new ActionExecutePayload { CapabilityId = "example.button.testAction", Argument = true },
            requestId: "abc123",
            pluginId: "example.plugin");

        using var ms = new MemoryStream();
        await ProtocolSerializer.WriteAsync(ms, original);
        ms.Position = 0;
        var read = await ProtocolSerializer.ReadAsync(ms);

        Assert.NotNull(read);
        Assert.Equal(original.MessageType, read!.MessageType);
        Assert.Equal(original.RequestId, read.RequestId);
        Assert.Equal(original.PluginId, read.PluginId);
        Assert.Equal(ProtocolConstants.ProtocolVersion, read.ProtocolVersion);

        var payload = read.GetPayload<ActionExecutePayload>();
        Assert.NotNull(payload);
        Assert.Equal("example.button.testAction", payload!.CapabilityId);
    }

    [Fact]
    public async Task Read_EmptyStream_ReturnsNull()
    {
        using var ms = new MemoryStream();
        var result = await ProtocolSerializer.ReadAsync(ms);
        Assert.Null(result);
    }

    [Fact]
    public async Task Write_OversizedMessage_Throws()
    {
        var huge = new string('x', ProtocolConstants.MaxMessageSizeBytes + 100);
        var msg = ProtocolMessage.Create(MessageTypes.Log, new LogPayload { Message = huge });
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProtocolSerializer.WriteAsync(ms, msg));
    }

    [Fact]
    public async Task Read_InvalidLength_Throws()
    {
        using var ms = new MemoryStream();
        // length = 0x7FFFFFFF (way too large but we'll write a smaller claim)
        ms.WriteByte(0x00);
        ms.WriteByte(0x10); // claim ~1MB+
        ms.WriteByte(0x00);
        ms.WriteByte(0x01);
        // claim length = 0x00100001 > Max? Actually Max is 1_048_576 = 0x100000
        // 0x00100001 = 1048577 > max
        ms.Position = 0;
        // Fix: write length 0x00100001
        ms.SetLength(0);
        var len = ProtocolConstants.MaxMessageSizeBytes + 1;
        ms.WriteByte((byte)((len >> 24) & 0xFF));
        ms.WriteByte((byte)((len >> 16) & 0xFF));
        ms.WriteByte((byte)((len >> 8) & 0xFF));
        ms.WriteByte((byte)(len & 0xFF));
        ms.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => ProtocolSerializer.ReadAsync(ms));
    }

    [Fact]
    public void DeserializeJson_Works()
    {
        var json = """{"protocolVersion":1,"messageType":"heartbeat","payload":{"sequence":1}}""";
        var msg = ProtocolSerializer.DeserializeJson(json);
        Assert.Equal(MessageTypes.Heartbeat, msg.MessageType);
    }
}
