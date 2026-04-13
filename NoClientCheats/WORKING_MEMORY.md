# NoClientCheats (NCC) 工作记忆 / 项目日志

> 最后更新: 2026-04-05
>
> ⚠️ **核心未解决问题**：副机黑屏强退（NetId 错配）— ✅ **已修复**（三步协作方案，详见下方）
>
> **今日重大进展**：通过 ForkedRoad 源码研究和游戏 DLL 反编译，定位到 NetId 错配的根本原因，并提出 6 种解决思路。详见 `docs/NCC_NetId_思路分析.md`。**最新进展**：实现客机诊断模块 `ClientDiagnosticPatches.cs`（三步协作方案），通过 WaitForSync 的 ThreadLocal 上下文传递实现 NetId 黑屏修复。

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
3. **回滚副机卡组**到作弊前状态（主机本地 + 网络通知客机）
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
│   ├── PlayerChoiceReceivePatch                  # PlayerChoice 消息检测
│   ├── SyncReceivedPatch                          # SyncReceived 同步消息检测
│   ├── GameActionHook                            # GameAction 钩子
│   ├── _ImmediateRollbackHostPlayer()            # 即时回滚主机玩家
│   ├── _ForceResyncPlayer()                     # 强制同步 Player 对象
│   ├── _SendRollback() / _SendRollbackForImmediateCheat()  # 发送网络回滚
│   ├── _RollbackPlayerDeck()                    # 本地卡组回滚
│   └── _pendingPlayerRefreshes                   # 延迟刷新队列（地图阶段）
├── ClientDiagnosticPatches.cs # ★客机诊断 + NetId黑屏修复★（三步协作 Patch A/B/C）
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
| `_immediateRollbackDone` | `Dictionary<ulong, long>` | 已触发即时回滚的时间戳（Tick） |
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

1. **~~副机黑屏强退（NetId 错配）~~**：✅ **已修复**——`ClientDiagnosticPatches.cs` 三步协作方案（Patch A/B/C + ThreadLocal 上下文传递）。**待客机实测验证**。
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
