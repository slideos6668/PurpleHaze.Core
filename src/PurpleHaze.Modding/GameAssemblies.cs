using System.Reflection;

namespace PurpleHaze.Modding;

/// <summary>
/// Helpers to resolve Unity / game assemblies at runtime (after Managed folder is on the resolve path).
/// </summary>
public static class GameAssemblies
{
    public static Assembly? UnityEngine => Find("UnityEngine") ?? Find("UnityEngine.CoreModule");
    public static Assembly? AssemblyCSharp => Find("Assembly-CSharp");

    public static Type? FindType(string fullOrShortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullOrShortName, throwOnError: false);
            if (t != null) return t;
        }
        // short name search
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == fullOrShortName || t.FullName == fullOrShortName)
                        return t;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // skip
            }
        }
        return null;
    }

    private static Assembly? Find(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(asm.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                return asm;
        }
        try { return Assembly.Load(name); }
        catch { return null; }
    }
}
