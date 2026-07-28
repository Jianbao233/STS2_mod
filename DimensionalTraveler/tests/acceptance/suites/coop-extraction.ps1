$suite = "coop-extraction"

$start = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "start_pseudo_coop" } -TimeoutSeconds 120
Assert-DtTrue $start.ok "攻击药水伪联机启动失败：$(Get-DtToolError $start)"
$enter = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "enter_pseudo_coop_test_combat" } -TimeoutSeconds 90
Assert-DtTrue $enter.ok "攻击药水伪联机进入战斗失败：$(Get-DtToolError $enter)"

Invoke-DtCase -Suite $suite -Name "attack-extraction-uses-owner-scoped-native-choice-in-pseudo-coop" -Body {
    $state = Wait-DtPlayPhase -TimeoutSeconds 45
    $travelers = @($state.extensions.dimensionalTravelerTest.travelers | Sort-Object playerNetId)
    Assert-DtEqual 2 $travelers.Count "攻击药水伪联机旅者数量错误"
    $roster = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{ action = "inspect_players" }
    Assert-DtTrue $roster.ok "攻击药水伪联机名册读取失败：$(Get-DtToolError $roster)"
    $ownerId = [long]$roster.localNetId
    Assert-DtTrue ($ownerId -in @($travelers | ForEach-Object { [long]$_.playerNetId })) `
        "伪联机本地 NetId 不属于旅者名册"
    $otherId = [long]@($travelers | Where-Object { [long]$_.playerNetId -ne $ownerId })[0].playerNetId

    foreach ($traveler in $travelers) {
        $fixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
            action = "apply_player_fixture"; player_net_id = [long]$traveler.playerNetId; fixture = @{ id = "COOP_ATTACK_EXTRACTION_$($traveler.playerNetId)"; energy = 10 }
        }
        Assert-DtTrue $fixture.ok "攻击药水旅者夹具初始化失败：$(Get-DtToolError $fixture)"
    }

    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_player_native_potion"; player_net_id = $ownerId; potion_id = "ATTACK_POTION"
    }
    Assert-DtTrue $grant.ok "攻击药水原生授予失败：$(Get-DtToolError $grant)"
    $beforeExtraction = Get-DtState
    $beforeTravelers = @($beforeExtraction.extensions.dimensionalTravelerTest.travelers)
    $ownerBefore = @($beforeTravelers | Where-Object { [long]$_.playerNetId -eq $ownerId })[0]
    $otherBefore = @($beforeTravelers | Where-Object { [long]$_.playerNetId -eq $otherId })[0]
    $rngBefore = $ownerBefore.rng | ConvertTo-Json -Compress
    Assert-DtEqual $rngBefore ($otherBefore.rng | ConvertTo-Json -Compress) `
        "攻击药水选择前双方共享战斗 RNG 快照不一致"

    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_player_native_potion"; player_net_id = $ownerId; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 30
    Assert-DtTrue $extract.ok "攻击药水萃取动作未进入选择阶段：$(Get-DtToolError $extract)"
    Assert-DtEqual "awaiting_choice" $extract.state "攻击药水未等待原生同步选择"

    $selection = Wait-DtCardChoice
    Assert-DtEqual 3 @($selection.candidates).Count "攻击药水伪联机候选数错误"
    $pendingState = Get-DtState
    $pendingTravelers = @($pendingState.extensions.dimensionalTravelerTest.travelers)
    $ownerPending = @($pendingTravelers | Where-Object { [long]$_.playerNetId -eq $ownerId })[0]
    $otherPending = @($pendingTravelers | Where-Object { [long]$_.playerNetId -eq $otherId })[0]
    Assert-DtEqual $rngBefore ($ownerPending.rng | ConvertTo-Json -Compress) `
        "攻击药水伪联机候选预览推进了所有者 RNG"
    Assert-DtEqual $rngBefore ($otherPending.rng | ConvertTo-Json -Compress) `
        "攻击药水伪联机候选预览推进了队友 RNG"

    $chosen = Invoke-DtTool -Name "dimensional_traveler_test_selection" -Arguments @{ action = "select"; candidate_index = 0 }
    Assert-DtTrue $chosen.ok "攻击药水伪联机选择提交失败：$(Get-DtToolError $chosen)"
    $after = Wait-DtStateMatch -TimeoutSeconds 20 -FailureMessage "攻击药水伪联机选择提交后未完成萃取" -Predicate {
        param($candidateState)
        $candidateTravelers = @($candidateState.extensions.dimensionalTravelerTest.travelers)
        $candidateOwner = @($candidateTravelers | Where-Object { [long]$_.playerNetId -eq $ownerId })[0]
        $null -ne $candidateOwner -and
            @($candidateOwner.nativePotions.slots | Where-Object { $null -ne $_ }).Count -eq 0 -and
            $candidateOwner.backpack.count -eq 1
    }
    $afterTravelers = @($after.extensions.dimensionalTravelerTest.travelers | Sort-Object playerNetId)
    $owner = @($afterTravelers | Where-Object { [long]$_.playerNetId -eq $ownerId })[0]
    $other = @($afterTravelers | Where-Object { [long]$_.playerNetId -eq $otherId })[0]
    Assert-DtEqual 0 @($owner.nativePotions.slots | Where-Object { $null -ne $_ }).Count "攻击药水选择后未移除原生药水"
    Assert-DtEqual 1 $owner.principles.diffusion.amount "攻击药水未给所有者扩散"
    Assert-DtEqual 2 $owner.principles.volatility.amount "攻击药水未给所有者挥发"
    Assert-DtEqual 1 $owner.backpack.count "攻击药水未给所有者药剂产物"
    Assert-DtEqual 0 @($other.nativePotions.slots | Where-Object { $null -ne $_ }).Count "攻击药水错误移除了队友原生药水"
    Assert-DtEqual 0 $other.principles.diffusion.amount "攻击药水错误给队友扩散"
    Assert-DtEqual 0 $other.principles.volatility.amount "攻击药水错误给队友挥发"
    Assert-DtEqual 0 $other.backpack.count "攻击药水错误给队友药剂产物"
    $ownerRngAfter = $owner.rng | ConvertTo-Json -Compress
    $otherRngAfter = $other.rng | ConvertTo-Json -Compress
    Assert-DtTrue ($ownerRngAfter -ne $rngBefore) "攻击药水选择提交后未推进冻结 RNG"
    Assert-DtEqual $ownerRngAfter $otherRngAfter "攻击药水提交后双方战斗 RNG 快照不一致"
    $choices = @($after.extensions.dimensionalTravelerTest.choices)
    Assert-DtEqual 1 @($choices | Where-Object { $_.operation -eq "reserve" }).Count "攻击药水未唯一保留 choiceId"
    Assert-DtEqual 0 @($choices | Where-Object { [string]$_.playerNetId -ne [string]$ownerId }).Count "攻击药水选择归属到错误旅者"
    return @{
        owner = $ownerId
        choiceCount = $choices.Count
        potion = $owner.backpack.cards[0].cardId
        rngAdvancedOnce = $true
        finalRngConsistent = $true
    }
}

