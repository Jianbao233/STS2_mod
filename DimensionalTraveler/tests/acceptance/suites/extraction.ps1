$suite = "extraction"

Invoke-DtCase -Suite $suite -Name "block-potion-extraction-commits-native-removal-and-fixed-rewards" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_BLOCK" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "BLOCK_POTION"
    }
    Assert-DtTrue $grant.ok "格挡药水授予失败：$(Get-DtToolError $grant)"

    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "格挡药水萃取失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "completed" $extract.status "格挡药水萃取未完成"

    $after = Get-DtExtension
    Assert-DtEqual 0 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "萃取后原生药水未移除"
    Assert-DtEqual 1 $after.principles.diffusion.amount "格挡药水未获得 1 点扩散"
    Assert-DtEqual 2 $after.principles.vitality.amount "格挡药水未获得 2 点生机"
    Assert-DtEqual 1 $after.backpack.count "格挡药水未生成 1 瓶药剂"
    Assert-DtEqual "Shield" $after.backpack.cards[0].family "格挡药水生成了错误药剂家族"
    Assert-DtEqual "Normal" $after.backpack.cards[0].quality "格挡药水生成了错误药剂品质"
    Assert-DtEqual "Extracted" $after.backpack.cards[0].origin "萃取药剂未标记为独立来源"
    Assert-DtEqual $false $after.turn.hasBrewedOrUsedOriginalPotion "萃取错误记为炼成或使用药剂"
    return @{ special = "Diffusion"; basic = "Vitality:2"; origin = "Extracted" }
}

Invoke-DtCase -Suite $suite -Name "fixed-extraction-uses-native-hand-overflow-after-backpack-is-full" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_OVERFLOW" -Fixture @{
        energy = 10
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand"; count = 9 }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; origin = "Original" },
            @{ family = "Shield"; quality = "Normal"; origin = "Original" },
            @{ family = "Weakness"; quality = "Normal"; origin = "Original" }
        )
    }
    $before = Get-DtExtension
    Assert-DtEqual 3 $before.backpack.count "溢出夹具未填满药剂背包"
    Assert-DtEqual 10 @($before.piles.hand).Count "溢出夹具未填满普通手牌"

    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "BLOCK_POTION"
    }
    Assert-DtTrue $grant.ok "溢出测试原生药水授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "满背包/满手牌时萃取失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "completed" $extract.status "满背包/满手牌时萃取未完成"

    $after = Get-DtExtension
    Assert-DtEqual 0 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "溢出萃取后原生药水未移除"
    Assert-DtEqual 3 $after.backpack.count "萃取产物不应挤出既有背包药剂"
    Assert-DtEqual 10 @($after.piles.hand).Count "萃取产物不应突破普通手牌上限"
    $discardedProduct = @($after.piles.discard | Where-Object {
        $_.cardId -eq "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION" -and $_.origin -eq "Extracted"
    })
    Assert-DtEqual 1 $discardedProduct.Count "满手牌时萃取产物未按原版规则进入弃牌堆"
    Assert-DtEqual 1 $after.principles.diffusion.amount "溢出萃取未获得扩散"
    Assert-DtEqual 2 $after.principles.vitality.amount "溢出萃取未获得生机"
    return @{ backpack = 3; hand = 10; discardedExtractedPotion = "Shield" }
}

