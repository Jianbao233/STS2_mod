param(
    [string]$Endpoint = "http://127.0.0.1:9877/messages"
)

$ErrorActionPreference = "Stop"
$script:requestId = 0
$script:results = @()

function Invoke-Rpc([string]$Method, [hashtable]$Params) {
    $script:requestId += 1
    $body = @{
        jsonrpc = "2.0"
        id = $script:requestId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 20 -Compress
    $response = Invoke-RestMethod -Uri $Endpoint -Method Post -ContentType "application/json" -Body $body -TimeoutSec 30
    if ($null -ne $response.error) {
        throw "MCP RPC 失败：$($response.error.message)"
    }
    return $response.result
}

function Invoke-Tool([string]$Name, [hashtable]$Arguments = @{}) {
    $result = Invoke-Rpc "tools/call" @{ name = $Name; arguments = $Arguments }
    $text = $result.content[0].text
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "工具 $Name 未返回文本结果。"
    }
    return $text | ConvertFrom-Json
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message；期望=$Expected，实际=$Actual"
    }
}

function Assert-True($Actual, [string]$Message) {
    Assert-Equal $true ([bool]$Actual) $Message
}

function Wait-PlayPhase([int]$TimeoutSeconds = 30) {
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $state = Invoke-Tool "get_game_state"
        if ($null -ne $state.combat -and $state.combat.isPlayPhaseActive) {
            return $state
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::Now -lt $deadline)
    throw "战斗未在 $TimeoutSeconds 秒内进入 PlayPhase。"
}

function Start-TestCombat([string]$Seed) {
    $started = Invoke-Tool "dimensional_traveler_test_control" @{
        action = "start_test_combat"
        seed = $Seed
    }
    Assert-True $started.ok "创建测试战斗失败"
    Assert-Equal "DIMENSIONAL_TRAVELER_CHARACTER_TRAVELER" $started.characterId "测试角色不匹配"
    return Wait-PlayPhase
}

function Set-Principles([hashtable]$Principles) {
    $result = Invoke-Tool "dimensional_traveler_test_control" @{
        action = "set_principles"
        principles = $Principles
    }
    Assert-True $result.ok "设置炼金原理失败"
}

function Clear-Backpack {
    $result = Invoke-Tool "dimensional_traveler_test_control" @{ action = "clear_backpack" }
    Assert-True $result.ok "清空药剂背包失败"
}

function Clear-PaymentAudit {
    $result = Invoke-Tool "dimensional_traveler_test_control" @{ action = "clear_payment_audit" }
    Assert-True $result.ok "清空支付审计失败"
}

function Add-TestCard([string]$CardId, [int]$UpgradeLevels = 0) {
    $result = Invoke-Tool "dev_add_card" @{
        card_id = $CardId
        target = "hand"
        duration = "temp"
        upgrade_levels = $UpgradeLevels
    }
    Assert-True $result.ok "添加测试卡 $CardId 失败"
}

function Find-HandCardIndex($State, [string]$CardId, [bool]$RequirePlayable = $true) {
    for ($index = 0; $index -lt $State.combat.hand.Count; $index++) {
        $card = $State.combat.hand[$index]
        if ($card.id -eq $CardId -and (-not $RequirePlayable -or $card.canPlay)) {
            return $index
        }
    }
    throw "手牌中未找到符合条件的卡牌：$CardId"
}

function Invoke-HandCard([string]$CardId, [hashtable]$Target = @{}, [bool]$RequirePlayable = $true) {
    $state = Invoke-Tool "get_game_state"
    $cardIndex = Find-HandCardIndex $state $CardId $RequirePlayable
    $arguments = @{
        action = "play_card"
        card_index = $cardIndex
    }
    foreach ($entry in $Target.GetEnumerator()) {
        $arguments[$entry.Key] = $entry.Value
    }
    return Invoke-Tool "combat_action" $arguments
}

function Find-Combatant($State, [int]$CombatId) {
    $combatant = @($State.extensions.dimensionalTravelerTest.combatants) |
        Where-Object { $_.combatId -eq $CombatId } |
        Select-Object -First 1
    if ($null -eq $combatant) {
        throw "快照中未找到 CombatId=$CombatId 的生物。"
    }
    return $combatant
}

