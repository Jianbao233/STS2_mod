# MP_PlayerManager · 子记忆文本

> 本文件为 MP_PlayerManager v2 项目的专属记忆，每次新对话开始时请先阅读本文。  
> 当前版本：v2（开发中）
> 更新日期：2026-03-23

---

## 1. 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\` |
| **目标** | 在 FreeLoadout Mod 框架内扩展，实现角色模板配置 + 多人存档管理 |
| **发布** | https://github.com/Jianbao233/STS2_mod/releases |
| **父项目** | `MP_PlayerManager_v1/`（v1 独立 exe 工具，已归档） |

---

## 2. 活跃版本

| 版本 | 状态 | 说明 |
|------|------|------|
| **v1** | 已归档 | 独立 exe 工具，Python + PyInstaller，约 8.4MB |
| **v2** | 开发中 | Godot Mod（FreeLoadout 扩展）+ 外部工具（PyQt-SiliconUI），C# + Python |

---

## 3. 当前开发状态（v2）

### 3.1 阶段：FreeLoadout 扩展项目结构已创建（构建待环境配置）

- [x] 确认方案（方案2：Mod 内嵌）
- [x] 完成 v2 方案文档
- [x] 完成 FreeLoadout 源码分析
- [x] 分析 PyQt-SiliconUI，确认用于外部工具 GUI
- [x] FreeLoadout 扩展项目结构创建（命名空间重命名、配置文件、README）
- [x] README 致谢 FreeLoadout 作者 @BravoBon
- [ ] 完成首次 dotnet build 编译（**待安装 Godot 4.5.1 Mono 或 .NET 9 SDK**）
- [ ] 补写 TrainerBuffUi.cs 残留 NullableAttribute
- [ ] 重写/重新获取 13 个被排除的 UI 文件
- [ ] 开始 TemplatesTab.cs 开发（**待构建环境就绪**）
- [ ] 开始外部工具开发（PyQt-SiliconUI）

### 3.2 核心文件路径

| 文件 | 用途 |
|------|------|
| `doc/v2_方案文档.md` | 当前开发依据 |
| `doc/v1_方案文档.md` | 归档参考 |
| `WORKFLOW_RULES.md` | 工作流规则 |
| `MEMORY.md` | 本文件 |
| `tools/PYQT_SILICON_UI_ANALYSIS.md` | PyQt-SiliconUI 完整架构分析 |
| `FreeLoadout/README.md` | FreeLoadout 扩展开发说明 |

### 3.3 参考数据

| 资源 | 路径 |
|------|------|
| FreeLoadout 源码 | `K:/杀戮尖塔mod制作/Tools/freeloadout解包/FreeLoadout/` |
| FreeLoadout UI 关键文件 | `LoadoutPanel.cs`, `CardsTab.cs`, `RelicsTab.cs`, `CharacterTab.cs` |
| FreeLoadout 入口 | `TrainerBootstrap.cs`（`[ModInitializer("Init")]`） |
| PyQt-SiliconUI | `tools/PYQT_SILICON_UI_ANALYSIS.md` |
| STS2 modding 基础 | `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\` |
| 存档分析文档 | `K:\杀戮尖塔mod制作\STS2_mod\VC_STS2_SAVE_FILE_ANALYSIS.md` |

---

## 4. 架构决策

### 4.1 双端架构

```
游戏内（Mod / C#）
  └── FreeLoadout 扩展
      ├── 模板配置 Tab（TemplatesTab.cs）
      ├── 模板持久化（TemplateManager.cs）
      └── 导出 JSON

游戏外（外部工具 / Python + PyQt-SiliconUI）
  ├── PyQt-SiliconUI（灵动优雅的桌面 UI）
  ├── 读取模板 JSON
  ├── 读取 current_run_mp.save
  └── 向存档注入/替换/删除玩家
