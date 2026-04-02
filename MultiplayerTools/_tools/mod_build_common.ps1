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
    if (-not $exe) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show("dotnet not found. Please install .NET SDK.", "Build Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        throw "dotnet not found"
    }
    Write-Host "[build] dotnet build $ProjectPath -c $Config"
    & $exe build "$ProjectPath" -c $Config --nologo 2>&1 | Tee-Object -Variable buildOutput | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show("dotnet build failed (exit $LASTEXITCODE)`n`n$buildOutput", "Build Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        throw "dotnet build failed"
    }
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
    & $GodotExe $arg 2>&1 | Tee-Object -Variable exportOutput | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show("Godot export failed (exit $LASTEXITCODE)`n`n$exportOutput", "Build Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
        throw "Godot export failed"
    }
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
