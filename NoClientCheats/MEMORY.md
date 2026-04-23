# NoClientCheats · 项目记忆

> 本文件为 NoClientCheats 项目的专属记忆，每次新对话开始时请先阅读本文。
>
>
> **最新更新 (2026-04-18)**：Android 端启动报错定位到依赖缺失：`Could not load file or assembly 'Iced, Version=1.21.0.0'`（触发点：`Harmony.PatchAll -> ClientDiagnosticPatches`）。已改为随模组打包运行时依赖：`0Harmony.dll`、`Iced.dll`、`NoClientCheats.deps.json`、`NoClientCheats.runtimeconfig.json`。发布包 `NoClientCheats-v1.3.0.zip` 已包含上述文件并推送手机覆盖测试。
>
> **最新更新 (2026-04-23)**：手机最新日志显示 NCC 失效主因已切换为“方法适配失败”而非依赖缺失：`ClientCheatBlockPrefix.TargetMethod()` 返回 `null`，导致 `PatchAll` 中断。已修复 `ClientCheatBlockPatch.cs`：`TargetMethod` 改为 `public/nonpublic + 方法名 + 参数形状(?, ulong)` 双通道匹配，并增强 `message/action/cmd` 反射兜底（支持 `action/Action`、`cmd/Cmd`，含 `NonPublic`）。新版 `NoClientCheats-v1.3.0.zip` 已重新打包并推送手机 `/sdcard/Download/NoClientCheats-v1.3.0.zip`（2026-04-23 19:57）。
>
> **最新更新 (2026-04-23)**：卡组回滚模块进入弃用状态。为避免正常流程误报与黑屏，`NoClientCheatsMod.ApplyHarmonyPatches()` 已改为按类型逐个注入并跳过 `DeckSyncPatches*`、`ClientDiagnosticPatches*`；`ProcessPendingPlayerRefreshes()` 与 `EnsureInitialized()` 也在回滚模块关闭时直接短路，不再初始化/处理回滚链路。
>
> **最新更新 (2026-04-23)**：为彻底规避“自动 `Harmony.PatchAll` 先于 NCC 自定义补丁逻辑执行”导致回滚补丁仍被注入的问题，已将 `DeckSyncPatches.cs` 与 `ClientDiagnosticPatches.cs` 的完整历史实现改为 `#if false` 编译期注释，并新增无 `HarmonyPatch` 的桩类保留旧接口（`PendingPlayerRefresh*` / `EnsureInitialized`）。这样即使自动 PatchAll 扫描程序集，也不会再注入回滚与火堆卡组检测链路。

---

## 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\` |
| **目标** | 多人联机时禁止客机（非房主）使用控制台作弊指令 |
| **部署** | 仅房主需安装，客机无需安装 |
| **依赖** | ModConfig（游戏内配置）、Harmony（随模组打包 0Harmony.dll）、Iced（1.21.0，随模组打包） |
| **仓库** | `https://github.com/Jianbao233/STS2_mod` |
| **联机大厅仓库** | `https://github.com/emptylower/STS2-Game-Lobby`（STS2 LAN Connect） |

---

## 源码文件清单

