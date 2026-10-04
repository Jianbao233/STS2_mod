<#
.SYNOPSIS
  工坊上传前预检（针对"首次上传抢 ID"这一步不可逆操作）。

.DESCRIPTION
  ModUploader 本身没有 dry-run / validate 模式，所以这里把"上传会消费的一切"逐项检查一遍，
  给出 go / no-go。可在每次上传前重复执行。

  用法：
    .\preflight-workshop.ps1
    .\preflight-workshop.ps1 -ExpectUpdate   # 更新已有条目时用（要求 mod_id.txt 存在）
#>
[CmdletBinding()]
param(
    [string]$Workspace = 'D:\A-Developing\main\sts2\STS2_mod\_workshop_workspaces\SpireInstrument',
    [string]$UploaderDir = 'D:\A-Developing\repos\sts2-mod-uploader\artifacts\publish\ModUploader\release_win-x64',
    [string]$Staging = 'D:\A-Developing\main\sts2\STS2_mod\SpireInstrument\torelease',
    [switch]$ExpectUpdate
)

$ErrorActionPreference = 'Continue'
$fail = 0
function Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Bad($m)  { Write-Host "  [FAIL] $m" -ForegroundColor Red; $script:fail++ }
function Warn2($m){ Write-Host "  [WARN] $m" -ForegroundColor Yellow }

Write-Host "=== 工坊上传前预检 ===" -ForegroundColor Cyan
Write-Host "workspace: $Workspace`n"

# --- §0 上传器与 Steam ---
Write-Host "§0 上传器与 Steam"
foreach ($f in @('ModUploader.exe', 'steam_api64.dll', 'steam_appid.txt')) {
    if (Test-Path (Join-Path $UploaderDir $f)) { Ok "$f 存在" } else { Bad "$f 缺失（不要复制 exe 到别处运行）" }
}
if (Get-Process -Name 'steam' -ErrorAction SilentlyContinue) { Ok "Steam 进程在运行" }
else { Bad "Steam 未运行 —— SteamAPI.Init 会失败，请先启动并前台登录" }

# --- §1 workspace 结构 ---
Write-Host "`n§1 workspace 结构"
foreach ($n in @('workshop.json', 'image.png', 'content')) {
    if (Test-Path (Join-Path $Workspace $n)) { Ok "$n 存在" } else { Bad "$n 缺失" }
}
$contentFiles = @(Get-ChildItem (Join-Path $Workspace 'content') -Recurse -File -ErrorAction SilentlyContinue)
if ($contentFiles.Count -gt 0) { Ok "content/ 有 $($contentFiles.Count) 个文件" } else { Bad "content/ 为空" }
foreach ($bad in @('*.zip', '*.bak', '*.old', '*.pdb')) {
    $extra = @(Get-ChildItem $Workspace -Recurse -File -Filter $bad -ErrorAction SilentlyContinue)
    if ($extra.Count -gt 0) { Bad "发现多余文件 $bad : $($extra.Name -join ', ')" }
}

# --- §2 mod_id.txt 语义 ---
Write-Host "`n§2 条目 ID"
$idPath = Join-Path $Workspace 'mod_id.txt'
if (Test-Path $idPath) {
    $id = (Get-Content $idPath -Raw).Trim()
    if ($id -match '^\d+$') { Ok "mod_id.txt = $id（将走更新路径）" } else { Bad "mod_id.txt 内容非法：'$id'" }
} elseif ($ExpectUpdate) {
    Bad "更新模式下 mod_id.txt 必须存在"
} else {
    Warn2 "无 mod_id.txt —— **将创建全新工坊条目（不可逆）**，请确认这是预期"
}

# --- §3 workshop.json ---
Write-Host "`n§3 workshop.json"
$jsonPath = Join-Path $Workspace 'workshop.json'
try {
    $j = Get-Content $jsonPath -Raw | ConvertFrom-Json
    Ok "JSON 合法"
    if ($j.title -match 'placeholder|占位|TODO') { Bad "title 仍是占位文案：$($j.title)" } else { Ok "title: $($j.title)" }
    if ($j.visibility -eq 'private') { Ok "visibility=private（抢注阶段要求）" }
    else { Warn2 "visibility=$($j.visibility) —— 非 private，请确认已通过审核报告" }
    if ($j.dependencies.Count -gt 0) { Ok "dependencies: $($j.dependencies -join ', ')" } else { Warn2 "dependencies 为空（本 mod 需要 RitsuLib）" }
    if ($j.localizations.Count -gt 0) { Ok "localizations: $(($j.localizations | ForEach-Object { $_.language }) -join ', ')" }
    else { Warn2 "localizations 为空" }
} catch { Bad "workshop.json 解析失败：$($_.Exception.Message)" }

# --- §4 content 与构建产物一致性 ---
Write-Host "`n§4 content 与 torelease 一致性"
if (Test-Path $Staging) {
    $mismatch = 0
    foreach ($f in Get-ChildItem $Staging -File) {
        $t = Join-Path (Join-Path $Workspace 'content') $f.Name
        if (-not (Test-Path $t)) { Bad "content 缺少 $($f.Name)"; $mismatch++; continue }
        if ((Get-FileHash $f.FullName).Hash -ne (Get-FileHash $t).Hash) { Bad "$($f.Name) 与构建产物不一致（先跑 build.ps1 -StageWorkshop）"; $mismatch++ }
    }
    if ($mismatch -eq 0) { Ok "逐文件 SHA-256 一致" }
} else { Warn2 "找不到 $Staging，跳过一致性检查" }

# --- §5 封面 ---
Write-Host "`n§5 封面 image.png"
$imgPath = Join-Path $Workspace 'image.png'
if (Test-Path $imgPath) {
    $img = Get-Item $imgPath
    if ($img.Length -le 1MB) { Ok "大小 $([math]::Round($img.Length/1KB,1)) KB ≤ 1MB" } else { Bad "封面超过 1MB" }
    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
        $bmp = [System.Drawing.Image]::FromFile($imgPath)
        if ($bmp.Width -eq $bmp.Height) { Ok "正方形 $($bmp.Width)×$($bmp.Height)" } else { Bad "非正方形 $($bmp.Width)×$($bmp.Height)" }
        $bmp.Dispose()
    } catch { Warn2 "无法读取尺寸（缺 System.Drawing？）" }
}

# --- §6 文案 ---
Write-Host "`n§6 多语言文案"
foreach ($lang in @('english.md', 'schinese.md')) {
    $p = Join-Path $Workspace "i18n\$lang"
    if (Test-Path $p) {
        $c = Get-Content $p -Raw
        if ($c -match '# Title' -and $c -match '# Description') { Ok "$lang 含 Title/Description" } else { Bad "$lang 缺段" }
    } else { Bad "$lang 缺失" }
}

# --- 结论 ---
Write-Host "`n=== 结论 ===" -ForegroundColor Cyan
if ($fail -eq 0) {
    Write-Host "  ✅ 预检通过，可以上传：" -ForegroundColor Green
    Write-Host "     cd `"$UploaderDir`""
    Write-Host "     .\ModUploader.exe upload -w `"$Workspace`""
} else {
    Write-Host "  ❌ 有 $fail 项未通过，先修再上传" -ForegroundColor Red
}
exit $fail
