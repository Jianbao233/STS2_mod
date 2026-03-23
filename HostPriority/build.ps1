param(
    [string]$GodotExe = "..\..\..\..\..\..\SteamLibrary\steamapps\common\Slay the Spire 2\Godot_v4.5.1\Godot_v4.5.1.exe"
)
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$GameModsDir = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\HostPriority"
$GameModsDirAlt = "C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\mods\HostPriority"
Write-Host "=== HostPriority Mod Build ===" -ForegroundColor Cyan
Write-Host "Project: $ProjectRoot"
Write-Host "[1/3] dotnet build..." -ForegroundColor Yellow
dotnet build -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
Write-Host "[2/3] Copy to mods directory..." -ForegroundColor Yellow
if (Test-Path $GameModsDir) {
    $TargetDir = $GameModsDir
}
else {
    if (Test-Path $GameModsDirAlt) {
        $TargetDir = $GameModsDirAlt
    }
    else {
        if (Test-Path "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods") {
            $TargetDir = $GameModsDir
            New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
        }
        else {
            $TargetDir = $GameModsDirAlt
            New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
        }
    }
}
Write-Host "  Target: $TargetDir"
$DllPath = ".godot\mono\temp\bin\Debug\HostPriority.dll"
if (Test-Path $DllPath) {
    Copy-Item -Path $DllPath -Destination "$TargetDir\HostPriority.dll" -Force
    Write-Host "  Copied HostPriority.dll OK"
}
else {
    Write-Host "  Warning: $DllPath not found"
}
if (Test-Path "mod_manifest.json") {
    Copy-Item -Path "mod_manifest.json" -Destination "$TargetDir\mod_manifest.json" -Force
    Write-Host "  Copied mod_manifest.json OK"
}

# ── 同时复制到 torelease（发布专用，每次构建都是全新快照）─────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Force -Path $ToReleaseDir | Out-Null
if (Test-Path $DllPath) {
    Copy-Item -Path $DllPath -Destination (Join-Path $ToReleaseDir "HostPriority.dll") -Force
}
if (Test-Path "mod_manifest.json") {
    Copy-Item -Path "mod_manifest.json" -Destination (Join-Path $ToReleaseDir "mod_manifest.json") -Force
}

Write-Host "[3/3] Done. Target: $TargetDir" -ForegroundColor Green
Write-Host "         Also snapshot → $ToReleaseDir (for release packaging)"
Write-Host "Build complete. Please test in game." -ForegroundColor Yellow
