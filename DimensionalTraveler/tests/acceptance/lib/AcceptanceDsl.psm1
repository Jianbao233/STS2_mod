Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Endpoint = "http://127.0.0.1:9877/messages"
$script:RequestId = 0
$script:Results = [System.Collections.Generic.List[object]]::new()

function Initialize-DtAcceptance {
    param([Parameter(Mandatory)][string]$Endpoint)
    $script:Endpoint = $Endpoint
    $script:RequestId = 0
    $script:Results.Clear()
}

function Invoke-DtRpc {
    param(
        [Parameter(Mandatory)][string]$Method,
        [hashtable]$Params = @{},
        [int]$TimeoutSeconds = 30
    )
    $script:RequestId += 1
    $body = @{
        jsonrpc = "2.0"
        id = $script:RequestId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 40 -Compress
    $response = Invoke-RestMethod `
        -Uri $script:Endpoint `
        -Method Post `
        -ContentType "application/json" `
        -Body $body `
        -TimeoutSec $TimeoutSeconds
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
    param([int]$TimeoutSeconds = 90)
    $healthUri = $script:Endpoint -replace '/messages$', '/health'
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        try {
            $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 2
            if ($health.status -eq "ok") {
                $null = Invoke-DtRpc -Method "ping" -TimeoutSeconds 5
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
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
        $deadline = [DateTimeOffset]::Now.AddSeconds(20)
        $selectionState = $null
        do {
            Start-Sleep -Milliseconds 100
            $selectionState = Invoke-DtTool -Name "get_selection_state"
            if ($selectionState.active) { break }
            if ($job.State -in @("Completed", "Failed", "Stopped")) { break }
        } while ([DateTimeOffset]::Now -lt $deadline)
        Assert-DtTrue $selectionState.active "未进入标准卡牌选择界面。"
        Start-Sleep -Milliseconds 750

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
        $deadline = [DateTimeOffset]::Now.AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 100
            $targetState = Invoke-DtTool -Name "dimensional_traveler_test_target" -Arguments @{ action = "get" }
            if ($targetState.targeting.active) { break }
            if ($job.State -in @("Completed", "Failed", "Stopped")) { break }
        } while ([DateTimeOffset]::Now -lt $deadline)
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

function Invoke-DtCase {
    param(
        [Parameter(Mandatory)][string]$Suite,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )
    $started = [DateTimeOffset]::Now
    try {
        $evidence = & $Body
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
    $script:Results.Add([pscustomobject]$record)
    return [pscustomobject]$record
}

function Get-DtResults { return @($script:Results) }

Export-ModuleMember -Function Initialize-DtAcceptance, Invoke-DtRpc, Invoke-DtTool, Wait-DtBridge, Wait-DtMainMenuReady, `
    Assert-DtTools, Get-DtState, Get-DtExtension, Get-DtToolError, Reset-DtScenario, Apply-DtFixture, Wait-DtPlayPhase, Wait-DtNextTurn, `
    Find-DtHandCardIndex, Invoke-DtCard, Invoke-DtCardWithSelection, Invoke-DtSelection, Invoke-DtCardWithBattleTarget, `
    Get-DtCombatant, Get-DtPower, Assert-DtEqual, Assert-DtTrue, Assert-DtNotNull, Invoke-DtCase, Get-DtResults