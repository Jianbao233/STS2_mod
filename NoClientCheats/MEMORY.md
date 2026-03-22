# NoClientCheats · 项目记忆

> 本文件为 NoClientCheats 项目的专属记忆，每次新对话开始时请先阅读本文。

---

## 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\` |
| **目标** | 多人联机时禁止客机（非房主）使用控制台作弊指令 |
| **部署** | 仅房主需安装，客机无需安装 |
| **依赖** | ModConfig（游戏内配置）、Harmony（游戏内置） |
| **仓库** | `https://github.com/Jianbao233/STS2_mod` |

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
| `ModListFilterPatch.cs` | `GetGameplayRelevantModNameList` 的 Prefix，从联机 Mod 列表移除本 Mod |
| `CheatLocHelper.cs` | 反射 `LocString`，汉化角色名/遗物名/指令（无编译期 LocDB 引用） |
| `HarmonyPatches/` | 各补丁类分目录存放（`ClientCheatBlockPatch.cs` 等） |

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

---

## 开发备忘

### 构建

```powershell
cd NoClientCheats
.\build.ps1
# 产物复制到 Steam mods\NoClientCheats\ 目录
```

需要：.NET 8 SDK、Godot 4.5.1 Mono

### 发布

```powershell
gh release create v1.1.5 --title "v1.1.5" --notes "..."
# 上传 NoClientCheats.dll 到 release assets
```

### 调试日志（GD.Print 输出）

| 日志前缀 | 来源 |
|----------|------|
| `[NoClientCheats]` | `NoClientCheatsMod` 初始化/加载 |
| `[NCCInputHandler]` | `InputHandlerNode` EnterTree / Ready / key pressed |
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
