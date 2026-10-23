namespace PurpleHaze.SDK.Attributes;

/// <summary>
/// Marks a class as a Purple Haze plugin (BepInEx-style convenience).
/// Class should implement <see cref="Abstractions.IPlugin"/> or inherit PurpleHaze.Modding.PurpleHazePluginBase.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PurpleHazePluginAttribute : Attribute
{
    public PurpleHazePluginAttribute(string id, string name, string version)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>Unique id, e.g. "myauthor.mymod".</summary>
    public string Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>SemVer string, e.g. "1.0.0".</summary>
    public string Version { get; }

    /// <summary>Author or team.</summary>
    public string Author { get; set; } = "";

    /// <summary>Short description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Minimum API version required.</summary>
    public string RequiredApiVersion { get; set; } = "0.1.0";
}
