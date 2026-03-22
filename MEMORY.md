# STS2_mod · 工作区主记忆

> 本文件为 `STS2_mod/` 工作区的总记忆，每次新对话开始时请先阅读本文。

---

## 工作区概述

| 项目 | 路径 | 说明 |
|------|------|------|
| **主目录** | `K:\杀戮尖塔mod制作\STS2_mod\` | 所有 STS2 Mod 源码的父目录 |
| **仓库** | `https://github.com/Jianbao233/STS2_mod` | GitHub 仓库，git 管理 |
| **目标游戏** | Slay the Spire 2（Steam 正式版） |
| **开发环境** | .NET 8 SDK + Godot 4.5.1 Mono + Harmony（游戏内置） |

---

## 模组清单

| 模组 | 路径 | 功能 |
|------|------|------|
| **NoClientCheats** | `NoClientCheats\` | 禁止客机作弊（拦截控制台指令 + 历史面板 + 顶栏呼出按钮） |
| **RunHistoryAnalyzer** | `RunHistoryAnalyzer\` | 历史记录作弊检测（金币守恒、HP守恒、卡牌/遗物来源追溯） |
| **RichPing** | `RichPing\` | 多人联机延迟显示（参考 DamageMeter） |
| **HostPriority** | `HostPriority\` | 房主权限管理 |
| **ControlPanel** | `ControlPanel\` | 控制面板 |
| **SharedConfig** | `SharedConfig\` | 共享配置 |
| **MP_PlayerManager** | `MP_PlayerManager\` | 多人玩家管理 |
| **MP_SavePlayerRemover** | `MP_SavePlayerRemover\` | 多人存档玩家移除工具 |

---

## 各模组专属记忆

| 模组 | 记忆文件 |
|------|----------|
| NoClientCheats | `NoClientCheats\MEMORY.md` |
| RunHistoryAnalyzer | `RunHistoryAnalyzer\MEMORY.md` |

---

## 通用技术决策

### 框架栈

```
Slay the Spire 2
  └─ Godot 4.5.1 Mono（游戏引擎）
       └─ Harmony 2.x（内置，无需额外安装）
            └─ 各 Mod（PatchAll() 自动发现 [HarmonyPatch] 类）
```

### ModConfig 集成模式（所有模组通用）

**不引用编译期 ModConfig DLL**——所有模组均通过反射运行时解析：

```csharp
var apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig");
var entryType = Type.GetType("ModConfig.ConfigEntry, ModConfig");
var managerType = asm.GetType("ModConfig.ModConfigManager");
```

**注册延迟 2 帧**（`Engine.GetMainLoop()` 在静态构造时可能为 null）：

```csharp
tree.ProcessFrame += OnFrame1; // 帧1
// OnFrame1:
tree.ProcessFrame -= OnFrame1;
tree.ProcessFrame += OnFrame2; // 帧2
// OnFrame2: DoRegister()
```

### 初始化三重保险（所有主动作 Mod 通用）

1. **静态构造**：Harmony `PatchAll` 时尝试（Engine 可能为 null，静默跳过）
2. **Postfix**：`ModManager.Initialize` 的 Harmony Postfix（Engine 应该就绪）
3. **懒触发**：业务逻辑首次触发时（如作弊首次拦截）兜底调用

### Godot 节点与静态字段

Godot 节点作为静态类字段时，必须在每次访问前检查 `GodotObject.IsInstanceValid(node)`——Godot 会静默使已释放的节点失效。

### Godot 输入与游戏输入兼容

**禁止**在 Mod 节点覆盖 `_Input` 并调用 `SetInputAsHandled()`——这会阻断游戏自身的输入处理。

正确做法：
- 纯轮询：`Node._Process` + `Input.IsKeyPressed()` + edge detection（`prevDown`）
- `ProcessMode = ProcessModeEnum.Always` 让暂停时也继续

### ModConfig 操作按钮防递归

`OnChanged` 在每次值变化时触发。在 `OnChanged` 内调用 `ModConfig.SetValue` 会触发新的 `OnChanged`，导致死循环。

解决：用私有 `ModConfigManager.SetValue`（反射）绕过 ModConfig 的 `OnChanged` 回调链，直接重置值。

### Harmony Patch 游戏节点类

游戏内置类型（如 `NTopBar`）无编译期引用。用 `AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar")` 运行时解析。搜索目标方法时 `BindingFlags` 必须含 `AccessTools.all`。

---

## 通用源码索引

| 类型/方法 | 来源 | 说明 |
|-----------|------|------|
| `ModManager.Initialize` | 游戏主程序集 | Mod 加载入口 |
| `ActionQueueSynchronizer.HandleRequestEnqueueActionMessage` | 游戏主程序集 | 多人联机动作入队，是作弊指令拦截点 |
| `ModManager.GetGameplayRelevantModNameList` | 游戏主程序集 | 联机时发给客户端的 Mod 列表 |
| `NTopBar._Ready` | 游戏内置节点 | 游戏顶栏加载完毕事件（注入按钮的时机） |
| `LocString` | 游戏内置 | 文本本地化字符串 |
| `RunState.CurrentRun.Players` | 游戏内置 | 当前跑动的玩家列表（用于匹配作弊者角色） |

**源码参考目录**：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260318\sts2\`

---

## 通用开发备忘

- **构建**：各模组目录下 `build.ps1`（参考 `NoClientCheats\build.ps1`）
- **发布**：`git push` 后手动 `gh release create vX.X.X` 并上传 DLL
- **调试日志**：用 `GD.Print("[ModName] ...")` 输出，在游戏安装目录日志中查看
- **GitHub Releases**：`https://github.com/Jianbao233/STS2_mod/releases`

---

## 提示词（快速承接）

| 场景 | 提示词 |
|------|--------|
| 继续 NoClientCheats | "继续 NoClientCheats 项目" |
| 继续 RunHistoryAnalyzer | "继续 RunHistoryAnalyzer 项目" |
| 了解作弊拦截逻辑 | "NoClientCheats 是怎么拦截作弊指令的" |
| 构建发布 | "发布 NoClientCheats 新版本" |
| 查看项目清单 | "工作区有哪些模组" |
