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
        (Join-Path $ProjectRoot "..\..\..\..\..\SteamLibrary\steamapps\common\Slay the Spire 2\Godot_v4.5.1-stable_mono_win64.exe"),
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

$BuildOutput = Join-Path $ProjectRoot ".godot\mono\temp\bin\Debug"
$DllSrc = Join-Path $BuildOutput "NoClientCheats.dll"
if (-not (Test-Path $DllSrc)) { Write-Error "NoClientCheats.dll not found"; exit 1 }

$ExcludedDependencyDlls = @("NoClientCheats.dll", "GodotSharp.dll", "GodotSharpEditor.dll")
$DependencyDlls = Get-ChildItem -Path $BuildOutput -Filter "*.dll" -File |
    Where-Object { $ExcludedDependencyDlls -notcontains $_.Name }
$DependencyMetaFiles = @("NoClientCheats.deps.json", "NoClientCheats.runtimeconfig.json") |
    ForEach-Object { Join-Path $BuildOutput $_ } |
    Where-Object { Test-Path $_ }

function Copy-ModPayload([string]$TargetDir) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Copy-Item $DllSrc -Destination (Join-Path $TargetDir "NoClientCheats.dll") -Force
    Copy-Item "NoClientCheats.pck" -Destination (Join-Path $TargetDir "NoClientCheats.pck") -Force
    if (Test-Path "mod_manifest.json") {
        Copy-Item "mod_manifest.json" -Destination (Join-Path $TargetDir "mod_manifest.json") -Force
    }
    foreach ($dep in $DependencyDlls) {
        Copy-Item $dep.FullName -Destination (Join-Path $TargetDir $dep.Name) -Force
    }
    foreach ($meta in $DependencyMetaFiles) {
        $metaName = [System.IO.Path]::GetFileName($meta)
        Copy-Item $meta -Destination (Join-Path $TargetDir $metaName) -Force
    }
}

New-Item -ItemType Directory -Path $ModsOutput -Force | Out-Null
Copy-ModPayload -TargetDir $ModsOutput
$buildStamp = Get-Date -Format "yyyy-MM-dd HH:mm"
Set-Content -Path (Join-Path $ModsOutput "last_build.txt") -Value "v1.3.1 $buildStamp" -Encoding UTF8

# ── 同时复制到 torelease（发布专用，每次构建都是全新快照）─────────────
$ToReleaseDir = Join-Path $ProjectRoot "torelease"
New-Item -ItemType Directory -Path $ToReleaseDir -Force | Out-Null
Copy-ModPayload -TargetDir $ToReleaseDir
Set-Content -Path (Join-Path $ToReleaseDir "last_build.txt") -Value "v1.3.1 $buildStamp" -Encoding UTF8

if ($DependencyDlls.Count -gt 0) {
    Write-Host "         Included dependencies: $($DependencyDlls.Name -join ', ')"
}
if ($DependencyMetaFiles.Count -gt 0) {
    $metaNames = $DependencyMetaFiles | ForEach-Object { [System.IO.Path]::GetFileName($_) }
    Write-Host "         Included metadata: $($metaNames -join ', ')"
}

Write-Host "[3/3] Copied to $ModsOutput"
Write-Host "         Also snapshot → $ToReleaseDir (for release packaging)"
Write-Host "Build done. Host install only. ModConfig for toggle."
