# MultiplayerTools Build Script
# Usage: .\build.ps1 [-Config Release|Debug]

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path $PSCommandPath -Parent
Set-Location $ScriptDir

# Load shared build helpers (optional, for future use)
$Config = if ($args[0] -eq "-Debug") { "Debug" } else { "Release" }
$ModId = "MultiplayerTools"
$ProjectFile = "$ScriptDir\MultiplayerTools.csproj"
$GameModDir = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods"

# Step 1: dotnet build
Write-Host "=== Building MultiplayerTools ($Config) ==="
& dotnet build "$ProjectFile" -c $Config --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# Step 2: Copy DLL + PDB
# Godot.NET.Sdk outputs to .godot/mono/temp/bin/{Config}/
$dll = "$ScriptDir\.godot\mono\temp\bin\$Config\$ModId.dll"
$pdb = "$ScriptDir\.godot\mono\temp\bin\$Config\$ModId.pdb"
$targetDir = Join-Path $GameModDir $ModId
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
if (Test-Path $dll) {
    Copy-Item $dll "$targetDir\$ModId.dll" -Force
    Write-Host "[build] DLL -> $targetDir\$ModId.dll"
} else {
    Write-Host "[build] DLL not found at $dll"
}
if (Test-Path $pdb) {
    Copy-Item $pdb "$targetDir\$ModId.pdb" -Force
    Write-Host "[build] PDB copied"
}

# Step 3: Copy mod_manifest as {ModId}.json
Copy-Item "$ScriptDir\mod_manifest.json" "$targetDir\$ModId.json" -Force
Write-Host "[build] Manifest -> $targetDir\$ModId.json"

# Step 4: PCK export (requires Godot editor installed)
$godotExe = @(
    "C:\Program Files\Godot\Godot.exe",
    "C:\Godot\Godot.exe",
    "$env:LOCALAPPDATA\Programs\Godot\Godot.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($godotExe) {
    Write-Host "=== Exporting PCK with Godot ==="
    & $godotExe --headless --path "$ScriptDir" --export-release "$ScriptDir\export_presets.cfg"
    if ($LASTEXITCODE -eq 0) {
        $pckDest = Join-Path $GameModDir "$ModId.pck"
        if (Test-Path "$ScriptDir\build\$ModId.pck") {
            Copy-Item "$ScriptDir\build\$ModId.pck" $pckDest -Force
            Write-Host "[build] PCK -> $pckDest"
        }
    } else {
        Write-Host "[build] Godot export failed (exit $LASTEXITCODE) - PCK not generated"
    }
} else {
    Write-Host "[build] Godot not found - skip PCK export. Install Godot to enable."
    Write-Host "    Godot editor path hints:"
    Write-Host "      C:\Program Files\Godot\Godot.exe"
    Write-Host "      C:\Godot\Godot.exe"
    Write-Host "      `$env:LOCALAPPDATA\Programs\Godot\Godot.exe"
}

Write-Host ""
Write-Host "=== Build complete ==="
Write-Host "Test: Start SlayTheSpire2 and press F1 to open MultiplayerTools panel"
