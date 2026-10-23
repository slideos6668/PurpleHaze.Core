# Install the example plugin into Muck's PurpleHaze/plugins folder.
# Usage:
#   .\install-sample-plugin.ps1 -MuckPath "C:\Program Files (x86)\Steam\steamapps\common\Muck"

param(
  [Parameter(Mandatory = $true)]
  [string]$MuckPath
)

$ErrorActionPreference = "Stop"

$repo = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$pluginProj = Join-Path $repo "src\PurpleHaze.SamplePlugin\PurpleHaze.SamplePlugin.csproj"

Write-Host "Building sample plugin (netstandard2.0 Release)..."
dotnet build $pluginProj -c Release -f netstandard2.0
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$outDir = Join-Path $repo "src\PurpleHaze.SamplePlugin\bin\Release\netstandard2.0"
$dll = Join-Path $outDir "PurpleHaze.SamplePlugin.dll"
$sdk = Join-Path $outDir "PurpleHaze.SDK.dll"

if (-not (Test-Path $dll)) { throw "Missing $dll" }

$dest = Join-Path $MuckPath "PurpleHaze\plugins\example.plugin"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Copy-Item $dll (Join-Path $dest "PurpleHaze.SamplePlugin.dll") -Force
if (Test-Path $sdk) {
  Copy-Item $sdk (Join-Path $dest "PurpleHaze.SDK.dll") -Force
}

$manifestSrc = Join-Path $PSScriptRoot "sample-plugin\manifest.json"
Copy-Item $manifestSrc (Join-Path $dest "manifest.json") -Force

Write-Host ""
Write-Host "Installed to: $dest"
Write-Host "Files:"
Get-ChildItem $dest | ForEach-Object { Write-Host "  - $($_.Name)" }
Write-Host ""
Write-Host "Launch Muck, then check:"
Write-Host "  PurpleHaze\logs\bootstrap.log   (should say 1 plugin)"
Write-Host "  PurpleHaze\logs\example.plugin.log"
