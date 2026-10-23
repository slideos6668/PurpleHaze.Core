using PurpleHaze.Protocol.Messages;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Tests.Protocol;

public class MessageValidatorTests
{
    [Fact]
    public void ValidateEnvelope_AcceptsValid()
    {
        var msg = ProtocolMessage.Create(MessageTypes.Heartbeat, new HeartbeatPayload { Sequence = 1 });
        var result = MessageValidator.ValidateEnvelope(msg);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEnvelope_RejectsBadVersion()
    {
        var msg = ProtocolMessage.Create(MessageTypes.Heartbeat);
        msg.ProtocolVersion = 99;
        var result = MessageValidator.ValidateEnvelope(msg);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateEnvelope_RejectsUnknownTypeChars()
    {
        var msg = new ProtocolMessage { MessageType = "bad type!", ProtocolVersion = 1 };
        var result = MessageValidator.ValidateEnvelope(msg);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateEnvelope_RejectsBadPluginId()
    {
        var msg = ProtocolMessage.Create(MessageTypes.Log, pluginId: "Bad.Id");
        var result = MessageValidator.ValidateEnvelope(msg);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateHandshake_RequiresToken()
    {
        var result = MessageValidator.ValidateHandshake(new HandshakeRequest
        {
            ClientName = "test",
            PairingToken = "",
            SupportedProtocolVersions = new[] { 1 }
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateActionExecute_RequiresCapabilityId()
    {
        var result = MessageValidator.ValidateActionExecute(new ActionExecutePayload());
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(MessageTypes.Handshake, true)]
    [InlineData(MessageTypes.ActionExecute, true)]
    [InlineData("not.a.real.type", false)]
    [InlineData("eval", false)]
    public void IsKnownMessageType(string type, bool expected)
    {
        Assert.Equal(expected, MessageValidator.IsKnownMessageType(type));
    }
}
