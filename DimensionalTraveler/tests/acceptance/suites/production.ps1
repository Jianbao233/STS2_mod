$suite = "production"

Invoke-DtCase -Suite $suite -Name "c03-production-c04-no-recursion" -Body {
    $null = Reset-DtScenario -Id "CATALYSIS_CHAIN" -Fixture @{
        energy = 10
        principles = @{ catalysis = 3 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_PRODUCTION_AMPLIFICATION"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_CATALYSIS_PRODUCTION"; pile = "hand" },
            @{ id = "DIMENSIONAL_TRAVELER_CARD_REPEAT_PRODUCTION"; pile = "hand" }
        )
    }

    $amplify = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_PRODUCTION_AMPLIFICATION" -Target @{ target_index = -1 }
    Assert-DtTrue $amplify.success "C03 生产强化未成功打出"
    $prepared = Get-DtExtension
    Assert-DtEqual 1 $prepared.principles.catalysis.amount "C03 支付后的催化层数错误"
    Assert-DtEqual 3 $prepared.turn.productionBoostCatalysisSnapshot "C03 未保存支付前催化快照"

    $produce = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_CATALYSIS_PRODUCTION" -Target @{ target_index = -1 }
    Assert-DtTrue $produce.success "C01 催化生产未成功打出"
    $produced = Get-DtExtension
    Assert-DtEqual 3 $produced.principles.catalysis.amount "C03 未将催化生产基础值翻倍"
    Assert-DtEqual $null $produced.turn.productionBoostCatalysisSnapshot "生产后未清除强化资格"
    $snapshot = @($produced.turn.latestProduction.resources)
    Assert-DtEqual 1 $snapshot.Count "生产快照资源项数量错误"
    Assert-DtEqual 2 $snapshot[0].amount "强化后的最终生产快照错误"

    $repeat = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_REPEAT_PRODUCTION" -Target @{ target_index = -1 }
    Assert-DtTrue $repeat.success "C04 重复生产未成功打出"
    $repeated = Get-DtExtension
    Assert-DtEqual 3 $repeated.principles.catalysis.amount "C04 未复制最终生产快照"
    Assert-DtEqual 2 @($repeated.turn.latestProduction.resources)[0].amount "C04 不应覆盖原始生产快照"
    return @{ amplifiedProduction = 2; finalCatalysis = 3; recursiveSnapshot = $false }
}

Invoke-DtCase -Suite $suite -Name "directed-production-catalysis-passive" -Body {
    $null = Reset-DtScenario -Id "DIRECTED_PRODUCTION" -Fixture @{
        principles = @{ catalysis = 3 }
        cards = @(
            @{ id = "DIMENSIONAL_TRAVELER_CARD_VITALITY_BURST"; pile = "hand" }
        )
    }
    $play = Invoke-DtCard -CardId "DIMENSIONAL_TRAVELER_CARD_VITALITY_BURST" -Target @{ target_index = -1 }
    Assert-DtTrue $play.success "生机爆发未成功打出"
    $extension = Get-DtExtension
    Assert-DtEqual 5 $extension.principles.vitality.amount "3 层催化下定向生产应得到 3+2"
    Assert-DtEqual 3 $extension.principles.catalysis.amount "催化被动不应消耗层数"
    Assert-DtEqual 5 @($extension.turn.latestProduction.resources)[0].amount "最终生产快照错误"
    return @{ vitality = 5; catalysis = 3 }
}