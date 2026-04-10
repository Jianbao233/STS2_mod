# NoClientCheats (NCC) 工作记忆 / 项目日志

> 最后更新: 2026-04-08
>
> ⚠️ **当前待复测问题**：用户最新日志显示，除真实作弊外，还存在两条误报链：一条是**弱 `PlayerChoice` 重复进入两次**导致 `_choiceCallCount` 被错误累加；另一条是**新局初始化 / 角色切换 / Ascender's Bane 注入阶段**被通用 `add_cards` 规则误判。— ✅ **已加弱 `PlayerChoice` 去重 + startup baseline warmup，待双机实测**
>
> **今日重大进展（2026-04-07）**：
> 1. 复查双端最新日志后确认，旧的 `Tried to sync player that has net ID ...` 已不再是主故障，新的黑屏根因是**NCC 回滚快照被同步到了错误的玩家对象**，在客机 `WaitForSync -> Player.SyncWithSerializedPlayer` 阶段触发 `Character changed for player ...`。已在 `ClientDiagnosticPatches.cs` 中改为：收到回滚包时登记“原始目标玩家 NetId”，并在 `SyncWithSerializedPlayer` 前按真实目标玩家重定向同步、跳过错误调用。
> 2. 黑屏修复实测通过后，新的 act 过渡不同步根因也已锁定：主机先通过 `RewardSynchronizer.HandleRewardObtainedMessage` 把奖励卡加入远程玩家活体对象，但 NCC 仍拿奖励前的 `_lastSerializablePlayer / _lastSyncState` 比较下一条 `SyncReceived`，于是把合法 `10 -> 11` 误判为 `add_cards(count=1 allowed=0)`。已在 `DeckSyncPatches.cs` 新增 `RewardObtainedMessagePostfix`，在主机收到合法 `RewardObtainedMessage(Card)` 后立刻重新 `ToSerializable()` 并调用 `SyncReceivedPatch.SetBaselineFromSerializable()` 推进基线，同时把 `_preCheatSnapshot` 一并刷新。
> 3. 最新一轮日志进一步确认：`RewardObtainedMessagePostfix` 在地图过渡阶段有时拿不到 `RunManager.State`，导致 live `Player` 解析失败，合法奖励仍可能漏刷基线；同时同一奖励消息会在短时间内重复进入。已继续在 `DeckSyncPatches.cs` 中加入 `(senderId, cardId, wasSkipped)` 短窗口去重，并在 live `Player` 为 null 时调用 `SyncReceivedPatch.AdvanceBaselineForRewardCardFallback()` 直接推进 `_lastSyncState / _lastSyncDeckSize` 的合法 `+1` 基线，专门消除这种“正常奖励后误弹窗”。
> 4. 最新回归日志又把黑屏路径重新钉死：客机在 `ClientDiagnosticPatches.OnSyncPlayerMessageReceived.Prefix` 已看到 `senderId != msgPlayer.NetId`，但因为 `localDeck/msgDeck` 都返回 `null`，仍把消息视作“正常自身同步”，于是原版 `OnSyncPlayerMessageReceived` 把回滚快照写进了错误的 `_syncData[senderId=HostNetId]`。随后 `WaitForSync` 用这份 Ironclad 回滚快照去同步主机的 Necrobinder 玩家，再次触发 `Character changed for player ...`。已在 `ClientDiagnosticPatches.cs` 改成：`senderId != msgPlayer.NetId` 且命中本地玩家时，直接视为显式 NCC 回滚包，优先对本地玩家执行 `SyncWithSerializedPlayer` 并跳过原版方法；同时 `GetLocalPlayer()` 的解析补到 `State / CurrentRun / RunState`，减少地图阶段取不到本地玩家的概率。
> 5. **host-only 最关键的新结论（2026-04-07 20:20）**：客机关闭 NCC 后，黑屏根因不是检测逻辑，而是**任何主机发往 vanilla 客机的 `SyncPlayerDataMessage` 回滚包都会被原版 `CombatStateSynchronizer` 按 `senderId=主机` 写进 `_syncData[Host]`，因此结构性不安全**。已在 `DeckSyncPatches.cs` 新增 `_TrySendRollbackToPeer()`，默认走 `host_only_no_client_capability`，即只保留主机侧 `_syncData / _lastSerializablePlayer` 权威回滚，不再默认发 wire rollback。
> 6. **进一步源码排查结论**：原版 `NetHostGameService.SendMessageToClientInternal(..., overrideSenderId)` 虽然允许主机私下伪装 `senderId`，但现有 vanilla 消息里没有一条能完整覆盖“未装 NCC 客机本地玩家的任意卡组回滚”。`RewardObtainedMessage/GoldLostMessage` 之类最多能补加卡/改金币；`CardRemovedMessage`、`MerchantCardRemovalMessage`、`PaelsWingSacrificeMessage` 对本地玩家都会直接抛异常；`CombatStateSynchronizer.WaitForSync` 还会跳过 `LocalContext.IsMe(player)`。因此**当前更安全的默认策略已统一为 detect-only + 本地化/国际化提醒**，包括 `SyncReceived / PlayerChoice / ChooseOption(ui_exploit)` 三条 deck 检测链。
> 7. **2026-04-08 13:55 新增修复**：最新日志显示，`OnReceivePlayerChoice` 在奖励/地图阶段会对同一个仅含 `indexes` 的弱 payload 重复调用两次，旧逻辑直接累加 `_choiceCallCount`，导致后续正常同步被误打成 `reward_multi_select(calls=2 delta=1)`；同时新局切角色时，旧角色起始基线与新角色 `starter + ASCENDERS_BANE` 的同步差也会被误报成 `add_cards(count=1 allowed=0)`。已在 `DeckSyncPatches.cs` 中加入 `_ShouldSkipDuplicateWeakPlayerChoice()`（2 秒内按签名去重弱 payload），并增加 `_startupSyncWarmupUntil` 预热窗口：首个同步 / 角色切换期间若没有明确 choice/option 上下文，则只接受同步并重建 `_preCheatSnapshot / _lastSerializablePlayer` 基线，不进入通用 deck 作弊判定；detect-only 首次命中后也会立刻刷新通知抑制并清空 choice tracking，避免旧状态在后续 `SyncReceived` 中反复弹窗。

