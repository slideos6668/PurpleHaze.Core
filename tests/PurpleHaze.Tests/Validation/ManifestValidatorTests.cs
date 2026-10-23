using PurpleHaze.SDK.Models;
using PurpleHaze.SDK.Validation;

namespace PurpleHaze.Tests.Validation;

public class ManifestValidatorTests
{
    private static PluginManifest ValidManifest() => new()
    {
        ManifestVersion = 1,
        Id = "example.plugin",
        Name = "Example",
        Author = "Test",
        Version = "1.0.0",
        Description = "A test plugin",
        RequiredApiVersion = "0.1.0",
        Dependencies = new List<string>()
    };

    [Fact]
    public void Validate_AcceptsValidManifest()
    {
        var result = ManifestValidator.Validate(ValidManifest());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsNull()
    {
        var result = ManifestValidator.Validate(null);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsWrongManifestVersion()
    {
        var m = ValidManifest();
        m.ManifestVersion = 99;
        var result = ManifestValidator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("manifestVersion"));
    }

    [Fact]
    public void Validate_RejectsInvalidId()
    {
        var m = ValidManifest();
        m.Id = "Bad.Id";
        var result = ManifestValidator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsMissingName()
    {
        var m = ValidManifest();
        m.Name = "";
        var result = ManifestValidator.Validate(m);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsIncompatibleApi()
    {
        var m = ValidManifest();
        m.RequiredApiVersion = "99.0.0";
        var result = ManifestValidator.Validate(m);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("API"));
    }

    [Fact]
    public void Validate_RejectsInvalidDependency()
    {
        var m = ValidManifest();
        m.Dependencies = new List<string> { "Bad.Dep" };
        var result = ManifestValidator.Validate(m);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ToMetadata_MapsCorrectly()
    {
        var m = ValidManifest();
        m.Homepage = "https://example.com";
        var meta = m.ToMetadata();
        Assert.Equal(m.Id, meta.Id);
        Assert.Equal(m.Name, meta.Name);
        Assert.Equal(m.Author, meta.Author);
        Assert.Equal(m.Version, meta.Version);
        Assert.Equal(m.Homepage, meta.Homepage);
        Assert.Empty(meta.Dependencies);
    }
}
