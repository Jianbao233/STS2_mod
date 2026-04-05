# NCC 项目 Bug 分析与修复报告

> 分析日期：2026-04-04
> 分析范围：作弊弹窗不弹出、回滚功能失效

---

## 一、问题概述

| 问题 | 现象 | 严重程度 |
|------|------|----------|
| 作弊弹窗不弹出 | 客机使用作弊指令时，拦截记录存在但无弹窗 | 🔴 高 |
| 回滚功能失效 | 检测到作弊后卡组未回滚 | 🔴 高 |
| Transpiler 不工作 | AsyncLocal 未被正确注入到调用链中 | 🔴 高 |

---

## 二、根因分析

### 2.1 作弊弹窗不弹出

**调用链分析**：

```
ClientCheatBlockPrefix.Prefix
    → NoClientCheatsMod.RecordCheat()
    → CheatNotification.Show()
```

**问题定位**：`CheatNotification.Show()` 第 24 行：

```csharp
public static void Show(string senderName, string characterName, string cheatCommand)
{
    if (_instance == null) return;  // ⚠️ 如果 _instance 为 null，直接返回
    _instance._Enqueue(senderName, characterName ?? "", cheatCommand);
}
```

**`_instance` 何时设置**：`CheatNotification._Ready()` 第 30 行：

```csharp
public override void _Ready()
{
    _instance = this;
    Layer = 900;
    SetProcess(false);
}
```

**`_instance` 为 null 的可能原因**：

1. **`EnsureInitialized()` 未被调用**
   - 检查：`NoClientCheatsMod._initialized` 是否为 true
   - 调用路径：`HarmonyPatcher.TryScheduleInit()` → `OnInitFrame2()` → `EnsureInitialized()`

2. **`_Ready()` 在 `_instance = null` 时被调用**
   - Godot 节点被加入场景树时 `_Ready()` 才执行
   - `EnsureInitialized()` 中使用 `CallDeferred("AddChild")`，节点异步加入树

3. **多次初始化导致覆盖**
   - 如果 NCC 在同一进程中多次初始化，`_instance` 可能被后续实例覆盖

**关键发现**：日志第 497 行显示 `[NoClientCheats] Loaded.`，说明 `EnsureInitialized()` 已被调用。但 `_notificationNode` 可能还未完成 `AddChild`，导致 `_instance` 为 null。

---

### 2.2 Transpiler 不工作（核心问题）

**代码结构**：

```csharp
// ClientCheatBlockPatch.cs - 第 107-138 行
static IEnumerable<CodeInstruction> Transpiler(...)
{
    // 在 HandleRequestEnqueueActionMessage 开头插入 SetCurrentRemotePlayer
    list.Insert(0, new CodeInstruction(OpCodes.Ldarg_2));  // 加载 senderId
    list.Insert(1, new CodeInstruction(OpCodes.Call, setMethod));  // 调用 SetCurrentRemotePlayer
    // ...
}
```

**问题**：STS2 使用 **IL2CPP** 编译（C# → C++ → 机器码），Harmony 的 Transpiler 在运行时修改 IL 字节码，但 IL2CPP 编译后 IL 指令集已经被转换。**Transpiler 可能在大部分场景下不工作或行为不一致**。

**证据**：
- 日志中 NCC 的诊断信息正常打印，说明 Patch 本身被应用
- 但 `DeckSyncPatches.cs` 中完全没有使用 `GetCurrentRemotePlayer()`
- 日志第 198-215 行显示 `PlayerChoice` Patch 正常工作

---

### 2.3 回滚功能失效

**调用链**：

```
SyncReceivedPostfix.Postfix
    → 检测作弊
    → _RollbackPlayerDeck()
    → 清空 CardPile 并重建
```

**日志分析**（第 182-197 行）：

```
[FULLTRACE] Sync.Postfix senderId=76561199718354550 0C/0U -> 11C/0U delta=11/0
[FULLTRACE] TransformCheck FIXED: no preDeck cached, using _lastSyncDeckSize=11
[FULLTRACE] TransformCheck: LEGITIMATE (choiceCalls=0 delta=0)
```

**问题**：
1. `choiceCalls=0` → `OnReceivePlayerChoice` 的 Postfix 没有正确记录 `choiceCallCount`
2. `hasPrev=False` → 没有之前的快照可用
3. `allowedD=0/0` → 没有设置允许的增量

**代码问题**：第 513-521 行是回退逻辑，但当 `preDeckSize=0` 且 `choiceCallCount=0` 时，仍然无法检测作弊：

```csharp
if (preDeckSize == 0 && choiceCallCount == 0)
{
    lock (_lastSyncDeckSize)
        _lastSyncDeckSize.TryGetValue(senderId, out preDeckSize);
    // 如果 _lastSyncDeckSize 也为空，preDeckSize 仍为 0
    // 导致第 527 行的条件 `preDeckSize > 0` 不满足，跳过检测
}
```

---

## 三、源码对比分析

### 3.1 NetConsoleCmdGameAction 结构

```csharp
// 游戏源码 - NetConsoleCmdGameAction.cs
public struct NetConsoleCmdGameAction : INetAction, IPacketSerializable
{
    public string cmd;  // ⚠️ 这是字段，不是属性
}

// ClientCheatBlockPatch.cs 第 71 行
var cmdField = action.GetType().GetField("cmd", BindingFlags.Public | BindingFlags.Instance);
var cmd = cmdField?.GetValue(action) as string;
```

