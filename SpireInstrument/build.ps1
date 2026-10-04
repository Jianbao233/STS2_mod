# =============================================================================
#  SpireInstrument / 尖塔乐器 —— 构建、自检、打包、部署
#
#  用法：
#    .\build.ps1                      构建 + 跑离线自检 + 组装 torelease + 本地部署
#    .\build.ps1 -SelfTestOnly        只跑离线自检
#    .\build.ps1 -NoLocalDeploy       不写游戏 mods 目录（只出包）
#    .\build.ps1 -StageWorkshop       额外同步到工坊工作区 content/
#    .\build.ps1 -Sts2DataDir <路径>  指定编译所对的游戏程序集目录（换分支时用）
# =============================================================================
[CmdletBinding()]
param(
    [switch]$SelfTestOnly,
    [switch]$NoLocalDeploy,
    [switch]$StageWorkshop,
    [string]$Configuration = 'Release',
    [string]$Sts2DataDir = '',
    [string]$GameDir = 'F:\Steam\steamapps\common\Slay the Spire 2',
    [string]$WorkshopWorkspace = 'D:\A-Developing\main\sts2\STS2_mod\_workshop_workspaces\SpireInstrument'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'SpireInstrument.csproj'
$testProj = Join-Path $root 'tests\SpireInstrument.Tests\SpireInstrument.Tests.csproj'
$manifestPath = Join-Path $root 'mod_manifest.json'

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Die($msg)  { Write-Host "  [FAIL] $msg" -ForegroundColor Red; exit 1 }

# ---- 版本以 csproj 为唯一真源 ----
[xml]$csproj = Get-Content $proj -Raw
$version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { Die 'csproj 里找不到 <Version>' }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version -ne $version) { Die "manifest 版本 $($manifest.version) 与 csproj $version 不一致" }
Write-Host "SpireInstrument v$version  ($Configuration)"

$buildArgs = @('build', $proj, '-c', $Configuration, '--nologo')
if ($Sts2DataDir) { $buildArgs += "/p:Sts2DataDir=$Sts2DataDir" }

if (-not $SelfTestOnly) {
    Step '编译 mod 本体'
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { Die 'dotnet build 失败' }
    Ok '编译通过'
}

# ---- 离线自检（不需要启动游戏）----
Step '离线自检（音频合成 + 音阶映射）'
& dotnet run --project $testProj -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { Die '离线自检未通过' }
Ok '自检通过'

if ($SelfTestOnly) { Write-Host "`n仅自检模式，结束。"; exit 0 }

# ---- 组装 torelease（staging：只有 dll + manifest）----
Step '组装 torelease'
$binDir = Join-Path $root ".godot\mono\temp\bin\$Configuration"
$dll = Join-Path $binDir 'SpireInstrument.dll'
if (-not (Test-Path $dll)) { Die "找不到构建产物 $dll" }

$stage = Join-Path $root 'torelease'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item $dll $stage
Copy-Item $manifestPath $stage
Ok "torelease/ = SpireInstrument.dll ($([int]((Get-Item $dll).Length/1KB)) KB) + mod_manifest.json"

# ---- 打包自检 ----
Step '打包自检'
$stageDlls = @(Get-ChildItem $stage -Filter *.dll)
if ($stageDlls.Count -ne 1) { Die "torelease 根目录应只有 1 个 dll，实际 $($stageDlls.Count) 个" }
if (-not (Test-Path (Join-Path $stage 'mod_manifest.json'))) { Die 'torelease 缺少 mod_manifest.json' }
$m2 = Get-Content (Join-Path $stage 'mod_manifest.json') -Raw | ConvertFrom-Json
if ($m2.version -ne $version) { Die 'torelease manifest 版本不一致' }
if ($m2.author -ne '@Bilibili我叫煎包') { Die '作者字段必须为 @Bilibili我叫煎包' }
Ok "包体结构正确，min_game_version=$($m2.min_game_version)"

# ---- 本地部署 ----
if (-not $NoLocalDeploy) {
    Step '本地部署'
    $gameExe = Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue
    if ($gameExe) { Die '游戏正在运行，先关掉游戏再部署（否则 dll 被占用）' }

    $modsDir = Join-Path $GameDir 'mods'
    if (-not (Test-Path $modsDir)) { Die "找不到游戏 mods 目录：$modsDir" }
    $target = Join-Path $modsDir 'SpireInstrument'
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item (Join-Path $stage '*') $target -Force
    Ok "已部署到 $target"
}

# ---- 同步到工坊工作区 ----
if ($StageWorkshop) {
    Step '同步到工坊工作区'
    $wsContent = Join-Path $WorkshopWorkspace 'content'
    if (-not (Test-Path $wsContent)) { Die "工坊工作区 content 目录不存在：$wsContent（先用 ModUploader 或手动建立 workspace）" }
    Get-ChildItem $wsContent -Recurse -File | Remove-Item -Force
    Copy-Item (Join-Path $stage '*') $wsContent -Force
    Ok "content/ 已与 torelease/ 同步（上传前请再核对 workshop.json 与实际内容）"
}

Write-Host "`n完成：SpireInstrument v$version" -ForegroundColor Green
