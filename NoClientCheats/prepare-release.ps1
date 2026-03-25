# Prepare GitHub Release package for NoClientCheats
# Usage: .\prepare-release.ps1 [-Version "1.0.0"]
# IMPORTANT: Always run .\build.ps1 FIRST before this script.
#   build.ps1 copies artifacts to both:
#     1) Steam mods\NoClientCheats\  (for live testing)
#     2) torelease\                   (this script uses this)
#   This ensures release packages always contain freshly-built files.
param(
    [string]$Version = "1.2.0"
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
$ReleaseDir = Join-Path $ProjectRoot "release"
$ZipName = "NoClientCheats-v$Version.zip"

# Verify torelease folder has all required files (proving build.ps1 was run)
$RequiredFiles = @("NoClientCheats.dll", "NoClientCheats.pck", "mod_manifest.json")
$Missing = @()
foreach ($f in $RequiredFiles) {
    $p = Join-Path $ToReleaseDir $f
    if (-not (Test-Path $p)) {
        $Missing += $f
    }
}
if ($Missing.Count -gt 0) {
    Write-Host "ERROR: Missing files in torelease\: $($Missing -join ', ')" -ForegroundColor Red
    Write-Host "Run .\build.ps1 first to build and snapshot files." -ForegroundColor Yellow
    exit 1
}

New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
$ZipPath = Join-Path $ReleaseDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

$TempDir = Join-Path $env:TEMP "NoClientCheats-release-$(Get-Random)"
New-Item -ItemType Directory -Path (Join-Path $TempDir "NoClientCheats") -Force | Out-Null
# Copy from torelease (freshly built snapshot), NOT from game mods folder
Copy-Item (Join-Path $ToReleaseDir "NoClientCheats.dll")   -Destination (Join-Path $TempDir "NoClientCheats")
Copy-Item (Join-Path $ToReleaseDir "NoClientCheats.pck")   -Destination (Join-Path $TempDir "NoClientCheats")
Copy-Item (Join-Path $ToReleaseDir "mod_manifest.json")    -Destination (Join-Path $TempDir "NoClientCheats")
if (Test-Path (Join-Path $ToReleaseDir "last_build.txt")) {
    Copy-Item (Join-Path $ToReleaseDir "last_build.txt")   -Destination (Join-Path $TempDir "NoClientCheats")
}

Compress-Archive -Path (Join-Path $TempDir "NoClientCheats") -DestinationPath $ZipPath
Remove-Item $TempDir -Recurse -Force

Write-Host "Release package: $ZipPath"
Write-Host ""
Write-Host "Next: Create GitHub Release (tag v$Version), upload $ZipName as asset."
Write-Host "Or: gh release create v$Version $ZipPath --title 'No Client Cheats v$Version'"
