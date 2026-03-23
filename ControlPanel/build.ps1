# ControlPanel build script
param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$GodotExe = ""
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModsOutput = Join-Path $Sts2GamePath "mods\ControlPanel"

$GodotPath = $null
if ($GodotExe -and (Test-Path $GodotExe)) { $GodotPath = (Resolve-Path $GodotExe).Path }
elseif ($cmd = Get-Command "Godot" -ErrorAction SilentlyContinue) { $GodotPath = $cmd.Source }
else {
    $candidates = @(
        (Join-Path $ProjectRoot "..\..\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"),
        "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Godot\Godot_v4.5.1-stable_mono_win64.exe"
    )
    $GodotPath = $candidates | Where-Object { Test-Path $_ } | ForEach-Object { (Resolve-Path $_).Path } | Select-Object -First 1
}
if (-not $GodotPath) {
    Write-Host "Godot 4.5.1 Mono not found. Use: .\build.ps1 -GodotExe ""path\to\Godot.exe"""
    exit 1
}
Set-Location $ProjectRoot
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "[1/3] Dotnet build done"

$PckPath = Join-Path $ProjectRoot "ControlPanel.pck"
if (Test-Path "ControlPanel.pck") { Remove-Item "ControlPanel.pck" -Force }
& $GodotPath --path . --export-pack "Windows Desktop" "ControlPanel.pck" --headless
$wait = 0
while (-not (Test-Path "ControlPanel.pck") -and $wait -lt 60) { Start-Sleep -Seconds 2; $wait += 2 }
if (-not (Test-Path "ControlPanel.pck")) { Write-Error "PCK export failed"; exit 1 }
Write-Host "[2/3] PCK export done"

$DllSrc = ".godot\mono\temp\bin\Debug\ControlPanel.dll"
if (-not (Test-Path $DllSrc)) { Write-Error "ControlPanel.dll not found"; exit 1 }
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ModsOutput "ControlPanel.dll") -Force
Copy-Item "ControlPanel.pck" -Destination (Join-Path $ModsOutput "ControlPanel.pck") -Force
$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "v2 $buildStamp" -Encoding UTF8
if (Test-Path "mod_manifest.json") { Copy-Item "mod_manifest.json" -Destination (Join-Path $ModsOutput "mod_manifest.json") -Force }
# 复制到游戏根目录，避免被 mod 扫描器当成 manifest 解析报错
$jsonPath = Join-Path $ProjectRoot "..\VC_STS2_FULL_IDS.json"
if (Test-Path $jsonPath) {
    Copy-Item $jsonPath -Destination (Join-Path $Sts2GamePath "VC_STS2_FULL_IDS.json") -Force
    Write-Host "Copied VC_STS2_FULL_IDS.json to game root"
}

# ── 同时复制到 torelease（发布专用，每次构建都是全新快照）─────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Path $ToReleaseDir -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ToReleaseDir "ControlPanel.dll") -Force
Copy-Item "ControlPanel.pck" -Destination (Join-Path $ToReleaseDir "ControlPanel.pck") -Force
Set-Content -Path (Join-Path $ToReleaseDir "last_build.txt") -Value "v2 $buildStamp" -Encoding UTF8
if (Test-Path "mod_manifest.json") { Copy-Item "mod_manifest.json" -Destination (Join-Path $ToReleaseDir "mod_manifest.json") -Force }

Write-Host "[3/3] Copied to $ModsOutput"
Write-Host "         Also snapshot → $ToReleaseDir (for release packaging)"
Write-Host "Build done. F7 toggles Control Panel in game."
