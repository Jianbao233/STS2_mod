# RunHistoryAnalyzer build script
# Usage: .\build.ps1
#        .\build.ps1 -GodotExe "C:\path\Godot.exe"

param(
    [string]$GodotExe = "",
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ModName = "RunHistoryAnalyzer"
$ModsOutput = Join-Path $Sts2GamePath "mods\$ModName"

# -- Find Godot 4.5.1 Mono --
$GodotPath = $null
if ($GodotExe -and (Test-Path $GodotExe)) {
    $GodotPath = (Resolve-Path $GodotExe).Path
}
elseif ($cmd = Get-Command "Godot" -ErrorAction SilentlyContinue) {
    $GodotPath = $cmd.Source
}
else {
    $candidates = @(
        (Join-Path $ProjectRoot "..\..\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"),
        "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Godot\Godot_v4.5.1-stable_mono_win64.exe"
    )
    $GodotPath = $candidates | Where-Object { Test-Path $_ } | ForEach-Object { (Resolve-Path $_).Path } | Select-Object -First 1
}

if (-not $GodotPath) {
    Write-Host "ERROR: Godot 4.5.1 Mono not found." -ForegroundColor Red
    Write-Host "Download from: https://github.com/godotengine/godot/releases"
    Write-Host "Or use -GodotExe to specify path."
    exit 1
}

Write-Host "Godot: $GodotPath"

# -- Step 1: dotnet build --
Set-Location $ProjectRoot
Write-Host "[1/4] dotnet build..."
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "[1/4] dotnet build OK"

# -- Step 2: Godot export PCK --
$PckOut = Join-Path $ProjectRoot "$ModName.pck"
if (Test-Path $PckOut) { Remove-Item $PckOut -Force }

Write-Host "[2/4] Godot --export-pack..."
& $GodotPath --path . --export-pack "Windows Desktop" $PckOut --headless

$wait = 0
while (-not (Test-Path $PckOut) -and $wait -lt 60) {
    Start-Sleep -Seconds 2
    $wait += 2
}

if (-not (Test-Path $PckOut)) {
    Write-Host "PCK export failed!" -ForegroundColor Red
    exit 1
}
Write-Host "[2/4] PCK export OK"

# -- Step 3: Sync to game mods folder --
Write-Host "[3/4] Sync to mod folder..."
New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null

$DllSrc = ".godot\mono\temp\bin\Debug\$ModName.dll"
if (-not (Test-Path $DllSrc)) {
    Write-Host "DLL not found: $DllSrc" -ForegroundColor Red
    exit 1
}

Copy-Item $DllSrc         -Destination (Join-Path $ModsOutput "$ModName.dll")       -Force
Copy-Item $PckOut         -Destination (Join-Path $ModsOutput "$ModName.pck")     -Force
Copy-Item "mod_manifest.json" -Destination (Join-Path $ModsOutput "mod_manifest.json") -Force

$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "$buildStamp" -Encoding UTF8

Write-Host "[3/4] Synced to: $ModsOutput"

# -- Step 4: Verify --
Write-Host "[4/4] Verifying..."
$required = @("$ModName.dll", "$ModName.pck", "mod_manifest.json")
$allOk = $true
foreach ($f in $required) {
    $p = Join-Path $ModsOutput $f
    if (Test-Path $p) {
        $size = (Get-Item $p).Length
        $sizeKb = [Math]::Round($size / 1KB, 1)
        Write-Host "  OK   $f  ($sizeKb KB)"
    }
    else {
        Write-Host "  MISSING  $f" -ForegroundColor Red
        $allOk = $false
    }
}

if ($allOk) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " Build + Sync COMPLETE" -ForegroundColor Green
    Write-Host " Mod: $ModsOutput" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Green
}
else {
    Write-Host "Some files missing!" -ForegroundColor Red
    exit 1
}
