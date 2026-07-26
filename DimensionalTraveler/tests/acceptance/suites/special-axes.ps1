$suite = "special-axes"

function Set-AllDtEnemyHp {
    param([Parameter(Mandatory)]$State, [int]$Hp = 40)
    for ($index = 0; $index -lt @($State.combat.enemies).Count; $index++) {
        $result = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
            action = "set_enemy_hp"; enemy_index = $index; hp = $Hp
        }
        Assert-DtTrue $result.ok "设置敌人 $index 生命失败：$(Get-DtToolError $result)"
    }
}

function Move-DtPotionToHand {
    param([int]$BackpackIndex = 0)
    $result = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "move_backpack_potion_to_hand"; backpack_index = $BackpackIndex
    }
    Assert-DtTrue $result.ok "药剂未能移入手牌：$(Get-DtToolError $result)"
}

Invoke-DtCase -Suite $suite -Name "d03-local-diffusion-second-target" -Body {
    $state = Reset-DtScenario -Id "LOCAL_DIFFUSION" -Fixture @{
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
        Assert-DtTrue $added.ok "D03 未能添加第二个敌人"
        Start-Sleep -Milliseconds 500
        $state = Get-DtState
    }
    Assert-DtTrue (@($state.combat.enemies).Count -ge 2) "D03 至少需要两个敌人"
    Set-AllDtEnemyHp -State $state -Hp 40

    $prepare = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION" -Target @{ target_index = -1 }
    Assert-DtTrue $prepare.success "D03 局部扩散未成功打出"
    $prepared = Get-DtExtension
    Assert-DtEqual "AdditionalTarget" $prepared.turn.pendingDiffusion "D03 未写入追加目标资格"
    Assert-DtEqual 0 $prepared.principles.diffusion.amount "D03 扩散支付错误"

    Move-DtPotionToHand
    $before = Get-DtState
    $enemies = @($before.extensions.dimensionalTravelerTest.combatants |
        Where-Object { $_.side -eq "Enemy" } | Sort-Object combatId)
    $initialId = [int]$enemies[0].combatId
    $additionalId = [int]$enemies[1].combatId
    $play = Invoke-DtCardWithBattleTarget -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" `
        -InitialTargetIndex 0 -AdditionalCombatId $additionalId
    Assert-DtTrue $play.success "D03 攻击药剂未完成双目标结算"

    $after = Get-DtState
    $initialDamage = (Get-DtCombatant -State $before -CombatId $initialId).currentHp -
        (Get-DtCombatant -State $after -CombatId $initialId).currentHp
    $additionalDamage = (Get-DtCombatant -State $before -CombatId $additionalId).currentHp -
        (Get-DtCombatant -State $after -CombatId $additionalId).currentHp
    Assert-DtEqual 9 $initialDamage "D03 首次目标伤害错误"
    Assert-DtEqual 9 $additionalDamage "D03 第二目标伤害错误"
    $extension = $after.extensions.dimensionalTravelerTest
    Assert-DtEqual "None" $extension.turn.pendingDiffusion "D03 结算后未清除资格"
    $targets = @($extension.turn.latestOriginalPotion.targetCombatIds)
    Assert-DtEqual 2 $targets.Count "D03 冻结目标数量错误"
    Assert-DtEqual $initialId $targets[0] "D03 首次目标快照错误"
    Assert-DtEqual $additionalId $targets[1] "D03 第二目标快照错误"
    return @{ initial = $initialId; additional = $additionalId; damages = @($initialDamage, $additionalDamage) }
}

