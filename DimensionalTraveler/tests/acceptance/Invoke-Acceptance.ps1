param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string[]]$Suite = @("infrastructure", "production", "formulas", "operations", "special-axes", "relics", "extraction", "coop-extraction"),
    [int]$BridgePort = 9877,
    [int]$ViewerPort = 9878,
    [string]$Endpoint,
    [string]$GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [string]$SettingsPath,
    [switch]$SkipBuild,
    [switch]$ReuseSession,
    [switch]$AllowStartGame
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
$runId = "dt-$timestamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$sessionDir = Join-Path $reportDir $runId
New-Item -ItemType Directory -Force -Path $sessionDir | Out-Null
$reportPath = Join-Path $sessionDir "final.json"
$runPath = Join-Path $sessionDir "run.json"
$casePath = Join-Path $sessionDir "cases.ndjson"
$runtimeLog = Join-Path $projectRoot "_runtime_acceptance_$timestamp.log"
$gameExe = Join-Path $GamePath "SlayTheSpire2.exe"
$startedProcess = $null
$observedGameProcess = $null
$previousKitLibMcpPort = [Environment]::GetEnvironmentVariable("KITLIB_MCP_PORT", "Process")
$previousAcceptanceRunId = [Environment]::GetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID", "Process")
if ($BridgePort -ne 9877) {
    throw "根治型验收固定使用 KitLib MCP 端口 9877，当前值为 $BridgePort。"
}
if ($ViewerPort -ne 9878) {
    throw "根治型验收固定使用 KitLib 开发者面板端口 9878，当前值为 $ViewerPort。"
}
if (-not [string]::IsNullOrWhiteSpace($Endpoint)) {
    throw "根治型验收不允许自定义 Endpoint，必须连接 http://127.0.0.1:9877/messages。"
}
$bridgeEndpoint = "http://127.0.0.1:$BridgePort/messages"

$initialRun = [ordered]@{
    runId = $runId
    createdAt = [DateTimeOffset]::Now.ToString("o")
    bridgePort = $BridgePort
    viewerPort = $ViewerPort
    endpoint = $bridgeEndpoint
    viewerUrl = "http://127.0.0.1:$ViewerPort/#/logs"
    configuration = $Configuration
    suites = $Suite
    runtimeLog = $runtimeLog
    status = "created"
}
$initialRun | ConvertTo-Json -Depth 20 | Set-Content -Path $runPath -Encoding UTF8

function Write-FinalReport {
    param(
        [Parameter(Mandatory)]$Report
    )
    $Report.runId = $runId
    $Report.sessionDirectory = $sessionDir
    $Report.runManifest = $runPath
    $Report.caseResults = $casePath
    $Report | ConvertTo-Json -Depth 50 | Set-Content -Path $reportPath -Encoding UTF8
}

$pendingReport = [ordered]@{
    passed = $false
    status = "running"
    configuration = $Configuration
    reusedSession = $false
    suites = $Suite
    testCount = 0
    passedCount = 0
    failedCount = 0
    runtimeLog = $runtimeLog
    viewerUrl = "http://127.0.0.1:$ViewerPort/#/logs"
    checkedAt = [DateTimeOffset]::Now.ToString("o")
    tests = @()
}
Write-FinalReport -Report $pendingReport

function Open-DevViewer {
    $viewerUrl = "http://127.0.0.1:$ViewerPort/#/logs"
    try {
        Start-Process $viewerUrl | Out-Null
        return $viewerUrl
    }
    catch {
        Write-Warning "无法自动打开 KitLib 开发者面板：$($_.Exception.Message)"
        return $viewerUrl
    }
}

function Assert-SessionHandshake {
    $session = Invoke-DtTool -Name "dimensional_traveler_test_session" -Arguments @{ action = "handshake" }
    Assert-DtEqual $runId $session.runId "测试适配器 runId 与验收会话不一致"
    Assert-DtEqual $BridgePort ([int]$session.mcpPort) "测试适配器 MCP 端口与验收端口不一致"
    if ($null -ne $observedGameProcess) {
        Assert-DtEqual $observedGameProcess.Id ([int]$session.processId) "测试适配器 PID 与验收游戏进程不一致"
    }
    foreach ($tool in @(
        "dimensional_traveler_test_control",
        "dimensional_traveler_test_target",
        "dimensional_traveler_test_selection",
        "dimensional_traveler_test_session"
    )) {
        Assert-DtTrue ($tool -in @($session.tools)) "测试适配器握手缺少工具：$tool"
    }
    return $session
}

