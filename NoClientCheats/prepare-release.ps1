# Prepare GitHub Release package for NoClientCheats
# Usage: .\prepare-release.ps1 [-Version "1.0.0"]
# 1) Run build.ps1 first to build and deploy the mod
# 2) This script packs mods\NoClientCheats into NoClientCheats-vX.X.X.zip
# 3) Output to release/ for gh release create or web upload
param(
    [string]$Version = "1.0.0",
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2"
)
$ErrorActionPreference = "Stop"
$ModsPath = Join-Path $Sts2GamePath "mods\NoClientCheats"
$ReleaseDir = Join-Path $PSScriptRoot "release"
$ZipName = "NoClientCheats-v$Version.zip"

if (-not (Test-Path $ModsPath)) {
    Write-Host "Mod folder not found: $ModsPath"
    Write-Host "Run build.ps1 first."
    exit 1
}

$RequiredFiles = @("NoClientCheats.dll", "NoClientCheats.pck", "mod_manifest.json")
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

$TempDir = Join-Path $env:TEMP "NoClientCheats-release-$(Get-Random)"
New-Item -ItemType Directory -Path (Join-Path $TempDir "NoClientCheats") -Force | Out-Null
Copy-Item (Join-Path $ModsPath "NoClientCheats.dll") -Destination (Join-Path $TempDir "NoClientCheats")
Copy-Item (Join-Path $ModsPath "NoClientCheats.pck") -Destination (Join-Path $TempDir "NoClientCheats")
Copy-Item (Join-Path $ModsPath "mod_manifest.json") -Destination (Join-Path $TempDir "NoClientCheats")

Compress-Archive -Path (Join-Path $TempDir "NoClientCheats") -DestinationPath $ZipPath
Remove-Item $TempDir -Recurse -Force

Write-Host "Release package: $ZipPath"
Write-Host ""
Write-Host "Next: Create GitHub Release (tag v$Version), upload $ZipName as asset."
Write-Host "Or: gh release create v$Version $ZipPath --title 'No Client Cheats v$Version'"