```

### 4.2 为什么选 PyQt-SiliconUI 作为外部工具 GUI

- **灵动优雅**：自绘控件 + 内置动画（SiExpAnimation），UI 体验远优于标准 PyQt5
- **零资产**：图标从游戏 extracted/ 读取，外部工具无需打包大量图片
- **游戏内已有资产复用**：Mod 导出的 JSON 中含卡牌/遗物 ID，工具只需显示名称，无需内嵌图标
- **深色主题原生支持**：DarkColorGroup 开箱即用，契合 STS2 风格

### 4.3 为什么选方案2（Mod 内嵌）

- **零资产**：直接引用游戏内置卡牌/遗物图标，无需打包，不增加 Mod 体积
- **实时数据**：从 `ModelDb` 获取卡牌/遗物数据，永远与游戏版本同步
- **游戏内交互**：玩家在游戏内直接配置模板，体验流畅
- **技术可行性**：FreeLoadout 已有完整的 UI 框架（Godot C#），可直接扩展

### 4.4 存档操作时机

- **模板配置**：游戏**内**进行（Mod）
- **存档修改**：游戏**外**进行（外部工具，读取模板 JSON + 存档 JSON）
- **理由**：存档文件需要独占写入，无法在游戏运行时修改

### 4.5 Steam 好友用途

仅用于**显示**：读取好友列表，将 `steam_id` 映射为昵称，**不做任何网络通信**。

---

## 5. PyQt-SiliconUI 快速备忘

### 5.1 必读文档

`tools/PYQT_SILICON_UI_ANALYSIS.md` — 完整架构分析，以下是关键摘要：

### 5.2 初始化（必须在任何窗口前调用）

```python
import siui
from siui.gui import reload_scale_factor
reload_scale_factor()
```

### 5.3 推荐控件

```
SiPushButton       — 普通按钮
SiToggleButton     — 开关按钮
SiSwitch           — 滑动开关
SiCheckBox         — 多选框
SiLabel            — 文字标签
SiIconLabel        — 图标+文字
SiSvgLabel         — SVG 图标
SiPixLabel         — 图片标签
SiCard             — 卡片容器
SiScrollArea       — 滚动区域
```

### 5.4 基础用法

```python
from siui.components import SiPushButton, SiCard

btn = SiPushButton()
btn.setText("添加玩家")
btn.resize(120, 40)
btn.clicked.connect(handler)