| 文件 | 职责 |
|------|------|
| `NoClientCheatsMod.cs` | 中心枢纽：静态配置字段、节点懒创建、作弊记录 API、补丁引导 |
| `InputHandlerNode.cs` | `_Process` 轮询 `Input.IsKeyPressed(Key.F6)`，边沿触发呼出/隐藏面板 |
| `TopBarHistoryButtonPatch.cs` | Harmony Patch `NTopBar._Ready`，在游戏顶栏 PauseButton 左侧注入「记录」按钮 |
| `ModConfigIntegration.cs` | 反射式 ModConfig 注册（运行时解析类型，无编译期引用） |
| `CheatHistoryPanel.cs` | `CanvasLayer` 可拖拽/可调整大小的作弊拦截历史窗口，懒构建 |
| `CheatNotification.cs` | `CanvasLayer` 红色拦截通知弹窗，最多同时 4 个，带 Tween 渐隐 |
| `HarmonyPatcher.cs` | `ModManager.Initialize` 的 Harmony Postfix，三重保险初始化入口 |
| `ClientCheatBlockPatch.cs` | `HandleRequestEnqueueActionMessage` 的 Prefix，返回 `false` 静默丢弃作弊指令 |
| `Localization.cs` | 静态本地化模块：`GetLocale()` 检测语言，`Tr(key)` 返回中/英文本，UI 字符串集中管理 |
| `LanConnectBridge` | 反射桥接 `Sts2LanConnect.LanConnectLobbyRuntime`，将作弊拦截广播到大厅房间聊天。`SendRoomChatMessageAsync`/`HasActiveRoomSession`/`Instance` 均为 `internal`（需 `BindingFlags.NonPublic`） |
| `ModListFilterPatch.cs` | `GetGameplayRelevantModNameList` 的 Prefix，从联机 Mod 列表移除本 Mod |
| `CheatLocHelper.cs` | 反射 `LocString`，汉化角色名/遗物名/指令（无编译期 LocDB 引用） |
| `HarmonyPatches/` | 各补丁类分目录存放 |
| `ClientDiagnosticPatches.cs` | 客机诊断模块：Patch A/B/C 三步协作修复 NetId 黑屏问题，含完整日志追踪 |
| `NetIdFixTranspiler.cs` | NetId 修正注册表 API（仅存储，已不再 Patch 游戏 IL） |

---

## 核心设计决策

### 初始化时序（三重保险）

```
Harmony 静态补丁 → ModManager.Initialize Postfix → ProcessFrame ×2帧 → EnsureInitialized()
```

- **时机 1**：静态构造函数（Harmony `PatchAll` 时，可能 Engine==null，静默跳过）
- **时机 2**：`ModManager.Initialize` Postfix（Engine 应该就绪，调度 ProcessFrame ×2）
- **时机 3**：`ClientCheatBlockPrefix.Prefix` 内调用（作弊首次触发时兜底）

### 历史面板懒构建

- `ToggleHistoryPanel()` / `ShowPanelUI()` 调用 `EnsureHistoryPanelCreated()`
- `CheatHistoryPanel` 是 `CanvasLayer`，首次 `ShowPanel()` 时若 `_BuildUI()` 尚未调用，由 `EnsureWindowBuilt()` 兜底
- **`_Ready()` 覆盖调用 `_BuildUI()`**——这是必须的，之前漏掉了导致窗口从不出现

### 顶栏呼出按钮

- Hook `NTopBar._Ready`（运行时 `AccessTools.TypeByName` 解析，无编译期类型引用）
- `AddChild(btn, false, Node.InternalMode.Disabled)` — Disabled 模式避免截获其他控件输入
- `btn.Pressed += () => NoClientCheatsMod.ToggleHistoryPanel()` — 完全绕 ModConfig

### 快捷键轮询机制

```csharp
// InputHandlerNode._Process
bool down = Input.IsKeyPressed(key);
if (down && !_prevDown) { ToggleHistoryPanel(); }
_prevDown = down;
```

- `_Process`（非 `_Input`）完全被动，不调用 `SetInputAsHandled()`
- `ProcessMode = Always`（暂停时也轮询）
- `prevDown` 提供边沿检测（按下触发，非按住持续触发）

### ModConfig 反射注册

- `ModConfigApi` / `ConfigEntry` / `ConfigType` / `ModConfigManager` 均通过 `Type.GetType("...ModConfig, ModConfig")` 运行时解析
- 操作按钮使用 `ModConfigManager.SetValue`（私有 API）绕过 `OnChanged` 回调，防止递归
- 注册延迟 2 帧（`ProcessFrame += OnFrame1` → `OnFrame2` → `DoRegister`）

### 本地化（Localization）

