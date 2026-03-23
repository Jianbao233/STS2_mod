# Prepare GitHub Release package for RichPing
# Usage: .\prepare-release.ps1 [-Version "0.1.1"]
# IMPORTANT: Always run .\build.ps1 FIRST before this script.
#   build.ps1 copies artifacts to both:
#     1) Steam mods\RichPing\    (for live testing)
#     2) torelease\              (this script uses this)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
$ReleaseDir = Join-Path $ProjectRoot "release"
$ZipName = "RichPing-v$Version.zip"

# Verify torelease folder (build.ps1 must be run first)
$RequiredFiles = @("RichPing.dll", "RichPing.pck", "mod_manifest.json")
$Missing = @()
foreach ($f in $RequiredFiles) {
    $p = Join-Path $ToReleaseDir $f
    if (-not (Test-Path $p)) { $Missing += $f }
}
if ($Missing.Count -gt 0) {
    Write-Host "ERROR: Missing files in torelease\: $($Missing -join ', ')" -ForegroundColor Red
    Write-Host "Run .\build.ps1 first." -ForegroundColor Yellow
    exit 1
}

New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
$ZipPath = Join-Path $ReleaseDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

$TempDir = Join-Path $env:TEMP "RichPing-release-$(Get-Random)"
New-Item -ItemType Directory -Path (Join-Path $TempDir "RichPing") -Force | Out-Null
Copy-Item (Join-Path $ToReleaseDir "RichPing.dll")       -Destination (Join-Path $TempDir "RichPing")
Copy-Item (Join-Path $ToReleaseDir "RichPing.pck")       -Destination (Join-Path $TempDir "RichPing")
Copy-Item (Join-Path $ToReleaseDir "mod_manifest.json")  -Destination (Join-Path $TempDir "RichPing")
Compress-Archive -Path (Join-Path $TempDir "RichPing") -DestinationPath $ZipPath
Remove-Item $TempDir -Recurse -Force

Write-Host "Release package: $ZipPath"
Write-Host ""
Write-Host "Next: Create GitHub Release (tag v$Version), upload $ZipName as asset."
Write-Host "Or: gh release create v$Version $ZipPath --title 'Rich Ping v$Version'"