---

## 项目基本信息

- **项目路径**: `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats`
- **部署路径**: `K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\NoClientCheats`
- **构建脚本**: `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\build.ps1`
- **游戏日志**: `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\logs\godot.log`
- **NCC诊断日志**: `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\NCC_diag.log`（如存在）

---

## 项目目标

**NoClientCheats (NCC)** 是一个 Slay the Spire 2 联机模组，用于：
1. **主机端检测**副机客户端作弊（卡组变化/transform 多选等）
2. **第一时间弹出作弊警告**（即时在作弊发生帧）
3. **在具备安全客机能力前，仅做检测与提醒**；deck 类作弊在 host-only 模式下不再做半回滚，以免把对局推入 `StateDivergence`
4. **记录作弊历史**（历史面板 UI）

### 核心设计原则
- **仅在主机端执行**检测和回滚逻辑；客机不应运行 NCC 的检测逻辑
- `ChooseOptionPostfix` 和 `PlayerChoiceReceivePatch` 的核心检测仅在 `IsMultiplayerHost() == true` 时执行
- **作弊弹窗仅弹一次**：通过 `_immediateCheatNotifyTicks` 字典记录已弹窗时间戳

---

## 核心文件结构

```
STS2_mod/NoClientCheats/
├── NoClientCheatsMod.cs      # 主模块入口、全局状态、快照字典、作弊记录
├── DeckSyncPatches.cs        # ★核心作弊检测逻辑★
│   ├── ChooseOptionPrefix / ChooseOptionPostfix   # 休息点/事件选项检测
│   ├── RewardObtainedMessagePostfix               # 奖励牌同步后立即刷新 NCC 基线；地图阶段失败时回退到 +1 基线推进并做消息去重
│   ├── PlayerChoiceReceivePatch                  # PlayerChoice 消息检测
│   ├── SyncReceivedPatch                          # SyncReceived 同步消息检测
│   ├── GameActionHook                            # GameAction 钩子
│   ├── _ImmediateRollbackHostPlayer()            # 即时回滚主机玩家（仅在未来具备安全客机回滚能力时启用）
│   ├── _ForceResyncPlayer()                     # 强制同步 Player 对象
│   ├── _SendRollback() / _SendRollbackForImmediateCheat()  # 发送网络回滚（当前 host-only detect-only 默认不会走到）
│   ├── _RollbackPlayerDeck()                    # 本地卡组回滚
│   └── _pendingPlayerRefreshes                   # 延迟刷新队列（地图阶段）
├── ClientDiagnosticPatches.cs # ★客机回滚重定向修复★（登记原始目标玩家 + 重定向 SyncWithSerializedPlayer）
├── NetIdFixTranspiler.cs      # NetId 修正注册表 API（已不再 Patch）
├── CheatNotification.cs      # 屏幕顶部作弊弹窗
├── CheatHistoryPanel.cs      # 作弊历史记录面板（可拖拽/缩放）
├── InputHandlerNode.cs       # 热键处理器（F6 打开历史面板）
├── ModConfigIntegration.cs   # 配置界面集成
└── Localization.cs           # 多语言文本
```

