param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$Sts2GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$adapterRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $adapterRoot
$modId = "DimensionalTraveler.TestAdapter"
$outputDir = Join-Path $adapterRoot "bin\$Configuration\net9.0"
$gameModDir = Join-Path $Sts2GamePath "mods\$modId"

Push-Location $adapterRoot
try {
    dotnet build .\DimensionalTraveler.TestAdapter.csproj -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "测试适配器构建失败，退出码：$LASTEXITCODE"
    }

    New-Item -ItemType Directory -Force -Path $gameModDir | Out-Null
    Copy-Item (Join-Path $outputDir "$modId.dll") (Join-Path $gameModDir "$modId.dll") -Force
    Copy-Item .\mod_manifest.json (Join-Path $gameModDir "mod_manifest.json") -Force
    Write-Host "测试适配器已部署：$gameModDir"
}
finally {
    Pop-Location
}