Import-Module (Join-Path $root "lib\AcceptanceDsl.psm1") -Force
$runtimeStartedAt = [DateTimeOffset]::Now
Initialize-DtAcceptance `
    -Endpoint $bridgeEndpoint `
    -RuntimeLogPath $runtimeLog `
    -RuntimeStartedAt $runtimeStartedAt `
    -RunId $runId `
    -CaseReportPath $casePath
$executionSuites = @($Suite | Sort-Object {
    if ($_ -like "coop-*") { 0 } else { 1 }
})

function Test-BridgeAvailable {
    $healthUri = $bridgeEndpoint -replace '/messages$', '/health'
    try {
        $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 2
        return $health.status -eq "ok"
    }
    catch {
        return $false
    }
}

function Wait-DevViewer {
    $viewerUri = "http://127.0.0.1:$ViewerPort/"
    $deadline = [DateTimeOffset]::Now.AddSeconds(30)
    do {
        if ($null -ne $observedGameProcess) {
            $observedGameProcess.Refresh()
            if ($observedGameProcess.HasExited) {
                throw "[game_process_crash] KitLib 开发者面板等待期间游戏已退出；PID=$($observedGameProcess.Id)。"
            }
        }
        try {
            $response = Invoke-WebRequest -Uri $viewerUri -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                return $viewerUri
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "KitLib 开发者面板未在 30 秒内就绪：$viewerUri"
}

function Get-DtRunningGameProcesses {
    @(Get-Process -Name "SlayTheSpire2" -ErrorAction SilentlyContinue)
}

function Start-DtAcceptanceGame {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][int]$Port
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    # KitLib 的 Dev/MCP 初始化依赖完整场景树；验收进程必须保留图形生命周期。
    $startInfo.Arguments = "--rendering-driver opengl3 --log-file `"$LogPath`""
    $startInfo.EnvironmentVariables["KITLIB_MCP_PORT"] = "$Port"
    $startInfo.EnvironmentVariables["DT_ACCEPTANCE_RUN_ID"] = $runId

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "无窗口游戏进程未能启动。"
    }
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    return $process
}

$reportWritten = $false

