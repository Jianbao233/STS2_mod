# RichPing build script (like jiegec/STS2FirstMod)
# Run: .\build.ps1 from project root
# Requires: Godot 4.5.1 Mono, .NET 8 SDK, Godot in PATH or -GodotExe

param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$GodotExe = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModsOutput = Join-Path $Sts2GamePath "mods\RichPing"

# 1. Find Godot (needed for PCK export)
$GodotPath = $null
if ($GodotExe -and (Test-Path $GodotExe)) { $GodotPath = (Resolve-Path $GodotExe).Path }
elseif ($cmd = Get-Command "Godot" -ErrorAction SilentlyContinue) { $GodotPath = $cmd.Source }
else {
    $godotRelative = Join-Path $ProjectRoot "..\..\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"
    if (Test-Path $godotRelative) { $GodotPath = (Resolve-Path $godotRelative).Path }
    else {
        $candidates = @(
            (Join-Path $ProjectRoot "..\..\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"),
            "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe",
            "$env:USERPROFILE\Godot\Godot_v4.5.1-stable_mono_win64.exe"
        )
        $GodotPath = $candidates | Where-Object { Test-Path $_ } | ForEach-Object { (Resolve-Path $_).Path } | Select-Object -First 1
    }
}
if (-not $GodotPath) {
    Write-Host "Godot 4.5.1 Mono not found."
    Write-Host "Usage: .\build.ps1 -GodotExe ""C:\path\to\Godot_v4.5.1-stable_mono_win64.exe"""
    Write-Error "Add Godot to PATH or pass -GodotExe with full path."
}
Set-Location $ProjectRoot
# Use dotnet build instead of Godot --build-solutions (avoids .NET 9 runtime load error in Godot Mono)
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { Write-Error "Dotnet build failed" }
Write-Host "[1/3] Dotnet build done"

# 2. Export PCK (use . for path to avoid encoding issues with Chinese chars)
$PckPath = Join-Path $ProjectRoot "RichPing.pck"
if (Test-Path "RichPing.pck") { Remove-Item "RichPing.pck" -Force }
& $GodotPath --path . --export-pack "Windows Desktop" "RichPing.pck" --headless
$wait = 0
while (-not (Test-Path "RichPing.pck") -and $wait -lt 60) { Start-Sleep -Seconds 2; $wait += 2 }
if (-not (Test-Path "RichPing.pck")) { Write-Error "PCK export failed" }
Write-Host "[2/3] PCK export done"

# 3. Copy to mods folder (use relative paths)
$DllSrc = ".godot\mono\temp\bin\Debug\RichPing.dll"
if (-not (Test-Path $DllSrc)) { Write-Error "RichPing.dll not found: $DllSrc" }
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ModsOutput "RichPing.dll") -Force
Copy-Item "RichPing.pck" -Destination (Join-Path $ModsOutput "RichPing.pck") -Force
if (Test-Path "mod_manifest.json") { Copy-Item "mod_manifest.json" -Destination (Join-Path $ModsOutput "mod_manifest.json") -Force }

# ── 同时复制到 torelease（发布专用，每次构建都是全新快照）─────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Path $ToReleaseDir -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ToReleaseDir "RichPing.dll") -Force
Copy-Item "RichPing.pck" -Destination (Join-Path $ToReleaseDir "RichPing.pck") -Force
if (Test-Path "mod_manifest.json") { Copy-Item "mod_manifest.json" -Destination (Join-Path $ToReleaseDir "mod_manifest.json") -Force }

Write-Host "[3/3] Copied to $ModsOutput"
Write-Host "         Also snapshot → $ToReleaseDir (for release packaging)"
Write-Host "Build done. Launch game to test."