- `Localization.cs`：语言**每次 `Tr()` 时**解析，随设置页改语言生效（无需重启）
- **优先**反射 `MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language`（与游戏内 Language 下拉一致）；`zho`/`chs`/`zhs`/`zh*` → 中文，其余（如 `eng`）→ 英文
- **回退** `TranslationServer.GetLocale()`（STS2 常仍为系统区域，不可靠，仅作兜底）
- `Localization.Tr` / `Localization.Trf` 返回当前语言字符串；`Trf` 内部用 `string.Format`，占位符须为 **`{0}`、`{1}`**（勿用 C 风格 `%d`/`%s`）
- **禁止**在继承 `Node` 的类里 `using static Localization` 后写裸 `Tr(...)`：会与 Godot **`Node.Tr`** 冲突，解析到引擎翻译，找不到 key 就原样显示 key 名（如 `btn_clear`、`empty`）。应写 **`Localization.Tr`**
- 所有 UI 字符串集中在 `_tr` 字典：`_tr["key"] = ("English", "中文")`

---

## 关键数据结构

```csharp
// 作弊记录（record）
public record CheatRecord(
    string Time,        // "HH:mm:ss"
    string SenderName,  // Steam 显示名
    string CharacterName, // "CHARACTER.IRONCLAD" 或 "IRONCLAD"
    string Command,    // 原始指令如 "relic ICE_CREAM"
    ulong SenderId,    // Steam ID
    bool WasBlocked    // 是否被拦截（true=拦截，false=允许但记录）
);

// 历史记录列表（NoClientCheatsMod 静态字段）
List<CheatRecord> _historyRecords; // 容量上限 HistoryMaxRecords * 2
object _historyLock;                 // 线程安全保护
```

---

## 已知坑点（Godot 4 + Harmony + C#）

| 问题 | 详情 |
|------|------|
| **静态初始化时 Engine 为 null** | Harmony 补丁在 Godot 场景树创建前就应用，所有 Godot API 调用必须用 `CallDeferred` 或 `ProcessFrame` 延迟 |
| **Harmony Patch Godot 节点类** | 游戏类型用 `AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes...")` 运行时解析；搜索 `_Ready` 时 `BindingFlags` 要含 `all` |
| **`_Input` 覆盖会阻断游戏输入** | `InputHandlerNode` 若覆盖 `_Input` 并调用 `SetInputAsHandled()` 会破坏游戏输入。解决：只用 `_Process` 轮询 |
| **Godot 节点作为静态类字段** | 必须 `GodotObject.IsInstanceValid(node)` 检查——Godot 会静默使已释放的节点失效 |
| **拖拽/缩放用 CanvasLayer._Input** | 不用 `GuiInput` 信号可避免焦点冲突。边缘检测和光标形状全在单个 `_Input` 覆盖里处理 |
| **ModConfig 操作按钮递归** | `OnChanged` 在每次值变化时触发；在 `OnChanged` 内调用 `ModConfig.SetValue` 会死循环。解决：用私有 `ModConfigManager.SetValue`（反射）绕过回调 |
| **注入节点用 InternalMode.Disabled** | `AddChild(node, false, Node.InternalMode.Disabled)` 防止注入按钮截获本来流向其他控件的 Godot 输入事件 |
| **角色 ID 格式不统一** | 可能是 `"CHARACTER.IRONCLAD"` 也可能是 `"IRONCLAD"`，`CheatLocHelper` 两边都处理 |
| **多个通知弹窗同时出现** | `_visible.Count >= MAX_VISIBLE(4)` 保护防止无限堆积；旧条目必须完成淡出才能出现新条目 |
| **struct 参数被 boxing 导致 Harmony Patch 不生效** | `SerializablePlayer` 是 struct，参数通过 `object` 传参时触发 boxing，Prefix/Finalizer 修改的是副本，原方法看不到改动。这是导致副机强退补丁全部失效的根本原因之一。详见 `docs/NCC_NetId_思路分析.md` 第2.3节。**已解决**：Patch C 通过反射调用同步，跳过原方法避免 boxing 副本问题 |
| **客机 NCC 被游戏设置禁用** | 客机 NCC 被 `it is set to disabled in settings` 跳过，导致修正补丁无法在客机运行，是副机强退的直接原因。**修复**：客机安装 NCC DEBUG 版并在设置中启用 |

