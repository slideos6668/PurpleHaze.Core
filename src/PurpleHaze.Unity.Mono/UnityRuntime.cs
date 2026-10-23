using System;
using System.Reflection;
using System.Threading;

namespace PurpleHaze.Unity.Mono;

/// <summary>
/// Late-binds UnityEngine so we do not need Unity DLLs at compile time.
/// </summary>
internal static class UnityRuntime
{
    /// <summary>DontDestroyOnLoad host GameObject instance (boxed Unity object).</summary>
    public static object? HostGameObject { get; private set; }

    public static Assembly? WaitForUnity(TimeSpan timeout, Action<string> log)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var asm = FindUnityEngine();
            if (asm != null) return asm;
            Thread.Sleep(250);
        }
        return FindUnityEngine();
    }

    public static Assembly? FindUnityEngine()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name == "UnityEngine" || name == "UnityEngine.CoreModule")
                return asm;
        }
        try { return Assembly.Load("UnityEngine"); }
        catch
        {
            try { return Assembly.Load("UnityEngine.CoreModule"); }
            catch { return null; }
        }
    }

    public static bool TryAttachPersistentHost(Assembly unityAsm, Action<string> log)
    {
        try
        {
            var goType = unityAsm.GetType("UnityEngine.GameObject") ?? FindType("UnityEngine.GameObject");
            var objectType = unityAsm.GetType("UnityEngine.Object") ?? FindType("UnityEngine.Object");
            if (goType == null || objectType == null)
            {
                log("Unity types GameObject/Object not found.");
                return false;
            }

            var ctor = goType.GetConstructor(new[] { typeof(string) });
            if (ctor == null)
            {
                log("GameObject(string) ctor missing.");
                return false;
            }

            var go = ctor.Invoke(new object[] { "PurpleHaze.Host" });
            HostGameObject = go;
            log("Created GameObject PurpleHaze.Host");

            var ddol = objectType.GetMethod(
                "DontDestroyOnLoad",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { objectType },
                null);

            if (ddol != null)
            {
                ddol.Invoke(null, new[] { go });
                log("DontDestroyOnLoad applied.");
            }
            else log("DontDestroyOnLoad not found (non-fatal).");

            return true;
        }
        catch (Exception ex)
        {
            log("TryAttachPersistentHost failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// BepInEx-style: AddComponent&lt;T&gt; on the host GameObject when T : MonoBehaviour.
    /// Plugin assemblies that reference UnityEngine can use real Update/Awake.
    /// </summary>
    public static object? TryAddComponent(Type componentType, Action<string> log)
    {
        if (HostGameObject == null)
        {
            log("AddComponent skipped: no host GameObject");
            return null;
        }

        try
        {
            var mb = FindType("UnityEngine.MonoBehaviour");
            if (mb == null || !mb.IsAssignableFrom(componentType))
            {
                log("AddComponent skipped: type is not a MonoBehaviour: " + componentType.Name);
                return null;
            }

            var goType = HostGameObject.GetType();
            // AddComponent(Type)
            var add = goType.GetMethod("AddComponent", new[] { typeof(Type) });
            if (add == null)
            {
                log("AddComponent(Type) not found on GameObject");
                return null;
            }

            var comp = add.Invoke(HostGameObject, new object[] { componentType });
            log("AddComponent: " + componentType.FullName);
            return comp;
        }
        catch (Exception ex)
        {
            log("AddComponent failed: " + ex.Message);
            return null;
        }
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }
}
