$suite = "infrastructure"

Invoke-DtCase -Suite $suite -Name "snapshot-schema-and-registration" -Body {
    $state = Reset-DtScenario -Id "INFRASTRUCTURE_SCHEMA" -Fixture @{
        energy = 7
        principles = @{
            vitality = 1; volatility = 2; corruption = 3
            catalysis = 1; diffusion = 2; echo = 3
        }
        enemies = @(@{ index = 0; hp = 40; block = 5 })
    }
    $extension = $state.extensions.dimensionalTravelerTest
    Assert-DtEqual 2 $extension.schemaVersion "快照 schemaVersion 不匹配"
    Assert-DtTrue $extension.combatStateAttached "AlchemyCombatStatePower 未附加"
    Assert-DtTrue $extension.backpack.attached "药剂背包未附加"
    Assert-DtEqual 7 $state.combat.currentEnergy "夹具能量不匹配"
    Assert-DtEqual 3 $extension.principles.corruption.amount "夹具原理不匹配"
    Assert-DtEqual 5 $state.combat.enemies[0].block "夹具敌人格挡不匹配"
    return @{ schemaVersion = 2; playerNetId = $extension.playerNetId }
}

Invoke-DtCase -Suite $suite -Name "fixture-clears-transient-state" -Body {
    $first = Apply-DtFixture -Id "INFRASTRUCTURE_DIRTY" -Fixture @{
        energy = 9
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_VITALITY_BURST"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "draw" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
        principles = @{ vitality = 9; volatility = 8; corruption = 7 }
        turn = @{ pendingDiffusion = "AdditionalTarget"; prePurificationCharges = 2 }
    }
    Assert-DtEqual 1 @($first.extensions.dimensionalTravelerTest.piles.draw).Count "夹具未加入抽牌堆卡牌"
    Assert-DtEqual 1 $first.extensions.dimensionalTravelerTest.backpack.count "夹具未加入背包药剂"

    $clean = Apply-DtFixture -Id "INFRASTRUCTURE_CLEAN" -Fixture @{ energy = 3 }
    $extension = $clean.extensions.dimensionalTravelerTest
    Assert-DtEqual 0 @($extension.piles.draw).Count "夹具未清空抽牌堆"
    Assert-DtEqual 0 @($extension.piles.discard).Count "夹具未清空弃牌堆"
    Assert-DtEqual 0 @($extension.piles.exhaust).Count "夹具未清空消耗堆"
    Assert-DtEqual 0 $extension.backpack.count "夹具未清空药剂背包"
    Assert-DtEqual 0 $extension.principles.vitality.amount "夹具未清空原理"
    Assert-DtEqual "None" $extension.turn.pendingDiffusion "夹具未重置扩散状态"
    Assert-DtEqual 0 $extension.turn.prePurificationCharges "夹具未重置预提纯"
    return @{ handCount = @($extension.piles.hand).Count; backpackCount = 0 }
}

Invoke-DtCase -Suite $suite -Name "runtime-card-catalog" -Body {
    $catalog = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "inspect_catalog" }
    Assert-DtTrue $catalog.ok "运行时卡牌目录查询失败：$(Get-DtToolError $catalog)"
    Assert-DtEqual 95 $catalog.counts.all "次元旅人运行时可解析卡牌模型总数错误"
    Assert-DtEqual 87 $catalog.counts.formal "次元旅人正式卡牌模型总数错误"
    Assert-DtEqual 8 $catalog.counts.selection "临时选择牌模型总数错误"
    Assert-DtEqual 45 $catalog.counts.reward "奖励卡总数错误"
    Assert-DtEqual 10 $catalog.counts.common "普通奖励卡数量错误"
    Assert-DtEqual 18 $catalog.counts.uncommon "蓝色奖励卡数量错误"
    Assert-DtEqual 17 $catalog.counts.rare "金色奖励卡数量错误"
    Assert-DtEqual 27 $catalog.counts.potion "药剂模型数量错误"

    $ids = @($catalog.all | ForEach-Object { $_.id })
    Assert-DtEqual $ids.Count @($ids | Sort-Object -Unique).Count "运行时卡牌 ID 不唯一"
    $nonUpgradeable = @($catalog.formal | Where-Object { $_.maxUpgradeLevel -ne 1 })
    Assert-DtEqual 0 $nonUpgradeable.Count "存在不符合单次升级约定的正式卡牌模型"
    return @{
        all = $catalog.counts.all
        formal = $catalog.counts.formal
        selectionTokens = $catalog.counts.selection
        localizedTitles = 95
        rewards = "$($catalog.counts.common)/$($catalog.counts.uncommon)/$($catalog.counts.rare)"
        potions = $catalog.counts.potion
    }
}

Invoke-DtCase -Suite $suite -Name "localization-key-parity" -Body {
    $projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $zhsPath = Join-Path $projectRoot "DimensionalTraveler\localization\zhs\cards.json"
    $engPath = Join-Path $projectRoot "DimensionalTraveler\localization\eng\cards.json"
    $zhs = Get-Content -Raw -Path $zhsPath | ConvertFrom-Json
    $eng = Get-Content -Raw -Path $engPath | ConvertFrom-Json
    $zhsKeys = @($zhs.PSObject.Properties.Name | Sort-Object)
    $engKeys = @($eng.PSObject.Properties.Name | Sort-Object)
    Assert-DtEqual 202 $zhsKeys.Count "中文卡牌本地化键数错误"
    Assert-DtEqual 202 $engKeys.Count "英文卡牌本地化键数错误"
    Assert-DtEqual ($zhsKeys -join "`n") ($engKeys -join "`n") "中英文卡牌本地化键集合不一致"
    Assert-DtEqual 95 @($zhsKeys | Where-Object { $_ -like "*.title" }).Count "卡牌标题键数量错误"
    return @{ keys = $zhsKeys.Count; titles = 95 }
}

Invoke-DtCase -Suite $suite -Name "target-tool-contract" -Body {
    $target = Invoke-DtTool -Name "dimensional_traveler_test_target" -Arguments @{ action = "get" }
    Assert-DtTrue $target.ok "目标查询命令失败"
    Assert-DtEqual $false $target.targeting.active "空闲状态不应存在目标选择"
    Assert-DtEqual 0 @($target.targeting.candidates).Count "空闲状态不应存在目标候选"
    return @{ active = $target.targeting.active }
}