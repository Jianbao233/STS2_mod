param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string[]]$Suite = @("infrastructure", "production", "formulas", "operations", "special-axes"),
    [string]$Endpoint = "http://127.0.0.1:9877/messages",
    [string]$GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [switch]$SkipBuild,
    [switch]$ReuseSession
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent $root)
$reportDir = Join-Path $root "reports"
if (-not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
}
$timestamp = [DateTimeOffset]::Now.ToString("yyyyMMdd_HHmmss")
$reportPath = Join-Path $reportDir "acceptance_$timestamp.json"
$runtimeLog = Join-Path $projectRoot "_runtime_acceptance_$timestamp.log"
$gameExe = Join-Path $GamePath "SlayTheSpire2.exe"
$startedProcess = $null

Import-Module (Join-Path $root "lib\AcceptanceDsl.psm1") -Force
Initialize-DtAcceptance -Endpoint $Endpoint

function Test-BridgeAvailable {
    $healthUri = $Endpoint -replace '/messages$', '/health'
    try {
        $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 2
        return $health.status -eq "ok"
    }
    catch {
        return $false
    }
}

try {
    if (-not $SkipBuild) {
        & (Join-Path $projectRoot "build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "正式 Mod 构建部署失败。" }
        & (Join-Path $projectRoot "test-adapter\build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "测试适配器构建部署失败。" }
    }

    $bridgeExists = Test-BridgeAvailable
    if ($bridgeExists -and -not $ReuseSession) {
        throw "检测到已有 KitLib MCP 会话。为避免抢占用户游戏，请关闭该会话或显式使用 -ReuseSession。"
    }
    if (-not $bridgeExists) {
        if ($ReuseSession) {
            throw "指定了 -ReuseSession，但当前没有可用的 KitLib MCP 会话。"
        }
        if (-not (Test-Path $gameExe)) { throw "找不到游戏程序：$gameExe" }
        $startedProcess = Start-Process -FilePath $gameExe -ArgumentList @(
            "--headless",
            "--rendering-driver", "opengl3",
            "--log-file", $runtimeLog
        ) -PassThru
    }

    Wait-DtBridge -TimeoutSeconds 120
    Assert-DtTools -Required @(
        "get_game_state",
        "dev_get_session",
        "combat_action",
        "get_selection_state",
        "selection_action",
        "dev_add_monster",
        "dimensional_traveler_test_control",
        "dimensional_traveler_test_target",
        "dimensional_traveler_test_selection"
    )
    $null = Wait-DtMainMenuReady -TimeoutSeconds 180

    foreach ($suiteName in $Suite) {
        $suitePath = Join-Path $root "suites\$suiteName.ps1"
        if (-not (Test-Path $suitePath)) {
            throw "验收套件不存在：$suitePath"
        }
        & $suitePath
    }

    $results = @(Get-DtResults)
    $failed = @($results | Where-Object { -not $_.passed })
    $report = [ordered]@{
        passed = $failed.Count -eq 0
        configuration = $Configuration
        reusedSession = [bool]$ReuseSession
        suites = $Suite
        testCount = $results.Count
        passedCount = $results.Count - $failed.Count
        failedCount = $failed.Count
        runtimeLog = $runtimeLog
        checkedAt = [DateTimeOffset]::Now.ToString("o")
        tests = $results
    }
    $report | ConvertTo-Json -Depth 50 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ("验收完成：{0}/{1} 通过" -f $report.passedCount, $report.testCount)
    Write-Host "报告：$reportPath"
    if ($failed.Count -gt 0) {
        foreach ($failure in $failed) {
            Write-Host ("  FAIL [{0}] {1}: {2}" -f $failure.suite, $failure.name, $failure.error)
        }
        exit 1
    }
}
finally {
    if ($null -ne $startedProcess) {
        try {
            if (-not $startedProcess.HasExited) {
                Stop-Process -Id $startedProcess.Id
                $startedProcess.WaitForExit(10000)
            }
        }
        catch {
            Write-Warning "无法关闭本次验收启动的游戏进程 PID=$($startedProcess.Id)：$($_.Exception.Message)"
        }
    }
}