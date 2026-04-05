# NCC 副机强退问题：根因分析与解决思路

> 本文档整合了 NCC 项目日志分析、ForkedRoad 源码研究、Web 搜索结果，生成日期：2026-04-05

---

## 一、问题全貌

### 1.1 测试现象

| 现象 | 主机端 | 副机端 |
|------|--------|--------|
| 作弊警告弹窗 | ✅ 正常弹出 2 次 | N/A |
| 主机端副机卡组 | ✅ 正确回滚 | N/A |
| 副机端游戏状态 | N/A | ❌ 黑屏，强退 |
| 游戏强退错误 | 无 | `Tried to sync player that has net ID X with SerializablePlayer that has net ID Y!` |

**关键矛盾**：主机端一切正常，客机端强退。说明检测和回滚逻辑正确执行，但网络同步出了问题。

### 1.2 客机 NCC 状态

客机日志（第 80-117 行）：
```
[INFO] Found mod manifest file C:\steam\steamapps\common\Slay the Spire 2\mods\NoClientCheats\mod_manifest.json
[INFO] Skipping loading mod NoClientCheats, it is set to disabled in settings
```

**结论**：客机 NCC 未加载（被用户在设置中禁用了）。

---

## 二、根本原因（源代码级确认）

### 2.1 网络消息的 NetId 错配链

```
主机 NCC 检测到副机 transform_multi_select 作弊
    │
    ▼
_SendRollback(副机NetId, correctSnapshot)
    │
    ▼  ① 创建 SyncPlayerDataMessage
       msg.player.NetId = correctSnapshot.NetId = 副机NetId
    │
    ▼  ② 调用 netService.SendMessage(msg, 副机NetId)
       → SendMessageToClientInternal(msg, 副机NetId, channel, overrideSenderId=null)
    │
    ▼  ③ SerializeMessage(overrideSenderId ?? this._netHost.NetId, msg, ...)
                                              ↑
                                        overrideSenderId 为 null
                                        用 this._netHost.NetId（主机NetId）
    │
    ▼  ④ 包 header senderId = 主机NetId
       msg.player.NetId = 副机NetId（不变）
    │
    ▼  ⑤ 副机收到包，反序列化
       _syncData[主机NetId] = msg.player
    │
    ▼  ⑥ WaitForSync 执行
       GetPlayer(主机NetId)
         → 副机上用主机NetId 查找 → 找到副机的 Player(NetId=主机NetId)
       SyncWithSerializedPlayer(msg.player)
         → 副机Player.NetId(主机NetId) ≠ msg.player.NetId(副机NetId)
         → 强退！
```

### 2.2 源代码证据

**`NetHostGameService.cs` 第 168 行**：
```csharp
byte[] array = this._messageBus.SerializeMessage<T>(overrideSenderId ?? this._netHost.NetId, message, out num);
```

当 `overrideSenderId=null` 时，senderId = 主机 NetId，与 `msg.player.NetId`（副机 NetId）不匹配。

**`CombatStateSynchronizer.cs` 第 70 行**：
```csharp
this._syncData[senderId] = syncMessage.player;  // ← 用 senderId（主机NetId）作为 key
```

副机用 senderId（主机NetId）去 `GetPlayer(主机NetId)`，找到的是副机的 Player，再用副机的 Player 与 msg.player（NetId=副机NetId）同步 → NetId 不等 → 强退。

### 2.3 为什么 `SyncWithSerializedPatch` 无效

当前 NCC 中的 `SyncWithSerializedPatch`（Prefix）在客机上**不执行**，因为客机 NCC 被禁用了。即使客机启用了 NCC，参数 `SerializablePlayer player` 是 **struct（值类型）**，通过 `object` 传参触发 boxing，Prefix 修改的是 boxed 副本，原方法看到的仍是原值，强退仍然发生。

---

## 三、为什么必须重新考虑方案

### 3.1 不可行的方案

| 方案 | 不可行原因 |
|------|-----------|
| 让客机装 NCC | 用户明确拒绝，且设计原则是"仅主机安装" |
| `SyncWithSerializedPatch` Prefix | struct boxing，修改不持久 |
| `SendMessage<T>(msg, peerId)` Postfix | struct boxing，修改不持久 |
| `OnSyncPlayerMessageReceived` Prefix | struct boxing，修改不持久 |

