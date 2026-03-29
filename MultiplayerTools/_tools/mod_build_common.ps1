# Shared build functions for STS2 Mod projects
# Sourced by each mod's build.ps1

function Get-RepoRoot {
    $script:RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}

function Find-GodotExe {
    param([string]$HintDir)
    if ($HintDir) {
        $candidates = @(
            "$HintDir\Godot_v4.2.2_win64.exe",
            "$HintDir\Godot_v4.2.1_win64.exe",
            "$HintDir\Godot.exe"
        )
    } else {
        $candidates = @(
            "$env:LOCALAPPDATA\Programs\Godot\Godot.exe",
            "C:\Program Files\Godot\Godot.exe",
            "C:\Godot\Godot.exe"
        )
    }
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    return $null
}

function Invoke-DotnetBuild {
    param(
        [string]$ProjectPath,
        [string]$Config = "Release"
    )
    $exe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $exe) { throw "dotnet not found" }
    Write-Host "[build] dotnet build $ProjectPath -c $Config"
    & $exe build "$ProjectPath" -c $Config --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
}

function Copy-ModsFiles {
    param(
        [string]$SourceDir,
        [string]$ModId,
        [string]$GameModDir
    )
    $target = Join-Path $GameModDir $ModId
    if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
    Copy-Item "$SourceDir\mod_manifest.json" "$target\$ModId.json" -Force
    Write-Host "[build] copied manifest -> $target\$ModId.json"
}

function Build-ModPck {
    param(
        [string]$GodotExe,
        [string]$ExportPreset,
        [string]$OutputDir
    )
    $arg = "--headless", "--path", ".", "--export-release", $ExportPreset
    Write-Host "[build] Godot export: $arg"
    & $GodotExe $arg
    if ($LASTEXITCODE -ne 0) { throw "Godot export failed" }
    Write-Host "[build] PCK created"
}

function Package-ModZip {
    param(
        [string]$ModId,
        [string]$SourceDir,
        [string]$OutputDir
    )
    $zip = Join-Path $OutputDir "$ModId.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path "$SourceDir\*" -DestinationPath $zip -Force
    Write-Host "[build] ZIP -> $zip"
}