Invoke-DtCase -Suite $suite -Name "d04-full-diffusion-whole-side" -Body {
    $state = Reset-DtScenario -Id "FULL_DIFFUSION" -Fixture @{
        energy = 10
        principles = @{ diffusion = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_FULL_DIFFUSION"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    if (@($state.combat.enemies).Count -lt 2) {
        $added = Invoke-DtTool -Name "dev_add_monster" -Arguments @{ monster_id = "LEAF_SLIME_M" }
        Assert-DtTrue $added.ok "D04 未能添加第二个敌人"
        Start-Sleep -Milliseconds 500
        $state = Get-DtState
    }
    Set-AllDtEnemyHp -State $state -Hp 10

    $prepare = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_FULL_DIFFUSION" -Target @{ target_index = -1 }
    Assert-DtTrue $prepare.success "D04 完整扩散未成功打出"
    Move-DtPotionToHand
    $before = Get-DtState
    $beforeEnemies = @($before.extensions.dimensionalTravelerTest.combatants |
        Where-Object { $_.side -eq "Enemy" } |
        Sort-Object combatId |
        ForEach-Object {
            [pscustomobject]@{
                CombatId = [int]$_.combatId
                CurrentHp = [int]$_.currentHp
            }
        })
    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" `
        -Target @{ target_side = "enemy"; target_index = 0 }
    Assert-DtTrue $play.success "D04 攻击药剂未成功打出"

    $after = Get-DtState
    $afterCombatants = @($after.extensions.dimensionalTravelerTest.combatants |
        Where-Object { $_.side -eq "Enemy" })
    $targets = @($after.extensions.dimensionalTravelerTest.turn.latestOriginalPotion.targetCombatIds)
    Assert-DtEqual $beforeEnemies.Count $targets.Count "D04 冻结目标集合与敌方存活集合不一致"
    foreach ($enemy in $beforeEnemies) {
        Assert-DtTrue ($targets -contains $enemy.CombatId) "D04 冻结目标缺少 CombatId=$($enemy.CombatId)"
        $current = $afterCombatants | Where-Object { [int]$_.combatId -eq $enemy.CombatId } | Select-Object -First 1
        if ($null -eq $current) {
            Assert-DtTrue ($enemy.CurrentHp -le 9) "D04 丢失了本应存活的 CombatId=$($enemy.CombatId)"
        }
        else {
            Assert-DtEqual 9 ($enemy.CurrentHp - $current.currentHp) "D04 未对 CombatId=$($enemy.CombatId) 结算 9 点伤害"
        }
    }
    Assert-DtEqual "None" $after.extensions.dimensionalTravelerTest.turn.pendingDiffusion "D04 结算后未清除资格"
    return @{ targetCount = $targets.Count; damageEach = 9 }
}

Invoke-DtCase -Suite $suite -Name "e03-replay-frozen-target" -Body {
    $null = Reset-DtScenario -Id "ECHO_REPLAY" -Fixture @{
        energy = 10
        principles = @{ echo = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ECHO_REPLAY"; pile = "hand" }
        )
        backpack = @(
            @{ family = "SelfDefense"; quality = "Normal"; upgraded = $false; origin = "Original" }
        )
    }
    Move-DtPotionToHand
    $original = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_SELF_DEFENSE_POTION" -Target @{ target_index = -1 }
    Assert-DtTrue $original.success "E03 基线自卫药剂未成功打出"
    $beforeReplay = Get-DtState
    Assert-DtEqual 10 $beforeReplay.combat.playerBlock "E03 基线自卫药剂格挡错误"
    $snapshotTarget = @($beforeReplay.extensions.dimensionalTravelerTest.turn.latestOriginalPotion.targetCombatIds)[0]

    $replay = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ECHO_REPLAY" -Target @{ target_index = -1 }
    Assert-DtTrue $replay.success "E03 回响重放未成功打出"
    $afterReplay = Get-DtState
    Assert-DtEqual 20 $afterReplay.combat.playerBlock "E03 未按冻结目标重放 10 点格挡"
    Assert-DtEqual 0 $afterReplay.extensions.dimensionalTravelerTest.principles.echo.amount "E03 回响支付错误"
    Assert-DtEqual $snapshotTarget @($afterReplay.extensions.dimensionalTravelerTest.turn.latestOriginalPotion.targetCombatIds)[0] `
        "E03 不应覆盖原始药剂快照"
    return @{ blockBefore = 10; blockAfter = 20; target = $snapshotTarget }
}

Invoke-DtCase -Suite $suite -Name "e04-derived-potion-inheritance-and-isolation" -Body {
    $state = Reset-DtScenario -Id "ECHO_DERIVED" -Fixture @{
        energy = 10
        principles = @{ echo = 1 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ECHO_POTION_CREATION"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_POTION_REPACK"; pile = "hand" }
        )
        backpack = @(
            @{ family = "Attack"; quality = "Refined"; upgraded = $true; origin = "Original" }
        )
    }
    Set-AllDtEnemyHp -State $state -Hp 60
    Move-DtPotionToHand
    $original = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_REFINED_ATTACK_POTION" `
        -Target @{ target_side = "enemy"; target_index = 0 }
    Assert-DtTrue $original.success "E04 基线精制+攻击药剂未成功打出"
    $originalState = Get-DtState
    $originalSnapshot = $originalState.extensions.dimensionalTravelerTest.turn.latestOriginalPotion
    Assert-DtEqual $true $originalSnapshot.upgraded "E04 基线快照未记录 +"
    Assert-DtEqual "Refined" $originalSnapshot.quality "E04 基线快照品质错误"

    $create = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ECHO_POTION_CREATION" -Target @{ target_index = -1 }
    Assert-DtTrue $create.success "E04 未生成回响药剂"
    $created = Get-DtState
    $derived = @($created.extensions.dimensionalTravelerTest.piles.hand | Where-Object {
        $_.cardId -eq "DIMENSIONAL_TRAVELER_CARD_REFINED_ATTACK_POTION" -and $_.origin -eq "EchoDerived"
    })
    Assert-DtEqual 1 $derived.Count "E04 未生成唯一 EchoDerived 药剂"
    Assert-DtEqual "Refined" $derived[0].quality "E04 未继承品质"
    Assert-DtEqual $true $derived[0].upgraded "E04 未继承 + 状态"
    Assert-DtEqual 2 $derived[0].costForTurn "E04 精制回响药剂费用应为 2"
    Assert-DtEqual 0 $created.extensions.dimensionalTravelerTest.principles.echo.amount "E04 回响支付错误"

    $repack = @($created.combat.hand | Where-Object { $_.id -eq "DIMENSIONAL_TRAVELER_CARD_POTION_REPACK" })[0]
    Assert-DtEqual $false $repack.canPlay "EchoDerived 药剂不应允许重新装包"
    $derivedPlay = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_REFINED_ATTACK_POTION" `
        -Target @{ target_side = "enemy"; target_index = 0 }
    Assert-DtTrue $derivedPlay.success "E04 派生药剂未成功使用"
    $after = Get-DtExtension
    Assert-DtEqual "Original" $after.turn.latestOriginalPotion.origin "EchoDerived 使用不应覆盖原始快照"
    Assert-DtEqual "Refined" $after.turn.latestOriginalPotion.quality "EchoDerived 使用改变了原始快照品质"
    return @{ origin = "EchoDerived"; quality = "Refined"; upgraded = $true; cost = 2; repackable = $false }
}

Invoke-DtCase -Suite $suite -Name "x02-three-experiments-to-special-principle" -Body {
    $state = Reset-DtScenario -Id "EXPERIMENT_CONVERSION" -Fixture @{
        energy = 10
        principles = @{ corruption = 3 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PURIFICATION"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_EXPERIMENT_CONVERSION"; pile = "hand" }
        )
    }
    Set-AllDtEnemyHp -State $state -Hp 60
    $formula = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" -Target @{ target_index = -1 }
    Assert-DtTrue $formula.success "X02 前置炼成失败"
    $purify = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PURIFICATION" `
        -Target @{ target_index = -1; selection_index = 0 }
    Assert-DtTrue $purify.success "X02 前置提纯失败"
    Move-DtPotionToHand
    $potion = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION" `
        -Target @{ target_side = "enemy"; target_index = 0 }
    Assert-DtTrue $potion.success "X02 前置原始药剂使用失败"
    $recorded = Get-DtExtension
    Assert-DtEqual 3 $recorded.turn.experimentCount "X02 前置实验记录应为三项"

    $convert = Invoke-DtCardWithSelection -CardId "DIMENSIONAL_TRAVELER_CARD_EXPERIMENT_CONVERSION" `
        -Target @{ target_index = -1 } -SelectionIndex 0 -TimeoutSeconds 45
    Assert-DtTrue $convert.success "X02 实验转化未成功打出"
    $after = Get-DtExtension
    Assert-DtEqual 3 $after.principles.catalysis.amount "X02 未按三项实验生产 3 层所选特殊原理"
    Assert-DtEqual 3 $after.turn.experimentCount "X02 不应消耗实验记录"
    return @{ experiments = $after.turn.experiments; selected = "catalysis"; amount = 3 }
}