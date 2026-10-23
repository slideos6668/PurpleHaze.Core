namespace PurpleHaze.Protocol.Messages;

/// <summary>
/// Protocol versioning, limits, and well-known defaults.
/// </summary>
public static class ProtocolConstants
{
    public const int ProtocolVersion = 1;

    /// <summary>Maximum serialized message size in bytes (1 MiB).</summary>
    public const int MaxMessageSizeBytes = 1_048_576;

    /// <summary>Default request timeout.</summary>
    public const int DefaultRequestTimeoutMs = 10_000;

    /// <summary>Heartbeat interval suggested by the server.</summary>
    public const int HeartbeatIntervalMs = 5_000;

    /// <summary>If no heartbeat/traffic is received for this long, the session is considered dead.</summary>
    public const int HeartbeatTimeoutMs = 15_000;

    /// <summary>Maximum concurrent in-flight requests per session.</summary>
    public const int MaxInFlightRequests = 64;

    /// <summary>Soft rate limit: max messages accepted per second per session.</summary>
    public const int MaxMessagesPerSecond = 200;

    /// <summary>Default named-pipe name (Windows) / Unix socket name segment.</summary>
    public const string DefaultPipeName = "PurpleHaze.Bridge";

    /// <summary>Length of the pairing token in characters (hex).</summary>
    public const int PairingTokenLength = 32;
}
