$suite = "operations"

Invoke-DtCase -Suite $suite -Name "bq-purify-sublimate-masterpiece-chain" -Body {
    $null = Reset-DtScenario -Id "QUALITY_CHAIN_PURIFY" -Fixture @{
        energy = 10
        principles = @{ corruption = 1 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PURIFICATION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    $purify = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PURIFICATION" `
        -Target @{ target_index = -1; selection_index = 0 }
    Assert-DtTrue $purify.success "提纯未成功打出"
    $purified = Get-DtExtension
    Assert-DtEqual $true $purified.backpack.cards[0].upgraded "提纯未保留在普通品质并写入 +"
    Assert-DtEqual 0 $purified.principles.corruption.amount "提纯主原理支付错误"
    Assert-DtEqual "UpgradedExistingPotion" $purified.turn.experiments "提纯未写入品质实验"

    $null = Apply-DtFixture -Id "QUALITY_CHAIN_SUBLIMATE" -Fixture @{
        energy = 10
        principles = @{ corruption = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_SUBLIMATION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $true; origin = "Original" }
        )
    }
    $sublimate = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_SUBLIMATION" `
        -Target @{ target_index = -1; selection_index = 0 }
    Assert-DtTrue $sublimate.success "升华未成功打出"
    $refined = Get-DtExtension
    Assert-DtEqual "Refined" $refined.backpack.cards[0].quality "升华未生成精制药剂"
    Assert-DtEqual $true $refined.backpack.cards[0].upgraded "升华未保留 + 状态"
    Assert-DtEqual 0 $refined.principles.corruption.amount "升华支付错误"

    $null = Apply-DtFixture -Id "QUALITY_CHAIN_MASTERPIECE" -Fixture @{
        energy = 10
        principles = @{ corruption = 4 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_MASTERPIECE_TRANSFORMATION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Refined"; upgraded = $true; origin = "Original" }
        )
    }
    $masterpiece = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_MASTERPIECE_TRANSFORMATION" `
        -Target @{ target_index = -1; selection_index = 0 }
    Assert-DtTrue $masterpiece.success "杰作转化未成功打出"
    $final = Get-DtExtension
    Assert-DtEqual 0 $final.backpack.count "未升级药剂包不应收纳杰作"
    $masterpieceInHand = @($final.piles.hand | Where-Object {
        $_.cardId -eq "DIMENSIONAL_TRAVELER_CARD_MASTERPIECE_ATTACK_POTION" -and $_.upgradeLevel -eq 1
    })
    Assert-DtEqual 1 $masterpieceInHand.Count "杰作+ 未路由到普通手牌"
    Assert-DtEqual 0 $final.principles.corruption.amount "杰作转化支付错误"
    return @{ normalPlus = $true; refinedPlus = $true; masterpiecePlusInHand = $true }
}

Invoke-DtCase -Suite $suite -Name "g02-perpetual-energy-power" -Body {
    $before = Reset-DtScenario -Id "PERPETUAL_ENERGY" -Fixture @{
        energy = 10
        principles = @{ vitality = 2; volatility = 2; corruption = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PERPETUAL_ENERGY"; pile = "hand" }
        )
    }
    $baseMaxEnergy = $before.extensions.dimensionalTravelerTest.playerCombat.effectiveMaxEnergy
    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PERPETUAL_ENERGY" -Target @{ target_index = -1 }
    Assert-DtTrue $play.success "G02 永续能量未成功打出"
    $after = Get-DtState
    $player = @($after.extensions.dimensionalTravelerTest.combatants | Where-Object { $_.isPlayer })[0]
    $power = Get-DtPower -Combatant $player -PowerId "PerpetualEnergyPower"
    Assert-DtNotNull $power "G02 未施加永续能量能力"
    Assert-DtEqual 1 $power.amount "G02 能力层数错误"
    Assert-DtEqual ($baseMaxEnergy + 1) $after.extensions.dimensionalTravelerTest.playerCombat.effectiveMaxEnergy `
        "G02 未提高后续回合使用的有效最大能量"
    Assert-DtEqual 8 $after.combat.currentEnergy "G02 不应追溯补充当前能量"

    $previousTurn = [int]$after.extensions.dimensionalTravelerTest.playerCombat.turnNumber
    $freeze = Invoke-DtTool -Name "dev_set_cheat" -Arguments @{ cheat = "freeze_enemies"; enabled = $true }
    Assert-DtTrue $freeze.ok "无法冻结敌人以推进 G02 后续回合"
    $endTurn = Invoke-DtTool -Name "combat_action" -Arguments @{ action = "end_turn" } -TimeoutSeconds 60
    Assert-DtTrue $endTurn.success "G02 测试无法结束当前回合"
    $nextTurn = Wait-DtNextTurn -PreviousTurnNumber $previousTurn -TimeoutSeconds 45
    Assert-DtEqual ($baseMaxEnergy + 1) $nextTurn.extensions.dimensionalTravelerTest.playerCombat.effectiveMaxEnergy `
        "G02 后续回合有效最大能量错误"
    Assert-DtEqual ($baseMaxEnergy + 1) $nextTurn.combat.currentEnergy "G02 后续回合初始能量错误"
    return @{
        maxEnergyBefore = $baseMaxEnergy
        maxEnergyAfter = $nextTurn.extensions.dimensionalTravelerTest.playerCombat.effectiveMaxEnergy
    }
}