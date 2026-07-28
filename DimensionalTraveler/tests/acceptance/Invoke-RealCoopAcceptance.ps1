param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GamePath = "K:\SteamLibrary\steamapps\common\Slay the Spire 2",
    [int]$HostBridgePort = 9877,
    [int]$ClientBridgePort = 9887,
    [ValidateRange(5, 30)]
    [int]$TestFps = 15,
    [switch]$SkipBuild,
    [switch]$AllowStartGame
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if ($HostBridgePort -ne 9877) {
    throw "真实双进程验收的主桥固定使用 9877。"
}
if ($ClientBridgePort -eq $HostBridgePort) {
    throw "客机测试桥不能与主桥共用端口。"
}
if (-not $AllowStartGame) {
    throw "真实双进程验收必须显式传 -AllowStartGame。"
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent (Split-Path -Parent $root)
$reportRoot = Join-Path $root "reports"
$timestamp = [DateTimeOffset]::Now.ToString("yyyyMMdd_HHmmss")
$runId = "dt-real-coop-$timestamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$sessionDir = Join-Path $reportRoot $runId
$runPath = Join-Path $sessionDir "run.json"
$casePath = Join-Path $sessionDir "cases.ndjson"
$finalPath = Join-Path $sessionDir "final.json"
$hostLog = Join-Path $projectRoot "_runtime_real_coop_host_$timestamp.log"
$clientLog = Join-Path $projectRoot "_runtime_real_coop_client_$timestamp.log"
$gameExe = Join-Path $GamePath "SlayTheSpire2.exe"
$hostEndpoint = "http://127.0.0.1:$HostBridgePort/messages"
$clientEndpoint = "http://127.0.0.1:$ClientBridgePort/messages"
$hostProcess = $null
$clientProcess = $null
$caseStartedAt = $null
$failureEvidence = $null

New-Item -ItemType Directory -Force -Path $sessionDir | Out-Null

function Write-AtomicJson {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)
    $temporary = "$Path.tmp"
    $Value | ConvertTo-Json -Depth 60 | Set-Content -Path $temporary -Encoding UTF8
    Move-Item -Path $temporary -Destination $Path -Force
}

function Write-CaseResult {
    param([Parameter(Mandatory)]$Value)
    ($Value | ConvertTo-Json -Depth 40 -Compress) | Add-Content -Path $casePath -Encoding UTF8
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected=$Expected Actual=$Actual"
    }
}

function Get-ToolError {
    param([Parameter(Mandatory)]$Result)
    $property = $Result.PSObject.Properties["error"]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        return "未返回错误详情"
    }
    return [string]$property.Value
}