**结论**：字段访问方式是正确的，但 `action` 的类型检查可能有问题。

### 3.2 HandleRequestEnqueueActionMessage 调用链

```csharp
// 游戏源码 - ActionQueueSynchronizer.cs 第 336-353 行
private void HandleRequestEnqueueActionMessage(RequestEnqueueActionMessage message, ulong senderId)
{
    GameAction gameAction = this.NetActionToGameAction(message.action, senderId);
    this.EnqueueAction(gameAction, senderId);  // ← Transpiler 注入点
}
```

**关键发现**：`HandleRequestEnqueueActionMessage` 内部调用 `EnqueueAction`，Transpiler 在 `HandleRequestEnqueueActionMessage` 开头注入 `SetCurrentRemotePlayer(senderId)`，在所有 `return` 前注入 `ClearCurrentRemotePlayer()`。

但问题是：`HandleRequestEnqueueActionMessage` 的返回值是 `void`，没有显式 `return` 语句！

---

## 四、修复建议

### 4.1 修复作弊弹窗问题

**方案 A**：在 `Show()` 方法中添加更多诊断日志

```csharp
public static void Show(string senderName, string characterName, string cheatCommand)
{
    GD.Print($"[NCC] CheatNotification.Show called: sender={senderName}, cmd={cheatCommand}");
    if (_instance == null) {
        GD.PushWarning($"[NCC] CheatNotification._instance is null! Notification dropped.");
        return;
    }
    _instance._Enqueue(senderName, characterName ?? "", cheatCommand);
}
```

**方案 B**：确保节点已加入场景树后再显示

```csharp
public static void Show(string senderName, string characterName, string cheatCommand)
{
    if (_instance == null) {
        // 尝试从场景树获取已存在的实例
        var tree = Engine.GetMainLoop() as SceneTree;
        var existing = tree?.Root?.GetNodeOrNull<CheatNotification>("CheatNotification");
        if (existing != null) {
            _instance = existing;
        } else {
            GD.PushWarning("[NCC] CheatNotification not initialized yet.");
            return;
        }
    }
    _instance._Enqueue(senderName, characterName ?? "", cheatCommand);
}
```

### 4.2 修复 Transpiler 问题

**根本问题**：IL2CPP 下 Transpiler 可能不工作。

**替代方案**：使用 Prefix/Postfix 代替 Transpiler

```csharp
// 方案：使用 Prefix 直接设置 AsyncLocal
static bool Prefix(object __instance, object message, ulong senderId)
{
    // 在方法开头直接设置
    SetCurrentRemotePlayer(senderId);
    
    // ... 原有逻辑 ...
    
    // 在方法结束前清除
    ClearCurrentRemotePlayer();
    return true;
}

// 使用 Finalizer 确保清除（如果有的话）
// 或者在 Prefix 中用 try-finally
```

### 4.3 修复回滚检测逻辑

**问题**：`choiceCallCount` 没有被正确累加。

**修复**：在 `OnReceivePlayerChoicePatch` 的 Postfix 中确保计数正确：

```csharp
static void Postfix(object __instance, object player, uint choiceId, object result)
{
    // ... 现有代码 ...
    
    // 强制累加 choiceCallCount，无论 canonicalCards 是否为空
    lock (_choiceCallCount)
    {
        if (!_choiceCallCount.ContainsKey(playerId))
            _choiceCallCount[playerId] = 0;
        _choiceCallCount[playerId]++;
    }
    
    // ... 后续代码 ...
}
```

### 4.4 修复 AsyncLocal 未使用问题

**问题**：`DeckSyncPatches.cs` 中完全没有使用 `GetCurrentRemotePlayer()`。

**修复**：在 `ChooseOptionPrefix.Prefix()` 中使用 AsyncLocal：

```csharp
static void Prefix(object __instance, object player, int optionIndex)
{
    // 使用 Transpiler 设置的 AsyncLocal 值
    ulong remotePlayerId = NoClientCheatsMod.GetCurrentRemotePlayer();
    
    // 如果 remotePlayerId != 0 且等于当前 player 的 NetId，则为远程玩家
    if (remotePlayerId != 0)
    {
        ulong playerNetId = GetPlayerNetId(player);
        if (playerNetId == remotePlayerId)
        {
            // 这是远程玩家触发的 ChooseOption
            // 记录卡组快照...
        }
    }
}
```

---

## 五、日志诊断建议

在关键位置添加更详细的日志：

```csharp
// 在 RecordCheat 开头
GD.Print($"[NCC] RecordCheat called: senderId={senderId}, cmd={cheatCommand}, blocked={wasBlocked}");

// 在 CheatNotification.Show 开头
GD.Print($"[NCC] CheatNotification.Show: _instance={(_instance != null ? "OK" : "NULL")}");

// 在 Rollback 开头
GD.Print($"[NCC] _RollbackPlayerDeck called for playerId={playerId}");
```

---

## 六、测试验证清单

1. [ ] 客机发送作弊指令后，检查 `godot.log` 中是否有 `[NCC] CheatNotification.Show` 日志
2. [ ] 检查 `NCC_diag.log` 中 `PlayerChoice` 相关的 `[PREFIX]` 和 `[POSTFIX]` 日志
3. [ ] 验证 `choiceCallCount` 是否正确累加
4. [ ] 验证回滚后卡组数量是否正确恢复

---

*本文档由 AI 自动生成，基于游戏源码分析和日志解读。*
