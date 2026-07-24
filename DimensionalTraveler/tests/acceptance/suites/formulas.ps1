$suite = "formulas"

Invoke-DtCase -Suite $suite -Name "normal-formula-payment-and-brew" -Body {
    $null = Reset-DtScenario -Id "FORMULA_SUCCESS" -Fixture @{
        energy = 5
        principles = @{ corruption = 2 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand" }
        )
    }

    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" -Target @{ target_index = -1 }
    Assert-DtTrue $play.success "攻击药剂配方未成功打出"
    $extension = Get-DtExtension
    Assert-DtEqual 0 $extension.principles.corruption.amount "配方支付后的腐化原理错误"
    Assert-DtEqual 1 $extension.backpack.count "配方未生成唯一药剂"
    Assert-DtEqual "Attack" $extension.backpack.cards[0].family "配方生成了错误药剂家族"
    Assert-DtEqual "Normal" $extension.backpack.cards[0].quality "配方生成了错误品质"
    Assert-DtEqual "BrewedOriginalPotion" $extension.turn.experiments "配方未写入炼成实验"
    $payments = @($extension.payments)
    Assert-DtEqual 1 $payments.Count "配方支付审计记录数错误"
    Assert-DtEqual 2 $payments[0].requestedAmount "配方支付请求量错误"
    Assert-DtEqual 2 $payments[0].before "配方支付前数量错误"
    Assert-DtEqual 0 $payments[0].after "配方支付后数量错误"
    Assert-DtTrue $payments[0].succeeded "配方支付未记录成功"
    return @{ payment = "2->0"; product = $extension.backpack.cards[0].cardId }
}

Invoke-DtCase -Suite $suite -Name "formula-payment-failure-is-atomic" -Body {
    $before = Reset-DtScenario -Id "FORMULA_FAILURE" -Fixture @{
        energy = 5
        principles = @{ corruption = 1 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA"; pile = "hand" }
        )
    }
    $energyBefore = $before.combat.currentEnergy
    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_ATTACK_POTION_FORMULA" `
        -Target @{ target_index = -1 } -RequirePlayable $false
    Assert-DtEqual $false $play.success "资源不足的配方不应成功打出"
    $after = Get-DtState
    $extension = $after.extensions.dimensionalTravelerTest
    Assert-DtEqual 1 $extension.principles.corruption.amount "失败支付修改了原理"
    Assert-DtEqual 0 $extension.backpack.count "失败支付仍生成了药剂"
    Assert-DtEqual 0 @($extension.payments).Count "失败支付进入了提交阶段"
    Assert-DtEqual $energyBefore $after.combat.currentEnergy "失败支付消耗了能量"
    return @{ corruption = 1; energy = $energyBefore; backpack = 0 }
}