try {
    if (-not $SkipBuild) {
        & (Join-Path $projectRoot "build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "正式 Mod 构建部署失败。" }
        & (Join-Path $projectRoot "test-adapter\build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "测试适配器构建部署失败。" }
    }

    $bridgeExists = Test-BridgeAvailable
    if ($ReuseSession) {
        throw "根治型验收不支持 -ReuseSession：人工会话无法证明 DT_ACCEPTANCE_RUN_ID。请关闭现有游戏后使用 -AllowStartGame，由验收器启动唯一可追踪进程。"
    }
    if ($bridgeExists) {
        throw "检测到已有 KitLib MCP 会话。为避免抢占用户游戏，请关闭该会话后使用 -AllowStartGame。"
    }
    if (-not $AllowStartGame) {
        throw "根治型验收必须显式传 -AllowStartGame，以启动带 DT_ACCEPTANCE_RUN_ID 的唯一游戏进程。"
    }
    if (-not (Test-Path $gameExe)) { throw "找不到游戏程序：$gameExe" }
    $existingGames = @(Get-DtRunningGameProcesses)
    if ($existingGames.Count -gt 0) {
        $pids = $existingGames.Id -join ", "
        throw "检测到已有 SlayTheSpire2 进程 PID=$pids。为避免 KitLib MCP 端口 9877 冲突，拒绝启动第二个游戏实例；请关闭现有实例后重试。"
    }

    $startedProcess = Start-DtAcceptanceGame `
        -Executable $gameExe `
        -LogPath $runtimeLog `
        -Port $BridgePort
    $observedGameProcess = $startedProcess
    Initialize-DtAcceptance `
        -Endpoint $bridgeEndpoint `
        -ObservedGameProcess $observedGameProcess `
        -RuntimeLogPath $runtimeLog `
        -RuntimeStartedAt $runtimeStartedAt `
        -RunId $runId `
        -CaseReportPath $casePath

    Wait-DtBridge -TimeoutSeconds 120 -ObservedProcess $observedGameProcess
    $null = Wait-DevViewer
    $viewerUrl = Open-DevViewer
    $session = Assert-SessionHandshake
    Assert-DtTools -Required @(
        "get_game_state",
        "dev_get_session",
        "combat_action",
        "get_selection_state",
        "selection_action",
        "dev_add_monster",
        "dimensional_traveler_test_control",
        "dimensional_traveler_test_target",
        "dimensional_traveler_test_selection",
        "dimensional_traveler_test_session"
    )
    $runMetadata = Get-Content -Raw -Path $runPath | ConvertFrom-Json
    $runMetadata | Add-Member -NotePropertyName "status" -NotePropertyValue "handshake_verified" -Force
    $runMetadata | Add-Member -NotePropertyName "gameProcessId" -NotePropertyValue $observedGameProcess.Id -Force
    $runMetadata | Add-Member -NotePropertyName "viewerUrl" -NotePropertyValue $viewerUrl -Force
    $runMetadata | Add-Member -NotePropertyName "handshake" -NotePropertyValue $session -Force
    $runMetadata | ConvertTo-Json -Depth 30 | Set-Content -Path $runPath -Encoding UTF8
    $null = Wait-DtMainMenuReady -TimeoutSeconds 180

    foreach ($suiteName in $executionSuites) {
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
        reusedSession = $false
        suites = $Suite
        testCount = $results.Count
        passedCount = $results.Count - $failed.Count
        failedCount = $failed.Count
        runtimeLog = $runtimeLog
        viewerUrl = "http://127.0.0.1:$ViewerPort/#/logs"
        handshake = $session
        gameProcess = if ($null -eq $startedProcess) { $null } else {
            [ordered]@{
                pid = $startedProcess.Id
                exited = $startedProcess.HasExited
                exitCode = if ($startedProcess.HasExited) { $startedProcess.ExitCode } else { $null }
            }
        }
        checkedAt = [DateTimeOffset]::Now.ToString("o")
        tests = $results
    }
    Write-FinalReport -Report $report
    $reportWritten = $true

    Write-Host ("验收完成：{0}/{1} 通过" -f $report.passedCount, $report.testCount)
    Write-Host "报告：$reportPath"
    if ($failed.Count -gt 0) {
        foreach ($failure in $failed) {
            Write-Host ("  FAIL [{0}] {1}: {2}" -f $failure.suite, $failure.name, $failure.error)
        }
        exit 1
    }
}
catch {
    if (-not $reportWritten) {
        $failure = $_.Exception.Message
        $failureCategory = if ($failure -like "[[]game_process_crash[]]*") {
            "game_process_crash"
        }
        elseif ($failure -like "KitLib MCP *" -or $failure -like "*MCP RPC*") {
            "test_bridge_or_protocol"
        }
        else {
            "test_runner_or_build"
        }
        $report = [ordered]@{
            passed = $false
            configuration = $Configuration
            reusedSession = $false
            suites = $Suite
            testCount = 0
            passedCount = 0
            failedCount = 1
            runtimeLog = $runtimeLog
            viewerUrl = "http://127.0.0.1:$ViewerPort/#/logs"
            failureCategory = $failureCategory
            gameProcess = if ($null -eq $startedProcess) { $null } else {
                [ordered]@{
                    pid = $startedProcess.Id
                    exited = $startedProcess.HasExited
                    exitCode = if ($startedProcess.HasExited) { $startedProcess.ExitCode } else { $null }
                }
            }
            checkedAt = [DateTimeOffset]::Now.ToString("o")
            startupError = $failure
            tests = @()
        }
        Write-FinalReport -Report $report
        $reportWritten = $true
        Write-Host "验收未进入套件执行阶段：$failure"
        Write-Host "报告：$reportPath"
    }
    throw
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
    [Environment]::SetEnvironmentVariable("KITLIB_MCP_PORT", $previousKitLibMcpPort, "Process")
    [Environment]::SetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID", $previousAcceptanceRunId, "Process")
}