---

## 核心数据类型与字段

### DeckSyncPatches.cs 中关键字典

| 字段 | 类型 | 用途 |
|---|---|---|
| `_preCheatSnapshot` | `Dictionary<ulong, object>` | 第一次 SyncReceived 时的干净快照（供 PlayerChoice 回滚用） |
| `_lastSerializablePlayer` | `Dictionary<ulong, object>` | 上一次 SyncReceived 的 SerializablePlayer |
| `_lastSyncDeckSize` | `Dictionary<ulong, int>` | 上一次 Sync 收到的卡数（供 TransformCheck 兜底） |
| `_choiceCallCount` | `Dictionary<ulong, int>` | PlayerChoice 被调用次数（多选时 > 1） |
| `_immediateRollbackDone` | `Dictionary<ulong, long>` | 已触发即时回滚的时间戳（Tick）；detect-only 路线下不再写入 |
| `_immediateCheatNotifyTicks` | `Dictionary<ulong, long>` | 已弹窗时间戳（防重复弹） |
| `_canonicalSerializablePlayer` | `Dictionary<ulong, object>` | canonicalCards 对应的完整 SerializablePlayer |
| `_pendingPlayerRefreshes` | `Dictionary<ulong, (ulong playerId, object snapshot, object synchronizer)>` | 延迟刷新队列（地图阶段用） |
| `_lastRemotePlayerByNetId` | `Dictionary<ulong, object>` | 缓存最近一次解析到的远程 Player 对象 |

### NoClientCheatsMod.cs 中作弊记录

```csharp
private static readonly List<CheatRecord> _historyRecords = new();
public record CheatRecord(string Time, string SenderName, string CharacterName, string Command);
```

---

## 核心检测流程

### 路径A: ChooseOptionPostfix（休息点/事件选项）
1. `ChooseOptionPrefix`: 记录 `player.ToSerializable()` → `SetPreDeckSnapshot(playerNetId, preSnapshot)`
2. `ChooseOptionPostfix`: 记录 `player.ToSerializable()` → `postSnapshot`
3. 对比 preSnapshot vs postSnapshot：`_DecksMatch()`
4. 不匹配 → 检测到作弊 → `_RollbackPlayerDeck(player, preSnapshot)` → `_SendRollbackForImmediateCheat(playerNetId, preSnapshot)` → `RecordCheat()`
5. **即时弹窗**：调用 `RecordCheat()` 触发 `CheatNotification` 弹窗

### 路径B: PlayerChoiceReceivePatch（Transform 多选作弊）
1. `SyncReceivedPatch.Postfix`: 每次收到副机同步消息，记录 `_lastSerializablePlayer` 和 `_preCheatSnapshot`
2. `PlayerChoiceReceivePatch.Postfix`: 收到副机的 `PlayerChoiceMessage`（transform/reward 多选）
3. `choiceCallCount >= 2` 时，`delta != calls - 1` → 检测到作弊
4. `_ImmediateRollbackHostPlayer(senderId, preDeckSize)` → 即时回滚
5. **网络通知客机**: `_SendRollbackForImmediateCheat` 调用 `_SendRollback` → `SyncPlayerDataMessage`
6. `RecordCheat()` → 弹窗

