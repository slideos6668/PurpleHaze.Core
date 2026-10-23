using System.Text.RegularExpressions;

namespace PurpleHaze.SDK.Validation;

/// <summary>
/// Validates plugin identifiers and metadata fields.
/// </summary>
public static class PluginIdValidator
{
    // Lowercase letters, digits, dots. Must start with a letter. No consecutive dots.
    private static readonly Regex IdPattern = new(
        @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the identifier is valid.
    /// </summary>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (id.Length > 128)
            return false;
        return IdPattern.IsMatch(id);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if the identifier is invalid.
    /// </summary>
    public static void EnsureValidId(string? id, string paramName = "id")
    {
        if (!IsValidId(id))
            throw new ArgumentException(
                $"Invalid plugin ID '{id}'. IDs must be lowercase, start with a letter, and use dots as separators (e.g. 'example.plugin').",
                paramName);
    }

    /// <summary>
    /// Parses a semantic version string. Returns false if malformed.
    /// </summary>
    public static bool TryParseVersion(string? version, out Version? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(version))
            return false;
        return Version.TryParse(version, out result);
    }

    /// <summary>
    /// Checks whether a required API version is satisfied by the host's API version.
    /// </summary>
    public static bool IsApiCompatible(string requiredApiVersion, string hostApiVersion)
    {
        if (!TryParseVersion(requiredApiVersion, out var required) || required is null)
            return false;
        if (!TryParseVersion(hostApiVersion, out var host) || host is null)
            return false;
        return host.Major > required.Major
            || (host.Major == required.Major && host.Minor >= required.Minor);
    }
}
