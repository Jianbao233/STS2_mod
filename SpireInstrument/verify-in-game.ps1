# =============================================================================
#  SpireInstrument 游戏内无头自检
#
#  作用：不靠人眼，验证 mod 真的能在游戏里加载 —— 挂载成功、浮窗建出来、
#        琴键数量与音阶模式匹配、6 个音色都在、试奏能产生声部、无异常。
#
#  用法：
#    .\verify-in-game.ps1              部署 + 自检（需要游戏未运行）
#    .\verify-in-game.ps1 -SkipDeploy  只自检（复用已部署的构建）
#    .\verify-in-game.ps1 -KeepMarker  结束后保留标记文件（便于手挂调试器）
#    .\verify-in-game.ps1 -ShowLog     结束后打印日志尾部
#
#  注意：
#   * 游戏必须处于关闭状态；脚本自己会起一个无头实例并在自检结束后退出。
#   * 游戏启动时会**轮转日志**（godot.log → godot<时间戳>.log），
#     所以判定必须按"本次启动之后被写过的日志文件"来找，不能只看 godot.log。
# =============================================================================
[CmdletBinding()]
param(
    [string]$GameDir = 'F:\Steam\steamapps\common\Slay the Spire 2',
    [int]$TimeoutSec = 180,
    [switch]$SkipDeploy,
    [switch]$KeepMarker,
    [switch]$ShowLog,
    [switch]$Capture,
    [string]$Resolution = '2560x1440',
    [string]$ShotDir = 'D:\A-Developing\tmp\spireinstrument-shots'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$marker = Join-Path $env:TEMP 'spireinstrument-selftest'
$logDir = Join-Path $env:APPDATA 'SlayTheSpire2\logs'
$exe = Join-Path $GameDir 'SlayTheSpire2.exe'

function Step($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  [OK] $m" -ForegroundColor Green }
function Bad($m)  { Write-Host "  [FAIL] $m" -ForegroundColor Red }
function Die($m)  { Bad $m; exit 1 }

Step '前置检查'
if (-not (Test-Path $exe)) { Die "找不到游戏可执行文件：$exe" }
if (Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue) {
    Die '游戏正在运行，请先关闭游戏（无头自检需要独占启动）。'
}
Ok '游戏未运行'

if (-not $SkipDeploy) {
    Step '部署最新构建'
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { Die 'build.ps1 失败' }
    Ok '部署完成'
} else {
    $deployed = Join-Path $GameDir 'mods\SpireInstrument\SpireInstrument.dll'
    if (-not (Test-Path $deployed)) { Die "未部署：$deployed（去掉 -SkipDeploy 再跑）" }
    Ok '使用已部署的构建'
}

$startTime = Get-Date
$exitCode = $null

try {
    Step '建立自检标记'
    Set-Content -Path $marker -Value 'selftest' -Encoding ASCII
    Ok "标记文件：$marker"

    Step '启动游戏（自检结束会自行退出）'
    # -Capture：必须有真实渲染器才能截图，所以开窗口跑（窗口会短暂出现）
    # 注意 Godot 4 的参数格式：--position 用逗号，--resolution 用 x
    $gameArgs = if ($Capture) {
        @('--windowed', '--resolution', $Resolution, '--position', '40,40', '--audio-driver', 'Dummy')
    } else {
        @('--headless', '--audio-driver', 'Dummy')
    }
    Write-Host "  模式：$(if ($Capture) { '窗口化截图巡游' } else { '无头自检' })"
    Write-Host "  参数：$($gameArgs -join ' ')"

    # 捕获子进程 stdout/stderr，启动失败时能直接看到原因
    $outFile = Join-Path $env:TEMP 'spireinstrument-run.out.txt'
    $errFile = Join-Path $env:TEMP 'spireinstrument-run.err.txt'
    Remove-Item $outFile, $errFile -Force -ErrorAction SilentlyContinue

    $proc = Start-Process -FilePath $exe -WorkingDirectory $GameDir `
        -ArgumentList $gameArgs -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    Write-Host "  PID=$($proc.Id)，最长等待 $TimeoutSec 秒…"

    if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
        & taskkill /PID $proc.Id /T /F | Out-Null
        Die "超时 $TimeoutSec 秒未退出，已强杀。多半是自检链断了（检查日志里的 [SELFTEST] 行）。"
    }
    $proc.Refresh()
    $exitCode = $proc.ExitCode
    Ok "游戏已退出，退出码 $(if ($null -eq $exitCode) { '（不可读，按日志判定）' } else { $exitCode })"
}
finally {
    # 无论成败都别把标记文件留给下一次启动
    if (-not $KeepMarker -and (Test-Path $marker)) { Remove-Item $marker -Force -ErrorAction SilentlyContinue }
}

Step '解析日志（按"本次启动后被写过的日志文件"查找，兼容日志轮转）'
$logs = Get-ChildItem $logDir -Filter *.log -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -ge $startTime.AddSeconds(-5) } |
    Sort-Object LastWriteTime
if (-not $logs) {
    Write-Host "`n--- 子进程 stdout ---"; Get-Content $outFile -Tail 25 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
    Write-Host "`n--- 子进程 stderr ---"; Get-Content $errFile -Tail 25 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
    Die "本次启动没有产生任何日志（$logDir）—— 游戏可能根本没跑起来（见上面的子进程输出）。"
}

$text = ''
foreach ($f in $logs) {
    Write-Host "  读取 $($f.Name)  ($([math]::Round($f.Length/1KB)) KB)"
    $text += (Get-Content $f.FullName -Raw) + "`n"
}

# 只分析「最后一次启动」的片段：自检标记被读取的那一行是每次运行的起点。
# 这样即使上次运行的日志还在窗口期内（日志轮转），也不会把旧结果混进来。
$chunks = $text -split '\[SpireInstrument\] \[SELFTEST\] marker found'
if ($chunks.Count -lt 2) {
    Die '本次启动没有看到自检标记被读取（日志里没有 "[SELFTEST] marker found"）—— mod 可能没加载，或标记文件没生效。'
}
$session = $chunks[-1]

$hostLine   = [regex]::Match($session, 'host=(\d)')
$probeLine  = [regex]::Match($session, '(panel=[^\r\n]+)')
$resultLine = [regex]::Match($session, 'RESULT=(\w+) failures=(\d+)')
$failLines  = [regex]::Matches($session, '\[SELFTEST\] FAIL: ([^\r\n]+)')

Write-Host "  host   : $(if ($hostLine.Success) { $hostLine.Groups[1].Value } else { '（缺）' })"
Write-Host "  探测串 : $(if ($probeLine.Success) { $probeLine.Groups[1].Value.Trim() } else { '（缺）' })"
Write-Host "  结果   : $(if ($resultLine.Success) { $resultLine.Groups[1].Value + ' failures=' + $resultLine.Groups[2].Value } else { '（缺）' })"
foreach ($fl in $failLines) { Bad $fl.Groups[1].Value.Trim() }

if ($ShowLog) { Write-Host "`n--- 日志中 SpireInstrument 相关行 ---"; ($text -split "`n" | Where-Object { $_ -match 'SpireInstrument' }) | ForEach-Object { Write-Host $_ } }

# ---- 截图巡游：把游戏内渲染出的 PNG 收进工作区 ----
if ($Capture) {
    Step '收集截图'
    $userDir = Join-Path $env:APPDATA 'SlayTheSpire2'
    $shots = Get-ChildItem $userDir -Filter 'spireinstrument_*.png' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $startTime.AddSeconds(-5) }
    if (-not $shots) {
        Bad "没有产生截图 —— 多半是窗口化启动失败了（检查日志里的 capture ... failed 行）。"
    } else {
        New-Item -ItemType Directory -Path $ShotDir -Force | Out-Null
        Get-ChildItem $ShotDir -Filter '*.png' -ErrorAction SilentlyContinue | Remove-Item -Force
        foreach ($s in $shots) {
            Copy-Item $s.FullName (Join-Path $ShotDir $s.Name) -Force
            Write-Host "  $($s.Name)  $([math]::Round($s.Length/1KB)) KB"
        }
        Ok "已收集 $($shots.Count) 张到 $ShotDir"
        if ($shots.Count -lt 8) { Bad "截图数量偏少（期望 8 张：6 档位 + 1 跟弹轨道 + 1 英文界面），可能有步骤没跑到。" }
    }
}

$ok = $hostLine.Success -and $hostLine.Groups[1].Value -eq '1' `
      -and $probeLine.Success `
      -and $resultLine.Success -and $resultLine.Groups[1].Value -eq 'PASS' `
      -and ($null -eq $exitCode -or $exitCode -eq 0)

if ($ok) { Write-Host "`n游戏内自检通过。" -ForegroundColor Green; exit 0 }
Bad '游戏内自检未通过（详见上面的探测串与失败项）'
exit 1