### 3.2 根本约束

**仅在主机端安装 NCC**，无法直接修改客机上收到的网络消息内容。必须找到一种方式，让消息在**发送前**（主机端）就正确构造，或者找到一种**绕过高风险消息**的替代机制。

---

## 四、参考项目：ForkedRoad 的网络消息模式

### 4.1 ForkedRoad 源码关键发现

项目：[Snoivyel/STS2-Forked-Road](https://github.com/Snoivyel/STS2-Forked-Road)，v1.0.3，MIT 协议

**自定义消息结构**（参考 `ForkedRoadBranchStartMessage.cs`）：

```csharp
public struct ForkedRoadBranchStartMessage : INetMessage
{
    public int actIndex;
    public MapCoord coord;
    public int branchSequence;
    public int remainingBranches;
    public List playerIds;

    public bool ShouldBroadcast => true;   // 关键：广播模式
    public NetTransferMode Mode => NetTransferMode.Reliable;

    public void Serialize(PacketWriter writer) { ... }
    public void Deserialize(PacketReader reader) { ... }
}
```

**消息发送模式**（`ForkedRoadManager.cs`）：

```csharp
// 广播发送（Host → 所有 Client）
_netService.SendMessage(new ForkedRoadBranchStartMessage { ... });
// SendMessage 重载：无 peerId 参数，通过 ShouldBroadcast 判断是否广播

// 接收处理
public static void HandleBranchStartMessage(
    ForkedRoadBranchStartMessage msg,
    ulong senderId  // ← handler 自动接收 senderId
)
{
    // senderId = 发这条消息的端（Host 的 NetId）
    // msg 里包含自己的 playerIds（作弊玩家的 NetId 列表）
}
```

**关键区别**：ForkedRoad 的消息 **不携带玩家数据的完整同步**（只携带 MapCoord、playerIds 等元数据），接收端根据这些元数据在**本地**重新构造游戏状态，不存在跨端 NetId 错配问题。

### 4.2 ForkedRoad 的网络层设计原则

1. **消息携带元数据，不携带完整 Player 状态**：接收端用元数据在本地查表，找到对应的本地 Player 对象
2. **senderId 仅用于日志/调试**：实际业务逻辑依赖消息内的 playerIds 字段
3. **ShouldBroadcast=true**：通过广播发送，senderId 自动由网络层填充
4. **Handler 在 InitializeForRun 中注册**：`netService.RegisterMessageHandler<T>(handler)`，永不注销

---

## 五、解决思路（优先级排序）

### 5.1 思路 A：Finalizer 修正 `msg.player.NetId`（推荐先试）

**原理**：`OnSyncPlayerMessageReceived` 的参数 `SyncPlayerDataMessage syncMessage` 虽然是 struct（按值传递，box 了），但 `syncMessage.player` 字段是**引用类型**（`SerializablePlayer`，class）。

```
boxed_syncMessage ─┬─► syncMessage.player ──► [堆上的 SerializablePlayer 对象]
                   │                        （所有引用指向同一个对象）
boxed 副本 ─────────┘
```

Finalizer 在原方法执行后运行，此时 `_syncData[senderId] = syncMessage.player` 已经执行完毕。如果此时修改**同一个** `SerializablePlayer` 对象的 `NetId` 字段，由于 `msg.player` 是引用类型，修改会影响到已经写入 `_syncData` 的对象。

**但需要注意**：boxing 后，`syncMessage.player` 作为 struct 的字段，可能已经被复制了一份。这需要实测验证。

**代码实现**：

```csharp
[HarmonyPatch]
private static class OnSyncPlayerReceivedFinalizer
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer");
        return t != null
            ? AccessTools.Method(t, "OnSyncPlayerMessageReceived", BindingFlags.NonPublic | BindingFlags.Instance)
            : null;
    }

    // Finalizer 在原方法执行后运行
    static void Finalizer(object __instance, object syncMessage, ulong senderId)
    {
        if (syncMessage == null || __instance == null) return;
        if (syncMessage.GetType().FullName?.Contains("SyncPlayerDataMessage") != true) return;

        try
        {
            var sp = GetSyncMessagePlayer(syncMessage);
            if (sp == null) return;

            var spNetId = GetPlayerNetId(sp);
            // 如果 msg.player.NetId 与 senderId 不匹配，强制修正
            if (spNetId != 0 && spNetId != senderId)
            {
                foreach (var name in new[] { "NetId", "net_id" })
                    _SetMemberAny(sp, name, senderId);
                LogDiag("Finalizer", $"[FINAL] Fixed NetId {spNetId}->{senderId} on received SerializablePlayer");
            }
        }
        catch (Exception ex)
        {
            LogDiag("Finalizer", $"[FINAL] OnSyncPlayerMessageReceived fix error: {ex.Message}");
        }
    }
}
```

**局限性**：如果 struct boxing 导致 `msg.player` 也是值类型的"快照"而非引用，Finalizer 仍不生效。

### 5.2 思路 B：绕过 `SyncPlayerDataMessage`，发自定义作弊通知消息（推荐）

**原理**：参考 ForkedRoad 的消息设计，不使用游戏的 `SyncPlayerDataMessage`，而是自定义一条作弊通知消息，携带元数据（作弊玩家 NetId + 作弊类型），让客机收到后**在本地重新构造正确的同步数据**。

**优势**：
- 完全避开 NetId 错配问题
- 不需要修改游戏的网络同步逻辑
- 客机收到后，从本地 `RunState.GetPlayer(msg.targetPlayerNetId)` 找到正确的 Player 对象，在本地应用回滚

**消息设计**：

```csharp
public struct NCCCheatRollbackMessage : INetMessage
{
    public ulong targetPlayerNetId;      // 作弊玩家的 NetId（在客机上 = 主机NetId）
    public string deckSnapshotJson;       // 作弊前的卡组 JSON
    public int preDeckSize;              // 作弊前卡数（用于验证）
    public string cheatType;             // "transform_multi_select" / "deck_mismatch"
    public bool ShouldBroadcast => false;  // 点对点，Host → 作弊玩家
    public NetTransferMode Mode => NetTransferMode.Reliable;

    public void Serialize(PacketWriter writer) { ... }
    public void Deserialize(PacketReader reader) { ... }
}
```

**发送端**（主机 NCC）：

```csharp
// 检测到作弊后
_netService.SendMessage(new NCCCheatRollbackMessage
{
    targetPlayerNetId =作弊玩家NetId,
    deckSnapshotJson = SerializeDeckSnapshot(preCheatSnapshot),
    preDeckSize = preDeckSize,
    cheatType = "transform_multi_select"
});
// 关键：使用无 peerId 的 SendMessage，ShouldBroadcast=false
// 由 NetHostGameService 内部处理，点对点发送到作弊玩家
```

**接收端**（客机 NCC，Handler）：

```csharp
public static void HandleNCCRollbackMessage(NCCCheatRollbackMessage msg, ulong senderId)
{
    // senderId = 主机NetId
    // msg.targetPlayerNetId = 主机NetId（在客机上代表作弊玩家）
    // 用 targetPlayerNetId 在本地找到 Player
    var player = RunState.GetPlayer(msg.targetPlayerNetId);
    if (player == null) return;

    // 验证卡数
    if (player.Deck.Count != msg.preDeckSize) return; // 数据不一致，拒绝

    // 本地应用回滚
    RestoreDeckFromSnapshot(player, msg.deckSnapshotJson);
    ShowRollbackNotification(msg.cheatType);
}
```

**需要解决的技术问题**：
1. `ShouldBroadcast=false` 时 `SendMessage` 如何指定目标 peerId？需要研究 `NetHostGameService` 的 `SendMessage<T>(T msg)` 无 peerId 重载的实现
2. 如何在客机上注册 Handler（客机 NCC 被禁用的情况下）？

### 5.3 思路 C：Transpiler 注入 IL 代码（难度高，理论可行）

**原理**：用 Harmony Transpiler 修改 `SendMessageToClientInternal` 的 IL，在序列化之前修正 `msg.player.NetId`。

```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    foreach (var ci in instructions)
    {
        yield return ci;
        // 在 SerializeMessage 调用之后（序列化已经完成，不需要了）
        // 需要找到 SerializeMessage 的调用位置
    }
    // 更好的方式：在 ldarg.1 (msg) 和 ldc.i8 peerId 之后，
    // 调用 FixNetId(msg, peerId)
}
```

**难度**：需要精确找到 IL 中的 `call SerializeMessage` 指令，然后在其前后注入修正代码。STS2 可能对 DLL 有混淆，IL 指令位置不稳定。

### 5.4 思路 D：修改 `SerializablePlayer.ToSerializable` 的 NetId 字段（间接方案）

**原理**：在主机端，作弊玩家调用 `ToSerializable()` 时，`SerializablePlayer.NetId` 被设置为作弊玩家的 Steam ID（副机 NetId）。如果 Hook `ToSerializable`，在序列化时将 `NetId` 替换为接收端的正确 NetId...

但这需要知道接收端是谁（发往哪个 peer）。无法在 `ToSerializable` 中获取。

### 5.5 思路 E：Transpiler 修改 `WaitForSync` 的 NetId 比较逻辑

**原理**：在 `WaitForSync` 中 `SyncWithSerializedPlayer` 调用之前，加一段 IL 代码，检查如果 `msg.NetId != this.NetId`，则跳过比较或使用其他方式同步。

**优势**：不需要改网络消息，在接收端处理
**风险**：如果 `SyncWithSerializedPlayer` 内部的检查是防御性断言（而不是可以跳过的一行），强行跳过可能导致其他问题

### 5.6 思路 F：修改 NCC 设计原则，允许客机被动接收（折中方案）

**折中**：修改 NCC 设计，允许客机装 NCC 但**不启用检测功能**（只装补丁，不注册 Handler）。这样：
- 客机 NCC 启用 `SyncWithSerializedPatch` 和 `OnSyncPlayerReceivedFinalizer`
- 客机 NCC 不注册任何检测 Handler（`ChooseOptionPostfix`、`PlayerChoiceReceivePatch` 等）
- 客机 NCC 不显示任何 UI

这样 NCC 仍然保持"仅主机检测"的设计原则，客机只是被动接收修正补丁。

---

## 六、技术可行性评估

| 思路 | 难度 | 侵入性 | 风险 | 备注 |
|------|------|--------|------|------|
| A: Finalizer | 低 | 无 | 中 | 需要实测 struct boxing 行为 |
| B: 自定义消息 | 中 | 中 | 低 | 参考 ForkedRoad，最干净 |
| C: Transpiler | 高 | 低 | 高 | 需要精确 IL 分析 |
| D: ToSerializable Hook | 中 | 低 | 高 | 无法感知目标 peer |
| E: WaitForSync Transpiler | 高 | 中 | 高 | 跳过检查可能有副作用 |
| F: 客机被动装 NCC | 低 | 低 | 低 | 最简单有效 |

---

## 七、推荐行动计划

**第一步（低成本验证）**：实现思路 A（Finalizer），测试是否真的能修改 `msg.player.NetId`。如果不行，立刻转向思路 F。

**第二步（如果 A 无效）**：实施思路 F，将 `SyncWithSerializedPatch` 和 `OnSyncPlayerReceivedFinalizer` 打包为"网络修正模块"，要求客机启用。更新 MEMORY.md 的设计原则描述。

**第三步（如果 F 被拒绝）**：实施思路 B（自定义消息），参考 ForkedRoad 实现完整的作弊通知消息，绕过游戏内置的 `SyncPlayerDataMessage`。

---

## 八、参考链接

- [Snoivyel/STS2-Forked-Road](https://github.com/Snoivyel/STS2-Forked-Road) — 自定义网络消息实现参考
- [Harmony Transpiler 文档](https://harmony.pardeike.net/articles/patching-transpiler.html) — IL 注入教程
- [Harmony Finalizer 文档](https://harmony.pardeike.net/articles/patching-finalizer.html) — Finalizer 执行顺序
- [Harmony Issue #371](https://github.com/pardeike/Harmony/issues/371) — struct ref 参数 boxing 问题（已修复）
