# Troubleshooting

## Build fails with missing SDK

Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

```bash
dotnet --list-sdks
```

## Tests fail on path / permission issues

Discovery tests create folders under the system temp directory. Ensure the process can write to `%TEMP%` / `/tmp`.

## Sample plugin capabilities not appearing in host

Confirm you are running `PurpleHaze.PluginHost` and that `InitializeAsync` completed without exceptions. Check the console log for `[INF]` and `[EVENT]` lines.

## “This is not a real game loader”

Correct. The PluginHost is deliberately a development tool. Real in-game loading begins only after a target game is chosen (Milestone 5).

## Manifest validation errors

- ID must be lowercase, start with a letter, use only `a-z0-9.`.
- `requiredApiVersion` must be compatible with the host API (`0.1.0` in Milestone 1).
- `manifestVersion` must be `1`.