function Invoke-HandCardWithAdditionalTarget(
    [string]$CardId,
    [int]$InitialTargetIndex,
    [int]$AdditionalCombatId
) {
    $state = Invoke-Tool "get_game_state"
    $cardIndex = Find-HandCardIndex $state $CardId
    $playJob = Start-Job -ScriptBlock {
        param($RequestEndpoint, $HandIndex, $TargetIndex)
        $body = @{
            jsonrpc = "2.0"
            id = 9001
            method = "tools/call"
            params = @{
                name = "combat_action"
                arguments = @{
                    action = "play_card"
                    card_index = $HandIndex
                    target_side = "enemy"
                    target_index = $TargetIndex
                }
            }
        } | ConvertTo-Json -Depth 20 -Compress
        $response = Invoke-RestMethod `
            -Uri $RequestEndpoint `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 60
        return $response.result.content[0].text
    } -ArgumentList $Endpoint, $cardIndex, $InitialTargetIndex

    try {
        $deadline = [DateTimeOffset]::Now.AddSeconds(20)
        $targetState = $null
        do {
            Start-Sleep -Milliseconds 100
            $targetState = Invoke-Tool "dimensional_traveler_test_target" @{ action = "get" }
            if ($targetState.targeting.active) {
                break
            }
            if ($playJob.State -in @("Completed", "Failed", "Stopped")) {
                break
            }
        } while ([DateTimeOffset]::Now -lt $deadline)

        Assert-True $targetState.targeting.active "局部扩散未进入第二目标选择"
        $candidate = @($targetState.targeting.candidates) |
            Where-Object { $_.combatId -eq $AdditionalCombatId } |
            Select-Object -First 1
        if ($null -eq $candidate) {
            throw "局部扩散候选中不存在 CombatId=$AdditionalCombatId。"
        }

        $selection = Invoke-Tool "dimensional_traveler_test_target" @{
            action = "select"
            combat_id = $AdditionalCombatId
        }
        Assert-True $selection.ok "局部扩散第二目标选择失败"

        $null = Wait-Job $playJob -Timeout 30
        if ($playJob.State -ne "Completed") {
            throw "局部扩散药剂结算未在 30 秒内完成，状态=$($playJob.State)。"
        }
        $play = (Receive-Job $playJob) | ConvertFrom-Json
        return [ordered]@{
            play = $play
            targeting = $targetState.targeting
            selection = $selection
        }
    }
    finally {
        if ($playJob.State -notin @("Completed", "Failed", "Stopped")) {
            Stop-Job $playJob
        }
        Remove-Job $playJob -Force
    }
}

function Add-PassResult([string]$Name, [hashtable]$Evidence) {
    $script:results += [ordered]@{
        name = $Name
        passed = $true
        evidence = $Evidence
    }
}

$requiredTools = @(
    "get_game_state",
    "combat_action",
    "dev_add_card",
    "dev_add_monster",
    "dimensional_traveler_test_control",
    "dimensional_traveler_test_target"
)
$tools = (Invoke-Rpc "tools/list" @{}).tools.name
foreach ($requiredTool in $requiredTools) {
    if ($requiredTool -notin $tools) {
        throw "缺少 MCP 工具：$requiredTool"
    }
}

# 配方支付成功：2 点腐化必须原子转化为一瓶普通攻击药剂。
$null = Start-TestCombat "DT-FORMULA-PAYMENT-SUCCESS"
Clear-Backpack
Set-Principles @{
    vitality = 0; volatility = 0; corruption = 2
    catalysis = 0; diffusion = 0; echo = 0
}
Clear-PaymentAudit
Add-TestCard "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"
$play = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" @{ target_index = -1 }
Assert-True $play.success "攻击药剂配方未能成功打出"
$state = Invoke-Tool "get_game_state"
$extension = $state.extensions.dimensionalTravelerTest
Assert-Equal 0 $extension.principles.corruption.amount "配方支付后的腐化原理错误"
Assert-Equal 1 $extension.backpack.count "配方未生成唯一药剂"
Assert-Equal "Attack" $extension.backpack.cards[0].family "配方生成了错误药剂家族"
Assert-Equal "Normal" $extension.backpack.cards[0].quality "配方生成了错误药剂品质"
$payments = @($extension.payments)
Assert-Equal 1 $payments.Count "配方支付审计记录数错误"
Assert-Equal 2 $payments[0].requestedAmount "配方支付请求量错误"
Assert-Equal 2 $payments[0].before "配方支付前数量错误"
Assert-Equal 0 $payments[0].after "配方支付后数量错误"
Assert-True $payments[0].succeeded "配方支付未记录成功"
Assert-Equal "BrewedOriginalPotion" $extension.turn.experiments "炼成实验标记错误"
Add-PassResult "formula-payment-success" @{
    payment = "2->0"
    backpackCard = $extension.backpack.cards[0].cardId
}