Invoke-DtCase -Suite $suite -Name "attack-potion-extraction-keeps-source-until-native-choice-commits" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_ATTACK" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "攻击药水授予失败：$(Get-DtToolError $grant)"

    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "攻击药水萃取入队失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "awaiting_choice" $extract.status "攻击药水未进入同步三选一阶段"

    $beforeChoice = Get-DtExtension
    Assert-DtEqual 1 @($beforeChoice.nativePotions.slots | Where-Object { $null -ne $_ }).Count `
        "攻击药水在选择前被提前移除"
    $selection = Wait-DtCardChoice
    Assert-DtEqual 3 @($selection.candidates).Count "攻击药水候选数量错误"
    $candidateIds = @($selection.candidates.cardId)
    Assert-DtEqual 3 @($candidateIds | Sort-Object -Unique).Count "攻击药水候选出现重复"
    $allowed = @(
        "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION",
        "DIMENSIONAL_TRAVELER_CARD_CORRUPTION_POTION",
        "DIMENSIONAL_TRAVELER_CARD_WEAKNESS_POTION",
        "DIMENSIONAL_TRAVELER_CARD_STRENGTH_REDUCTION_POTION"
    )
    Assert-DtEqual 0 @($candidateIds | Where-Object { $_ -notin $allowed }).Count "攻击药水候选不属于冻结四家族"

    $chosen = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{
        action = "select"; candidate_index = 0
    }
    Assert-DtTrue $chosen.ok "攻击药水候选提交失败：$(Get-DtToolError $chosen)"
    $afterState = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "攻击药水选择提交后未完成萃取" -Predicate {
        param($state)
        $extension = $state.extensions.dimensionalTravelerTest
        @($extension.nativePotions.slots | Where-Object { $null -ne $_ }).Count -eq 0 -and
            $extension.backpack.count -eq 1
    }
    $after = $afterState.extensions.dimensionalTravelerTest
    Assert-DtEqual 0 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "攻击药水选择后未移除"
    Assert-DtEqual 1 $after.principles.diffusion.amount "攻击药水未获得 1 点扩散"
    Assert-DtEqual 2 $after.principles.volatility.amount "攻击药水未获得 2 点挥发"
    Assert-DtEqual 1 $after.backpack.count "攻击药水选择后未生成 1 瓶药剂"
    Assert-DtTrue ($after.backpack.cards[0].cardId -in $candidateIds) "攻击药水产物不属于本次候选"
    Assert-DtEqual "Extracted" $after.backpack.cards[0].origin "攻击药水产物来源错误"
    return @{ candidates = $candidateIds; chosen = $after.backpack.cards[0].cardId; origin = "Extracted" }
}

Invoke-DtCase -Suite $suite -Name "attack-potion-slot-race-rejects-without-rewards-or-rng-advance" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_ATTACK_SLOT_RACE" -Fixture @{ energy = 10 }
    $null = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "clear_extraction_audit" }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "竞态测试原生药水授予失败：$(Get-DtToolError $grant)"

    $rngBefore = (Get-DtExtension).rng | ConvertTo-Json -Compress
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $extract.ok "竞态测试萃取入队失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "awaiting_choice" $extract.status "竞态测试未进入选择阶段"
    $null = Wait-DtCardChoice

    $rngWhilePending = (Get-DtExtension).rng | ConvertTo-Json -Compress
    Assert-DtEqual $rngBefore $rngWhilePending "攻击药水候选预览不应推进战斗 RNG"

    $discard = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "discard_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $discard.ok "竞态测试未能外部移除源药水：$(Get-DtToolError $discard)"

    $selected = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{
        action = "select"; candidate_index = 0
    }
    Assert-DtTrue $selected.ok "竞态测试原生选择提交失败：$(Get-DtToolError $selected)"

    $afterState = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "竞态测试动作未完成拒绝" -Predicate {
        param($state)
        @($state.extensions.dimensionalTravelerTest.extractions | Where-Object {
            $_.stage -eq "Rejected" -and $_.detail -eq "commit_source_changed"
        }).Count -eq 1
    }
    $after = $afterState.extensions.dimensionalTravelerTest
    Assert-DtEqual 0 $after.backpack.count "槽位竞态拒绝后不应生成药剂"
    Assert-DtEqual 0 $after.principles.diffusion.amount "槽位竞态拒绝后不应获得特殊原理"
    Assert-DtEqual 0 $after.principles.volatility.amount "槽位竞态拒绝后不应获得基础原理"
    Assert-DtEqual $rngBefore ($after.rng | ConvertTo-Json -Compress) "槽位竞态拒绝不应推进战斗 RNG"
    return @{ rejection = "commit_source_changed"; rngUnchanged = $true }
}

Invoke-DtCase -Suite $suite -Name "attack-potion-cancel-keeps-source-rewards-and-rng-unchanged" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_ATTACK_CANCEL" -Fixture @{ energy = 10 }
    $null = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "clear_extraction_audit" }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "取消测试原生药水授予失败：$(Get-DtToolError $grant)"

    $rngBefore = (Get-DtExtension).rng | ConvertTo-Json -Compress
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $extract.ok "取消测试萃取入队失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "awaiting_choice" $extract.status "取消测试未进入选择阶段"
    $null = Wait-DtCardChoice

    $cancel = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{ action = "cancel" }
    Assert-DtTrue $cancel.ok "原生选择取消失败：$(Get-DtToolError $cancel)"
    Assert-DtEqual "native_skip" $cancel.completionPath "取消未经过原生选择跳过路径"

    $afterState = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "取消后萃取动作未记录取消" -Predicate {
        param($state)
        @($state.extensions.dimensionalTravelerTest.extractions | Where-Object {
            $_.stage -eq "Cancelled" -and $_.detail -eq "choice_not_committed"
        }).Count -eq 1
    }
    $after = $afterState.extensions.dimensionalTravelerTest
    Assert-DtEqual 1 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "取消后源药水不应移除"
    Assert-DtEqual "ATTACK_POTION" $after.nativePotions.slots[[int]$grant.slotIndex].id "取消后源槽位药水错误"
    Assert-DtEqual 0 $after.backpack.count "取消后不应生成药剂"
    Assert-DtEqual 0 $after.principles.diffusion.amount "取消后不应获得特殊原理"
    Assert-DtEqual 0 $after.principles.volatility.amount "取消后不应获得基础原理"
    Assert-DtEqual $rngBefore ($after.rng | ConvertTo-Json -Compress) "取消不应推进战斗 RNG"
    return @{ cancellation = "choice_not_committed"; sourceKept = $true; rngUnchanged = $true }
}

Invoke-DtCase -Suite $suite -Name "attack-potion-same-id-slot-replacement-is-not-the-frozen-source" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_ATTACK_SAME_ID_RACE" -Fixture @{ energy = 10 }
    $null = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "clear_extraction_audit" }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "同 ID 竞态测试原生药水授予失败：$(Get-DtToolError $grant)"

    $rngBefore = (Get-DtExtension).rng | ConvertTo-Json -Compress
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $extract.ok "同 ID 竞态测试萃取入队失败：$(Get-DtToolError $extract)"
    $null = Wait-DtCardChoice

    $discard = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "discard_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $discard.ok "同 ID 竞态未能移除冻结源药水：$(Get-DtToolError $discard)"
    $replacement = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $replacement.ok "同 ID 竞态替换药水授予失败：$(Get-DtToolError $replacement)"
    Assert-DtEqual ([int]$grant.slotIndex) ([int]$replacement.slotIndex) "同 ID 药水未替换到原槽位"

    $selected = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{
        action = "select"; candidate_index = 0
    }
    Assert-DtTrue $selected.ok "同 ID 竞态原生选择提交失败：$(Get-DtToolError $selected)"

    $afterState = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "同 ID 竞态动作未拒绝替换实例" -Predicate {
        param($state)
        @($state.extensions.dimensionalTravelerTest.extractions | Where-Object {
            $_.stage -eq "Rejected" -and $_.detail -eq "commit_source_changed"
        }).Count -eq 1
    }
    $after = $afterState.extensions.dimensionalTravelerTest
    Assert-DtEqual 1 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "同 ID 替换实例不应被提交移除"
    Assert-DtEqual "ATTACK_POTION" $after.nativePotions.slots[[int]$replacement.slotIndex].id "同 ID 替换实例状态错误"
    Assert-DtEqual 0 $after.backpack.count "同 ID 竞态拒绝后不应生成药剂"
    Assert-DtEqual 0 $after.principles.diffusion.amount "同 ID 竞态拒绝后不应获得特殊原理"
    Assert-DtEqual 0 $after.principles.volatility.amount "同 ID 竞态拒绝后不应获得基础原理"
    Assert-DtEqual $rngBefore ($after.rng | ConvertTo-Json -Compress) "同 ID 竞态拒绝不应推进战斗 RNG"
    return @{ rejection = "commit_source_changed"; replacementKept = $true; rngUnchanged = $true }
}

Invoke-DtCase -Suite $suite -Name "attack-potion-choice-from-ended-turn-cannot-commit" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_ATTACK_END_TURN" -Fixture @{ energy = 10 }
    $null = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "clear_extraction_audit" }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "结束回合测试原生药水授予失败：$(Get-DtToolError $grant)"

    $before = Get-DtExtension
    $turnBefore = [int]$before.playerCombat.turnNumber
    $rngBefore = $before.rng | ConvertTo-Json -Compress
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtTrue $extract.ok "结束回合测试萃取入队失败：$(Get-DtToolError $extract)"
    $null = Wait-DtCardChoice

    $endTurn = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "force_end_player_turn"
    }
    Assert-DtTrue $endTurn.ok "选择待定期间结束回合失败：$(Get-DtToolError $endTurn)"
    Assert-DtEqual $true $endTurn.readyToEndTurn "原版回合状态未标记为准备结束"

    $selected = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{
        action = "select"; candidate_index = 0
    }
    Assert-DtTrue $selected.ok "结束回合测试原生选择提交失败：$(Get-DtToolError $selected)"

    $afterState = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "结束回合后待定萃取未被拒绝" -Predicate {
        param($state)
        @($state.extensions.dimensionalTravelerTest.extractions | Where-Object {
            $_.stage -eq "Rejected" -and $_.detail -eq "turn_no_longer_valid"
        }).Count -eq 1
    }
    $after = $afterState.extensions.dimensionalTravelerTest
    Assert-DtEqual 1 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "结束回合拒绝后源药水不应移除"
    Assert-DtEqual 0 $after.backpack.count "结束回合拒绝后不应生成药剂"
    Assert-DtEqual 0 $after.principles.diffusion.amount "结束回合拒绝后不应获得特殊原理"
    Assert-DtEqual 0 $after.principles.volatility.amount "结束回合拒绝后不应获得基础原理"
    Assert-DtEqual $rngBefore ($after.rng | ConvertTo-Json -Compress) "结束回合拒绝不应推进战斗 RNG"
    return @{ rejection = "turn_no_longer_valid"; originalTurn = $turnBefore; rngUnchanged = $true }
}

Invoke-DtCase -Suite $suite -Name "extraction-catalog-covers-shared-pool-and-declares-special-recipes" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_CATALOG" -Fixture @{ energy = 10 }
    $catalog = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "inspect_extraction_catalog"
    }
    Assert-DtTrue $catalog.ok "萃取目录读取失败：$(Get-DtToolError $catalog)"
    Assert-DtEqual $true $catalog.validation.valid "共享药水池与萃取目录不一致"
    Assert-DtEqual 0 @($catalog.validation.missingPlans).Count "共享药水池存在缺失配方"
    Assert-DtEqual 0 @($catalog.validation.staleSharedPlans).Count "萃取目录存在过期共享配方"
    Assert-DtEqual 0 @($catalog.validation.invalidPlans).Count "萃取目录存在结构无效配方"

    $plans = @($catalog.plans)
    Assert-DtEqual $plans.Count @($plans.potionId | Sort-Object -Unique).Count "萃取目录存在重复药水 ID"
    $specialIds = @($plans | Where-Object { $_.scope -eq "ExplicitSpecial" } | ForEach-Object { $_.potionId })
    Assert-DtEqual 3 $specialIds.Count "显式特殊药水配方数量错误"
    foreach ($expectedId in @("GLOWWATER_POTION", "FOUL_POTION", "POTION_SHAPED_ROCK")) {
        Assert-DtTrue ($expectedId -in $specialIds) "缺少特殊药水配方：$expectedId"
    }
    return @{ planCount = $plans.Count; specialPotionIds = $specialIds }
}

Invoke-DtCase -Suite $suite -Name "unregistered-character-potion-is-rejected-without-state-change" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_NO_RECIPE" -Fixture @{ energy = 10 }
    $null = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "clear_extraction_audit" }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "BLOOD_POTION"
    }
    Assert-DtTrue $grant.ok "无配方测试药水授予失败：$(Get-DtToolError $grant)"

    $before = Get-DtExtension
    $rngBefore = $before.rng | ConvertTo-Json -Compress
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    }
    Assert-DtEqual $false $extract.ok "未注册角色药水不应允许萃取"
    Assert-DtEqual "recipe_unregistered" $extract.code "未注册药水拒绝码错误"

    $after = Get-DtExtension
    Assert-DtEqual "BLOOD_POTION" $after.nativePotions.slots[[int]$grant.slotIndex].id "无配方拒绝后源药水不应移除"
    Assert-DtEqual 0 $after.backpack.count "无配方拒绝后不应生成药剂"
    Assert-DtEqual 0 @($after.extractions).Count "入队前无配方拒绝不应产生受管动作审计"
    Assert-DtEqual $rngBefore ($after.rng | ConvertTo-Json -Compress) "无配方拒绝不应推进战斗 RNG"
    return @{ potionId = "BLOOD_POTION"; rejection = "recipe_unregistered"; rngUnchanged = $true }
}

Invoke-DtCase -Suite $suite -Name "fysh-oil-extraction-commits-two-independent-products" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_DOUBLE_PRODUCT" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "FYSH_OIL"
    }
    Assert-DtTrue $grant.ok "双产物药水授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "双产物药水萃取失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "completed" $extract.status "双产物药水萃取未完成"

    $after = Get-DtExtension
    Assert-DtEqual 0 @($after.nativePotions.slots | Where-Object { $null -ne $_ }).Count "双产物萃取后源药水未移除"
    Assert-DtEqual 1 $after.principles.catalysis.amount "双产物萃取未获得 1 点催化"
    Assert-DtEqual 3 $after.principles.vitality.amount "双产物萃取未获得 3 点生机"
    Assert-DtEqual 2 $after.backpack.count "双产物萃取未生成两瓶药剂"
    $families = @($after.backpack.cards.family | Sort-Object)
    Assert-DtEqual "TemporaryDexterity" $families[0] "双产物第一家族错误"
    Assert-DtEqual "TemporaryStrength" $families[1] "双产物第二家族错误"
    Assert-DtEqual 0 @($after.backpack.cards | Where-Object { $_.quality -ne "Normal" -or $_.origin -ne "Extracted" }).Count `
        "双产物品质或来源错误"
    Assert-DtEqual $false $after.turn.hasBrewedOrUsedOriginalPotion "萃取双产物不应污染炼成/原始药剂状态"
    return @{ products = $families; origin = "Extracted"; brewedStateIsolated = $true }
}

