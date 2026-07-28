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

Invoke-DtCase -Suite $suite -Name "narrative-localization-and-event-contract" -Body {
    $projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $localeRoot = Join-Path $projectRoot "DimensionalTraveler\localization"
    $expectedKeyCounts = @{
        "epochs.json" = 16
        "events.json" = 25
        "ancients.json" = 38
        "narrative.json" = 3
    }

    foreach ($fileName in $expectedKeyCounts.Keys) {
        $zhs = Get-Content -Raw -Path (Join-Path $localeRoot "zhs\$fileName") | ConvertFrom-Json
        $eng = Get-Content -Raw -Path (Join-Path $localeRoot "eng\$fileName") | ConvertFrom-Json
        $zhsKeys = @($zhs.PSObject.Properties.Name | Sort-Object)
        $engKeys = @($eng.PSObject.Properties.Name | Sort-Object)
        Assert-DtEqual $expectedKeyCounts[$fileName] $zhsKeys.Count "中文 $fileName 键数错误"
        Assert-DtEqual $expectedKeyCounts[$fileName] $engKeys.Count "英文 $fileName 键数错误"
        Assert-DtEqual ($zhsKeys -join "`n") ($engKeys -join "`n") "中英文 $fileName 键集合不一致"
    }

    $events = Get-Content -Raw -Path (Join-Path $localeRoot "zhs\events.json") | ConvertFrom-Json
    foreach ($key in @(
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.title",
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.pages.TRAVELER_INITIAL.options.CONTAIN.title",
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.pages.TRAVELER_INITIAL.options.TRANSFER.title",
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.pages.TRAVELER_INITIAL.options.EXPLOIT.disabled",
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.pages.OTHER_INITIAL.options.COPY.disabled",
        "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD.pages.OTHER_COPY.selectionScreenPrompt"
    )) {
        Assert-DtTrue ($null -ne $events.PSObject.Properties[$key]) "未封的记录缺少本地化键：$key"
    }

    $ancients = Get-Content -Raw -Path (Join-Path $localeRoot "zhs\ancients.json") | ConvertFrom-Json
    $ancientIds = @("NEOW", "DARV", "OROBAS", "NONUPEIPE", "PAEL", "TANX", "VAKUU")
    foreach ($ancientId in $ancientIds) {
        Assert-DtTrue (@($ancients.PSObject.Properties.Name | Where-Object { $_ -like "$ancientId.talk.DIMENSIONAL_TRAVELER.*" }).Count -gt 0) "先古 $ancientId 缺少旅人分支"
    }
    foreach ($placeholderId in @("NONUPEIPE", "PAEL", "TANX", "VAKUU")) {
        Assert-DtTrue ([string]$ancients.PSObject.Properties["$placeholderId.talk.DIMENSIONAL_TRAVELER.0-0r.ancient"].Value -match "测试占位") "先古 $placeholderId 的测试占位未明确标注"
    }

    $eventSource = Get-Content -Raw -Path (Join-Path $projectRoot "src\Content\Events\UnsealedRecord.cs")
    foreach ($fragment in @(
        "[RegisterSharedEvent]",
        "public sealed class UnsealedRecord : ModEventTemplate",
        'ContentAssetProfiles.Event("AROMA_OF_CHAOS")',
        "RelicSelectCmd.FromChooseARelicScreen",
        "FirstFormulaPrincipleDiscount",
        "PotionSatchelExpansion"
    )) {
        Assert-DtTrue $eventSource.Contains($fragment) "未封的记录未满足注册或奖励边界：$fragment"
    }

    return @{ tables = $expectedKeyCounts; ancients = $ancientIds.Count; eventId = "DIMENSIONAL_TRAVELER_EVENT_UNSEALED_RECORD" }
}

Invoke-DtCase -Suite $suite -Name "shared-potion-pool-contract" -Body {
    $pool = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "inspect_shared_potion_pool" }
    Assert-DtTrue $pool.ok "共享药水池查询失败：$(Get-DtToolError $pool)"
    Assert-DtEqual 45 ([int]$pool.count) "当前版本共享药水池数量错误"
    Assert-DtEqual 45 @($pool.potions).Count "共享药水池快照数量错误"
    Assert-DtEqual 45 @($pool.potions.id | Sort-Object -Unique).Count "共享药水池存在重复稳定 ID"
    Assert-DtEqual 3 @($pool.specialPotions).Count "特殊药水目录快照数量错误"
    Assert-DtEqual 3 @($pool.specialPotions.id | Sort-Object -Unique).Count "特殊药水目录存在重复稳定 ID"
    return @{
        sharedPotions = @($pool.potions | ForEach-Object { "$($_.id):$($_.type):$($_.rarity)" })
        specialPotions = @($pool.specialPotions | ForEach-Object { "$($_.id):$($_.type):$($_.rarity)" })
    }
}

Invoke-DtCase -Suite $suite -Name "extraction-catalog-covers-shared-pool-and-frozen-specials" -Body {
    $catalog = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "inspect_extraction_catalog" }
    Assert-DtTrue $catalog.ok "萃取目录查询失败：$(Get-DtToolError $catalog)"
    Assert-DtTrue $catalog.validation.valid "萃取目录与当前共享池不完整：$(($catalog.validation | ConvertTo-Json -Compress))"
    Assert-DtEqual 48 @($catalog.plans).Count "萃取目录必须包含 45 瓶共享药水和 3 瓶冻结特例"
    Assert-DtEqual 45 @($catalog.plans | Where-Object { $_.scope -eq "SharedPool" }).Count "共享药水萃取映射数量错误"
    Assert-DtEqual 3 @($catalog.plans | Where-Object { $_.scope -eq "ExplicitSpecial" }).Count "冻结特殊药水映射数量错误"

    $attack = @($catalog.plans | Where-Object { $_.potionId -eq "ATTACK_POTION" })
    Assert-DtEqual 1 $attack.Count "攻击药水萃取计划缺失或重复"
    Assert-DtEqual "AttackPotion" $attack[0].choiceMode "攻击药水未使用专属三选一计划"
    Assert-DtEqual 0 @($attack[0].rewards).Count "攻击药水不应预先生成固定产物"

    $dualRewards = @($catalog.plans | Where-Object { @($_.rewards).Count -eq 2 })
    Assert-DtEqual 2 $dualRewards.Count "双产物药水数量错误"
    Assert-DtEqual "FYSH_OIL`nPOTION_OF_BINDING" (($dualRewards.potionId | Sort-Object) -join "`n") "双产物药水 ID 错误"

    $foul = @($catalog.plans | Where-Object { $_.potionId -eq "FOUL_POTION" })[0]
    Assert-DtEqual 200 ([int]$foul.gold) "污浊药水金币收益错误"
    Assert-DtEqual 3 ([int]$foul.maxHp) "污浊药水最大生命收益错误"
    return @{ shared = 45; special = 3; attackChoice = $attack[0].choiceMode; dualRewards = $dualRewards.potionId }
}

Invoke-DtCase -Suite $suite -Name "target-tool-contract" -Body {
    $target = Invoke-DtTool -Name "dimensional_traveler_test_target" -Arguments @{ action = "get" }
    Assert-DtTrue $target.ok "目标查询命令失败"
    Assert-DtEqual $false $target.targeting.active "空闲状态不应存在目标选择"
    Assert-DtEqual 0 @($target.targeting.candidates).Count "空闲状态不应存在目标候选"
    return @{ active = $target.targeting.active }
}