Invoke-DtCase -Suite $suite -Name "fixed-extraction-is-owned-by-initiating-traveler" -Body {
    $state = Wait-DtPlayPhase -TimeoutSeconds 45
    $travelers = @($state.extensions.dimensionalTravelerTest.travelers | Sort-Object playerNetId)
    Assert-DtEqual 2 $travelers.Count "伪联机旅者数量错误"
    $ownerId = [long]$travelers[0].playerNetId
    $otherId = [long]$travelers[1].playerNetId

    foreach ($travelerId in @($ownerId, $otherId)) {
        $fixture = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
            action = "apply_player_fixture"; player_net_id = $travelerId; fixture = @{ id = "COOP_EXTRACTION_$travelerId"; energy = 10 }
        }
        Assert-DtTrue $fixture.ok "旅者 $travelerId 夹具初始化失败：$(Get-DtToolError $fixture)"
    }

    $grant = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_player_native_potion"; player_net_id = $ownerId; potion_id = "BLOCK_POTION"
    }
    Assert-DtTrue $grant.ok "主旅者原生药水授予失败：$(Get-DtToolError $grant)"
    $extract = Invoke-DtTool -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_player_native_potion"; player_net_id = $ownerId; potion_slot_index = [int]$grant.slotIndex
    } -TimeoutSeconds 45
    Assert-DtTrue $extract.ok "伪联机萃取失败：$(Get-DtToolError $extract)"
    Assert-DtEqual "Finished" $extract.state "伪联机萃取动作未完成"

    $after = Get-DtState
    $afterTravelers = @($after.extensions.dimensionalTravelerTest.travelers | Sort-Object playerNetId)
    $owner = @($afterTravelers | Where-Object { [long]$_.playerNetId -eq $ownerId })[0]
    $other = @($afterTravelers | Where-Object { [long]$_.playerNetId -eq $otherId })[0]
    Assert-DtEqual 0 @($owner.nativePotions.slots | Where-Object { $null -ne $_ }).Count "主旅者萃取后未移除原生药水"
    Assert-DtEqual 1 $owner.principles.diffusion.amount "主旅者未获得扩散收益"
    Assert-DtEqual 2 $owner.principles.vitality.amount "主旅者未获得生机收益"
    Assert-DtEqual 1 $owner.backpack.count "主旅者未获得萃取药剂"
    Assert-DtEqual 0 $other.principles.diffusion.amount "队友错误获得扩散收益"
    Assert-DtEqual 0 $other.principles.vitality.amount "队友错误获得生机收益"
    Assert-DtEqual 0 $other.backpack.count "队友错误获得萃取药剂"
    return @{ owner = $ownerId; other = $otherId; ownerBackpack = $owner.backpack.count; otherBackpack = $other.backpack.count }
}