Invoke-DtCase -Suite $suite -Name "glowwater-special-extraction-produces-upgraded-refined-volatile-draw" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_SPECIAL_GLOWWATER" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "GLOWWATER_POTION"
    }
    Assert-DtTrue $grant.ok "Glowwater Potion 授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "Glowwater Potion 萃取失败：$(Get-DtToolError $extract)"

    $after = Get-DtExtension
    Assert-DtEqual 1 $after.principles.catalysis.amount "Glowwater 未获得 1 点催化"
    Assert-DtEqual 5 $after.principles.corruption.amount "Glowwater 未获得 5 点腐化"
    Assert-DtEqual 1 $after.backpack.count "Glowwater 未生成唯一产物"
    Assert-DtEqual "VolatileDraw" $after.backpack.cards[0].family "Glowwater 产物家族错误"
    Assert-DtEqual "Refined" $after.backpack.cards[0].quality "Glowwater 产物品质错误"
    Assert-DtEqual $true $after.backpack.cards[0].upgraded "Glowwater 产物未升级"
    Assert-DtEqual "Extracted" $after.backpack.cards[0].origin "Glowwater 产物来源错误"
    return @{ potionId = "GLOWWATER_POTION"; product = "VolatileDraw:Refined+" }
}

Invoke-DtCase -Suite $suite -Name "foul-special-extraction-grants-gold-max-hp-and-no-product" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_SPECIAL_FOUL" -Fixture @{ energy = 10 }
    $before = Get-DtExtension
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "FOUL_POTION"
    }
    Assert-DtTrue $grant.ok "Foul Potion 授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "Foul Potion 萃取失败：$(Get-DtToolError $extract)"

    $after = Get-DtExtension
    Assert-DtEqual 1 $after.principles.diffusion.amount "Foul Potion 未获得 1 点扩散"
    Assert-DtEqual 3 $after.principles.corruption.amount "Foul Potion 未获得 3 点腐化"
    Assert-DtEqual 200 ([int]$after.player.gold - [int]$before.player.gold) "Foul Potion 金币收益错误"
    Assert-DtEqual 3 ([int]$after.player.maxHp - [int]$before.player.maxHp) "Foul Potion 最大生命收益错误"
    Assert-DtEqual 0 $after.backpack.count "Foul Potion 不应生成药剂产物"
    return @{ potionId = "FOUL_POTION"; gold = 200; maxHp = 3; products = 0 }
}

