param(
    [string]$Endpoint = "http://127.0.0.1:9877/messages",
    [string]$Seed = "DIMENSIONAL-TRAVELER-ACCEPTANCE",
    [switch]$SkipStateMutation,
    [switch]$SkipCombatBootstrap
)

$ErrorActionPreference = "Stop"
$script:requestId = 0

function Invoke-Rpc([string]$Method, [hashtable]$Params) {
    $script:requestId += 1
    $body = @{
        jsonrpc = "2.0"
        id = $script:requestId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 20 -Compress
    $response = Invoke-RestMethod -Uri $Endpoint -Method Post -ContentType "application/json" -Body $body
    if ($null -ne $response.error) {
        throw "MCP RPC 失败：$($response.error.message)"
    }
    return $response.result
}

function Invoke-Tool([string]$Name, [hashtable]$Arguments = @{}) {
    $result = Invoke-Rpc "tools/call" @{ name = $Name; arguments = $Arguments }
    $text = $result.content[0].text
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "工具 $Name 未返回文本结果。"
    }
    return $text | ConvertFrom-Json
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message；期望=$Expected，实际=$Actual"
    }
}

$tools = (Invoke-Rpc "tools/list" @{}).tools.name
foreach ($requiredTool in @(
    "get_game_state",
    "dimensional_traveler_test_control",
    "dimensional_traveler_test_target"
)) {
    if ($requiredTool -notin $tools) {
        throw "缺少 MCP 工具：$requiredTool"
    }
}

$state = Invoke-Tool "get_game_state"
$extension = $state.extensions.dimensionalTravelerTest
if ($null -eq $extension -and -not $SkipCombatBootstrap) {
    $bootstrap = Invoke-Tool "dimensional_traveler_test_control" @{
        action = "start_test_combat"
        seed = $Seed
    }
    Assert-Equal $true $bootstrap.ok "创建次元旅人测试战斗失败"

    $deadline = [DateTimeOffset]::Now.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $state = Invoke-Tool "get_game_state"
        $extension = $state.extensions.dimensionalTravelerTest
    } while ($null -eq $extension -and [DateTimeOffset]::Now -lt $deadline)
}
if ($null -eq $extension) {
    throw "当前不是已进入战斗的次元旅人，或测试快照未注册。"
}
Assert-Equal 2 $extension.schemaVersion "快照 schemaVersion 不匹配"
Assert-Equal $true $extension.combatStateAttached "AlchemyCombatStatePower 未附加"
Assert-Equal $true $extension.backpack.attached "药剂背包未附加"

$targetState = Invoke-Tool "dimensional_traveler_test_target" @{ action = "get" }
Assert-Equal $true $targetState.ok "目标查询命令失败"

if (-not $SkipStateMutation) {
    $original = @{}
    foreach ($name in @("vitality", "volatility", "corruption", "catalysis", "diffusion", "echo")) {
        $original[$name] = $extension.principles.$name.amount
    }

    try {
        $expected = @{
            vitality = 7
            volatility = 8
            corruption = 9
            catalysis = 1
            diffusion = 2
            echo = 3
        }
        $setResult = Invoke-Tool "dimensional_traveler_test_control" @{
            action = "set_principles"
            principles = $expected
        }
        Assert-Equal $true $setResult.ok "设置六原理失败"

        $updated = (Invoke-Tool "get_game_state").extensions.dimensionalTravelerTest.principles
        foreach ($name in $expected.Keys) {
            Assert-Equal $expected[$name] $updated.$name.amount "原理 $name 的快照值不匹配"
        }

        $clearAudit = Invoke-Tool "dimensional_traveler_test_control" @{ action = "clear_payment_audit" }
        Assert-Equal $true $clearAudit.ok "清理支付审计失败"
        $payments = (Invoke-Tool "get_game_state").extensions.dimensionalTravelerTest.payments
        Assert-Equal 0 @($payments).Count "支付审计未清空"
    }
    finally {
        $restore = Invoke-Tool "dimensional_traveler_test_control" @{
            action = "set_principles"
            principles = $original
        }
        if (-not $restore.ok) {
            Write-Warning "六原理恢复失败：$($restore.error)"
        }
    }
}

[ordered]@{
    passed = $true
    schemaVersion = $extension.schemaVersion
    playerNetId = $extension.playerNetId
    backpackCount = $extension.backpack.count
    targetSelectionActive = $targetState.targeting.active
    checkedAt = [DateTimeOffset]::Now.ToString("o")
} | ConvertTo-Json -Depth 5