### 路径C: SyncReceivedPatch（周期性同步校验）
- 辅助检测：在 `SyncReceived` 时对比 `canonicalCards` vs `receivedDeck`
- **仅在主机执行**，且仅在作弊检测到时发 `SyncPlayerDataMessage`

---

## 网络同步机制

### 发送回滚消息
```
_SendRollbackForImmediateCheat(playerNetId, correctSnapshot)
  └→ _SendRollback(synchronizer, playerNetId, correctSnapshot)
       ├→ 获取 _netService
       ├→ 创建 SyncPlayerDataMessage
       ├→ _PopulateSyncPlayerDataMessage(msg, correctSnapshot)
       └→ _TrySendMessageToPeer(netService, msg, playerNetId)
```

### 填充 SyncPlayerDataMessage
```csharp
private static void _PopulateSyncPlayerDataMessage(object msg, object correctSnapshot)
{
    foreach (var name in new[] { "player", "Player", "SerializablePlayer", "PlayerData", "Data" })
        _SetMemberAny(msg, name, correctSnapshot);
}
```

---

## 地图阶段 Player 对象问题

**问题**: `_ImmediateRollbackHostPlayer` 在地图阶段调用时，`RunManager.Instance.State` 为 null，导致 `_TryResolveLivePlayerByNetId()` 返回 null，Player 对象找不到。

**解决方案**: 延迟刷新队列
1. `DeckSyncPatches._pendingPlayerRefreshes` 存储 `(playerId, snapshot, synchronizer)`
2. `InputHandlerNode._Process` 每帧调用 `NoClientCheatsMod.ProcessPendingPlayerRefreshes()`
3. `NoClientCheatsMod._DoProcessPendingPlayerRefreshes()` 通过 `Callable.From(...).CallDeferred()` 在下一帧处理
4. `DeckSyncPatches.ProcessDeferredPlayerRefresh()` 再次尝试解析 Player 并应用快照

---

## 当前已知问题 / 待修复

1. **副机黑屏强退（最新形态：`Character changed for player ...`）**：✅ **已修复代码**——`ClientDiagnosticPatches.cs` 现在会把 NCC 回滚快照与“原始目标玩家 NetId”绑定，并在 `Player.SyncWithSerializedPlayer` 前把同步重定向到正确玩家，避免把 Ironclad/其他角色快照错误同步给房主角色对象。**待客机实测验证**。
2. **客机 NCC 未启用**：客机 NCC 被游戏设置禁用（`it is set to disabled in settings`），导致关键修正补丁无法在客机运行。
   - **解决**：客机需要安装 NCC DEBUG 版并在设置中启用
   - 客机启用后，诊断日志输出到 `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\NCC_diag.log`
3. **ChooseOptionPostfix 作弊时弹窗显示延迟**（本次已修复：添加 `_RecordImmediateCheatNotifyTick`）
4. **地图阶段 Player 对象找不到导致回滚不完整**（本次已修复：延迟刷新队列）
5. **CheatHistoryPanel._BuildUI 的 GetTree() 崩溃**（本次已修复：try-catch）

---

## 卡组回滚网络同步 NetId 问题（深度分析版）

> **状态：根因已确认（源代码级），6 种解决思路已提出，详见 `docs/NCC_NetId_思路分析.md`**

### 问题描述

副机在休息点/事件作弊（transform_multi_select）后：
1. ✅ 主机端正确弹出作弊警告
2. ✅ 主机端副机卡组回滚正常
3. ❌ 副机端黑屏，无法游戏
4. ❌ 副机端游戏强退，报错：`Tried to sync player that has net ID X with SerializablePlayer that has net ID Y!`

**关键矛盾**：主机端一切正常，客机端强退。说明检测和回滚逻辑正确执行，但网络同步出了问题。

### 根本原因（源代码级确认）

**网络消息中的 NetId 错配链**：