---

## 联机同步关键机制

### NetHostGameService 消息发送

```csharp
// NetHostGameService.SendMessage<T>(T message, ulong peerId)  ← NCC 用这个发回滚消息
// → SendMessageToClientInternal(message, peerId, channel, overrideSenderId=null)
// → SerializeMessage(overrideSenderId ?? this._netHost.NetId, message, ...)
// 当 overrideSenderId=null 时，packet header senderId = 主机NetId
// 【关键】msg.player.NetId = 副机NetId（作弊玩家的 Steam ID），与 senderId（主机NetId）不匹配
```

**NetId 错配的根源**：NCC 用 `correctSnapshot.ToSerializable()` 填充消息，`SerializablePlayer.NetId` = 副机的 Steam ID。但发送时 `senderId` = 主机NetId（由网络层自动填入包头）。副机收到后，用 `senderId`（主机NetId）查找 Player 对象，找到的是自己的 Player（其 NetId 已被游戏设为主机NetId），然后用 `msg.player.NetId`（副机NetId）做比较——两边 NetId 不等，强退。

### CombatStateSynchronizer WaitForSync 流程

```csharp
// 副机收到回滚消息时：
// 1. packet header senderId = 主机NetId
// 2. 反序列化：_syncData[主机NetId] = msg.player（作弊玩家的 SerializablePlayer，NetId=副机NetId）
// 3. WaitForSync: GetPlayer(主机NetId) → 找到副机的 Player（NetId=主机NetId）
// 4. SyncWithSerializedPlayer(msg.player)
//    → 检查 msg.player.NetId != this.NetId → 副机NetId != 主机NetId → 强退！
```

**代码证据**（`CombatStateSynchronizer.cs` 第70行）：
```csharp
this._syncData[senderId] = syncMessage.player;  // 用 senderId（主机NetId）作为 key
// 副机用 senderId 查 _syncData，找到 msg.player，再用 senderId 查 Player，两边 NetId 不等
```

### SerializablePlayer.NetId 与 Player.NetId 的区别

| 对象 | NetId 来源 | 说明 |
|------|-----------|------|
| `Player.NetId` | 构造函数传入 | 副机的 Player.NetId = **主机NetId**（游戏网络层分配，特殊处理） |
| `SerializablePlayer.NetId` | `ToSerializable()` 时复制 | 副机的 SP.NetId = 副机NetId（作弊玩家的 Steam ID） |

这两个不是同一个值！游戏用来查找玩家的 `NetId` 与 `SerializablePlayer` 里的 `NetId` 来自不同体系。

### 关键源代码文件（游戏 DLL 存档）

```
K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260322\sts2\MegaCrit\sts2\
├── Core\Multiplayer\
│   ├── CombatStateSynchronizer.cs     ← WaitForSync，强退点，第70行 _syncData[senderId]=msg.player
│   ├── NetHostGameService.cs          ← SendMessage，senderId 来源，第168行
│   ├── NetMessageBus.cs               ← SerializeMessage，senderId 写入
│   └── Messages\Game\
│       └── SyncPlayerDataMessage.cs   ← 消息结构
├── Core\Saves\Runs\
│   └── SerializablePlayer.cs          ← SerializablePlayer.NetId
└── Core\Entities\Players\
    └── Player.cs                      ← Player.NetId，SyncWithSerializedPlayer
```

### STS2 联机 NetId 分配机制（重要补充）

游戏中 `Player.NetId` 的分配策略：
- 每个端（host/client）在**加入联机游戏时**，从本地的 `ConnectedPlayer` 列表中获取
- 主机端：`HostNetId = 本机 Steam ID`
- 副机端：自己的 NetId 可能被设置为**主机的 Steam ID**（STS2 的特殊处理）
- 这导致同一个玩家在不同端有不同的 NetId 表示

**结论**：`senderId` 和 `SerializablePlayer.NetId` 在不同端代表的含义可能不同，跨端发送带 NetId 的消息时需要特别注意。

