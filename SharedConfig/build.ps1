# SharedConfig build script
# Run: .\build.ps1 from project root
# Requires: .NET 8 SDK

param(
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$OutputDir = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\SharedConfig"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Set-Location $ProjectRoot

dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { Write-Error "Dotnet build failed" }
Write-Host "[1/2] Dotnet build done"

$dllSrc = ".godot\mono\temp\bin\Debug\SharedConfig.dll"
if (-not (Test-Path $dllSrc)) { Write-Error "SharedConfig.dll not found: $dllSrc" }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Copy-Item $dllSrc -Destination $OutputDir -Force
Write-Host "[2/2] Copied to $OutputDir"
Write-Host "Build done."
