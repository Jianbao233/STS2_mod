# MultiplayerTools Build Script - 默认 Debug 构建并覆盖 mods
param(
    [string]$Config = "Debug",
    [string]$GodotExe = ""
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path $PSCommandPath -Parent
Set-Location $ScriptDir

$ModId = "MultiplayerTools"
$GameModDir = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods"
$ModsOutput = Join-Path $GameModDir $ModId

# dotnet build
Write-Host "=== Building $ModId ($Config) ===" -ForegroundColor Cyan
dotnet build -c $Config --no-restore --nologo 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] build failed" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Build done" -ForegroundColor Green

# DLL 路径
$DllSrc = ".godot\mono\temp\bin\$Config\$ModId.dll"
if (-not (Test-Path $DllSrc)) {
    Write-Host "[ERROR] DLL not found: $DllSrc" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] DLL: $DllSrc ($((Get-Item $DllSrc).Length) bytes)"

# 复制到 mods
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ModsOutput "$ModId.dll") -Force
Write-Host "[OK] Copied to mods: $ModsOutput" -ForegroundColor Green
Write-Host ""
Write-Host "BUILD SUCCESS - $ModId.dll updated" -ForegroundColor Green
