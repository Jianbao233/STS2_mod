# FreeLoadout build script
param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$GodotExe = ""
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$BuildDir = Join-Path $ProjectRoot "build"
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
    # 与工作区其他 Mod（RichPing/ControlPanel）一致：杀戮尖塔mod制作 下的 Godot 安装包
    $candidates = @(
        (Join-Path $ProjectRoot "..\..\..\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"),
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

# ── 清理 build/ 并重建 ──────────────────────────────────────────────────────
Set-Location $ProjectRoot
if (Test-Path $BuildDir) { Remove-Item $BuildDir -Recurse -Force }
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null

# ── dotnet build ──────────────────────────────────────────────────────────
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { Write-Host "[dotnet] BUILD FAILED"; exit 1 }
Write-Host "[dotnet] Build done"

# ── 导出 PCK（输出到 build/） ───────────────────────────────────────────────
$PckName = "MP_PlayerManager.pck"
$PckDest = Join-Path $BuildDir $PckName
if (Test-Path $PckName) { Remove-Item $PckName -Force }
& $GodotPath --path . --export-pack "Windows Desktop" $PckName --headless
$wait = 0
while (-not (Test-Path $PckName) -and $wait -lt 60) {
    Start-Sleep -Seconds 2
    $wait += 2
}
if (-not (Test-Path $PckName)) {
    Write-Host "[Godot] PCK export FAILED"
    exit 1
}
Move-Item $PckName -Destination $PckDest -Force
Write-Host "[Godot] PCK export done"

# ── 收集 DLL ───────────────────────────────────────────────────────────────
$DllName = "MP_PlayerManager.dll"
$DllSrc = ".godot\mono\temp\bin\Debug\$DllName"
if (-not (Test-Path $DllSrc)) {
    $DllSrc = "bin\Debug\net8.0\$DllName"
}
if (-not (Test-Path $DllSrc)) {
    Write-Host "[ERROR] $DllName not found"
    exit 1
}
Copy-Item $DllSrc -Destination (Join-Path $BuildDir $DllName) -Force

# ── 复制静态资源到 build/（仅快照；勿同步 localization 到游戏 mods/，会被当作 mod_manifest 扫描）──
if (Test-Path "mod_manifest.json") {
    Copy-Item "mod_manifest.json" -Destination $BuildDir -Force
}
if (Test-Path "localization") {
    Copy-Item "localization" -Destination (Join-Path $BuildDir "localization") -Recurse -Force
}
if (Test-Path "assets") {
    Copy-Item "assets" -Destination (Join-Path $BuildDir "assets") -Recurse -Force
}

# ── 写入 last_build.txt ────────────────────────────────────────────────────
$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $BuildDir "last_build.txt") -Value "v0.1.0 $buildStamp" -Encoding UTF8

# ── 同步到游戏 mods/ ────────────────────────────────────────────────────────
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item (Join-Path $BuildDir $DllName) -Destination (Join-Path $ModsOutput $DllName) -Force
Copy-Item $PckDest -Destination (Join-Path $ModsOutput $PckName) -Force
if (Test-Path "mod_manifest.json") {
    Copy-Item "mod_manifest.json" -Destination $ModsOutput -Force
}
# 清理旧版误部署的 loose 文件（游戏会扫描 mods 下所有 .json）
Remove-Item (Join-Path $ModsOutput "localization") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ModsOutput "config.json") -Force -ErrorAction SilentlyContinue
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "v0.1.0 $buildStamp" -Encoding UTF8
Write-Host "[sync] → $ModsOutput"

# ── 快照到 torelease/ ──────────────────────────────────────────────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Path $ToReleaseDir -Force | Out-Null
Copy-Item (Join-Path $BuildDir $DllName) -Destination (Join-Path $ToReleaseDir $DllName) -Force
Copy-Item $PckDest -Destination (Join-Path $ToReleaseDir $PckName) -Force
if (Test-Path "mod_manifest.json") {
    Copy-Item "mod_manifest.json" -Destination $ToReleaseDir -Force
}
Set-Content -Path (Join-Path $ToReleaseDir "last_build.txt") -Value "v0.1.0 $buildStamp" -Encoding UTF8

Write-Host ""
Write-Host "=========================================="
Write-Host "  BUILD SUCCESS"
Write-Host "=========================================="
Write-Host "  Build    : $BuildDir"
Write-Host "  Mods     : $ModsOutput"
Write-Host "  Snapshot : $ToReleaseDir"
Write-Host "  Time     : $buildStamp"
Write-Host ""
Write-Host "  停游戏后再操作 DLL，避免文件被占用"
Write-Host ""