function Invoke-BridgeRpc {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][string]$Method,
        [hashtable]$Params = @{},
        [int]$TimeoutSeconds = 30
    )
    $body = @{
        jsonrpc = "2.0"
        id = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 30 -Compress
    $response = Invoke-RestMethod -Uri $Endpoint -Method Post -ContentType "application/json" `
        -Body $body -TimeoutSec $TimeoutSeconds
    $errorProperty = $response.PSObject.Properties["error"]
    if ($null -ne $errorProperty -and $null -ne $errorProperty.Value) {
        throw "RPC $Method 失败：$($errorProperty.Value | ConvertTo-Json -Compress)"
    }
    return $response.result
}

function Invoke-GameTool {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][string]$Name,
        [hashtable]$Arguments = @{},
        [int]$TimeoutSeconds = 30
    )
    $result = Invoke-BridgeRpc -Endpoint $Endpoint -Method "tools/call" -Params @{
        name = $Name
        arguments = $Arguments
    } -TimeoutSeconds $TimeoutSeconds
    $text = $result.content[0].text
    if ([string]::IsNullOrWhiteSpace([string]$text)) {
        throw "工具 $Name 未返回文本结果。"
    }
    return $text | ConvertFrom-Json
}

function Get-LanSafeNeowOptionIndex {
    param([Parameter(Mandatory)]$RunState, [Parameter(Mandatory)][string]$Role)

    $safeKeys = @(
        # 被动遗物或数值变更，不打开奖励/选卡 UI。
        "FISHING_ROD",
        "GOLDEN_PEARL",
        "NUTRITIOUS_OYSTER",
        "CURSED_PEARL",
        "DOWSING_ROD",
        "LAVA_ROCK",
        "NEOWS_TORMENT",
        "PHIAL_HOLSTER",
        "SILKEN_TRESS",
        "STONE_HUMIDIFIER",
        "BOOMING_CONCH",
        "WINGED_BOOTS"
    )
    foreach ($safeKey in $safeKeys) {
        $candidate = @($RunState.eventOptions | Where-Object {
            $_.textKey -like "*$safeKey"
        } | Select-Object -First 1)
        if ($candidate.Count -gt 0 -and $null -ne $candidate[0]) {
            return [int]$candidate[0].index
        }
    }

    $available = @($RunState.eventOptions | ForEach-Object { $_.textKey })
    $diagnostic = [ordered]@{
        role = $Role
        roomType = $RunState.roomType
        eventOptionCount = $RunState.eventOptionCount
        eventOptionTextKeys = $available
    } | ConvertTo-Json -Depth 8 -Compress
    throw "$Role 初始事件没有可无二次选择结算的选项；diagnostic=$diagnostic"
}

function Assert-ProcessAlive {
    param([Parameter(Mandatory)]$Process, [Parameter(Mandatory)][string]$Role)
    $Process.Refresh()
    if ($Process.HasExited) {
        throw "[game_process_crash] $Role 游戏进程已退出；PID=$($Process.Id)，ExitCode=$($Process.ExitCode)。"
    }

    try {
        # 游戏启动链可能主动提权；验收期间持续收敛到低优先级，避免抢占桌面与编辑器资源。
        if ($Process.PriorityClass -ne [System.Diagnostics.ProcessPriorityClass]::BelowNormal) {
            $Process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
        }
    }
    catch {
        # 资源约束失败不应掩盖真实的联机验收结果；进程存活检查仍继续生效。
    }
}

function Wait-Bridge {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)][string]$Role,
        [int]$TimeoutSeconds = 180
    )
    $health = $Endpoint -replace '/messages$', '/health'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Assert-ProcessAlive -Process $Process -Role $Role
        try {
            $result = Invoke-RestMethod -Uri $health -TimeoutSec 2
            if ($result.status -eq "ok") { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "$Role MCP bridge 未在 $TimeoutSeconds 秒内就绪：$health"
}

function Wait-ToolResult {
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][hashtable]$Arguments,
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)][string]$Role,
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 90
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    $lastError = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Assert-ProcessAlive -Process $Process -Role $Role
        try {
            $last = Invoke-GameTool -Endpoint $Endpoint -Name $Name -Arguments $Arguments -TimeoutSeconds 5
            $lastError = $null
            if (& $Predicate $last) { return $last }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 400
    }
    $detail = if ($null -eq $last) { "<none>" } else { $last | ConvertTo-Json -Depth 20 -Compress }
    $errorDetail = if ([string]::IsNullOrWhiteSpace($lastError)) { "<none>" } else { $lastError }
    throw "$Role 等待工具状态超时：$Name；last=$detail；lastRpcError=$errorDetail"
}

function Get-LanRunPair {
    $hostRun = Invoke-GameTool -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "inspect_lan_run" } -TimeoutSeconds 5
    $clientRun = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "inspect_lan_run" } -TimeoutSeconds 5
    return [ordered]@{
        checkedAt = [DateTimeOffset]::Now.ToString("o")
        host = $hostRun
        client = $clientRun
    }
}

function Test-LanAllPlayersHaveRelic {
    param(
        [Parameter(Mandatory)]$RunState,
        [Parameter(Mandatory)][string]$RelicId
    )

    $players = @($RunState.playerRelics)
    if ($players.Count -lt 2) {
        return $false
    }

    return @($players | Where-Object {
        @($_.relicIds) -contains $RelicId
    }).Count -eq $players.Count
}

function Save-LanCheckpoint {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)]$Evidence)

    Write-Host "[real-coop] checkpoint.begin=$Name"
    $checkpointsProperty = $runManifest.PSObject.Properties["lanCheckpoints"]
    if ($null -eq $checkpointsProperty) {
        $runManifest["lanCheckpoints"] = [ordered]@{}
    }
    $runManifest["lanCheckpoints"][$Name] = $Evidence
    Write-AtomicJson -Path $runPath -Value $runManifest
    Write-Host "[real-coop] checkpoint.saved=$Name"
}

function Wait-LanRunPair {
    param(
        [Parameter(Mandatory)][string]$Checkpoint,
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 90,
        [int]$CombatAsymmetryGraceSeconds = 15,
        [switch]$RejectCombatAsymmetry
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $asymmetryStartedAt = $null
    $last = $null
    $lastError = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Assert-ProcessAlive -Process $hostProcess -Role "Host"
        Assert-ProcessAlive -Process $clientProcess -Role "Client"
        try {
            $last = Get-LanRunPair
            $lastError = $null
        }
        catch {
            $lastError = $_.Exception.Message
            Start-Sleep -Milliseconds 400
            continue
        }

        $isCombatAsymmetric = $RejectCombatAsymmetry -and
            [bool]$last.host.combatActive -ne [bool]$last.client.combatActive
        if ($isCombatAsymmetric) {
            if ($null -eq $asymmetryStartedAt) {
                $asymmetryStartedAt = [DateTimeOffset]::UtcNow
            }
            elseif (([DateTimeOffset]::UtcNow - $asymmetryStartedAt).TotalSeconds -ge $CombatAsymmetryGraceSeconds) {
                Save-LanCheckpoint -Name "${Checkpoint}_asymmetric_combat" -Evidence $last
                throw "$Checkpoint 检测到持续 $CombatAsymmetryGraceSeconds 秒的房间转换分叉：Host room=$($last.host.roomType), combat=$($last.host.combatActive)；Client room=$($last.client.roomType), combat=$($last.client.combatActive)。"
            }
        }
        else {
            $asymmetryStartedAt = $null
        }
        if (& $Predicate $last.host $last.client) {
            Save-LanCheckpoint -Name $Checkpoint -Evidence $last
            return $last
        }
        Start-Sleep -Milliseconds 400
    }

    if ($null -ne $last) {
        Save-LanCheckpoint -Name "${Checkpoint}_timeout" -Evidence $last
    }
    $detail = if ($null -eq $last) { "<none>" } else { $last | ConvertTo-Json -Depth 20 -Compress }
    $errorDetail = if ([string]::IsNullOrWhiteSpace($lastError)) { "<none>" } else { $lastError }
    throw "$Checkpoint 未在 $TimeoutSeconds 秒内达成双端屏障；last=$detail；lastRpcError=$errorDetail"
}

function Start-AcceptanceGame {
    param(
        [Parameter(Mandatory)][string]$Role,
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][string]$FastMpArguments
    )

    $previousMcpPort = [Environment]::GetEnvironmentVariable("KITLIB_MCP_PORT", "Process")
    $previousRunId = [Environment]::GetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID", "Process")
    $previousRole = [Environment]::GetEnvironmentVariable("DT_ACCEPTANCE_ROLE", "Process")
    try {
        # Godot 的进程启动链会在部分运行路径重建环境块。同步写入当前环境与 StartInfo，
        # 保证第二个实例不会继承 Host 的 9877。
        [Environment]::SetEnvironmentVariable("KITLIB_MCP_PORT", "$Port", "Process")
        [Environment]::SetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID", $runId, "Process")
        [Environment]::SetEnvironmentVariable("DT_ACCEPTANCE_ROLE", $Role, "Process")

        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $gameExe
        $startInfo.WorkingDirectory = $GamePath
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.Arguments = "--rendering-driver opengl3 --audio-driver Dummy --resolution 640x360 --position 0,0 --max-fps $TestFps --log-file=`"$LogPath`" $FastMpArguments"
        $startInfo.EnvironmentVariables["KITLIB_MCP_PORT"] = "$Port"
        $startInfo.EnvironmentVariables["DT_ACCEPTANCE_RUN_ID"] = $runId
        $startInfo.EnvironmentVariables["DT_ACCEPTANCE_ROLE"] = $Role
        $process = [System.Diagnostics.Process]::Start($startInfo)
        if ($null -eq $process) { throw "$Role 游戏进程启动失败。" }
        $process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
        return $process
    }
    finally {
        [Environment]::SetEnvironmentVariable("KITLIB_MCP_PORT", $previousMcpPort, "Process")
        [Environment]::SetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID", $previousRunId, "Process")
        [Environment]::SetEnvironmentVariable("DT_ACCEPTANCE_ROLE", $previousRole, "Process")
    }
}