```
主机 NCC 检测到副机作弊 → _SendRollback(副机NetId, correctSnapshot)
  → 创建 SyncPlayerDataMessage，msg.player.NetId = correctSnapshot.NetId = 副机NetId
  → 调用 netService.SendMessage(msg, 副机NetId)
    → SendMessageToClientInternal(msg, 副机NetId, channel, overrideSenderId=null)
      → SerializeMessage(overrideSenderId ?? this._netHost.NetId, msg, ...)
      → overrideSenderId 为 null，用 this._netHost.NetId（主机NetId）作为包的 senderId
      → 副机收到包，packet header senderId = 主机NetId
        → 副机反序列化，_syncData[主机NetId] = msg.player
        → WaitForSync: GetPlayer(主机NetId) → 找到副机的 Player(NetId=主机NetId)
        → SyncWithSerializedPlayer(msg.player)
          → 副机Player.NetId(主机NetId) ≠ msg.player.NetId(副机NetId) → 强退！
```

关键代码证据（`NetHostGameService.cs` 第168行）：
```csharp
byte[] array = this._messageBus.SerializeMessage<T>(overrideSenderId ?? this._netHost.NetId, message, out num);
```
当 `overrideSenderId=null` 时，senderId = 主机NetId，与 `msg.player.NetId`（副机 NetId）不匹配。

### 相关源代码文件

