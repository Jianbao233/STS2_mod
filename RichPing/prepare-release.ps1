# Prepare GitHub Release package for RichPing
# Usage: .\prepare-release.ps1 [-Version "0.1.1"]
# 1) Run build.ps1 first to build and deploy the mod
# 2) This script packs mods\RichPing into RichPing-vX.X.X.zip
# 3) Output to release/ for gh release create or web upload
param(
    [string]$Version = "0.1.1",
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2"
)
$ErrorActionPreference = "Stop"
$ModsPath = Join-Path $Sts2GamePath "mods\RichPing"
$ReleaseDir = Join-Path $PSScriptRoot "release"
$ZipName = "RichPing-v$Version.zip"

if (-not (Test-Path $ModsPath)) {
    Write-Host "Mod folder not found: $ModsPath"
    Write-Host "Run build.ps1 first."
    exit 1
}

$RequiredFiles = @("RichPing.dll", "RichPing.pck", "mod_manifest.json")
foreach ($f in $RequiredFiles) {
    $p = Join-Path $ModsPath $f
    if (-not (Test-Path $p)) {
        Write-Host "Missing file: $f"
        exit 1
    }
}

New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
$ZipPath = Join-Path $ReleaseDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

$TempDir = Join-Path $env:TEMP "RichPing-release-$(Get-Random)"
New-Item -ItemType Directory -Path (Join-Path $TempDir "RichPing") -Force | Out-Null
Copy-Item (Join-Path $ModsPath "RichPing.dll") -Destination (Join-Path $TempDir "RichPing")
Copy-Item (Join-Path $ModsPath "RichPing.pck") -Destination (Join-Path $TempDir "RichPing")
Copy-Item (Join-Path $ModsPath "mod_manifest.json") -Destination (Join-Path $TempDir "RichPing")
Compress-Archive -Path (Join-Path $TempDir "RichPing") -DestinationPath $ZipPath
Remove-Item $TempDir -Recurse -Force

Write-Host "Release package: $ZipPath"
Write-Host ""
Write-Host "Next: Create GitHub Release (tag v$Version), upload $ZipName as asset."
Write-Host "Or: gh release create v$Version $ZipPath --title 'Rich Ping v$Version'"
