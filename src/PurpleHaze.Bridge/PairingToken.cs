using System.Security.Cryptography;

namespace PurpleHaze.Bridge;

/// <summary>
/// Generates and validates local pairing tokens used during bridge handshake.
/// </summary>
public static class PairingToken
{
    /// <summary>
    /// Creates a cryptographically random hex token suitable for local pairing.
    /// </summary>
    public static string Generate(int byteLength = 16)
    {
        if (byteLength < 8 || byteLength > 64)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
