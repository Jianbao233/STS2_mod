$suite = "coop-smoke"

Invoke-DtCase -Suite $suite -Name "pseudo-coop-two-traveler-shared-combat" -Body {
    $launch = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_pseudo_coop"
    } -TimeoutSeconds 60
    Assert-DtTrue $launch.ok "KitLib 伪联机启动失败：$(Get-DtToolError $launch)"

    $rosterDeadline = [DateTimeOffset]::Now.AddSeconds(90)
    $roster = $null
    do {
        Start-Sleep -Milliseconds 250
        $roster = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
            action = "inspect_players"
        } -TimeoutSeconds 5
        if ($roster.playerCount -eq 2 -and $roster.roomType -eq "Event") { break }
    } while ([DateTimeOffset]::Now -lt $rosterDeadline)
    Assert-DtEqual 2 $roster.playerCount "伪联机未形成双玩家名册"
    Assert-DtEqual "Event" $roster.roomType "伪联机未形成双玩家事件状态"

    $combat = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "enter_pseudo_coop_test_combat"
    } -TimeoutSeconds 120
    Assert-DtTrue $combat.ok "双玩家未进入共享战斗：$(Get-DtToolError $combat)"
    Assert-DtEqual 2 $combat.playerCount "伪联机玩家数量错误"

    $players = @($combat.players | Sort-Object { [long]$_.netId })
    Assert-DtEqual 2 $players.Count "伪联机名册数量错误"
    Assert-DtTrue $players[0].isTraveler "主机角色不是次元旅人"
    Assert-DtTrue $players[1].isTraveler "队友角色不是次元旅人"
    Assert-DtTrue $players[0].inCombat "主机未进入共享战斗"
    Assert-DtTrue $players[1].inCombat "队友未进入共享战斗"
    Assert-DtTrue $players[0].stateAttached "主机缺少炼金战斗状态"
    Assert-DtTrue $players[1].stateAttached "队友缺少炼金战斗状态"
    Assert-DtTrue ($players[0].netId -ne $players[1].netId) "双玩家 NetId 未隔离"

    $state = Get-DtState
    $travelers = @($state.extensions.dimensionalTravelerTest.travelers | Sort-Object { [long]$_.playerNetId })
    Assert-DtEqual 2 $travelers.Count "版本化快照未覆盖两位次元旅人"
    Assert-DtTrue ($travelers[0].playerNetId -ne $travelers[1].playerNetId) "双玩家快照 NetId 未隔离"

    $hostNetId = [string]$players[0].netId
    $peerNetId = [string]$players[1].netId
    $hostCombatId = [int]$players[0].combatId
    $peerCombatId = [int]$players[1].combatId

    $hostFixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_player_fixture"
        player_net_id = [long]$hostNetId
        fixture = @{
            id = "COOP_HOST_PAYMENT_BASELINE"
            energy = 7
            principles = @{ corruption = 5 }
            cards = @(
                @{ id = "DIMENSIONAL_TRAVELER_CARD_PRODUCE_VITALITY"; pile = "hand" }
            )
        }
    }
    Assert-DtTrue $hostFixture.ok "主机玩家夹具提交失败：$(Get-DtToolError $hostFixture)"
    $peerFixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_player_fixture"
        player_net_id = [long]$peerNetId
        fixture = @{
            id = "COOP_PEER_PAYMENT"
            energy = 6
            principles = @{ corruption = 2 }
            cards = @(
                @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand" }
            )
        }
    }
    Assert-DtTrue $peerFixture.ok "队友玩家夹具提交失败：$(Get-DtToolError $peerFixture)"

    $formulaPlay = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "play_player_card"
        player_net_id = [long]$peerNetId
        card_id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"
    } -TimeoutSeconds 45
    Assert-DtTrue $formulaPlay.ok "队友配方原生出牌失败：$(Get-DtToolError $formulaPlay)"

    $afterFormula = Get-DtState
    $formulaTravelers = @($afterFormula.extensions.dimensionalTravelerTest.travelers |
        Sort-Object { [long]$_.playerNetId })
    $hostAfterFormula = $formulaTravelers[0]
    $peerAfterFormula = $formulaTravelers[1]
    Assert-DtEqual 5 $hostAfterFormula.principles.corruption.amount "队友支付修改了主机原理"
    Assert-DtEqual 0 $peerAfterFormula.principles.corruption.amount "队友配方未从自身支付原理"
    Assert-DtEqual 7 $hostAfterFormula.playerCombat.currentEnergy "队友出牌扣除了主机能量"
    Assert-DtEqual 5 $peerAfterFormula.playerCombat.currentEnergy "队友配方未从自身扣除能量"
    Assert-DtEqual 0 $hostAfterFormula.backpack.count "队友炼成写入了主机背包"
    Assert-DtEqual 1 $peerAfterFormula.backpack.count "队友炼成未写入自身背包"
    $payments = @($afterFormula.extensions.dimensionalTravelerTest.payments)
    Assert-DtEqual 1 $payments.Count "双人支付审计记录数错误"
    Assert-DtEqual $peerNetId ([string]$payments[0].playerNetId) "支付记录归属错误玩家"
    Assert-DtEqual 2 $payments[0].before "队友支付前原理错误"
    Assert-DtEqual 0 $payments[0].after "队友支付后原理错误"

    $targetFixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_player_fixture"
        player_net_id = [long]$peerNetId
        fixture = @{
            id = "COOP_PEER_TARGET_HOST"
            energy = 5
            cards = @(
                @{ id = "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION"; pile = "hand" }
            )
        }
    }
    Assert-DtTrue $targetFixture.ok "队友目标夹具提交失败：$(Get-DtToolError $targetFixture)"
    $beforeTarget = Get-DtState
    $beforeTargetTravelers = @($beforeTarget.extensions.dimensionalTravelerTest.travelers |
        Sort-Object { [long]$_.playerNetId })
    $hostBlockBefore = [int](@($beforeTargetTravelers[0].combatants |
        Where-Object { [int]$_.combatId -eq $hostCombatId })[0].block)
    $peerBlockBefore = [int](@($beforeTargetTravelers[1].combatants |
        Where-Object { [int]$_.combatId -eq $peerCombatId })[0].block)

    $targetPlay = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "play_player_card"
        player_net_id = [long]$peerNetId
        card_id = "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION"
        target_combat_id = $hostCombatId
    } -TimeoutSeconds 45
    Assert-DtTrue $targetPlay.ok "队友药剂对主机目标结算失败：$(Get-DtToolError $targetPlay)"

    $afterTarget = Get-DtState
    $targetTravelers = @($afterTarget.extensions.dimensionalTravelerTest.travelers |
        Sort-Object { [long]$_.playerNetId })
    $hostViewOfHost = @($targetTravelers[0].combatants |
        Where-Object { [int]$_.combatId -eq $hostCombatId })[0]
    $peerViewOfHost = @($targetTravelers[1].combatants |
        Where-Object { [int]$_.combatId -eq $hostCombatId })[0]
    $peerViewOfPeer = @($targetTravelers[1].combatants |
        Where-Object { [int]$_.combatId -eq $peerCombatId })[0]
    Assert-DtEqual 8 ([int]$hostViewOfHost.block - $hostBlockBefore) "队友药剂未对指定主机目标提供 8 点格挡"
    Assert-DtEqual $peerBlockBefore ([int]$peerViewOfPeer.block) "队友药剂错误作用于出牌者自身"
    Assert-DtEqual ([int]$hostViewOfHost.block) ([int]$peerViewOfHost.block) "双玩家快照未观察到一致目标结果"
    Assert-DtEqual $null $targetTravelers[0].turn.latestOriginalPotion "队友药剂污染了主机炼金回合状态"
    Assert-DtEqual $hostCombatId ([int]@($targetTravelers[1].turn.latestOriginalPotion.targetCombatIds)[0]) `
        "队友药剂快照未记录指定目标"

    $selectionFixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "apply_player_fixture"
        player_net_id = [long]$peerNetId
        fixture = @{
            id = "COOP_PEER_NATIVE_SELECTION"
            energy = 5
            cards = @(
                @{ id = "DIMENSIONAL_TRAVELER_CARD_FORMULA_RETRIEVAL"; pile = "hand" },
                @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "draw" },
                @{ id = "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION_FORMULA"; pile = "draw" }
            )
        }
    }
    Assert-DtTrue $selectionFixture.ok "队友选择夹具提交失败：$(Get-DtToolError $selectionFixture)"
    $selectionPlay = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "play_player_card"
        player_net_id = [long]$peerNetId
        card_id = "DIMENSIONAL_TRAVELER_CARD_FORMULA_RETRIEVAL"
    } -TimeoutSeconds 45
    Assert-DtTrue $selectionPlay.ok "队友原生选择出牌失败：$(Get-DtToolError $selectionPlay)"

    $afterSelection = Get-DtState
    $selectionTravelers = @($afterSelection.extensions.dimensionalTravelerTest.travelers |
        Sort-Object { [long]$_.playerNetId })
    $choices = @($afterSelection.extensions.dimensionalTravelerTest.choices)
    Assert-DtEqual 1 @($choices | Where-Object { $_.operation -eq "reserve" }).Count "原生选择未唯一分配 choiceId"
    Assert-DtEqual 1 @($choices | Where-Object { $_.operation -eq "wait_remote" }).Count "主机未按远端玩家等待选择"
    Assert-DtEqual 1 @($choices | Where-Object { $_.operation -eq "receive_replay" }).Count "伪玩家未以远端身份提交选择"
    Assert-DtEqual 0 @($choices | Where-Object { $_.operation -eq "sync_local" }).Count "主机错误代替队友提交本地选择"
    Assert-DtEqual 0 @($choices | Where-Object { [string]$_.playerNetId -ne $peerNetId }).Count `
        "原生选择记录归属到了错误玩家"
    $selectedFormulaCount = @($selectionTravelers[1].piles.hand | Where-Object {
        $_.cardId -in @(
            "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA",
            "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION_FORMULA")
    }).Count
    Assert-DtEqual 1 $selectedFormulaCount "队友原生选择结果未进入自身手牌"

    return @{
        netIds = @($players | ForEach-Object { $_.netId })
        combatIds = @($players | ForEach-Object { $_.combatId })
        enemyCount = $combat.enemyCount
        paymentOwner = $payments[0].playerNetId
        sharedHostBlock = $hostViewOfHost.block
        choiceOwner = $peerNetId
    }
}