> **本项目核心问题的根因就在这里**：详见 `docs/NCC_NetId_思路分析.md` 中的「根本原因」和「技术可行性评估」章节。

### 修复方案：ClientDiagnosticPatches.cs 三步协作

**文件**：`ClientDiagnosticPatches.cs`

**核心思路**：利用 Harmony Patch 机制在关键方法间传递上下文信息，通过 ThreadLocal 在 Patch B → Patch C 间桥接：

1. **Patch A**（`OnSyncPlayerReceived_Patch`）：客机收到 `SyncPlayerDataMessage` 时记录 senderId 和 msg.player.NetId，写入 `NCC_diag.log`
2. **Patch B**（`WaitForSync_Patch.Prefix`）：遍历 `_syncData` 时检测 NetId 错配（key ≠ v.NetId），用 `ThreadLocal` 存储 `_pendingNCCSnapshot` 和 `_pendingNCCPlayerNetId`，设置 `_nccRollbackDetected=true`
3. **Patch C**（`SyncWithSerializedPlayer_Patch.Prefix`）：
   - 检测 `_nccRollbackDetected == true` → 进入 NCC 回滚上下文
   - 用 `_pendingNCCPlayerNetId`（= msg.player.NetId = 副机真实 ID）通过 `RunState.GetPlayer()` 查找 Player（找到的是客机本地的正确 Player 对象）
   - 直接调用 `localPlayer.SyncWithSerializedPlayer(nccSnapshot)`（NetId 匹配，不会抛异常）
   - 返回 `false` 跳过原方法（避免游戏内部再次比较导致强退）
   - 若无 NCC 上下文，则记录详细日志后继续执行原方法（游戏正常报错）

**关键变量**（均为 `ThreadLocal`，避免线程安全问题）：
```csharp
private static readonly ThreadLocal<object> _pendingNCCSnapshot = new();
private static readonly ThreadLocal<ulong> _pendingNCCPlayerNetId = new();
private static readonly ThreadLocal<bool> _nccRollbackDetected = new();
private static readonly ThreadLocal<bool> _prefixSkippedOriginal = new();
```

**部署**：
- NCC DEBUG 版构建后，客机将 DLL/PCK 复制到客机 mods 目录并在设置中启用
- 诊断日志输出到 `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\NCC_diag.log`
- 日志标签：`[NCC-DIAG][SyncRecv]`、`[NCC-DIAG][WaitSync]`、`[NCC-DIAG][SyncPlayer]`、`[NCC-DIAG][Context]`

---

## ForkedRoad 参考模式（自定义网络消息）

### ForkedRoad 自定义消息模式

参考 `Snoivyel/STS2-Forked-Road`（v1.0.3，MIT 协议）：

```csharp
public struct ForkedRoadBranchStartMessage : INetMessage
{
    public int actIndex;
    public MapCoord coord;
    public int branchSequence;
    public int remainingBranches;
    public List playerIds;

    public bool ShouldBroadcast => true;   // 广播到所有玩家
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer) { ... }
    public void Deserialize(PacketReader reader) { ... }
}
```

**关键设计**：
- `ShouldBroadcast = true`：消息通过 `NetHostGameService.SendMessage(msg)`（无 peerId 参数）广播
- 广播时 senderId 由游戏网络层自动处理，不存在 overrideSenderId 问题
- Handler 在 `InitializeForRun` 中注册：`netService.RegisterMessageHandler(HandleMethod)`
- Handler 方法签名：`void HandleMethod(TMessage msg, ulong senderId)`

### INetMessage 接口实现要点

```csharp
// 完整实现模式（参考 ForkedRoadBranchStartMessage）
public struct NCCRollbackNotifyMessage : INetMessage
{
    public ulong targetPlayerNetId;        // NCC 新增：明确指定目标玩家的 NetId
    public ulong serializedDeckData;       // NCC 新增：作弊前的卡组数据（JSON 或二进制）
    public bool ShouldBroadcast => false;  // 点对点，不要广播
    public NetTransferMode Mode => NetTransferMode.Reliable;
    public LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(targetPlayerNetId);
        writer.WriteString(serializedDeckJson);
    }

    public void Deserialize(PacketReader reader)
    {
        targetPlayerNetId = reader.ReadULong();
        serializedDeckJson = reader.ReadString();
    }
}
```

