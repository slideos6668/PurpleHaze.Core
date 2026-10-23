# Purple Haze Core — release notes draft

## Included
- Purple Haze Core source and solution.
- Windows Doorstop payload package is provided as a separate release asset.
- The optional Muck overlay is intentionally maintained as a separate plugin project and release asset.

## Compatibility
- Target path: Unity Mono; Muck is the first supported game profile.
- Requires a compatible UnityDoorstop 4 release for the Windows proxy/bootstrap setup. Download Doorstop from the official UnityDoorstop GitHub releases.
- This release does not include BepInEx and is not an IL2CPP loader.

## Assets
- `PurpleHaze.Core-source.zip`: clean source tree, excluding build outputs and the optional overlay project.
- `PurpleHaze.Core-payload-win.zip`: Windows runtime payload. Review the install guide before use.
- `PurpleHaze.MuckOverlay-source.zip`: standalone overlay source project; build it against your local Muck Managed assemblies. A precompiled overlay DLL is not included in this source package.

## Important
These are prepared release assets, not a GitHub upload. Review and test on a clean Muck installation before publishing.
