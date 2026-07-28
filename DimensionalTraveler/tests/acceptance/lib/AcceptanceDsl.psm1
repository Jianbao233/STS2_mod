Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Endpoint = "http://127.0.0.1:9877/messages"
$script:RequestId = 0
$script:Results = [System.Collections.Generic.List[object]]::new()
$script:ObservedGameProcess = $null
$script:RuntimeLogPath = $null
$script:RuntimeStartedAt = $null
$script:RunId = $null
$script:CaseReportPath = $null

function Initialize-DtAcceptance {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [System.Diagnostics.Process]$ObservedGameProcess,
        [string]$RuntimeLogPath,
        [DateTimeOffset]$RuntimeStartedAt = [DateTimeOffset]::MinValue,
        [string]$RunId,
        [string]$CaseReportPath
    )
    $script:Endpoint = $Endpoint
    $script:RequestId = 0
    $script:Results.Clear()
    $script:ObservedGameProcess = $ObservedGameProcess
    $script:RuntimeLogPath = $RuntimeLogPath
    $script:RuntimeStartedAt = $RuntimeStartedAt
    $script:RunId = $RunId
    $script:CaseReportPath = $CaseReportPath
}

function Assert-DtGameHealthy {
    if ($null -eq $script:ObservedGameProcess) {
        return
    }

    try {
        $script:ObservedGameProcess.Refresh()
        $alive = $null -ne (Get-Process -Id $script:ObservedGameProcess.Id -ErrorAction SilentlyContinue)
    }
    catch {
        $alive = $false
    }
    if ($alive -and -not $script:ObservedGameProcess.HasExited) {
        return
    }

    $exitCode = "unknown"
    try {
        $exitCode = $script:ObservedGameProcess.ExitCode
    }
    catch {
        $exitCode = "unknown"
    }
    $dump = Get-ChildItem "C:\Users\Administrator\AppData\Local\CrashDumps\SlayTheSpire2.exe*.dmp" `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -ge $script:RuntimeStartedAt.UtcDateTime } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $dumpDetail = if ($null -eq $dump) { "无新的 CrashDump" } else { $dump.FullName }
    $logDetail = if ([string]::IsNullOrWhiteSpace($script:RuntimeLogPath)) { "无本次运行日志" } else { $script:RuntimeLogPath }
    throw "[game_process_crash] SlayTheSpire2 已退出；PID=$($script:ObservedGameProcess.Id)，ExitCode=$exitCode，CrashDump=$dumpDetail，RuntimeLog=$logDetail"
}

function Invoke-DtRpc {
    param(
        [Parameter(Mandatory)][string]$Method,
        [hashtable]$Params = @{},
        [int]$TimeoutSeconds = 30
    )
    Assert-DtGameHealthy
    $script:RequestId += 1
    $body = @{
        jsonrpc = "2.0"
        id = $script:RequestId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 40 -Compress
    try {
        $response = Invoke-RestMethod `
            -Uri $script:Endpoint `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec $TimeoutSeconds
    }
    catch {
        $rpcFailure = $_.Exception.Message
        Assert-DtGameHealthy
        throw "MCP RPC 通信失败：$rpcFailure"
    }
    $errorProperty = $response.PSObject.Properties["error"]
    if ($null -ne $errorProperty -and $null -ne $errorProperty.Value) {
        throw "MCP RPC 失败：$($errorProperty.Value.message)"
    }
    $resultProperty = $response.PSObject.Properties["result"]
    if ($null -eq $resultProperty) {
        throw "MCP RPC 响应缺少 result：$($response | ConvertTo-Json -Depth 10 -Compress)"
    }
    return $resultProperty.Value
}

function Invoke-DtTool {
    param(
        [Parameter(Mandatory)][string]$Name,
        [hashtable]$Arguments = @{},
        [int]$TimeoutSeconds = 30
    )
    $result = Invoke-DtRpc -Method "tools/call" -Params @{
        name = $Name
        arguments = $Arguments
    } -TimeoutSeconds $TimeoutSeconds
    $isErrorProperty = $result.PSObject.Properties["isError"]
    if ($null -ne $isErrorProperty -and [bool]$isErrorProperty.Value) {
        throw "工具 $Name 返回错误：$($result.content[0].text)"
    }
    $text = $result.content[0].text
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "工具 $Name 未返回文本结果。"
    }
    return $text | ConvertFrom-Json
}

function Wait-DtBridge {
    param(
        [int]$TimeoutSeconds = 90,
        [System.Diagnostics.Process]$ObservedProcess
    )
    if ($null -ne $ObservedProcess) {
        $script:ObservedGameProcess = $ObservedProcess
    }

    $healthUri = $script:Endpoint -replace '/messages$', '/health'
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        Assert-DtGameHealthy
        try {
            $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 2
            if ($health.status -eq "ok") {
                $null = Invoke-DtRpc -Method "ping" -TimeoutSeconds 5
                return
            }
        }
        catch {
            # 连接失败是预期的启动态；进程退出必须优先结束等待并进入报告收口。
            Assert-DtGameHealthy
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::Now -lt $deadline)
    throw "KitLib MCP 未在 $TimeoutSeconds 秒内就绪：$healthUri"
}

function Wait-DtMainMenuReady {
    param([int]$TimeoutSeconds = 180)
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    $lastSession = $null
    do {
        try {
            $lastSession = Invoke-DtTool -Name "dev_get_session" -TimeoutSeconds 5
            $blocking = @($lastSession.blockingPrompts)
            if (-not $lastSession.runActive -and
                $lastSession.phase -eq "MainMenu" -and
                $blocking.Count -eq 0) {
                return $lastSession
            }
        }
        catch {
            Assert-DtGameHealthy
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::Now -lt $deadline)
    $description = if ($null -eq $lastSession) { "无会话状态" } else {
        $lastSession | ConvertTo-Json -Depth 10 -Compress
    }
    throw "游戏未在 $TimeoutSeconds 秒内进入可测试主菜单：$description"
}

function Assert-DtTools {
    param([string[]]$Required)
    $tools = @(Invoke-DtRpc -Method "tools/list").tools.name
    foreach ($name in $Required) {
        if ($name -notin $tools) {
            throw "缺少 MCP 工具：$name"
        }
    }
}

function Get-DtState {
    return Invoke-DtTool -Name "get_game_state"
}

function Get-DtExtension {
    $extension = (Get-DtState).extensions.dimensionalTravelerTest
    if ($null -eq $extension) {
        throw "次元旅人测试快照不存在。"
    }
    return $extension
}

function Get-DtToolError {
    param([Parameter(Mandatory)]$Result)
    $property = $Result.PSObject.Properties["error"]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        return "未返回错误详情"
    }
    return [string]$property.Value
}

function Reset-DtScenario {
    param(
        [Parameter(Mandatory)][string]$Id,
        [hashtable]$Fixture = @{},
        [string]$Seed
    )
    if ([string]::IsNullOrWhiteSpace($Seed)) {
        $Seed = "DT-$($Id.ToUpperInvariant().Replace('_', '-'))"
    }
    $start = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_test_combat"
        seed = $Seed
    } -TimeoutSeconds 60
    Assert-DtTrue $start.ok "创建场景 $Id 的测试战斗失败：$(Get-DtToolError $start)"
    $null = Wait-DtPlayPhase

    $Fixture.id = $Id
    $result = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_fixture"
        fixture = $Fixture
    } -TimeoutSeconds 45
    Assert-DtTrue $result.ok "提交场景 $Id 的夹具失败：$(Get-DtToolError $result)"
    return Get-DtState
}

function Apply-DtFixture {
    param(
        [Parameter(Mandatory)][string]$Id,
        [hashtable]$Fixture = @{}
    )
    $Fixture.id = $Id
    $result = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_fixture"
        fixture = $Fixture
    } -TimeoutSeconds 45
    Assert-DtTrue $result.ok "应用夹具 $Id 失败：$(Get-DtToolError $result)"
    return Get-DtState
}

function Wait-DtPlayPhase {
    param([int]$TimeoutSeconds = 30)
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $state = Get-DtState
        if ($null -ne $state.combat -and $state.combat.isPlayPhaseActive) {
            return $state
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTimeOffset]::Now -lt $deadline)
    throw "战斗未在 $TimeoutSeconds 秒内进入 PlayPhase。"
}

function Wait-DtNextTurn {
    param(
        [Parameter(Mandatory)][int]$PreviousTurnNumber,
        [int]$TimeoutSeconds = 45
    )
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $state = Get-DtState
        $turnNumber = $state.extensions.dimensionalTravelerTest.playerCombat.turnNumber
        if ($null -ne $state.combat -and
            $state.combat.isPlayPhaseActive -and
            $turnNumber -gt $PreviousTurnNumber) {
            return $state
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTimeOffset]::Now -lt $deadline)
    throw "战斗未在 $TimeoutSeconds 秒内进入下一玩家回合。"
}

function Wait-DtCardChoice {
    param([int]$TimeoutSeconds = 20)

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $choice = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{ action = "get" }
        Assert-DtTrue $choice.ok "原生卡牌选择状态读取失败：$(Get-DtToolError $choice)"
        if ($choice.selection.active -and $choice.selection.ready) {
            return $choice.selection
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "原生三选一卡牌界面未在 $TimeoutSeconds 秒内就绪。"
}

function Wait-DtStateMatch {
    param(
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 20,
        [string]$FailureMessage = "游戏状态未在限定时间内满足条件。"
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $state = Get-DtState
        if (& $Predicate $state) {
            return $state
        }
        Start-Sleep -Milliseconds 150
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "$FailureMessage TimeoutSeconds=$TimeoutSeconds"
}

function Find-DtHandCardIndex {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][string]$CardId,
        [bool]$RequirePlayable = $true
    )
    for ($index = 0; $index -lt $State.combat.hand.Count; $index++) {
        $card = $State.combat.hand[$index]
        if ($card.id -eq $CardId -and (-not $RequirePlayable -or $card.canPlay)) {
            return $index
        }
    }
    throw "手牌中未找到符合条件的卡牌：$CardId"
}

function Invoke-DtCard {
    param(
        [Parameter(Mandatory)][string]$CardId,
        [hashtable]$Target = @{},
        [bool]$RequirePlayable = $true,
        [int]$TimeoutSeconds = 45
    )
    $state = Get-DtState
    $arguments = @{
        action = "play_card"
        card_index = Find-DtHandCardIndex -State $state -CardId $CardId -RequirePlayable $RequirePlayable
    }
    foreach ($entry in $Target.GetEnumerator()) {
        $arguments[$entry.Key] = $entry.Value
    }
    return Invoke-DtTool -Name "combat_action" -Arguments $arguments -TimeoutSeconds $TimeoutSeconds
}

function Invoke-DtCardWithSelection {
    param(
        [Parameter(Mandatory)][string]$CardId,
        [hashtable]$Target = @{},
        [int]$SelectionIndex,
        [string]$SelectionCardId,
        [int]$TimeoutSeconds = 45
    )
    $state = Get-DtState
    $arguments = @{
        action = "play_card"
        card_index = Find-DtHandCardIndex -State $state -CardId $CardId
    }
    foreach ($entry in $Target.GetEnumerator()) {
        $arguments[$entry.Key] = $entry.Value
    }

    $endpoint = $script:Endpoint
    $job = Start-Job -ScriptBlock {
        param($RequestEndpoint, $PlayArguments, $RequestTimeout)
        $body = @{
            jsonrpc = "2.0"; id = 9101; method = "tools/call"
            params = @{ name = "combat_action"; arguments = $PlayArguments }
        } | ConvertTo-Json -Depth 30 -Compress
        $response = Invoke-RestMethod -Uri $RequestEndpoint -Method Post `
            -ContentType "application/json" -Body $body -TimeoutSec $RequestTimeout
        $response.result.content[0].text
    } -ArgumentList $endpoint, $arguments, $TimeoutSeconds

    try {
        Assert-DtGameHealthy
        $deadline = [DateTimeOffset]::Now.AddSeconds(20)
        $selectionState = $null
        do {
            Start-Sleep -Milliseconds 100
            Assert-DtGameHealthy
            $selectionState = Invoke-DtTool -Name "get_selection_state"
            if ($selectionState.active) { break }
            if ($job.State -in @("Completed", "Failed", "Stopped")) { break }
        } while ([DateTimeOffset]::Now -lt $deadline)
        Assert-DtGameHealthy
        Assert-DtTrue $selectionState.active "未进入标准卡牌选择界面。"
        Start-Sleep -Milliseconds 750
        Assert-DtGameHealthy

        $selectionArgs = @{ action = "select" }
        if ($PSBoundParameters.ContainsKey("SelectionIndex")) {
            $selectionArgs.candidate_index = $SelectionIndex
        }
        elseif (-not [string]::IsNullOrWhiteSpace($SelectionCardId)) {
            $selectionArgs.card_id = $SelectionCardId
        }
        else {
            $selectionArgs.candidate_index = 0
        }
        $customState = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{ action = "get" }
        if ($customState.selection.active) {
            $selection = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments $selectionArgs
        }
        else {
            $fallbackArgs = @{ confirm = $true }
            if ($selectionArgs.ContainsKey("candidate_index")) {
                $fallbackArgs.card_index = $selectionArgs.candidate_index
            }
            else {
                $fallbackArgs.card_id = $selectionArgs.card_id
            }
            $selection = Invoke-DtTool -Name "selection_action" -Arguments $fallbackArgs
        }
        Assert-DtTrue $selection.ok "标准卡牌选择失败：$(Get-DtToolError $selection)"

        $null = Wait-Job $job -Timeout $TimeoutSeconds
        Assert-DtGameHealthy
        if ($job.State -ne "Completed") {
            throw "包含标准选择的出牌未完成，状态=$($job.State)。"
        }
        return (Receive-Job $job) | ConvertFrom-Json
    }
    finally {
        if ($job.State -notin @("Completed", "Failed", "Stopped")) { Stop-Job $job }
        Remove-Job $job -Force
    }
}

