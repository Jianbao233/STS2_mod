# MP_PlayerManager build script
param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$GodotExe = ""
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModsOutput = Join-Path $Sts2GamePath "mods\MP_PlayerManager"

# ── 找 Godot.exe ──────────────────────────────────────────────────────────
$GodotPath = $null
if ($GodotExe -and (Test-Path $GodotExe)) {
    $GodotPath = (Resolve-Path $GodotExe).Path
}
elseif ($cmd = Get-Command "Godot" -ErrorAction SilentlyContinue) {
    $GodotPath = $cmd.Source
}
else {
    $candidates = @(
        (Join-Path $ProjectRoot "..\..\..\..\..\SteamLibrary\steamapps\common\Slay the Spire 2\Godot_v4.5.1-stable_mono_win64.exe"),
        "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Godot\Godot_v4.5.1-stable_mono_win64.exe"
    )
    $GodotPath = $candidates | Where-Object { Test-Path $_ } | ForEach-Object { (Resolve-Path $_).Path } | Select-Object -First 1
}
if (-not $GodotPath) {
    Write-Host "ERROR: Godot 4.5.1 Mono not found."
    Write-Host "  Provide via: .\build.ps1 -GodotExe ""path\to\Godot.exe"""
    exit 1
}
Write-Host "[Godot] $GodotPath"

# ── dotnet build ──────────────────────────────────────────────────────────
Set-Location $ProjectRoot
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { Write-Host "[dotnet] BUILD FAILED"; exit 1 }
Write-Host "[dotnet] Build done"

# ── export PCK ─────────────────────────────────────────────────────────────
$PckPath = Join-Path $ProjectRoot "MP_PlayerManager.pck"
if (Test-Path "MP_PlayerManager.pck") {
    Remove-Item "MP_PlayerManager.pck" -Force
}
& $GodotPath --path . --export-pack "Windows Desktop" "MP_PlayerManager.pck" --headless
$wait = 0
while (-not (Test-Path "MP_PlayerManager.pck") -and $wait -lt 60) {
    Start-Sleep -Seconds 2
    $wait += 2
}
if (-not (Test-Path "MP_PlayerManager.pck")) {
    Write-Host "[Godot] PCK export FAILED"
    exit 1
}
Write-Host "[Godot] PCK export done"

# ── 复制到 mods/ ───────────────────────────────────────────────────────────
$DllSrc = ".godot\mono\temp\bin\Debug\MP_PlayerManager.dll"
if (-not (Test-Path $DllSrc)) {
    # SDK-style 输出路径（某些配置下可能在这里）
    $DllSrc = "bin\Debug\net8.0\MP_PlayerManager.dll"
}
if (-not (Test-Path $DllSrc)) {
    Write-Host "[ERROR] MP_PlayerManager.dll not found"
    Write-Host "  Searched: $DllSrc"
    Write-Host "  (SDK-style output may differ, check .godot/mono/temp/)"
    exit 1
}
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ModsOutput "MP_PlayerManager.dll") -Force
Copy-Item "MP_PlayerManager.pck" -Destination (Join-Path $ModsOutput "MP_PlayerManager.pck") -Force
Copy-Item "mod_manifest.json" -Destination (Join-Path $ModsOutput "mod_manifest.json") -Force
$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "v0.1.0 $buildStamp" -Encoding UTF8

# ── 快照到 torelease ──────────────────────────────────────────────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Path $ToReleaseDir -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ToReleaseDir "MP_PlayerManager.dll") -Force
Copy-Item "MP_PlayerManager.pck" -Destination (Join-Path $ToReleaseDir "MP_PlayerManager.pck") -Force
Copy-Item "mod_manifest.json" -Destination (Join-Path $ToReleaseDir "mod_manifest.json") -Force
Set-Content -Path (Join-Path $ToReleaseDir "last_build.txt") -Value "v0.1.0 $buildStamp" -Encoding UTF8

Write-Host ""
Write-Host "=========================================="
Write-Host "  BUILD SUCCESS"
Write-Host "=========================================="
Write-Host "  Output : $ModsOutput"
Write-Host "  Snapshot: $ToReleaseDir"
Write-Host "  Time   : $buildStamp"
Write-Host ""
Write-Host "  安装：复制 $ModsOutput\ 到游戏 mods 目录"
Write-Host "  停游戏后再操作 DLL，避免文件被占用"
Write-Host ""