# 配方支付失败：资源、能量、背包和审计均不得产生提交副作用。
$state = Start-TestCombat "DT-FORMULA-PAYMENT-FAILURE"
Clear-Backpack
Set-Principles @{
    vitality = 0; volatility = 0; corruption = 1
    catalysis = 0; diffusion = 0; echo = 0
}
Clear-PaymentAudit
Add-TestCard "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"
$before = Invoke-Tool "get_game_state"
$energyBefore = $before.combat.currentEnergy
$play = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" @{ target_index = -1 } $false
Assert-Equal $false $play.success "资源不足的配方不应成功打出"
$after = Invoke-Tool "get_game_state"
$extension = $after.extensions.dimensionalTravelerTest
Assert-Equal 1 $extension.principles.corruption.amount "失败支付修改了腐化原理"
Assert-Equal 0 $extension.backpack.count "失败支付仍生成了药剂"
Assert-Equal 0 @($extension.payments).Count "失败支付进入了资源提交阶段"
Assert-Equal $energyBefore $after.combat.currentEnergy "失败支付消耗了能量"
Add-PassResult "formula-payment-failure-atomic" @{
    corruption = $extension.principles.corruption.amount
    energy = $after.combat.currentEnergy
    backpackCount = $extension.backpack.count
}

# 显式生产：3 层催化下，基础 3 点的定向生产应得到额外 2 点。
$null = Start-TestCombat "DT-DIRECTED-PRODUCTION"
Set-Principles @{
    vitality = 0; volatility = 0; corruption = 0
    catalysis = 3; diffusion = 0; echo = 0
}
Add-TestCard "DIMENSIONAL_TRAVELER_CARD_VITALITY_BURST"
$play = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_VITALITY_BURST" @{ target_index = -1 }
Assert-True $play.success "生机爆发未能成功打出"
$state = Invoke-Tool "get_game_state"
$extension = $state.extensions.dimensionalTravelerTest
Assert-Equal 5 $extension.principles.vitality.amount "催化后的生机爆发产量错误"
Assert-Equal 3 $extension.principles.catalysis.amount "生产不应消耗催化原理"
Assert-Equal 0 $extension.turn.latestProduction.energy "生产快照能量错误"
$produced = @($extension.turn.latestProduction.resources)
Assert-Equal 1 $produced.Count "生产快照资源项数量错误"
Assert-Equal "DIMENSIONAL_TRAVELER_SECONDARY_RESOURCE_VITALITY" $produced[0].resourceId "生产快照资源类型错误"
Assert-Equal 5 $produced[0].amount "生产快照最终产量错误"
Add-PassResult "directed-production-catalysis" @{
    catalysis = $extension.principles.catalysis.amount
    vitalityProduced = $produced[0].amount
}

# 原始攻击药剂：真实 Anyone 目标、9 点伤害和回响目标快照必须同时成立。
$null = Start-TestCombat "DT-ATTACK-POTION"
Clear-Backpack
$enemySetup = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "set_enemy_hp"; enemy_index = 0; hp = 30
}
Assert-True $enemySetup.ok "设置药剂目标生命失败"
$brew = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "brew_potion"
    family = "Attack"
    quality = "Normal"
    upgraded = $false
    origin = "Original"
}
Assert-True $brew.ok "创建攻击药剂失败"
$move = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "move_backpack_potion_to_hand"; backpack_index = 0
}
Assert-True $move.ok "攻击药剂未能移入手牌"
$before = Invoke-Tool "get_game_state"
$hpBefore = $before.combat.enemies[0].currentHp
$play = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" @{
    target_side = "enemy"; target_index = 0
}
Assert-True $play.success "攻击药剂未能成功打出"
$after = Invoke-Tool "get_game_state"
$extension = $after.extensions.dimensionalTravelerTest
$damage = $hpBefore - $after.combat.enemies[0].currentHp
Assert-Equal 9 $damage "普通攻击药剂伤害错误"
Assert-Equal "Attack" $extension.turn.latestOriginalPotion.family "药剂快照家族错误"
Assert-Equal "Normal" $extension.turn.latestOriginalPotion.quality "药剂快照品质错误"
Assert-Equal "Original" $extension.turn.latestOriginalPotion.origin "药剂快照来源错误"
Assert-Equal 1 @($extension.turn.latestOriginalPotion.targetCombatIds).Count "药剂目标快照数量错误"
Assert-Equal "UsedOriginalPotion" $extension.turn.experiments "使用原始药剂实验标记错误"
Assert-True $extension.turn.hasBrewedOrUsedOriginalPotion "药剂使用状态未更新"
Add-PassResult "attack-potion-target-and-snapshot" @{
    damage = $damage
    targetCombatId = $extension.turn.latestOriginalPotion.targetCombatIds[0]
}

