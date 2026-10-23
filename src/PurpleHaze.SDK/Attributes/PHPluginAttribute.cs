namespace PurpleHaze.SDK.Attributes;

/// <summary>
/// Short alias for <see cref="PurpleHazePluginAttribute"/> (BepInEx-style [BepInPlugin] feel).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PHPluginAttribute : PurpleHazePluginAttribute
{
    public PHPluginAttribute(string id, string name, string version)
        : base(id, name, version)
    {
    }
}