Invoke-DtCase -Suite $suite -Name "potion-shaped-rock-special-extraction-produces-normal-attack" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_SPECIAL_ROCK" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "POTION_SHAPED_ROCK"
    }
    Assert-DtTrue $grant.ok "Potion Shaped Rock 授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "Potion Shaped Rock 萃取失败：$(Get-DtToolError $extract)"

    $after = Get-DtExtension
    Assert-DtEqual 1 $after.principles.catalysis.amount "Potion Shaped Rock 未获得 1 点催化"
    Assert-DtEqual 1 $after.principles.corruption.amount "Potion Shaped Rock 未获得 1 点腐化"
    Assert-DtEqual 1 $after.backpack.count "Potion Shaped Rock 未生成唯一产物"
    Assert-DtEqual "Attack" $after.backpack.cards[0].family "Potion Shaped Rock 产物家族错误"
    Assert-DtEqual "Normal" $after.backpack.cards[0].quality "Potion Shaped Rock 产物品质错误"
    Assert-DtEqual $false $after.backpack.cards[0].upgraded "Potion Shaped Rock 产物不应升级"
    Assert-DtEqual "Extracted" $after.backpack.cards[0].origin "Potion Shaped Rock 产物来源错误"
    return @{ potionId = "POTION_SHAPED_ROCK"; product = "Attack:Normal" }
}

