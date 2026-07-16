param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GodotExe = "K:\杀戮尖塔mod制作\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe",
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modId = "DimensionalTraveler"
$gameModsDir = Join-Path $Sts2GamePath "mods\$modId"
$stagingDir = Join-Path $projectRoot "torelease\$modId"
$dllSource = Join-Path $projectRoot ".godot\mono\temp\bin\$Configuration\$modId.dll"
$pckSource = Join-Path $projectRoot "$modId.pck"
$artifactNames = @("mod_manifest.json", "$modId.dll", "$modId.pck")

Push-Location $projectRoot
try {
    if (-not (Test-Path $GodotExe)) {
        throw "未找到 Godot 4.5.1 Mono：$GodotExe"
    }

    dotnet build .\DimensionalTraveler.csproj -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build 失败，退出码：$LASTEXITCODE"
    }
    if (-not (Test-Path $dllSource)) {
        throw "构建成功但未找到 DLL：$dllSource"
    }

    if (Test-Path $pckSource) {
        Remove-Item $pckSource -Force
    }
    $godotProcess = Start-Process -FilePath $GodotExe -ArgumentList @(
        "--headless",
        "--path", $projectRoot,
        "--export-pack", "BasicExport", $pckSource
    ) -Wait -PassThru -NoNewWindow
    if ($godotProcess.ExitCode -ne 0) {
        $godotExitCode = $godotProcess.ExitCode
        throw "Godot PCK 导出失败，退出码：$godotExitCode"
    }
    if (-not (Test-Path $pckSource)) {
        throw "Godot 导出结束但未找到 PCK：$pckSource"
    }

    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $stagingDir, $gameModsDir | Out-Null

    Copy-Item .\mod_manifest.json (Join-Path $stagingDir "mod_manifest.json") -Force
    Copy-Item $dllSource (Join-Path $stagingDir "$modId.dll") -Force
    Copy-Item $pckSource (Join-Path $stagingDir "$modId.pck") -Force

    foreach ($artifactName in $artifactNames) {
        Copy-Item (Join-Path $stagingDir $artifactName) (Join-Path $gameModsDir $artifactName) -Force
    }

    Write-Host "构建与部署完成：$gameModsDir"
    foreach ($artifactName in $artifactNames) {
        $artifact = Get-Item (Join-Path $gameModsDir $artifactName)
        Write-Host ("  {0} ({1:N0} bytes)" -f $artifact.Name, $artifact.Length)
    }
}
finally {
    Pop-Location
}