card = SiCard()
card.setTitle("玩家列表")
card.setBodyContent(widget)
```

### 5.5 图标

```python
from siui.core import SiGlobal
iconpack = SiGlobal.siui.iconpack
iconpack.setDefaultColor("#D1CBD4")
svg_bytes = iconpack.get("ic_fluent_add_filled")
```

### 5.6 ⚠️ 禁用清单

- `templates/` 模块——重构中，禁止用于生产
- `SiLineEdit`——重构中，可能不稳定
- 未在推荐清单中的旧组件

---

## 6. FreeLoadout UI 框架关键点（备忘）

### 6.1 样式颜色

```csharp
StsColors.cream   // Color(0.91f, 0.86f, 0.75f)  — 正文
StsColors.gold    // Color(0.85f, 0.73f, 0.35f)  — 标题/高亮
StsColors.gray    // Color(0.5f, 0.5f, 0.5f)     — 禁用
StsColors.red     // Color(0.7f, 0.2f, 0.15f)     — 删除
StsColors.blue    // Color(0.4f, 0.6f, 1f)        — 特殊
// 背景：Color(0.08f, 0.06f, 0.1f, 0.95f)
```

### 6.2 关键构建函数

```csharp
LoadoutPanel.CreateTabButton(text)        // Tab 按钮
LoadoutPanel.CreateItemButton(text, ...) // 项目按钮
LoadoutPanel.CreateActionButton(text)    // 操作按钮
LoadoutPanel.CreateToggleButton(text, on)// 开关按钮
LoadoutPanel.CreateSectionHeader(text)   // 分组标题
LoadoutPanel.ClearChildren(container)    // 清空容器
LoadoutPanel.RequestRefresh()           // 刷新当前 Tab
```

### 6.3 数据获取

```csharp
ModelDb.AllCards         // 所有卡牌
ModelDb.AllRelicPools    // 所有遗物池
ModelDb.AllCharacters    // 所有角色
RunManager.Instance.DebugOnlyGetState()  // 当前跑图
LocalContext.GetMe(runState)             // 当前玩家
```

---

## 7. 开发备忘

### 7.1 FreeLoadout 扩展项目状态（2026-03-23）

**已完成**：
- 项目结构：`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\`
- 源码：36 个干净 C# 文件（命名空间重命名 + 清理反编译残留）
- 配置：`MP_PlayerManager.csproj`（Godot.NET.Sdk + Lib.Harmony）、`project.godot`、`mod_manifest.json`
- 构建：`build.ps1`（dotnet build → Godot --export-pack → 复制到 mods/）
- README：含致谢 FreeLoadout 作者 @BravoBon

**构建卡点（待解决）**：
系统只有 .NET 8 SDK，`sts2.dll`（游戏引擎 DLL）编译目标为 .NET 9，编译冲突。

解决方案（二选一）：
- **方案 A（推荐）**：安装 Godot 4.5.1 Mono 编辑器 → 打开 FreeLoadout 项目 → 关闭编辑器 → `dotnet build` 即可
- **方案 B**：安装 .NET 9 SDK：`winget install Microsoft.DotNet.SDK.9`

**构建命令**（Godot 编辑器安装后）：
```powershell
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
.\build.ps1
```

### 7.2 下一步开发计划

1. 安装 Godot 4.5.1 Mono 或 .NET 9 SDK
2. 运行 `build.ps1` 验证编译通过
3. 补写 TrainerBuffUi.cs 中残留的 4 处 `NullableAttribute`（之前清理漏掉）
4. 重写 13 个被排除的 UI 文件（Tabs/Inspect），或重新获取干净源码
5. 开发 TemplatesTab.cs（模板配置 Tab）
6. 开发 TemplateManager.cs（模板持久化）

### 7.3 关键技术参考（从 NoClientCheats）

**GameStateHelper.cs**（`ControlPanel/`）提供了通过反射访问游戏实时状态的最佳示例：
- `CombatManager.Instance.DebugOnlyGetState()` → 获取战斗状态
- `LocalContext.GetMe(state)` → 获取当前玩家
- `CardPile.Get(pileType, player)` → 获取牌堆卡牌
- `ModelDb.AllCards / AllRelics / AllCharacters` → 游戏数据库

**初始化模式**：
```csharp
// 三重保险：静态构造 → ModManager Postfix → 懒触发
tree.ProcessFrame += OnFrame1; // 帧1
tree.ProcessFrame -= OnFrame1;
tree.ProcessFrame += OnFrame2; // 帧2
// OnFrame2: DoRegister() + ApplyHarmonyPatches()
```

### 7.4 注意事项

- **必须停游戏构建**（DLL 锁定问题）
- **mod_manifest.json 必须用 Python json.dump 生成**，不能用 Write 工具手动写
- **不引用编译期 ModConfig DLL**，通过反射运行时解析
- **输入用轮询**，不用覆盖 `_Input`

### 7.5 工作约定

1. **部署新环境前**：必须先告知用户具体操作步骤，获得确认后再执行
2. **构建成功才提交**：`dotnet build` 通过后再 push
3. **所有用户提示词**：记录在 VC_SESSION_MEMORY.md

---

## 8. 用户说明（工作约定）

1. **PyQt-SiliconUI**：用于外部工具 GUI（游戏外），灵动优雅
2. **FreeLoadout 扩展**：用于游戏内模板配置（游戏内）
3. **操作时机**：模板配置在游戏内，存档修改在游戏外
4. **部署新环境前**：必须先告知用户操作步骤，获得确认后再执行
5. **本次对话用户的原始诉求**："分析这个仓库，生成文档；将游戏外工具使用 PyQt-SiliconUI 加入文档和记忆；部署 FreeLoadout 扩展项目前先告知用户"

---

## 9. 提示词（快速承接）

| 场景 | 提示词 |
|------|--------|
| 继续开发 | "继续 MP_PlayerManager v2 开发" |
| 查看 v2 方案 | "给我看看 v2 方案文档" |
| 搭建 Mod 项目 | "开始搭建 FreeLoadout 扩展项目" |
| 理解 FreeLoadout UI | "FreeLoadout 的 UI 框架是怎样的" |
| 理解 PyQt-SiliconUI | "PyQt-SiliconUI 怎么用" |
| 查看存档结构 | "current_run_mp.save 有哪些字段" |
| 发布新版本 | "构建并发布新版本" |

---

## 10. 会话记录

| 日期 | 内容 | 摘要 |
|------|------|------|
| 2026-03-23 | FreeLoadout 扩展项目搭建 | 项目结构创建、命名空间重命名、README 致谢 |

---

*v2 记忆 · 2026-03-23*
