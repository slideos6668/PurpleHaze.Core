namespace PurpleHaze.Protocol.Messages;

/// <summary>
/// Well-known message type strings used in the bridge protocol.
/// </summary>
public static class MessageTypes
{
    public const string Handshake = "handshake";
    public const string HandshakeAck = "handshake.ack";
    public const string Heartbeat = "heartbeat";
    public const string CapabilityRegister = "capability.register";
    public const string CapabilityUpdate = "capability.update";
    public const string CapabilityUnregister = "capability.unregister";
    public const string ActionExecute = "action.execute";
    public const string ActionResult = "action.result";
    public const string SettingUpdate = "setting.update";
    public const string StatusRequest = "status.request";
    public const string StatusResponse = "status.response";
    public const string Log = "log";
    public const string Event = "event";
    public const string Error = "error";
    public const string PluginState = "plugin.state";
}