function Get-LocalTravelerSnapshot {
    param([Parameter(Mandatory)]$State, [Parameter(Mandatory)][long]$NetId)
    return @($State.extensions.dimensionalTravelerTest.travelers | Where-Object {
        [long]$_.playerNetId -eq $NetId
    })[0]
}

$runManifest = [ordered]@{
    runId = $runId
    createdAt = [DateTimeOffset]::Now.ToString("o")
    mode = "real_dual_process_host_drive"
    hostBridgePort = $HostBridgePort
    clientBridgePort = $ClientBridgePort
    hostViewerPort = 9878
    hostEndpoint = $hostEndpoint
    clientEndpoint = $clientEndpoint
    hostLog = $hostLog
    clientLog = $clientLog
    status = "created"
}
Write-AtomicJson -Path $runPath -Value $runManifest
Write-AtomicJson -Path $finalPath -Value ([ordered]@{
    runId = $runId
    passed = $false
    status = "running"
    checkedAt = [DateTimeOffset]::Now.ToString("o")
})

try {
    if (-not (Test-Path $gameExe)) { throw "找不到游戏程序：$gameExe" }
    $existing = @(Get-Process -Name "SlayTheSpire2" -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        throw "检测到已有 SlayTheSpire2 进程 PID=$($existing.Id -join ', ')；拒绝启动双进程验收。"
    }

    if (-not $SkipBuild) {
        & (Join-Path $projectRoot "build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "正式 Mod 构建部署失败。" }
        & (Join-Path $projectRoot "test-adapter\build.ps1") -Configuration $Configuration -Sts2GamePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw "测试适配器构建部署失败。" }
    }

    $hostProcess = Start-AcceptanceGame -Role "host" -Port $HostBridgePort -LogPath $hostLog `
        -FastMpArguments "--fastmp=host_standard"
    $runManifest.hostPid = $hostProcess.Id
    $runManifest.status = "host_process_started"
    Write-AtomicJson -Path $runPath -Value $runManifest

    # `fastmp=join` 只尝试一次 ENet 连接。固定等待八秒会在 Host 尚未完成场景加载时
    # 让 Client 无意义超时，因此用 Host 的实际 MCP 与 LAN 大厅状态作为启动屏障。
    Wait-Bridge -Endpoint $hostEndpoint -Process $hostProcess -Role "Host"
    $hostHandshake = Invoke-GameTool -Endpoint $hostEndpoint -Name "dimensional_traveler_test_session" `
        -Arguments @{ action = "handshake" }
    Assert-Equal $runId $hostHandshake.runId "Host runId 不一致"
    Assert-Equal $hostProcess.Id ([int]$hostHandshake.processId) "Host PID 不一致"
    Assert-Equal $HostBridgePort ([int]$hostHandshake.mcpPort) "Host MCP 端口不一致"
    $runManifest.hostHandshake = $hostHandshake
    $runManifest.status = "host_bridge_ready"
    Write-AtomicJson -Path $runPath -Value $runManifest

    $hostLobbyBeforeJoin = Wait-ToolResult -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "inspect_lan_lobby" } -Process $hostProcess -Role "Host" -TimeoutSeconds 120 `
        -Predicate { param($value) $value.ok -and $value.netType -eq "Host" -and [int]$value.playerCount -eq 1 }
    $runManifest.hostLobbyBeforeJoin = $hostLobbyBeforeJoin
    $runManifest.status = "host_lan_ready"
    Write-AtomicJson -Path $runPath -Value $runManifest

    $clientProcess = Start-AcceptanceGame -Role "client" -Port $ClientBridgePort -LogPath $clientLog `
        -FastMpArguments "--fastmp=join --clientId=1001"
    $runManifest.clientPid = $clientProcess.Id
    $runManifest.status = "client_process_started"
    Write-AtomicJson -Path $runPath -Value $runManifest

    Wait-Bridge -Endpoint $clientEndpoint -Process $clientProcess -Role "Client"
    $clientHandshake = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_session" `
        -Arguments @{ action = "handshake" }
    Assert-Equal $runId $clientHandshake.runId "Client runId 不一致"
    Assert-Equal $clientProcess.Id ([int]$clientHandshake.processId) "Client PID 不一致"
    Assert-Equal $ClientBridgePort ([int]$clientHandshake.mcpPort) "Client MCP 端口不一致"
    $runManifest.clientHandshake = $clientHandshake
    $runManifest.status = "bridges_ready"
    Write-AtomicJson -Path $runPath -Value $runManifest

    $hostLobby = Wait-ToolResult -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "inspect_lan_lobby" } -Process $hostProcess -Role "Host" -TimeoutSeconds 120 `
        -Predicate { param($value) $value.ok -and [int]$value.playerCount -eq 2 }
    $clientLobby = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "inspect_lan_lobby" } -Process $clientProcess -Role "Client" -TimeoutSeconds 120 `
        -Predicate { param($value) $value.ok -and [int]$value.playerCount -eq 2 }
    Assert-Equal "Host" $hostLobby.netType "Host 网络类型错误"
    Assert-Equal "Client" $clientLobby.netType "Client 网络类型错误"

    $clientReady = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "select_lan_traveler_and_ready" }
    Assert-True $clientReady.ok "Client 选择次元旅人失败：$(Get-ToolError $clientReady)"
    $hostReady = Invoke-GameTool -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "select_lan_traveler_and_ready" }
    Assert-True $hostReady.ok "Host 选择次元旅人失败：$(Get-ToolError $hostReady)"

    $hostState = Wait-ToolResult -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" -Arguments @{ action = "capture_lan_snapshot" } `
        -Process $hostProcess -Role "Host" -TimeoutSeconds 120 -Predicate {
            param($value)
            $travelers = @($value.extensions.dimensionalTravelerTest.travelers)
            $travelers.Count -eq 2 -and @($travelers | Where-Object { $_.characterId -eq "DIMENSIONAL_TRAVELER_CHARACTER_TRAVELER" }).Count -eq 2
        }
    $clientState = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" -Arguments @{ action = "capture_lan_snapshot" } `
        -Process $clientProcess -Role "Client" -TimeoutSeconds 120 -Predicate {
            param($value)
            $travelers = @($value.extensions.dimensionalTravelerTest.travelers)
            $travelers.Count -eq 2 -and @($travelers | Where-Object { $_.characterId -eq "DIMENSIONAL_TRAVELER_CHARACTER_TRAVELER" }).Count -eq 2
        }

    # 双进程验收必须由运行器独占非战斗 UI。KitLib 的 LAN 自动器会独立驱动两端事件/地图，
    # 使一端离开初始事件时另一端仍在选择，从而在 checksum 0 前制造竞态。
    foreach ($endpointRole in @(
        @{ Endpoint = $hostEndpoint; Process = $hostProcess; Role = "Host" },
        @{ Endpoint = $clientEndpoint; Process = $clientProcess; Role = "Client" }
    )) {
        $autoDriver = Invoke-GameTool -Endpoint $endpointRole.Endpoint -Name "dimensional_traveler_test_control" `
            -Arguments @{ action = "disable_lan_auto_driver" }
        Assert-True $autoDriver.ok "$($endpointRole.Role) 禁用 LAN 自动器失败：$(Get-ToolError $autoDriver)"
        Assert-True $autoDriver.autoDriverDisabled "$($endpointRole.Role) 未确认关闭 LAN 自动器"
    }

    # Neow 奖励链路在真实双端环境中会异步重放每名玩家的奖励。该验收的职责是药水托管、
    # 原生三选一与最终同步，不应把独立的 Neow 奖励任务带入首个 combat checksum。
    # 两端确认进入初始事件后，直接调用原生 Proceed 打开地图，保留地图投票和后续战斗路径。
    $eventReady = Wait-LanRunPair -Checkpoint "event_ready" -TimeoutSeconds 90 `
        -Predicate {
            param($hostRun, $clientRun)
            $hostRun.ok -and $clientRun.ok -and
                $hostRun.roomType -eq "Event" -and $clientRun.roomType -eq "Event"
        } -RejectCombatAsymmetry
    foreach ($endpointRole in @(
        @{ Endpoint = $hostEndpoint; Process = $hostProcess; Role = "Host" },
        @{ Endpoint = $clientEndpoint; Process = $clientProcess; Role = "Client" }
    )) {
        $eventProceed = Wait-ToolResult -Endpoint $endpointRole.Endpoint -Name "dimensional_traveler_test_control" `
            -Arguments @{ action = "proceed_lan_event" } `
            -Process $endpointRole.Process -Role $endpointRole.Role -TimeoutSeconds 45 `
            -Predicate { param($value) $value.ok -and $value.proceeded }
    }
    # Proceed 只打开地图 UI；CurrentRoom 会在首次地图投票后才真正离开 Event。
    $mapReady = Wait-LanRunPair -Checkpoint "map_ready" -TimeoutSeconds 90 `
        -Predicate {
            param($hostRun, $clientRun)
            $hostRun.ok -and $clientRun.ok -and
                $hostRun.mapOpen -and $clientRun.mapOpen -and
                -not $hostRun.combatActive -and -not $clientRun.combatActive
        } -RejectCombatAsymmetry

    # 地图前进需要所有玩家投票。由两端分别执行原生点击，并等待共享房间在双端进入 Play，
    # 不再使用自动地图投票或只观察 Host 的局部 UI 状态。
    foreach ($endpointRole in @(
        @{ Endpoint = $clientEndpoint; Process = $clientProcess; Role = "Client" },
        @{ Endpoint = $hostEndpoint; Process = $hostProcess; Role = "Host" }
    )) {
        $enter = Wait-ToolResult -Endpoint $endpointRole.Endpoint -Name "map_action" `
            -Arguments @{ action = "select_map_node"; target_index = 0 } `
            -Process $endpointRole.Process -Role $endpointRole.Role -TimeoutSeconds 90 `
            -Predicate { param($value) $value.success }
    }

    $combatReady = Wait-LanRunPair -Checkpoint "combat_ready" -TimeoutSeconds 90 `
        -Predicate {
            param($hostRun, $clientRun)
            $hostRun.ok -and $clientRun.ok -and
                $hostRun.roomType -eq "Monster" -and $clientRun.roomType -eq "Monster" -and
                $hostRun.combatActive -and $clientRun.combatActive
        } -RejectCombatAsymmetry

    # 托管注入动作受 CombatPlayPhaseOnly 限制。共享战斗建立后仍可能处于敌方/过场阶段，
    # 必须等 Client 的本地 PlayerTurnPhase.Play 才允许提交药水请求。
    $clientPlayReady = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" `
        -Arguments @{ action = "capture_lan_snapshot" } -Process $clientProcess -Role "Client" -TimeoutSeconds 60 `
        -Predicate {
            param($value)
            $value.ok -and $value.combat.isPlayPhaseActive
        }
    $clientNetId = [long]$clientLobby.localNetId
    # 测试数据由客机本地所有者请求托管动作；Host 只作为排序权威并观察同一同步结果。
    $clientGrant = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" -Arguments @{
        action = "grant_native_potion"
        potion_id = "ATTACK_POTION"
    }
    Assert-True $clientGrant.ok "Client 建立本地源药水失败：$(Get-ToolError $clientGrant)"
    Assert-Equal "requested" $clientGrant.status "Client 药水注入请求未受理"

    $grantSubmitted = Wait-LanRunPair -Checkpoint "combat_after_grant_request" -TimeoutSeconds 15 `
        -Predicate {
            param($hostRun, $clientRun)
            $hostRun.ok -and $clientRun.ok -and
                $hostRun.combatActive -and $clientRun.combatActive
        } -RejectCombatAsymmetry

    $clientSource = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" -Arguments @{ action = "capture_lan_snapshot" } `
        -Process $clientProcess -Role "Client" -TimeoutSeconds 30 -Predicate {
            param($value)
            $owner = Get-LocalTravelerSnapshot -State $value -NetId $clientNetId
            if ($null -eq $owner) { return $false }
            @($owner.testPotionGrants | Where-Object {
                $_.potionId -eq "ATTACK_POTION" -and $_.stage -eq "Committed" -and $null -ne $_.slotIndex
            }).Count -eq 1
        }
    $clientSourceOwner = Get-LocalTravelerSnapshot -State $clientSource -NetId $clientNetId
    $sourceSlotIndex = [int]@($clientSourceOwner.testPotionGrants | Where-Object {
        $_.potionId -eq "ATTACK_POTION" -and $_.stage -eq "Committed"
    })[0].slotIndex
    Assert-Equal "ATTACK_POTION" $clientSourceOwner.nativePotions.slots[$sourceSlotIndex].id "Client 源药水槽位错误"

    $caseStartedAt = [DateTimeOffset]::UtcNow
    $extract = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" -Arguments @{
        action = "extract_native_potion"
        potion_slot_index = $sourceSlotIndex
    }
    Assert-True $extract.ok "Client 发起攻击药水萃取失败：$(Get-ToolError $extract)"
    Assert-Equal "awaiting_choice" $extract.status "Client 未进入原生选择阶段"

    # 受管动作由 Host 权威队列执行，但原生 `CardSelectCmd` 对动作所有者 player 1001 判定为本地时，
    # 选择 UI 会在 Client 出现；Host 则暂停队列等待该远端 choice。
    $selection = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_selection" `
        -Arguments @{ action = "get" } -Process $clientProcess -Role "Client" -TimeoutSeconds 30 `
        -Predicate { param($value) $value.ok -and $value.selection.active -and $value.selection.ready }
    Assert-Equal 3 @($selection.selection.candidates).Count "Client 攻击药水候选数错误"
    $chosen = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_selection" `
        -Arguments @{ action = "select"; candidate_index = 0 }
    Assert-True $chosen.ok "Client 提交攻击药水选择失败：$(Get-ToolError $chosen)"

    $hostFinal = Wait-ToolResult -Endpoint $hostEndpoint -Name "dimensional_traveler_test_control" -Arguments @{ action = "capture_lan_snapshot" } `
        -Process $hostProcess -Role "Host" -TimeoutSeconds 45 -Predicate {
            param($value)
            $owner = Get-LocalTravelerSnapshot -State $value -NetId $clientNetId
            $null -ne $owner -and $owner.backpack.count -eq 1 -and
                @($owner.nativePotions.slots | Where-Object { $null -ne $_ }).Count -eq 0
        }
    $clientFinal = Wait-ToolResult -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" -Arguments @{ action = "capture_lan_snapshot" } `
        -Process $clientProcess -Role "Client" -TimeoutSeconds 45 -Predicate {
            param($value)
            $owner = Get-LocalTravelerSnapshot -State $value -NetId $clientNetId
            $null -ne $owner -and $owner.backpack.count -eq 1 -and
                @($owner.nativePotions.slots | Where-Object { $null -ne $_ }).Count -eq 0
        }

    $hostOwner = Get-LocalTravelerSnapshot -State $hostFinal -NetId $clientNetId
    $clientOwner = Get-LocalTravelerSnapshot -State $clientFinal -NetId $clientNetId
    $expectedDiffusion = [int]$clientSourceOwner.principles.diffusion.amount + 1
    $expectedVolatility = [int]$clientSourceOwner.principles.volatility.amount + 2
    Assert-Equal $expectedDiffusion $hostOwner.principles.diffusion.amount "Host 快照中的客机扩散收益错误"
    Assert-Equal $expectedVolatility $hostOwner.principles.volatility.amount "Host 快照中的客机挥发收益错误"
    Assert-Equal $expectedDiffusion $clientOwner.principles.diffusion.amount "Client 本地扩散收益错误"
    Assert-Equal $expectedVolatility $clientOwner.principles.volatility.amount "Client 本地挥发收益错误"
    Assert-Equal $hostOwner.backpack.cards[0].cardId $clientOwner.backpack.cards[0].cardId "双方萃取产物不一致"
    Assert-Equal "Extracted" $hostOwner.backpack.cards[0].origin "Host 萃取产物来源错误"
    Assert-Equal "Extracted" $clientOwner.backpack.cards[0].origin "Client 萃取产物来源错误"
    Assert-Equal ($hostOwner.rng | ConvertTo-Json -Compress) ($clientOwner.rng | ConvertTo-Json -Compress) `
        "双方最终战斗 RNG 快照不一致"

    $availableGameLogs = @($hostLog, $clientLog | Where-Object { Test-Path $_ })
    $stateDivergence = $availableGameLogs.Count -gt 0 -and
        (Select-String -Path $availableGameLogs -Pattern "State divergence|ChecksumTracker.LogStateDivergence" -Quiet)
    Assert-True (-not $stateDivergence) "真实双进程日志出现状态分歧。"

    $caseResult = [ordered]@{
        suite = "real-coop-extraction"
        name = "client-owned-attack-extraction-converges-across-host-and-client"
        passed = $true
        durationMs = [int]([DateTimeOffset]::UtcNow - $caseStartedAt).TotalMilliseconds
        evidence = [ordered]@{
            hostPid = $hostProcess.Id
            clientPid = $clientProcess.Id
            ownerNetId = $clientNetId
            product = $clientOwner.backpack.cards[0].cardId
            origin = $clientOwner.backpack.cards[0].origin
            finalRngConsistent = $true
        }
    }
    Write-CaseResult -Value $caseResult

    $runManifest.status = "completed"
    $runManifest.hostHandshake = $hostHandshake
    $runManifest.clientHandshake = $clientHandshake
    Write-AtomicJson -Path $runPath -Value $runManifest
    Write-AtomicJson -Path $finalPath -Value ([ordered]@{
        runId = $runId
        passed = $true
        status = "completed"
        testCount = 1
        passedCount = 1
        failedCount = 0
        host = @{ pid = $hostProcess.Id; endpoint = $hostEndpoint; log = $hostLog }
        client = @{ pid = $clientProcess.Id; endpoint = $clientEndpoint; log = $clientLog }
        tests = @($caseResult)
        checkedAt = [DateTimeOffset]::Now.ToString("o")
        sessionDirectory = $sessionDir
    })
    Write-Host "真实双进程萃取验收完成：1/1 通过"
    Write-Host "报告：$finalPath"
}
catch {
    $errorMessage = $_.Exception.Message
    $failureCategory = if ($null -eq $caseStartedAt) { "infrastructure" } else { "verification" }
    if ($null -ne $clientProcess) {
        try {
            $clientProcess.Refresh()
            if (-not $clientProcess.HasExited) {
                $failureEvidence = [ordered]@{
                    clientSnapshot = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_control" `
                        -Arguments @{ action = "capture_lan_snapshot" } -TimeoutSeconds 5
                    clientSelection = Invoke-GameTool -Endpoint $clientEndpoint -Name "dimensional_traveler_test_selection" `
                        -Arguments @{ action = "get" } -TimeoutSeconds 5
                }
            }
        }
        catch {
            $failureEvidence = [ordered]@{
                captureError = $_.Exception.Message
            }
        }
    }
    if ($null -ne $caseStartedAt) {
        Write-CaseResult -Value ([ordered]@{
            suite = "real-coop-extraction"
            name = "client-owned-attack-extraction-converges-across-host-and-client"
            passed = $false
            durationMs = [int]([DateTimeOffset]::UtcNow - $caseStartedAt).TotalMilliseconds
            error = $errorMessage
        })
    }
    $runManifest.status = "failed"
    $runManifest.failureCategory = $failureCategory
    $runManifest.error = $errorMessage
    Write-AtomicJson -Path $runPath -Value $runManifest
    Write-AtomicJson -Path $finalPath -Value ([ordered]@{
        runId = $runId
        passed = $false
        status = if ($failureCategory -eq "infrastructure") { "infrastructure_failure" } else { "failed" }
        failureCategory = $failureCategory
        error = $errorMessage
        failureEvidence = $failureEvidence
        hostPid = if ($null -eq $hostProcess) { $null } else { $hostProcess.Id }
        clientPid = if ($null -eq $clientProcess) { $null } else { $clientProcess.Id }
        hostLog = $hostLog
        clientLog = $clientLog
        checkedAt = [DateTimeOffset]::Now.ToString("o")
        sessionDirectory = $sessionDir
    })
    Write-Error $errorMessage
    throw
}
finally {
    foreach ($process in @($clientProcess, $hostProcess)) {
        if ($null -eq $process) { continue }
        try {
            $process.Refresh()
            if (-not $process.HasExited) {
                $process.Kill()
                $process.WaitForExit(10000)
            }
        }
        catch { }
        $process.Dispose()
    }
}