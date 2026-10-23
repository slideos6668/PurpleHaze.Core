using System.Text.RegularExpressions;
using PurpleHaze.Protocol.Messages.Payloads;

namespace PurpleHaze.Protocol.Messages;

/// <summary>
/// Validates protocol envelopes and selected payloads. Rejects malformed traffic
/// before it reaches plugin handlers.
/// </summary>
public static partial class MessageValidator
{
    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PluginIdPattern();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9._\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MessageTypePattern();

    public sealed record ValidationResult(bool IsValid, string? Error)
    {
        public static ValidationResult Ok() => new(true, null);
        public static ValidationResult Fail(string error) => new(false, error);
    }

    public static ValidationResult ValidateEnvelope(ProtocolMessage? message)
    {
        if (message is null)
            return ValidationResult.Fail("Message is null.");

        if (message.ProtocolVersion != ProtocolConstants.ProtocolVersion)
            return ValidationResult.Fail(
                $"Unsupported protocolVersion {message.ProtocolVersion}; expected {ProtocolConstants.ProtocolVersion}.");

        if (string.IsNullOrWhiteSpace(message.MessageType))
            return ValidationResult.Fail("messageType is required.");

        if (!MessageTypePattern().IsMatch(message.MessageType))
            return ValidationResult.Fail($"Invalid messageType '{message.MessageType}'.");

        if (message.PluginId is not null && message.PluginId.Length > 0 && !PluginIdPattern().IsMatch(message.PluginId))
            return ValidationResult.Fail($"Invalid pluginId '{message.PluginId}'.");

        if (message.RequestId is { Length: > 128 })
            return ValidationResult.Fail("requestId exceeds 128 characters.");

        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateHandshake(HandshakeRequest? request)
    {
        if (request is null)
            return ValidationResult.Fail("Handshake payload is null.");
        if (string.IsNullOrWhiteSpace(request.ClientName))
            return ValidationResult.Fail("clientName is required.");
        if (string.IsNullOrWhiteSpace(request.PairingToken))
            return ValidationResult.Fail("pairingToken is required.");
        if (request.PairingToken.Length > 128)
            return ValidationResult.Fail("pairingToken is too long.");
        if (request.SupportedProtocolVersions is null || request.SupportedProtocolVersions.Length == 0)
            return ValidationResult.Fail("supportedProtocolVersions is required.");
        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateActionExecute(ActionExecutePayload? payload)
    {
        if (payload is null)
            return ValidationResult.Fail("Action payload is null.");
        if (string.IsNullOrWhiteSpace(payload.CapabilityId))
            return ValidationResult.Fail("capabilityId is required.");
        if (payload.CapabilityId.Length > 128)
            return ValidationResult.Fail("capabilityId is too long.");
        return ValidationResult.Ok();
    }

    /// <summary>
    /// Known message types that the bridge accepts after handshake.
    /// Unknown types are rejected (no arbitrary dispatch).
    /// </summary>
    public static bool IsKnownMessageType(string messageType) =>
        messageType is MessageTypes.Handshake
            or MessageTypes.HandshakeAck
            or MessageTypes.Heartbeat
            or MessageTypes.CapabilityRegister
            or MessageTypes.CapabilityUpdate
            or MessageTypes.CapabilityUnregister
            or MessageTypes.ActionExecute
            or MessageTypes.ActionResult
            or MessageTypes.SettingUpdate
            or MessageTypes.StatusRequest
            or MessageTypes.StatusResponse
            or MessageTypes.Log
            or MessageTypes.Event
            or MessageTypes.Error
            or MessageTypes.PluginState;
}
