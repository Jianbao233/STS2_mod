# Prepare GitHub Release package for MP_SavePlayerRemover
# Usage: .\prepare-release.ps1 [-Version "1.0.0"]
# 1) Runs build_exe.bat logic to produce exe (or uses existing dist/)
# 2) Packs exe + README into MP_SavePlayerRemover-vX.X.X.zip
# 3) Output to release/ for gh release create or web upload
param(
    [string]$Version = "1.0.0"
)
$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$DistExe = Join-Path $ScriptDir "dist\MP_SavePlayerRemover.exe"
$ReleaseDir = Join-Path $ScriptDir "release"
$ZipName = "MP_SavePlayerRemover-v$Version.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName

# Build exe if not exists
if (-not (Test-Path $DistExe)) {
    Write-Host "Building exe (dist\MP_SavePlayerRemover.exe not found)..."
    Set-Location $ScriptDir
    pip install pyinstaller -q 2>$null
    pyinstaller --onefile --name MP_SavePlayerRemover --clean remove_players.py 2>&1 | Out-Null
    if (-not (Test-Path $DistExe)) {
        Write-Host "Build failed. Run build_exe.bat manually."
        exit 1
    }
}

New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

$TempDir = Join-Path $env:TEMP "MP_SavePlayerRemover-release-$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
Copy-Item $DistExe -Destination $TempDir
Copy-Item (Join-Path $ScriptDir "README.md") -Destination $TempDir

Compress-Archive -Path (Join-Path $TempDir "*") -DestinationPath $ZipPath
Remove-Item $TempDir -Recurse -Force

Write-Host "Release package: $ZipPath"
Write-Host ""
Write-Host "=== Upload to GitHub ==="
Write-Host "1. Go to: https://github.com/Jianbao233/STS2_mod/releases/new"
Write-Host "2. Tag: v$Version"
Write-Host "3. Title: MP Save Player Remover v$Version"
Write-Host "4. Upload: $ZipName"
Write-Host ""
Write-Host "Or after gh auth login:"
Write-Host "  gh release create v$Version `"$ZipPath`" --title 'MP Save Player Remover v$Version' --notes '多人存档移除玩家工具'"
