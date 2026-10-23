#if HAS_BEPINEX
using BepInEx;
using UnityEngine;

namespace PurpleHaze.BepInExHost;

/// <summary>
/// BepInEx 5 plugin entry for Muck (and other Unity Mono games using BepInEx 5).
/// Build with libs/BepInEx.dll (+ UnityEngine) present to enable this type.
/// </summary>
[BepInPlugin("com.purplehaze.core", "Purple Haze Core", "0.1.0")]
public sealed class BepInExPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("Purple Haze Core BepInEx host loading...");
        InGameHostBootstrap.Start(Paths.GameRootPath);
        Logger.LogInfo("Purple Haze session dir: " + InGameHostBootstrap.LastSessionDirectory);
        Logger.LogInfo("Pairing token: " + InGameHostBootstrap.LastPairingToken);
        Logger.LogInfo("Connect TestClient / manager with this token once the bridge is up.");
    }
}
#else
namespace PurpleHaze.BepInExHost;

/// <summary>
/// Placeholder when BepInEx assemblies are not on the compile path.
/// See README.md in this project for how to build the real plugin against Muck + BepInEx.
/// </summary>
public static class BepInExPluginStub
{
    public const string Instructions =
        "Copy BepInEx.dll (and UnityEngine*.dll from Muck_Data/Managed) into src/PurpleHaze.BepInExHost/libs/, then rebuild.";
}
#endif
