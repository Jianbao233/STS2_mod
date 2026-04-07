# MultiplayerTools 项目记忆

> 记录架构决策、已知陷阱和规范要求，避免重复踩坑。

---

## Godot 4 C# UI 节点规范

### CanvasLayer 必须加入场景树

**所有 `CanvasLayer` 在创建后必须显式加入根节点：**

```csharp
// ✅ 正确
_layer = new CanvasLayer { Layer = 200, Name = "YourPanel" };
(Engine.GetMainLoop() as SceneTree)?.Root?.AddChild(_layer, false, Node.InternalMode.Disabled);

// ❌ 错误 — 创建了但没有加入树，节点完全不渲染
_layer = new CanvasLayer { Layer = 200 };
```

**验证方法：** 检查 `node.GetTree() != null`

### Control 节点锚点规范

为了避免布局计算不生效，所有 Control 子节点**必须同时设置锚点和偏移**：

```csharp
// ✅ 正确 — 锚点 + Offset 一起设置
var node = new Control {
    AnchorLeft = 0, AnchorRight = 1,
    AnchorTop = 0, AnchorBottom = 1,
    OffsetRight = 0, OffsetBottom = 0
};

// ⚠️ 危险 — 只设置锚点不设置偏移，依赖默认值，容易在某些分辨率下行为不一致
var node = new Control {
    AnchorLeft = 0, AnchorRight = 1
};
```

### backdrop（全屏遮罩）完整配置

用于拦截面板外点击关闭的 backdrop，需要：
```csharp
var backdrop = new ColorRect {
    AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
    OffsetRight = 0, OffsetBottom = 0,
    Color = Panel.Styles.Backdrop,
    MouseFilter = Control.MouseFilterEnum.Ignore  // 让点击穿透到后面的节点
};
```

### CallDeferred 在 Godot 4 C# 中的用法

`Node.CallDeferred()` 接受方法名字符串（`StringName`），不接受 `Callable`：

```csharp
// ✅ 正确
node.CallDeferred("set_visible", true);

// ❌ 错误 — Godot 4 C# 中 CallDeferred 不接受 Callable
node.CallDeferred(Callable.From(() => DoSomething()));
```

若需要传递复杂逻辑，使用 `Timer` 或 `InvokeDeferred` 辅助类。

### Timer 创建后必须入树

```csharp
// ✅ 正确 — 先 AddChild 再 Start
var timer = new Timer { OneShot = true, WaitTime = 0.1f };
node.AddChild(timer);
timer.Timeout += () => { /* ... */ };
timer.Start();

// ❌ 错误 — 在入树前调用 Start() 会报错
```

---

## 项目结构

```
MultiplayerTools/
├── src/
│   ├── MultiplayerToolsMain.cs   # 入口，Harmony 补丁、F1输入节点
│   ├── MpPanel.cs                # 面板主类，Build/Toggle/Show/Hide
│   ├── Panel/
│   │   ├── Styles.cs             # 面板样式定义
│   │   └── UiFont.cs             # 字体大小缩放
│   ├── Tabs/                     # 各功能页（Save/Player/Character/...）
│   ├── Core/                     # 核心逻辑（MpSessionState, SaveManagerHelper...）
│   ├── Steam/                    # Steam API 集成
│   ├── Config.cs                 # 配置管理（热键、语言、字号）
│   └── Loc.cs                    # 国际化
├── build.ps1                     # 构建脚本
└── mod_manifest.json
```

## 关键静态单例

- `MpPanel._layer` — CanvasLayer，整个 UI 的根节点
- `MpPanel._panel` — 主面板 Panel，覆盖全屏
- `Config` — 配置单例，在 `OnFrame2` 中加载
- `MpSessionState` — 存档上下文状态
- `F1InputNode` — 挂载在 `SceneTree.Root` 下，捕获 F1 按键

## 当前构建配置

- 目标框架：.NET 9
- Godot API 版本：4.5.1-mono
- 构建输出：`.godot/mono/temp/bin/Debug/MultiplayerTools.dll`
- 自动部署：`K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MultiplayerTools`

---

## 安卓端存档适配结论（2026-04-07）

- 安卓端多人/进度存档根目录不能再假设为 `%APPDATA%\SlayTheSpire2\steam\{steamId}`
- 实际应兼容游戏本体账户目录：`user://default/{localPlayerId}/[modded/]profileN/saves/`
- 当前测试设备上，本地玩家 ID 为 `1`，对应实际目录：
  - `files/default/1/profile1/saves/`
  - `files/default/1/modded/profile1/saves/`
- 安卓端 `current_run_mp.save` 与 PC 端格式一致；问题核心在于：
  - 路径扫描必须支持 `steam` / `default` / `editor`
  - 移动端当前身份不能依赖 Steam API，占位文本会导致目录推导失败

## 发布打包注意事项（2026-04-07）

- `build.ps1` 只负责构建并覆盖到游戏 `mods/MultiplayerTools/`，**不会自动刷新 `torelease/`**
- 进行 GitHub Release 前，必须手动同步：
  - `.godot/mono/temp/bin/Debug/MultiplayerTools.dll` → `torelease/MultiplayerTools.dll`
  - `mod_manifest.json` → `torelease/mod_manifest.json`
  - 生成 `torelease/last_build.txt`
- `v0.2.0` 的正式发布包为：
  - `release/MultiplayerTools-v0.2.0.zip`
- 主仓库 GitHub Release：
  - `https://github.com/Jianbao233/STS2_mod/releases/tag/v0.2.0`