# 回响重放：应复用原始自卫药剂的玩家目标，再次获得 10 点格挡并支付 2 点回响。
$null = Start-TestCombat "DT-ECHO-REPLAY"
Clear-Backpack
Set-Principles @{
    vitality = 0; volatility = 0; corruption = 0
    catalysis = 0; diffusion = 0; echo = 2
}
$brew = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "brew_potion"
    family = "SelfDefense"
    quality = "Normal"
    upgraded = $false
    origin = "Original"
}
Assert-True $brew.ok "创建自卫药剂失败"
$move = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "move_backpack_potion_to_hand"; backpack_index = 0
}
Assert-True $move.ok "自卫药剂未能移入手牌"
$play = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_SELF_DEFENSE_POTION" @{
    target_index = -1
}
Assert-True $play.success "自卫药剂未能成功打出"
$beforeReplay = Invoke-Tool "get_game_state"
Assert-Equal 10 $beforeReplay.combat.playerBlock "自卫药剂基础格挡错误"
Assert-Equal 0 $beforeReplay.extensions.dimensionalTravelerTest.turn.latestOriginalPotion.targetCombatIds[0] "自卫药剂快照目标错误"
Clear-PaymentAudit
Add-TestCard "DIMENSIONAL_TRAVELER_CARD_ECHO_REPLAY"
$replay = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_ECHO_REPLAY" @{ target_index = -1 }
Assert-True $replay.success "回响重放未能成功打出"
$afterReplay = Invoke-Tool "get_game_state"
$extension = $afterReplay.extensions.dimensionalTravelerTest
Assert-Equal 20 $afterReplay.combat.playerBlock "回响重放未复现 10 点格挡"
Assert-Equal 0 $extension.principles.echo.amount "回响重放支付后的回响原理错误"
$payments = @($extension.payments)
Assert-Equal 1 $payments.Count "回响重放支付审计记录数错误"
Assert-Equal 2 $payments[0].requestedAmount "回响重放支付请求量错误"
Assert-Equal 2 $payments[0].before "回响重放支付前数量错误"
Assert-Equal 0 $payments[0].after "回响重放支付后数量错误"
Assert-True $payments[0].succeeded "回响重放支付未记录成功"
Assert-Equal 0 $extension.turn.latestOriginalPotion.targetCombatIds[0] "回响重放改变了原始目标快照"
Add-PassResult "echo-replay-frozen-target" @{
    blockBeforeReplay = $beforeReplay.combat.playerBlock
    blockAfterReplay = $afterReplay.combat.playerBlock
    payment = "2->0"
    targetCombatId = $extension.turn.latestOriginalPotion.targetCombatIds[0]
}

