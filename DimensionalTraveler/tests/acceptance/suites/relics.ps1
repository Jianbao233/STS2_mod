$suite = "relics"

function Grant-DtRelic {
    param([Parameter(Mandatory)][string]$RelicId)
    $result = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_relic"; relic_id = $RelicId
    }
    Assert-DtTrue $result.ok "授予遗物 $RelicId 失败：$(Get-DtToolError $result)"
}

Invoke-DtCase -Suite $suite -Name "r01-first-formula-main-principle-discount-is-combat-scoped" -Body {
    $null = Reset-DtScenario -Id "R01_FIRST_FORMULA" -Fixture @{
        energy = 10
        principles = @{ corruption = 3 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand"; count = 2 }
        )
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_FIRST_FORMULA_PRINCIPLE_DISCOUNT"

    $first = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" -Target @{ target_index = -1 }
    Assert-DtTrue $first.success "R01 首张配方未成功打出"
    $afterFirst = Get-DtExtension
    Assert-DtEqual 2 $afterFirst.principles.corruption.amount "R01 首次配方未少付 1 点主原理"
    Assert-DtEqual $true $afterFirst.firstFormulaPrincipleDiscountConsumed "R01 成功炼成后未消费战斗级机会"
    Assert-DtEqual 1 $afterFirst.backpack.count "R01 首张配方未正常炼成药剂"

    $second = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" -Target @{ target_index = -1 }
    Assert-DtTrue $second.success "R01 第二张配方未成功打出"
    $afterSecond = Get-DtExtension
    Assert-DtEqual 0 $afterSecond.principles.corruption.amount "R01 错误减免了第二张配方"
    Assert-DtEqual 2 $afterSecond.backpack.count "R01 第二张配方未正常炼成药剂"
    return @{ firstCost = 1; secondCost = 2; combatScoped = $afterSecond.firstFormulaPrincipleDiscountConsumed }
}

Invoke-DtCase -Suite $suite -Name "r06-catalysis-refund-requires-completed-card-and-is-turn-limited" -Body {
    $null = Reset-DtScenario -Id "R06_CATALYSIS_RECEIPT" -Fixture @{
        energy = 10
        principles = @{ catalysis = 3 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PRODUCTION_AMPLIFICATION"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_IMMEDIATE_CONCOCTION"; pile = "hand" }
        )
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_CATALYSIS_REFUND"

    $first = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PRODUCTION_AMPLIFICATION" -Target @{ target_index = -1 }
    Assert-DtTrue $first.success "R06 首张催化消费卡未完成"
    $afterFirst = Get-DtExtension
    Assert-DtEqual 2 $afterFirst.principles.catalysis.amount "R06 未在卡牌完成后返还 1 层催化"
    Assert-DtTrue ($afterFirst.turn.relicTriggers -match "CatalysisRefund") "R06 未记录本回合触发位"
    Assert-DtEqual $null $afterFirst.turn.pendingCatalysisPayment "R06 完成后未清除支付收据"
    Assert-DtEqual 3 $afterFirst.turn.productionBoostCatalysisSnapshot "R06 返还错误影响了 C03 的支付前快照"

    $second = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_IMMEDIATE_CONCOCTION" -Target @{ target_index = -1 }
    Assert-DtTrue $second.success "R06 第二张催化消费卡未完成"
    $afterSecond = Get-DtExtension
    Assert-DtEqual 1 $afterSecond.principles.catalysis.amount "R06 在同回合重复返还催化"
    return @{ firstRefunded = 1; secondRefunded = 0; paymentSnapshot = 3 }
}

Invoke-DtCase -Suite $suite -Name "r03-formula-brew-draws-once-and-excludes-non-formula-brews" -Body {
    $null = Reset-DtScenario -Id "R03_FORMULA_DRAW" -Fixture @{
        energy = 10
        principles = @{ corruption = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PRODUCE_VITALITY"; pile = "draw" }
        )
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_ORIGINAL_BREW_DRAW"

    $formula = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" -Target @{ target_index = -1 }
    Assert-DtTrue $formula.success "R03 配方未完成"
    $afterFormula = Get-DtExtension
    Assert-DtEqual 1 @($afterFormula.piles.hand | Where-Object {
        $_.cardId -eq "DIMENSIONAL_TRAVELER_CARD_PRODUCE_VITALITY"
    }).Count "R03 首次配方未抽 1 张牌"
    Assert-DtTrue ($afterFormula.turn.relicTriggers -match "BrewDraw") "R03 未消费本回合抽牌机会"

    $brew = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "brew_potion"; family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original"
    }
    Assert-DtTrue $brew.ok "R03 非配方原始药剂夹具创建失败"
    $afterNonFormula = Get-DtExtension
    Assert-DtEqual 1 @($afterNonFormula.piles.hand | Where-Object {
        $_.cardId -eq "DIMENSIONAL_TRAVELER_CARD_PRODUCE_VITALITY"
    }).Count "R03 错误响应了非配方炼成"
    return @{ drawCount = 1; trigger = "BrewDraw" }
}

Invoke-DtCase -Suite $suite -Name "r04-existing-quality-operation-refunds-main-principle-once" -Body {
    $null = Reset-DtScenario -Id "R04_QUALITY_REFUND" -Fixture @{
        energy = 10
        principles = @{ corruption = 1 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PURIFICATION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_QUALITY_UPGRADE_REFUND"

    $purify = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PURIFICATION" `
        -Target @{ target_index = -1 }
    Assert-DtTrue $purify.success "R04 提纯未完成"
    $after = Get-DtExtension
    Assert-DtEqual 1 $after.principles.corruption.amount "R04 未返还对应主原理"
    Assert-DtEqual $true $after.backpack.cards[0].upgraded "R04 未完成既有药剂升级"
    Assert-DtTrue ($after.turn.relicTriggers -match "QualityRefund") "R04 未记录本回合触发位"
    return @{ refunded = 1; upgraded = $true }
}

Invoke-DtCase -Suite $suite -Name "r05-and-r08-original-potion-use-respect-target-and-turn-limit" -Body {
    $null = Reset-DtScenario -Id "R05_R08_ORIGINAL_USE" -Fixture @{
        energy = 10
        backpack = @(
            @{ family = "SelfDefense"; quality = "Normal"; upgraded = $false; origin = "Original" },
            @{ family = "SelfDefense"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_ORIGINAL_POTION_REFUND"
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_ORIGINAL_POTION_ECHO"

    $moveFirst = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "move_backpack_potion_to_hand"; backpack_index = 0
    }
    Assert-DtTrue $moveFirst.ok "R05/R08 首瓶药剂移入手牌失败"
    $first = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_SELF_DEFENSE_POTION" -Target @{ target_index = -1 }
    Assert-DtTrue $first.success "R05/R08 首瓶原始药剂未完成"
    $afterFirst = Get-DtExtension
    Assert-DtEqual 1 $afterFirst.principles.vitality.amount "R05 自身目标未返还 1 主原理"
    Assert-DtEqual 1 $afterFirst.principles.echo.amount "R08 首次原始药剂未获得回响"

    $moveSecond = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "move_backpack_potion_to_hand"; backpack_index = 0
    }
    Assert-DtTrue $moveSecond.ok "R05/R08 第二瓶药剂移入手牌失败"
    $second = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_SELF_DEFENSE_POTION" -Target @{ target_index = -1 }
    Assert-DtTrue $second.success "R05/R08 第二瓶原始药剂未完成"
    $afterSecond = Get-DtExtension
    Assert-DtEqual 1 $afterSecond.principles.vitality.amount "R05 在同回合重复返还主原理"
    Assert-DtEqual 1 $afterSecond.principles.echo.amount "R08 在同回合重复获得回响"
    return @{ selfRefund = 1; echoGain = 1; limited = $true }
}

Invoke-DtCase -Suite $suite -Name "r07-diffusion-refunds-only-after-two-target-original-resolution" -Body {
    $state = Reset-DtScenario -Id "R07_DIFFUSION" -Fixture @{
        energy = 10
        principles = @{ diffusion = 1 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    if (@($state.combat.enemies).Count -lt 2) {
        $added = Invoke-DtTool -Name "dev_add_monster" -Arguments @{ monster_id = "LEAF_SLIME_M" }
        Assert-DtTrue $added.ok "R07 未能添加第二敌人"
        Start-Sleep -Milliseconds 500
        $state = Get-DtState
    }
    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_DIFFUSION_REFUND"
    $prepare = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION" -Target @{ target_index = -1 }
    Assert-DtTrue $prepare.success "R07 局部扩散未完成"
    $move = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "move_backpack_potion_to_hand"; backpack_index = 0
    }
    Assert-DtTrue $move.ok "R07 药剂移入手牌失败"
    $enemies = @((Get-DtState).extensions.dimensionalTravelerTest.combatants |
        Where-Object { $_.side -eq "Enemy" } | Sort-Object combatId)
    $play = Invoke-DtCardWithBattleTarget -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" `
        -InitialTargetIndex 0 -AdditionalCombatId ([int]$enemies[1].combatId)
    Assert-DtTrue $play.success "R07 双目标药剂未完成"
    $after = Get-DtExtension
    Assert-DtEqual 1 $after.principles.diffusion.amount "R07 未在至少两目标原始结算后返还扩散"
    Assert-DtTrue ($after.turn.relicTriggers -match "DiffusionRefund") "R07 未记录本回合触发位"
    return @{ targets = 2; refund = 1 }
}

Invoke-DtCase -Suite $suite -Name "r02-satchel-expansion-adds-to-system-card-base-capacity" -Body {
    $before = Reset-DtScenario -Id "R02_CAPACITY" -Fixture @{ energy = 10 }
    $beforeExtension = $before.extensions.dimensionalTravelerTest
    Assert-DtEqual 3 $beforeExtension.backpack.capacity "R02 基线药剂包容量错误"
    Assert-DtEqual "Refined" $beforeExtension.backpack.maximumQuality "R02 不应改变药剂包默认最高品质"

    Grant-DtRelic -RelicId "DIMENSIONAL_TRAVELER_RELIC_POTION_SATCHEL_EXPANSION"
    $after = Get-DtExtension
    Assert-DtEqual 4 $after.backpack.capacity "R02 未在系统牌基础容量上叠加 +1"
    Assert-DtEqual "Refined" $after.backpack.maximumQuality "R02 错误提高了可收纳品质"
    return @{ baseCapacity = 3; capacityWithR02 = 4; maximumQuality = "Refined" }
}

Invoke-DtCase -Suite $suite -Name "r09-full-health-entry-uses-native-slots-and-shared-potion-pool" -Body {
    $run = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_test_run"; seed = "R09-FULL-HEALTH"
    } -TimeoutSeconds 60
    Assert-DtTrue $run.ok "R09 测试跑局创建失败：$(Get-DtToolError $run)"
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_run_relic"; relic_id = "DIMENSIONAL_TRAVELER_RELIC_POTION_RESERVE"
    }
    Assert-DtTrue $grant.ok "R09 跑局期获得失败：$(Get-DtToolError $grant)"

    $enter = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "enter_test_combat" } -TimeoutSeconds 60
    Assert-DtTrue $enter.ok "R09 未能进入战斗：$(Get-DtToolError $enter)"
    $state = Wait-DtPlayPhase
    $extension = $state.extensions.dimensionalTravelerTest
    Assert-DtEqual 5 $extension.nativePotions.maxCount "R09 未通过原生路径增加 2 个药水栏位"
    Assert-DtEqual 4 $extension.backpack.capacity "R09 未在药剂包基础容量上叠加 +1"
    Assert-DtEqual "Refined" $extension.backpack.maximumQuality "R09 错误提高了可收纳品质"
    Assert-DtEqual 4 $extension.nativePotions.openSlotCount "R09 满血入场未从共享池成功获得 1 瓶原生药水"
    $obtained = @($extension.nativePotions.slots | Where-Object { $null -ne $_ })
    Assert-DtEqual 1 $obtained.Count "R09 满血入场获得的原生药水数量错误"
    Assert-DtTrue (-not [string]::IsNullOrWhiteSpace([string]$obtained[0].id)) "R09 产物缺少稳定药水 ID"
    Assert-DtTrue (-not ([string]$obtained[0].id).StartsWith("DIMENSIONAL_TRAVELER_", [StringComparison]::Ordinal)) `
        "R09 错误获得了 Mod 自定义药水"
    return @{ maxPotionCount = 5; backpackCapacity = 4; potionId = $obtained[0].id }
}

Invoke-DtCase -Suite $suite -Name "r09-non-full-entry-does-not-procure-or-advance-reward-path" -Body {
    $run = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_test_run"; seed = "R09-NON-FULL"
    } -TimeoutSeconds 60
    Assert-DtTrue $run.ok "R09 非满血测试跑局创建失败：$(Get-DtToolError $run)"
    $injury = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "set_run_player_hp"; hp = 69 }
    Assert-DtTrue $injury.ok "R09 未能在战斗前设置非满血状态：$(Get-DtToolError $injury)"
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_run_relic"; relic_id = "DIMENSIONAL_TRAVELER_RELIC_POTION_RESERVE"
    }
    Assert-DtTrue $grant.ok "R09 非满血路径获得失败：$(Get-DtToolError $grant)"

    $enter = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "enter_test_combat" } -TimeoutSeconds 60
    Assert-DtTrue $enter.ok "R09 非满血路径未能进入战斗：$(Get-DtToolError $enter)"
    $state = Wait-DtPlayPhase
    $extension = $state.extensions.dimensionalTravelerTest
    Assert-DtEqual 5 $extension.nativePotions.maxCount "R09 非满血路径未保留原生栏位 +2"
    Assert-DtEqual 5 $extension.nativePotions.openSlotCount "R09 非满血路径错误获得了药水"
    Assert-DtEqual 4 $extension.backpack.capacity "R09 非满血路径未保留背包 +1"
    return @{ maxPotionCount = 5; openSlotCount = 5; backpackCapacity = 4 }
}

Invoke-DtCase -Suite $suite -Name "r09-native-player-save-round-trip-preserves-slots-and-capacity-source" -Body {
    $run = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_test_run"; seed = "R09-SAVE-LOAD"
    } -TimeoutSeconds 60
    Assert-DtTrue $run.ok "R09 保存读取测试跑局创建失败：$(Get-DtToolError $run)"
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_run_relic"; relic_id = "DIMENSIONAL_TRAVELER_RELIC_POTION_RESERVE"
    }
    Assert-DtTrue $grant.ok "R09 保存读取路径获得失败：$(Get-DtToolError $grant)"

    $roundTrip = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "inspect_run_player_round_trip"
    }
    Assert-DtTrue $roundTrip.ok "R09 原生玩家保存读取失败：$(Get-DtToolError $roundTrip)"
    Assert-DtEqual 5 ([int]$roundTrip.maxPotionCount) "R09 原生保存读取后丢失药水栏位"
    Assert-DtEqual 5 ([int]$roundTrip.openSlotCount) "R09 原生保存读取后药水槽状态错误"
    Assert-DtEqual 4 ([int]$roundTrip.backpackCapacity) "R09 原生保存读取后未恢复药剂包容量来源"
    Assert-DtTrue (@($roundTrip.relicIds) -contains "DIMENSIONAL_TRAVELER_RELIC_POTION_RESERVE") `
        "R09 原生保存读取后丢失遗物"
    return @{ maxPotionCount = 5; backpackCapacity = 4; relicPersisted = $true }
}

Invoke-DtCase -Suite $suite -Name "r09-merchant-potion-price-uses-native-hook-chain" -Body {
    $run = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "start_test_run"; seed = "R09-SHOP-PRICE"
    } -TimeoutSeconds 60
    Assert-DtTrue $run.ok "R09 商店测试跑局创建失败：$(Get-DtToolError $run)"
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_run_relic"; relic_id = "DIMENSIONAL_TRAVELER_RELIC_POTION_RESERVE"
    }
    Assert-DtTrue $grant.ok "R09 商店路径获得失败：$(Get-DtToolError $grant)"
    $price = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "inspect_merchant_potion_price" }
    Assert-DtTrue $price.ok "R09 原生商店药水价格读取失败：$(Get-DtToolError $price)"
    Assert-DtEqual ([int]([decimal]$price.rawCost * [decimal]0.25)) ([int]$price.effectiveCost) `
        "R09 未按原生价格 Hook 链将药水价格降至 25%"
    return @{ potionId = $price.potionId; rawCost = $price.rawCost; effectiveCost = $price.effectiveCost }
}