| 文件 | 路径 |
|------|------|
| `CombatStateSynchronizer.cs` | `MegaCrit\sts2\Core\Multiplayer\` — WaitForSync，强退点，第70行 `_syncData[senderId] = msg.player` |
| `NetHostGameService.cs` | `MegaCrit\sts2\Core\Multiplayer\` — SendMessage，senderId 来源，第168行 |
| `NetMessageBus.cs` | `MegaCrit\sts2\Core\Multiplayer\` — SerializeMessage，senderId 写入 |
| `SerializablePlayer.cs` | `MegaCrit\sts2\Core\Saves\Runs\` — SerializablePlayer.NetId |
| `Player.cs` | `MegaCrit\sts2\Core\Entities\Players\` — Player.NetId，SyncWithSerializedPlayer |
| `SyncPlayerDataMessage.cs` | `MegaCrit\sts2\Core\Multiplayer\Messages\Game\` — 消息结构 |

源代码存档：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260322\sts2\`

### 关键发现：STS2 的 NetId 体系特殊性

| 对象 | NetId 来源 | 说明 |
|------|-----------|------|
| `Player.NetId` | 构造函数传入 | 副机的 Player.NetId = **主机NetId**（游戏网络层分配） |
| `SerializablePlayer.NetId` | `ToSerializable()` 时复制 | 副机的 SP.NetId = 副机NetId（作弊玩家的 Steam ID） |

游戏中用 `Player.NetId` 查找玩家，但 NCC 回滚消息里装的是 `SerializablePlayer.NetId`。这两个值在副机上**代表不同玩家**，导致查找和比较都错位。

### 关键发现：客机 NCC 被禁用

客机日志（第80-117行）：
```
[INFO] Found mod manifest file C:\steam\steamapps\common\Slay the Spire 2\mods\NoClientCheats\mod_manifest.json
[INFO] Skipping loading mod NoClientCheats, it is set to disabled in settings
```
客机 NCC 被禁用，导致关键修正补丁无法在客机运行。

### 参考项目：ForkedRoad 的网络消息模式

项目：[Snoivyel/STS2-Forked-Road](https://github.com/Snoivyel/STS2-Forked-Road)，v1.0.3，MIT 协议

**自定义消息关键设计**：
- 消息**携带元数据，不携带完整 Player 状态**
- `ShouldBroadcast=true` 时 senderId 由网络层自动填充，不存在 overrideSenderId 问题
- Handler 接收 `msg` 和 `senderId`，用 `senderId` 和消息内 `playerIds` 做业务逻辑

### 解决思路（优先级排序）

| 思路 | 方案 | 难度 | 侵入性 | 风险 | 状态 |
|------|------|------|--------|------|------|
| A | Finalizer 修正 `msg.player.NetId` | 低 | 无 | 中 | 待实测 |
| B | 自定义作弊通知消息（绕过 SyncPlayerDataMessage） | 中 | 中 | 低 | 推荐 |
| C | Transpiler 注入 IL 修正 NetId | 高 | 低 | 高 | 难度高 |
| D | Hook ToSerializable | 中 | 低 | 高 | 不可行 |
| E | Transpiler 修改 WaitForSync 比较逻辑 | 高 | 中 | 高 | 难度高 |
| **F** | **客机被动装 NCC（只装补丁，不启用检测）** | **低** | **低** | **低** | **最简单有效** |

详细代码和原理见 `docs/NCC_NetId_思路分析.md`。

### 推荐行动计划

1. **第一步（低成本验证）**：实现思路 A（Finalizer），测试是否真的能修改 `msg.player.NetId`。如果不行，立刻转向思路 F。
2. **第二步（如果 A 无效）**：实施思路 F，将 `SyncWithSerializedPatch` 和 `OnSyncPlayerReceivedFinalizer` 打包为"网络修正模块"，要求客机启用。更新 MEMORY.md 的设计原则描述。
3. **第三步（如果 F 被拒绝）**：实施思路 B（自定义消息），参考 ForkedRoad 实现完整的作弊通知消息，绕过游戏内置的 `SyncPlayerDataMessage`。

---

## 调试日志标签

| 日志前缀 | 来源 |
|---|---|
| `[NCC|DIAG]` | `LogDiag()` 诊断信息 |
| `[NCC|FULLTRACE]` | 详细执行路径跟踪 |
| `[NCC|DIAG|时间戳]` | 带时间戳的诊断 |
| `[NCC]` | 普通 GD.Print |
| `[ChooseOptionPrefix]` | ChooseOption 前缀钩子 |
| `[ChooseOptionPostfix]` | ChooseOption 后缀钩子 |
| `[PlayerChoice]` | PlayerChoiceReceivePatch |
| `[SyncReceived]` | SyncReceivedPatch |
| `[IMMRB]` | `_ImmediateRollbackHostPlayer` |
| `[IMMRB-CHOOSE]` | `_SendRollbackForImmediateCheat` |
| `[Rollback]` | `_SendRollback` / 回滚相关 |
| `[DEFERRED]` | 延迟刷新相关 |
| `[ResolvePlayer]` | `_TryResolveLivePlayerByNetId` |
| `[CANONCHECK]` | canonicalCards 对比检测 |
| `[CHEATCHECK]` | PlayerChoice 作弊检测 |
| `[NCCInputHandler]` | InputHandlerNode |
| `[NoClientCheats]` | ModConfig / 初始化 |
| `[NCC-DIAG]` | `ClientDiagnosticPatches` 客机诊断日志（带毫秒时间戳）|
| `[SyncRecv]` | `OnSyncPlayerReceived_Patch` 收到的 SyncPlayerDataMessage |
| `[WaitSync]` | `WaitForSync_Patch` WaitForSync 执行流程 |
| `[SyncPlayer]` | `SyncPlayer_Patch` SyncWithSerializedPlayer 调用 |
| `[Context]` | 错配时的详细上下文 |

---

## 配置文件

- NCC 设置存储在 ModConfig 中（游戏内设置界面）
- `IsMultiplayerHost()` 通过 `INetGameService.Type == NetGameType.Host` 判断

---

## 测试流程

1. **主机**启动游戏，创建联机房间
2. **副机**加入房间
3. **副机**在休息点/事件中作弊（transform 多选）
4. **预期**：主机端在作弊发生后立即弹出作弊警告（含回滚状态）
5. **预期**：副机卡组在主机端显示为作弊前状态
6. **预期**：副机不会黑屏或崩溃
7. **预期**：历史面板记录作弊事件

---

## 构建与部署

```powershell
# 在 K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\ 目录执行
.\build.ps1
```

构建脚本会自动：
1. 编译 C# 代码
2. 复制 DLL 和资源到 `K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\NoClientCheats\`

---

## 版本历史

- **v1.3.0** (当前): 完整作弊检测 + 即时回滚 + 网络同步 + 历史面板
- **v1.2.x**: 早期版本，存在黑屏/不同步问题
- **v1.1.x**: 基础版本，仅本地检测
