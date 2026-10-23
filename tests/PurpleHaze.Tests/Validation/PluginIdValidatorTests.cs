using PurpleHaze.SDK.Validation;

namespace PurpleHaze.Tests.Validation;

public class PluginIdValidatorTests
{
    [Theory]
    [InlineData("example.plugin")]
    [InlineData("a")]
    [InlineData("my.mod.v2")]
    [InlineData("purple.haze.core")]
    public void IsValidId_AcceptsValidIds(string id)
    {
        Assert.True(PluginIdValidator.IsValidId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Example.Plugin")]
    [InlineData("1starts.with.number")]
    [InlineData(".starts.with.dot")]
    [InlineData("ends.with.")]
    [InlineData("has..double.dots")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("has-hyphen")]
    public void IsValidId_RejectsInvalidIds(string? id)
    {
        Assert.False(PluginIdValidator.IsValidId(id));
    }

    [Fact]
    public void EnsureValidId_ThrowsOnInvalid()
    {
        Assert.Throws<ArgumentException>(() => PluginIdValidator.EnsureValidId("Bad.Id"));
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.1.0", true)]
    [InlineData("10.20.30", true)]
    [InlineData("", false)]
    [InlineData("1.0", true)]
    [InlineData("abc", false)]
    public void TryParseVersion_Works(string version, bool expected)
    {
        var ok = PluginIdValidator.TryParseVersion(version, out var result);
        Assert.Equal(expected, ok);
        if (expected)
            Assert.NotNull(result);
    }

    [Theory]
    [InlineData("0.1.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.2.0", "0.1.0", false)]
    [InlineData("1.0.0", "0.9.0", false)]
    [InlineData("0.1.0", "1.0.0", true)]
    public void IsApiCompatible_RespectsMajorMinor(string required, string host, bool expected)
    {
        Assert.Equal(expected, PluginIdValidator.IsApiCompatible(required, host));
    }
}