### ForkedRoad 消息发送模式

```csharp
// ForkedRoadManager 中的消息发送
_netService.SendMessage(new ForkedRoadBranchMergeMessage { ... });
// 直接调用无 peerId 的 SendMessage，ShouldBroadcast 决定是否广播

// 发送端（Host）：
// → SendMessage(msg) → SendMessageToClientInternal → Broadcast=true → 发给所有 peers
// → 广播时每条消息的 senderId = 游戏自动填充（senderId=主机NetId）
// 接收端（Client）：
// → 收到广播，handler 接收 msg 和 senderId
// → 接收端使用 senderId 和 msg 里的 targetPlayerNetId 做业务逻辑
```

---

## 开发备忘

### 构建

```powershell
cd NoClientCheats
.\build.ps1           # 构建 + 同步到 Steam mods + 快照到 torelease
# 测试游戏功能...
.\prepare-release.ps1  # 从 torelease 打包（强制检查，缺文件则报错）
```

需要：.NET 8 SDK、Godot 4.5.1 Mono。

**发布安全规则**：
- `build.ps1` 同时写入 `Steam mods\NoClientCheats\` 和项目 `torelease\`
- `prepare-release.ps1` **只从 `torelease\` 打包**，且在检测到文件缺失时立即报错退出
- 绝不能跳过 `build.ps1` 直接运行 `prepare-release.ps1`，否则打包的是旧文件

**GitHub Release Tag 格式**：`NCC_v{主}.{次}.{修订}`（如 `NCC_v1.2.0`），与 release title（如 `No Client Cheats v1.2.0`）分开。发布命令：

```powershell
# 1. 提交源码
git add NoClientCheatsMod.cs MEMORY.md README.md mod_manifest.json build.ps1 prepare-release.ps1 release_body.md release_notes.txt
git commit -m "NoClientCheats v1.2.0: 大厅聊天广播 + 反射修复"
git push

# 2. 打 tag（须与 GitHub Release tag 名一致，如 NCC_v1.2.0）
git tag NCC_v1.2.0
git push origin NCC_v1.2.0

# 3. 创建 GitHub Release 并上传 zip
gh release create NCC_v1.2.0 --repo Jianbao233/STS2_mod --title "No Client Cheats v1.2.0" --notes-file release_body.md
gh release upload NCC_v1.2.0 release/NoClientCheats-v1.2.0.zip --repo Jianbao233/STS2_mod
```

### 调试日志（GD.Print 输出）

| 日志前缀 | 来源 |
|----------|------|
| `[NoClientCheats]` | `NoClientCheatsMod` 初始化/加载 |
| `[NCCInputHandler]` | `InputHandlerNode` EnterTree / Ready / key pressed |
| `[NoClientCheats] LanConnect | `LanConnectBridge` 桥接日志：类型未找到、反射未找到、桥接成功/失败/跳过原因 |
| `[NoClientCheats] ModConfig | `ModConfigIntegration` 注册失败 |
| `[NoClientCheats] Top bar | `TopBarHistoryButtonPatch` 注入结果 |

---

## 提示词（快速承接）

| 场景 | 提示词 |
|------|--------|
| 继续开发 | "继续 NoClientCheats 项目" |
| 查看快捷键机制 | "InputHandlerNode 的轮询逻辑是怎样的" |
| 查看 ModConfig 集成 | "ModConfig 反射注册是怎么实现的" |
| 查看作弊拦截 | "ClientCheatBlockPatch 怎么拦截作弊指令" |
| 查看通知弹窗 | "CheatNotification 弹窗是怎么实现的" |
| 构建发布 | "发布 NoClientCheats 新版本" |
