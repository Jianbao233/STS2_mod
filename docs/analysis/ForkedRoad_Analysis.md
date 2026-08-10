# Forked Road 代码分析报告

> 来源仓库：[Snoivyel/STS2-Forked-Road](https://github.com/Snoivyel/STS2-Forked-Road)
> 分析日期：2026-04-04
> 版本参考：v1.0.3

---

## 1. 项目概览

**Forked Road** 是一款《杀戮尖塔2》联机玩法模组，核心功能是：当联机玩家将地图投票投向不同节点时，不再强行覆盖少数票，而是按投票结果将路线拆分为多个分支，逐个结算各分支的战斗/房间后，再重新汇合回到共享地图流程。

- **语言**：C# (.NET 10 / Godot 4.5)
- **主要技术栈**：Harmony（热补丁）+ Godot GDScript 引擎接口 + 自定义网络消息
- **MIT 协议**
- **文件规模**：约 3,434 行补丁代码 + 1,641 行管理器代码 + 8 个网络消息结构体

---

## 2. 整体架构

### 2.1 文件结构

```
src/
  ForkedRoadEntry.cs          # 模组入口，注册 Harmony patch
  ForkedRoadManager.cs        # 核心状态机与分支逻辑（1641 行）
  ForkedRoadPatches.cs        # 所有 Harmony Patch 汇总（3434 行）
  ForkedRoadSavedState.cs     # 存档数据结构
  ForkedRoadBranchStartMessage.cs
  ForkedRoadBranchContinueMessage.cs
  ForkedRoadBranchCompleteMessage.cs
  ForkedRoadBranchMergeMessage.cs
  ForkedRoadBranchMergeResolvedMessage.cs
  ForkedRoadMerchantSceneReadyMessage.cs
  ForkedRoadSaveStateMessage.cs
```

### 2.2 核心类职责划分

| 文件 | 职责 |
|------|------|
| `ForkedRoadEntry` | 初始化 Harmony，注册所有 patch 到程序集，调用 `ScriptManagerBridge.LookupScriptsInAssembly` 注册 GDScript 脚本 |
| `ForkedRoadManager` | 管理所有分支状态（`_splitBatchInProgress`、`_activeGroup`、`PendingGroups`），处理分支开始/继续/完成/合并的网络消息，协调 `ForkedRoadPatches` 中的逻辑 |
| `ForkedRoadPatches` | 所有对游戏原类方法的 Harmony Patch，通过 `ForkedRoadManager` 提供的状态判断来改变游戏行为 |

### 2.3 入口注册流程

```csharp
[ModInitializer("Init")]
public static class ForkedRoadEntry
{
    private static Harmony? _harmony;
    public static void Init()
    {
        _harmony ??= new Harmony("sts2.snoivyel.forkedroad");
        _harmony.PatchAll(typeof(ForkedRoadEntry).Assembly);
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(ForkedRoadEntry).Assembly);
    }
}
```

关键点：
- 使用 `[ModInitializer("Init")]` 声明入口，游戏启动时自动调用
- `Harmony.PatchAll` 一次性注册所有 patch
- `ScriptManagerBridge.LookupScriptsInAssembly` 注册 GDScript 脚本（用于 UI 节点相关逻辑）

---

## 3. 核心状态机设计（ForkedRoadManager）

### 3.1 关键状态字段

```csharp
private static bool _splitBatchInProgress;           // 当前是否处于分叉批次中
private static BranchGroup? _activeGroup;             // 当前正在处理的分支
private static readonly Queue<BranchGroup> PendingGroups;  // 等待处理的分支队列
private static int _activeBranchSequence;             // 当前分支序号（从1递增）
private static int _remainingBranchesAfterCurrent;    // 当前分支之后剩余分支数
private static readonly HashSet<ulong> ReadyPlayers;  // 已准备好的玩家集合
private static readonly Dictionary<ulong, MapCoord> PlayerMergeTargets;  // 待合并玩家→目标分支坐标
private static bool _branchEndedByMerge;             // 当前分支是否以合并方式结束
```

### 3.2 BranchGroup 内部类

```csharp
private sealed class BranchGroup
{
    public required MapCoord Coord { get; init; }     // 该分支的目标坐标
    public required ulong[] PlayerIds { get; init; } // 进入该分支的玩家ID列表
}
```

### 3.3 分叉决策流程

```
玩家投票
    │
    ▼
TryHandleVote() — 判断所有票是否一致
    │
    ├─ 票完全一致 → 正常 MoveToMapCoord
    │
    ├─ ShouldSplitVotes() → 有多个不同目标坐标 → BeginSplitBatch(forceSingleGroupBatch=false)
    │
    └─ ShouldUseMergeBatch() → 同一目标坐标但来自不同起始点 → BeginSplitBatch(forceSingleGroupBatch=true)
```

### 3.4 分支处理流程

```
BeginSplitBatch()
    │ 按目标坐标分组（Linq GroupBy）
    │ 记录每个分支的人数 BranchPlayerCounts
    │ 全部入队 PendingGroups
    │ 若分组>1 或 forceSingleGroupBatch=true → 启动分叉
    │
    ▼
SendNextBranchStartMessage() — 仅Host执行
    │ Dequeue 下一个分支
    │ 广播 ForkedRoadBranchStartMessage
    │ HandleBranchStartMessage() 在所有端执行
    │
    ▼
HandleBranchStartMessage()
    │ 设置 _activeGroup
    │ 记录各玩家坐标 PlayerCoords
    │ 调用 PrepareAndBeginBranchAsync()
    │
    ▼
PrepareAndBeginBranchAsync()
    │ 检查是否有合并过来的死亡玩家 → ReviveBeforeCombatEnd()
    │ 若死亡玩家是本地玩家 → 直接进入房间
    │ 否则 → BeginBranchWhenReadyAsync()
    │
    ▼
BeginBranchWhenReadyAsync()
    │ 商店/宝箱 → 直接进入共享场景
    │ 战斗 → 打开地图屏幕 → TravelToMapCoord → EnterMapCoord
    │
    ▼
房间结算完成 → NotifyLocalTerminalProceed()
    │
    ▼
MarkPlayerReady() → 若所有玩家就绪 → TryAdvanceBranch()
    │
    ▼
TryAdvanceBranch()
    │ PendingGroups 还有 → SendNextBranchStartMessage()
    │ 否则 → 广播 ForkedRoadBranchCompleteMessage
    │
    ▼
CompleteBatchLocal() — 所有分支完成
    │ 处理剩余合并目标
    │ 恢复本地玩家位置 RestoreLocalRunLocation()
    │ 重置所有状态
    │ 打开地图屏幕
```

---

## 4. 补丁系统详解（ForkedRoadPatches）

### 4.1 补丁分类统计

全部 40+ 个 Harmony Patch，分为以下类别：

#### 4.1.1 地图投票相关（4 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `MapSelectionSynchronizer_PlayerVoted_Patch` | `PlayerVotedForMapCoord` | 拦截投票，交给 Manager 的 `TryHandleVote` 处理 |
| `NMapScreen_OnPlayerVoteChanged_Patch` | `OnPlayerVoteChanged` | 旁观玩家投票变化时，使用各自分支坐标而非全局坐标 |
| `NMapPoint_ShouldDisplayPlayerVote_Patch` | `ShouldDisplayPlayerVote` | 投票显示使用分支坐标匹配 |
| `NMapScreen_OnMapPointSelectedLocally_Patch` | `OnMapPointSelectedLocally` | 禁止旁观玩家手动选点 |

#### 4.1.2 地图屏幕渲染相关（4 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `NMapScreen_RecalculateTravelability_Patch` | `RecalculateTravelability` | 仅当前分支玩家的坐标标记为已访问；计算可行路径时基于本地玩家坐标 |
| `NMapScreen_Open_Patch` | `Open` | 打开时将地图滚动到本地玩家所在行；将地图标记设置到本地玩家坐标点 |
| `MapSplitVoteAnimation_TryPlay_Patch` | `TryPlay` | 分叉批次进行中时跳过分裂投票动画 |

#### 4.1.3 战斗旁观相关（13+ 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `NCombatRoom_OnCombatSetUp_Patch` | `OnCombatSetUp` | 旁观分支玩家跳过战斗设置 |
| `NCombatRoom_OnActiveScreenUpdated_Patch` | `OnActiveScreenUpdated` | 旁观者跳过屏幕更新 |
| `NCombatUi_Enable/Disable/AnimOut_Patch` | `Enable/Disable/AnimOut` | 旁观者不启用战斗UI |
| `NEndTurnButton_*` (8个) | `Initialize/OnTurnStarted/CallReleaseLogic` 等 | 旁观者的结束回合按钮不响应 |
| `NCreature_OnFocus/OnUnfocus_Patch` | `OnFocus/OnUnfocus` | 仅本地玩家在场的生物显示焦点 |
| `CombatManager_AfterAllPlayersReady_Patch` | `AfterAllPlayersReadyToEndTurn` | 旁观者执行 `RunAsSpectatorAsync` |
| `CombatManager_EndCombatInternal_Patch` | `EndCombatInternal` | 旁观者执行 `EndCombatAsSpectatorAsync`（完整战斗结算但不通知分支就绪） |

#### 4.1.4 校验和同步相关（2 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `ChecksumTracker_OnReceivedChecksumDataMessage_Patch` | `OnReceivedChecksumDataMessage` | 忽略不属于当前活跃分支玩家的校验和数据 |
| `ChecksumTracker_OnReceivedStateDivergenceMessage_Patch` | `OnReceivedStateDivergenceMessage` | 同上 |

> **设计意图**：旁观玩家的本地战斗状态（无敌人、无行动）与活跃玩家不同，会产生不同的校验和。若不忽略，联机校验系统会误判为状态分歧。

#### 4.1.5 死亡/合并相关（3 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `CombatManager_StartTurn_Patch` | `StartTurn` | 检测支线分支全员死亡时，立即触发合并 |
| `CombatManager_HandlePlayerDeath_Patch` | `HandlePlayerDeath` | 标记玩家待合并到其他分支 |
| `CreatureCmd_Kill_Patch` | `Kill` | 在 Kill 命令后检查是否满足支线团灭条件 |

#### 4.1.6 商店/宝箱/事件节点相关（8+ 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `NMerchantRoom_Ready_Patch` | `_Ready` | 旁观者禁用按钮；活跃玩家广播场景就绪 |
| `NMerchantRoom_OnActiveScreenUpdated_Patch` | `OnActiveScreenUpdated` | 旁观者始终禁用控件 |
| `NTreasureRoom_OnChestButtonReleased_Patch` | `OnChestButtonReleased` | 旁观者禁止开箱 |
| `NTreasureRoom_OnProceedButtonPressed_Patch` | `OnProceedButtonPressed` | 活跃玩家进入宝箱后直接返回地图 |
| `NTreasureRoomRelicCollection_DefaultFocusedControl_Patch` | `get_DefaultFocusedControl` | 宝箱旁观者焦点处理 |
| `NTreasureRoomRelicSynchronizer_PickRelicLocally_Patch` | `PickRelicLocally` | 分叉期间宝箱拾取条件判断 |
| `NEventRoom_Proceed_Patch` | `Proceed` | 事件旁观者直接返回地图 |
| `EventSynchronizer_BeginEvent_Patch` | `BeginEvent` | 修复无效事件状态 |

#### 4.1.7 存档相关（3 个）

| 补丁类 | 目标方法 | 行为 |
|--------|---------|------|
| `RunSaveManager_SaveRun_Patch` | `SaveRun` | 在存档JSON中嵌入 ForkedRoad 分支状态 |
| `RunSaveManager_LoadAndCanonicalizeMultiplayerRunSave_Patch` | `LoadAndCanonicalizeMultiplayerRunSave` | 读取存档时恢复 ForkedRoad 状态 |
| `RunManager_Launch_Patch` | `Launch` | 通知 Manager 处理存档恢复后的分支同步 |

### 4.2 反射访问私有成员的技巧

项目中大量使用 Harmony 的 `AccessTools` 读取/修改私有字段和方法：

```csharp
// 字段引用（快速读取）
internal static readonly AccessTools.FieldRef<MapSelectionSynchronizer, RunState> MapSelectionRunStateRef =
    AccessTools.FieldRefAccess<MapSelectionSynchronizer, RunState>("_runState");

// 方法引用
private static readonly MethodInfo CombatManagerWaitUntilQueueEmptyMethod =
    AccessTools.Method(typeof(CombatManager), "WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction")!;

// 调用私有方法
CombatManagerWaitUntilQueueEmptyMethod.Invoke(manager, Array.Empty<object>());
```

---

## 5. 网络消息设计

### 5.1 消息类型一览

| 消息结构体 | 用途 | 关键字段 |
|-----------|------|---------|
| `ForkedRoadBranchStartMessage` | 通知开始一个分支 | `coord`, `branchSequence`, `remainingBranches`, `playerIds` |
| `ForkedRoadBranchContinueMessage` | 玩家准备就绪（进入下一分支） | `branchSequence` |
| `ForkedRoadBranchCompleteMessage` | 批次所有分支完成 | — |
| `ForkedRoadBranchMergeMessage` | 玩家死亡待合并（目标坐标） | `playerId`, `targetCoord` |
| `ForkedRoadBranchMergeResolvedMessage` | 分支合并确认 | `branchSequence`, `coord` |
| `ForkedRoadMerchantSceneReadyMessage` | 商店场景就绪（旁观者切场景） | `branchSequence` |
| `ForkedRoadSaveStateMessage` | 存档状态同步（Host→Client） | `stateJson` |

### 5.2 消息处理注册模式

所有 Handler 在 `InitializeForRun` 中注册：

```csharp
if (!_branchStartHandlerRegistered)
{
    _netService.RegisterMessageHandler<ForkedRoadBranchStartMessage>(HandleBranchStartMessage);
    _branchStartHandlerRegistered = true;
}
```

Handler 注册后永不注销（除非 `_netService` 引用改变），保证消息始终可接收。

---

## 6. 支线合并（Branch Merge）机制

当支线分支中所有玩家死亡时，触发自动合并：

```
支线分支玩家全部死亡检测
    │
    ▼
ResolveSupportBranchDeathAsync()
    │ 强制结束战斗（设置 IsInProgress=false）
    │ 标记所有死亡玩家待合并
    │ 广播 BranchMergeResolvedMessage
    │
    ▼
HandleBranchMergeResolvedAsync()
    │ 切换到地图屏幕
    │ 复活玩家
    │ 通知终端继续
    │
    ▼
CompleteBatchLocal()
    │ 将合并玩家坐标更新到目标分支坐标
    │ 加入 PlayersFollowingMergedBranch 锁定列表
    │ 重置所有分支状态
```

合并时目标分支选择策略（`TryGetMergeTargetCoord`）：
1. 优先选择存活玩家最多的分支
2. 其次选择初始进入人数最多的分支
3. 最后按坐标 row/col 排序取第一个

---

## 7. 旁观者（Spectator）流程

旁观者指未进入当前活跃分支的玩家。其流程设计原则：

| 场景 | 旁观者行为 |
|------|----------|
| 战斗房间 | 显示战斗背景但无敌人、无UI、不参与操作 |
| 商店 | 显示镜像商店场景，禁用购买/离开按钮 |
| 宝箱 | 禁用开箱/离开按钮 |
| 事件 | 显示事件但禁用选项按钮 |
| 休息地 | 禁用休息选项 |
| 战斗结束 | 执行完整结算但不发送 `NotifyLocalTerminalProceed`（等待合并清理） |

---

## 8. 关键设计思想总结

### 8.1 状态驱动的 Patch 拦截

几乎所有 Patch 的前缀（Prefix）都以 `ForkedRoadManager.IsSplitBatchInProgress` 为第一判断条件。当该标志为 `false` 时，所有 Patch 直接 `return true`，游戏行为完全不受影响。这种方式使模组对非分叉场景零侵入。

### 8.2 Host 权威模式

分支启动消息 `ForkedRoadBranchStartMessage` 仅由 Host 发送（`_netService.Type != NetGameType.Client`），Client 仅接收和执行。所有分支排序、分组逻辑均在 Host 端计算后广播，保证一致性。

### 8.3 私有字段反射访问

游戏核心类（`MapSelectionSynchronizer`、`RunManager`、`CombatManager` 等）的内部状态通过 `AccessTools.FieldRefAccess` 和 `AccessTools.Method` 反射获取，而非依赖公开 API，保证了对游戏内部逻辑的精细控制。

### 8.4 存档状态嵌入

分叉状态序列化后嵌入游戏存档 JSON 的 `forked_road` 字段。读取存档时通过 `StageLoadedSaveState` 暂存，在 `RunManager.Launch` 时应用，保证存档加载后联机状态正确恢复。

### 8.5 校验和隔离

分叉期间旁观者战斗状态与活跃玩家完全不同，校验和会自然不同。通过 `ShouldIgnoreChecksumPeer` 过滤掉旁观者的校验和消息，避免触发联机状态分歧警告。

---

## 9. 可参考的代码模式

以下是该仓库中值得借鉴的具体代码实现模式：

### 9.1 反射访问私有字段（通用模式）

```csharp
// 只读字段引用
internal static readonly AccessTools.FieldRef<TClass, TField> FieldRef =
    AccessTools.FieldRefAccess<TClass, TField>("_fieldName");

// 调用私有方法
var method = AccessTools.Method(typeof(TClass), "MethodName", ...);
method.Invoke(instance, args);
```

### 9.2 消息处理注册（生命周期管理）

```csharp
private static bool _handlerRegistered = false;
public static void Initialize(INetGameService netService)
{
    if (!_handlerRegistered)
    {
        netService.RegisterMessageHandler<TMessage>(HandleMessage);
        _handlerRegistered = true;
    }
}
```

### 9.3 异步重试模式（存档写入）

```csharp
const int maxAttempts = 8;
for (int attempt = 1; ; attempt++)
{
    try { saveStore.WriteFileAsync(path, json); return; }
    catch (Exception ex) when (IsRetriableException(ex) && attempt < maxAttempts)
    {
        await Task.Delay(150 * attempt);
    }
}
```

### 9.4 旁观者屏幕切换（商店/宝箱）

```csharp
// 在旁观者端动态创建镜像场景
await MegaCrit.Sts2.Core.Assets.PreloadManager.LoadRoomMerchantAssets();
NMapScreen.Instance?.Close(animateOut: false);
NRun.Instance?.SetCurrentRoom(NMerchantRoom.Create(room, runState.Players));
```

### 9.5 存档状态嵌入/读取

```csharp
// 保存时嵌入
string baseJson = JsonSerializer.Serialize(serializableRun);
JsonObject root = JsonNode.Parse(baseJson) as JsonObject;
root["forked_road"] = JsonSerializer.SerializeToNode(forkedRoadState);

// 读取时恢复
JsonNode? node = root?["forked_road"];
ForkedRoadSavedState? state = node?.Deserialize<ForkedRoadSavedState>();
```

---

## 10. 对你的项目（杀戮尖塔2 Mod）的潜在参考价值

基于你的项目路径（`K:\杀戮尖塔mod制作\STS2_mod`）中已有的模组（如 `NoClientCheats`、`MultiplayerTools`、`MP_PlayerManager` 等），以下是 Forked Road 中对联机/多人玩法模组最有参考价值的部分：

1. **联机状态管理**：参考其 `ForkedRoadManager` 的状态字段设计（`_splitBatchInProgress` + `_activeGroup` + `PendingGroups`），为你的多人工具模组设计清晰的状态机
2. **Harmony Patch 模式**：特别是对战斗 UI、回合按钮、校验和处理的精细拦截方式
3. **网络消息注册**：如果你的模组需要在多人间同步数据，参考其 `INetMessage` + `INetGameService.RegisterMessageHandler` 模式
4. **存档嵌入**：如果模组需要保存联机相关的额外状态，可参考其 JSON 嵌入方式
5. **反射访问私有成员**：所有 `AccessTools.FieldRefAccess` 的使用方式可作为参考模板