# 局部扩散：首次药剂目标结算后，必须通过 NTargetManager 选择第二个同阵营目标。
$state = Start-TestCombat "DT-LOCAL-DIFFUSION"
if (@($state.combat.enemies).Count -lt 2) {
    $addedMonster = Invoke-Tool "dev_add_monster" @{ monster_id = "LEAF_SLIME_M" }
    Assert-True $addedMonster.ok "局部扩散测试未能添加第二个敌人"
    Start-Sleep -Milliseconds 500
}
$state = Invoke-Tool "get_game_state"
Assert-Equal 2 @($state.combat.enemies).Count "局部扩散测试需要恰好两个敌人"
Set-Principles @{
    vitality = 0; volatility = 0; corruption = 0
    catalysis = 0; diffusion = 1; echo = 0
}
Clear-PaymentAudit
Add-TestCard "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION"
$localDiffusion = Invoke-HandCard "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION" @{ target_index = -1 }
Assert-True $localDiffusion.success "局部扩散未能成功打出"
$prepared = Invoke-Tool "get_game_state"
Assert-Equal "AdditionalTarget" $prepared.extensions.dimensionalTravelerTest.turn.pendingDiffusion "局部扩散未写入待结算状态"
Assert-Equal 0 $prepared.extensions.dimensionalTravelerTest.principles.diffusion.amount "局部扩散支付后的扩散原理错误"
$payments = @($prepared.extensions.dimensionalTravelerTest.payments)
Assert-Equal 1 $payments.Count "局部扩散支付审计记录数错误"
Assert-Equal 1 $payments[0].requestedAmount "局部扩散支付请求量错误"
Assert-True $payments[0].succeeded "局部扩散支付未记录成功"

for ($enemyIndex = 0; $enemyIndex -lt 2; $enemyIndex++) {
    $setup = Invoke-Tool "dimensional_traveler_test_control" @{
        action = "set_enemy_hp"; enemy_index = $enemyIndex; hp = 30
    }
    Assert-True $setup.ok "设置局部扩散目标生命失败"
}
Clear-Backpack
$brew = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "brew_potion"
    family = "Attack"
    quality = "Normal"
    upgraded = $false
    origin = "Original"
}
Assert-True $brew.ok "局部扩散测试创建攻击药剂失败"
$move = Invoke-Tool "dimensional_traveler_test_control" @{
    action = "move_backpack_potion_to_hand"; backpack_index = 0
}
Assert-True $move.ok "局部扩散测试药剂未能移入手牌"
$beforeDiffusion = Invoke-Tool "get_game_state"
$enemyCombatants = @($beforeDiffusion.extensions.dimensionalTravelerTest.combatants) |
    Where-Object { $_.side -eq "Enemy" } |
    Sort-Object combatId
Assert-Equal 2 $enemyCombatants.Count "局部扩散稳定目标数量错误"
$initialCombatId = [int]$enemyCombatants[0].combatId
$additionalCombatId = [int]$enemyCombatants[1].combatId
$diffusionResolution = Invoke-HandCardWithAdditionalTarget `
    "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" `
    0 `
    $additionalCombatId
Assert-True $diffusionResolution.play.success "局部扩散药剂未能成功结算"
$afterDiffusion = Invoke-Tool "get_game_state"
$initialBefore = Find-Combatant $beforeDiffusion $initialCombatId
$initialAfter = Find-Combatant $afterDiffusion $initialCombatId
$additionalBefore = Find-Combatant $beforeDiffusion $additionalCombatId
$additionalAfter = Find-Combatant $afterDiffusion $additionalCombatId
$initialDamage = $initialBefore.currentHp - $initialAfter.currentHp
$additionalDamage = $additionalBefore.currentHp - $additionalAfter.currentHp
$extension = $afterDiffusion.extensions.dimensionalTravelerTest
Assert-Equal 9 $initialDamage "局部扩散首次目标伤害错误"
Assert-Equal 9 $additionalDamage "局部扩散第二目标伤害错误"
Assert-Equal "None" $extension.turn.pendingDiffusion "局部扩散结算后未清空待处理状态"
$snapshotTargets = @($extension.turn.latestOriginalPotion.targetCombatIds)
Assert-Equal 2 $snapshotTargets.Count "局部扩散冻结目标数量错误"
Assert-Equal $initialCombatId $snapshotTargets[0] "局部扩散首次冻结目标错误"
Assert-Equal $additionalCombatId $snapshotTargets[1] "局部扩散第二冻结目标错误"
Add-PassResult "local-diffusion-second-target" @{
    initialCombatId = $initialCombatId
    additionalCombatId = $additionalCombatId
    initialDamage = $initialDamage
    additionalDamage = $additionalDamage
    snapshotTargets = $snapshotTargets
}

[ordered]@{
    passed = $true
    testCount = $script:results.Count
    tests = $script:results
    checkedAt = [DateTimeOffset]::Now.ToString("o")
} | ConvertTo-Json -Depth 12