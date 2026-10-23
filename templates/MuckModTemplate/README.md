# Muck mod template (Purple Haze + Harmony)

## Build

```powershell
cd templates\MuckModTemplate
dotnet build -c Release
```

## Install into Muck

Copy to:

```
Muck\PurpleHaze\plugins\muckmod.template\
  manifest.json
  MuckModTemplate.dll
  PurpleHaze.Modding.dll
  PurpleHaze.SDK.dll
  0Harmony.dll          (and any HarmonyX deps from bin\Release\netstandard2.0\)
```

Easiest: copy **all** DLLs from `bin\Release\netstandard2.0\` into that plugin folder, plus `manifest.json`.

## Write real patches

1. Uncomment game references in the `.csproj` (Assembly-CSharp path).
2. Add classes with `[HarmonyPatch(typeof(SomeGameType), "MethodName")]`.
3. Rebuild and replace DLLs.

Play fair online — don't use advantage mods in public multiplayer without consent.