function Invoke-DtSelection {
    param(
        [int]$CardIndex,
        [int[]]$CardIndices,
        [string]$CardId,
        [bool]$Confirm = $true
    )
    $arguments = @{ confirm = $Confirm }
    if ($PSBoundParameters.ContainsKey("CardIndex")) { $arguments.card_index = $CardIndex }
    if ($null -ne $CardIndices) { $arguments.card_indices = $CardIndices }
    if (-not [string]::IsNullOrWhiteSpace($CardId)) { $arguments.card_id = $CardId }
    return Invoke-DtTool -Name "selection_action" -Arguments $arguments
}

function Invoke-DtCardWithBattleTarget {
    param(
        [Parameter(Mandatory)][string]$CardId,
        [int]$InitialTargetIndex,
        [ValidateSet("enemy", "player")][string]$InitialTargetSide = "enemy",
        [Parameter(Mandatory)][int]$AdditionalCombatId
    )
    $state = Get-DtState
    $cardIndex = Find-DtHandCardIndex -State $state -CardId $CardId
    $endpoint = $script:Endpoint
    $job = Start-Job -ScriptBlock {
        param($RequestEndpoint, $HandIndex, $TargetIndex, $TargetSide)
        $body = @{
            jsonrpc = "2.0"; id = 9001; method = "tools/call"
            params = @{
                name = "combat_action"
                arguments = @{
                    action = "play_card"; card_index = $HandIndex
                    target_side = $TargetSide; target_index = $TargetIndex
                }
            }
        } | ConvertTo-Json -Depth 20 -Compress
        $response = Invoke-RestMethod -Uri $RequestEndpoint -Method Post `
            -ContentType "application/json" -Body $body -TimeoutSec 60
        $response.result.content[0].text
    } -ArgumentList $endpoint, $cardIndex, $InitialTargetIndex, $InitialTargetSide

    try {
        Assert-DtGameHealthy
        $deadline = [DateTimeOffset]::Now.AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 100
            Assert-DtGameHealthy
            $targetState = Invoke-DtTool -Name "dimensional_traveler_test_target" -Arguments @{ action = "get" }
            if ($targetState.targeting.active) { break }
            if ($job.State -in @("Completed", "Failed", "Stopped")) { break }
        } while ([DateTimeOffset]::Now -lt $deadline)
        Assert-DtGameHealthy
        Assert-DtTrue $targetState.targeting.active "未进入追加战场目标选择。"

        $selection = Invoke-DtTool -Name "dimensional_traveler_test_target" -Arguments @{
            action = "select"; combat_id = $AdditionalCombatId
        }
        Assert-DtTrue $selection.ok "追加战场目标选择失败：$(Get-DtToolError $selection)"
        $null = Wait-Job $job -Timeout 30
        if ($job.State -ne "Completed") {
            throw "战场目标出牌未完成，状态=$($job.State)。"
        }
        return (Receive-Job $job) | ConvertFrom-Json
    }
    finally {
        if ($job.State -notin @("Completed", "Failed", "Stopped")) { Stop-Job $job }
        Remove-Job $job -Force
    }
}

function Get-DtCombatant {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][int]$CombatId
    )
    $combatant = @($State.extensions.dimensionalTravelerTest.combatants) |
        Where-Object { $_.combatId -eq $CombatId } |
        Select-Object -First 1
    if ($null -eq $combatant) { throw "快照中未找到 CombatId=$CombatId。" }
    return $combatant
}

function Get-DtPower {
    param(
        [Parameter(Mandatory)]$Combatant,
        [Parameter(Mandatory)][string]$PowerId
    )
    return @($Combatant.powers) |
        Where-Object { $_.id -eq $PowerId -or $_.type -eq $PowerId } |
        Select-Object -First 1
}

function Assert-DtEqual {
    param($Expected, $Actual, [Parameter(Mandatory)][string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message；期望=$Expected，实际=$Actual"
    }
}

function Assert-DtTrue {
    param($Actual, [Parameter(Mandatory)][string]$Message)
    if (-not [bool]$Actual) { throw $Message }
}

function Assert-DtNotNull {
    param($Actual, [Parameter(Mandatory)][string]$Message)
    if ($null -eq $Actual) { throw $Message }
}

function Write-DtCaseRecord {
    param([Parameter(Mandatory)]$Record)
    if ([string]::IsNullOrWhiteSpace($script:CaseReportPath)) {
        return
    }

    $payload = [ordered]@{
        runId = $script:RunId
        recordedAt = [DateTimeOffset]::Now.ToString("o")
        case = $Record
    } | ConvertTo-Json -Depth 50 -Compress
    [System.IO.File]::AppendAllText(
        $script:CaseReportPath,
        $payload + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-DtCase {
    param(
        [Parameter(Mandatory)][string]$Suite,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    $started = [DateTimeOffset]::Now
    try {
        Assert-DtGameHealthy
        $evidence = & $Body
        Assert-DtGameHealthy
        $record = [ordered]@{
            suite = $Suite; name = $Name; passed = $true
            durationMs = [int]([DateTimeOffset]::Now - $started).TotalMilliseconds
            evidence = $evidence
        }
    }
    catch {
        $snapshot = $null
        try { $snapshot = Get-DtState } catch { }
        $record = [ordered]@{
            suite = $Suite; name = $Name; passed = $false
            durationMs = [int]([DateTimeOffset]::Now - $started).TotalMilliseconds
            error = $_.Exception.Message
            snapshot = $snapshot
        }
    }
    $caseRecord = [pscustomobject]$record
    Write-DtCaseRecord -Record $caseRecord
    $script:Results.Add($caseRecord)
    return $caseRecord
}

function Get-DtResults { return @($script:Results) }

Export-ModuleMember -Function Initialize-DtAcceptance, Invoke-DtRpc, Invoke-DtTool, Wait-DtBridge, Wait-DtMainMenuReady, `
    Assert-DtTools, Get-DtState, Get-DtExtension, Get-DtToolError, Reset-DtScenario, Apply-DtFixture, Wait-DtPlayPhase, Wait-DtNextTurn, Wait-DtCardChoice, Wait-DtStateMatch, `
    Find-DtHandCardIndex, Invoke-DtCard, Invoke-DtCardWithSelection, Invoke-DtSelection, Invoke-DtCardWithBattleTarget, `
    Get-DtCombatant, Get-DtPower, Assert-DtEqual, Assert-DtTrue, Assert-DtNotNull, Invoke-DtCase, Get-DtResults