Invoke-DtCase -Suite $suite -Name "extracted-product-enters-normal-use-pipeline-with-origin-preserved" -Body {
    $null = Reset-DtScenario -Id "EXTRACTION_PRODUCT_USE" -Fixture @{ energy = 10 }
    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"; potion_id = "BLOCK_POTION"
    }
    Assert-DtTrue $grant.ok "后续机制测试原生药水授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "后续机制测试萃取失败：$(Get-DtToolError $extract)"

    $extracted = Get-DtExtension
    Assert-DtEqual $false $extracted.turn.hasBrewedOrUsedOriginalPotion "仅萃取不应写入炼成或使用记录"
    Assert-DtEqual "Extracted" $extracted.backpack.cards[0].origin "后续机制测试产物来源错误"
    $move = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "move_backpack_potion_to_hand"; backpack_index = 0
    }
    Assert-DtTrue $move.ok "萃取产物未能移入手牌：$(Get-DtToolError $move)"

    $beforeUse = Get-DtState
    $blockBefore = [int]$beforeUse.combat.playerBlock
    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION" -Target @{ target_index = -1 }
    Assert-DtTrue $play.success "萃取产物未能走标准出牌流程"

    $afterUse = Get-DtState
    $after = $afterUse.extensions.dimensionalTravelerTest
    Assert-DtEqual 8 ([int]$afterUse.combat.playerBlock - $blockBefore) "萃取盾药剂后续结算数值错误"
    Assert-DtEqual 0 $after.backpack.count "已使用的萃取产物不应残留在背包"
    Assert-DtEqual "UsedOriginalPotion" $after.turn.experiments "萃取产物使用未进入非回响药剂机制"
    Assert-DtEqual $true $after.turn.hasBrewedOrUsedOriginalPotion "萃取产物实际使用后应写入使用记录"
    Assert-DtEqual "Extracted" $after.turn.latestOriginalPotion.origin "后续机制快照未保留萃取来源"
    Assert-DtEqual "Shield" $after.turn.latestOriginalPotion.family "后续机制快照家族错误"
    return @{ origin = "Extracted"; block = 8; downstreamPipeline = "UsedOriginalPotion" }
}
