# NoClientCheats build script
param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$GodotExe = ""
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModsOutput = Join-Path $Sts2GamePath "mods\NoClientCheats"

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

$PckPath = Join-Path $ProjectRoot "NoClientCheats.pck"
if (Test-Path "NoClientCheats.pck") { Remove-Item "NoClientCheats.pck" -Force }
& $GodotPath --path . --export-pack "Windows Desktop" "NoClientCheats.pck" --headless
$wait = 0
while (-not (Test-Path "NoClientCheats.pck") -and $wait -lt 60) { Start-Sleep -Seconds 2; $wait += 2 }
if (-not (Test-Path "NoClientCheats.pck")) { Write-Error "PCK export failed"; exit 1 }
Write-Host "[2/3] PCK export done"

$DllSrc = ".godot\mono\temp\bin\Debug\NoClientCheats.dll"
if (-not (Test-Path $DllSrc)) { Write-Error "NoClientCheats.dll not found"; exit 1 }
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-Item $DllSrc -Destination (Join-Path $ModsOutput "NoClientCheats.dll") -Force
Copy-Item "NoClientCheats.pck" -Destination (Join-Path $ModsOutput "NoClientCheats.pck") -Force
$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "v1 $buildStamp" -Encoding UTF8
if (Test-Path "mod_manifest.json") { Copy-Item "mod_manifest.json" -Destination (Join-Path $ModsOutput "mod_manifest.json") -Force }
Write-Host "[3/3] Copied to $ModsOutput"
Write-Host "Build done. Host install only. ModConfig for toggle."
