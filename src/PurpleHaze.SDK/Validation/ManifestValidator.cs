using PurpleHaze.SDK.Models;

namespace PurpleHaze.SDK.Validation;

/// <summary>
/// Validates a <see cref="PluginManifest"/> and produces structured error messages.
/// </summary>
public static class ManifestValidator
{
    public const int CurrentManifestVersion = 1;
    public const string CurrentApiVersion = "0.1.0";

    public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
    {
        public static ValidationResult Success() => new(true, Array.Empty<string>());
        public static ValidationResult Failure(params string[] errors) => new(false, errors);
    }

    /// <summary>
    /// Validates all required fields and semantic constraints.
    /// </summary>
    public static ValidationResult Validate(PluginManifest? manifest)
    {
        if (manifest is null)
            return ValidationResult.Failure("Manifest is null.");

        var errors = new List<string>();

        if (manifest.ManifestVersion != CurrentManifestVersion)
            errors.Add($"Unsupported manifestVersion {manifest.ManifestVersion}. Expected {CurrentManifestVersion}.");

        if (!PluginIdValidator.IsValidId(manifest.Id))
            errors.Add($"Invalid plugin id '{manifest.Id}'.");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            errors.Add("Name is required.");
        else if (manifest.Name.Length > 128)
            errors.Add("Name must be 128 characters or fewer.");

        if (string.IsNullOrWhiteSpace(manifest.Author))
            errors.Add("Author is required.");

        if (!PluginIdValidator.TryParseVersion(manifest.Version, out _))
            errors.Add($"Invalid version '{manifest.Version}'. Expected semantic version (e.g. 1.0.0).");

        if (!PluginIdValidator.TryParseVersion(manifest.RequiredApiVersion, out _))
            errors.Add($"Invalid requiredApiVersion '{manifest.RequiredApiVersion}'.");

        if (!PluginIdValidator.IsApiCompatible(manifest.RequiredApiVersion, CurrentApiVersion))
            errors.Add($"Plugin requires API {manifest.RequiredApiVersion} but host provides {CurrentApiVersion}.");

        if (manifest.Dependencies is not null)
        {
            foreach (var dep in manifest.Dependencies)
            {
                if (!PluginIdValidator.IsValidId(dep))
                    errors.Add($"Invalid dependency id '{dep}'.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray());
    }
}
