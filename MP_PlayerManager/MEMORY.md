# MP_PlayerManager · 子记忆文本

> 本文件为 MP_PlayerManager v2 项目的专属记忆，每次新对话开始时请先阅读本文。  
> 当前版本：v2（开发中）
> 更新日期：2026-03-25

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

### 3.1 阶段：TemplatesTab + CardBrowserPanel 已完成，工具端集成未实现

- [x] 确认方案（方案2：Mod 内嵌）
- [x] 完成 v2 方案文档
- [x] 完成 FreeLoadout 源码分析
- [x] 分析 PyQt-SiliconUI，确认用于外部工具 GUI
- [x] FreeLoadout 扩展项目结构创建（命名空间重命名、配置文件、README）
- [x] README 致谢 FreeLoadout 作者 @BravoBon
- [x] TemplateData.cs — 数据模型（CardIds/RelicIds/PotionIds + MaxHp/Gold/Energy）
- [x] TemplateStorage.cs — JSON 持久化（LoadAll/SaveAll）
- [x] LoadoutPanel.cs — 注册 Templates Tab（第 8 个标签页）
- [x] 国际化文本（`localization/eng/ui.json`，仅打进 PCK；勿放 loose 到 mods/）
- [x] 模板列表（搜索 / 新建 / 复制 / 删除）
- [x] 模板编辑器（基础信息 MaxHP/Gold/Energy + 卡牌列表 + 添加卡牌）
- [x] CardBrowserPanel.cs — 模态卡牌选择弹窗（搜索 + 网格展示）
- [x] dotnet build + PCK 导出 + 同步 mods（Godot：`K:\杀戮尖塔mod制作\Godot_v4.5.1\...`）
- [ ] 外部工具开发（PyQt-SiliconUI，读取模板 JSON + 存档 JSON）
- [ ] Steam 好友功能（写入好友 Steam64ID + 昵称缓存，工具端读取）
- [ ] GitHub Release 发布

### 3.2 MP_PlayerManager_v2 外部工具状态

**路径**：`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager_v2\`

**文件清单**：

| 文件 | 状态 | 说明 |
|------|------|------|
| `main.py` | ✅ 完成 | CustomTkinter GUI，1200×780，暗色，6 个页面 |
| `core.py` | ✅ 完成 | 夺舍/添加/移除玩家，含 map_point_history 注入 |
| `save_io.py` | ✅ 完成 | 存档读写，CRLF 明文 JSON 格式 |
| `characters.py` | ✅ 完成 | 5 内置角色 + Mod 角色扫描（player_template.json） |
| `steam_api.py` | ✅ 完成 | VDF 解析、本机 localconfig.vdf 好友、WebAPI |
| `run.py` | ✅ 完成 | 入口脚本 |
| `requirements.txt` | ✅ 完成 | customtkinter>=5.2.0 |
| `MP_PlayerManager_v2.spec` | ✅ 完成 | PyInstaller 打包配置 |
| `build/` | ✅ 完成 | PyInstaller 构建产物（EXE-00.toc 等） |
| `DEVLOG.md` | ✅ 完成 | 开发记录 |

**GUI 功能**：
- 存档扫描（自动发现 modded/profile）
- 夺舍玩家（SteamID 输入 + 好友选择弹窗）
- 添加玩家（复制模式 / 模板模式）
- 移除玩家（清理 relic_grab_bag / map_history / map_drawings）
- 备份管理（创建 / 恢复）
- 设置（字体缩放 10 档）
- Steam 好友选择弹窗（本地离线，15 行虚拟滚动，无 API Key 要求）

**Steam 好友实现**：
- 读取 localconfig.vdf（ActiveUser 注册表定位当前账户）
- 好友 SteamID64 + 昵称，无需网络/API Key

**已知问题/待优化**：
- TemplatesTab UI（Mod 端）未实现，模板模式只能选内置角色
- Mod 导出的模板 JSON 尚未被工具端读取
- 工具端尚未集成 Mod 模板加载（目前只用内置 BUILTIN_CHARACTERS）

### 3.3 Mod 端（FreeLoadout 扩展）状态

| 文件 | 状态 | 说明 |
|------|------|------|
| `TemplateData.cs` | ✅ | 数据结构（Id/Name/Cards/Relics/Potions/MaxHp/Gold/Energy） |
| `TemplateStorage.cs` | ✅ | JSON 持久化（`OS.GetUserDataDir()`） |
| `LoadoutPanel.cs` | ✅ | 注册 Templates Tab（第 8 个） |
| `localization/eng/ui.json` | ✅ | 国际化（仅 PCK；`Loc.cs` 用 `res://localization/...`） |
| `CardBrowserPanel.cs` | ✅ | 模态卡牌选择弹窗（搜索 + 网格 + 回调） |
| `TemplatesTab.cs` | ✅ | 完整 UI（左右分栏：模板列表 + 编辑器） |
| `mod_manifest.json` | ✅ | version: 0.1.0 |
| `build.ps1` / `build_and_sync.ps1` | ✅ | dotnet build + Godot PCK 导出 + 同步 mods |
| `F1InputNode.cs` | ✅ | `_Process` 轮询输入（不阻断游戏，与 NCC 一致） |
| `CardConfigPanel.cs` | ❌ | 不存在（暂不需要，CardBrowserPanel 已覆盖） |
| `DialogPanel.cs` | ❌ | 不存在（暂不需要） |

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
      ├── 模板配置 Tab（TemplatesTab.cs — WIP）
      ├── 模板持久化（TemplateData.cs + TemplateStorage.cs）
      └── 导出 JSON → 游戏外

游戏外（外部工具 / Python + CustomTkinter）
  ├── CustomTkinter（1200×780，暗色主题）
  ├── 读取存档 current_run_mp.save
  ├── 读取 Mod 导出的模板 JSON（待实现）
  └── 向存档注入/替换/删除玩家
```

### 4.2 外部工具 UI 选型

**实际使用 CustomTkinter**，非 PyQt-SiliconUI。

原因：v2 外部工具先行开发，使用 CustomTkinter 更快更稳定。PyQt-SiliconUI 方案保留为未来备选（`tools/PYQT_SILICON_UI_ANALYSIS.md`）。

CustomTkinter 优势：
- 零学习成本，API 直观
- 内置暗色主题
- 支持字体缩放（10 档滑块）

### 4.3 为什么选方案2（Mod 内嵌）

- **零资产**：直接引用游戏内置卡牌/遗物图标，无需打包，不增加 Mod 体积
- **实时数据**：从 `ModelDb` 获取卡牌/遗物数据，永远与游戏版本同步
- **游戏内交互**：玩家在游戏内直接配置模板，体验流畅
- **技术可行性**：FreeLoadout 已有完整的 UI 框架（Godot C#），可直接扩展

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

## 5. GUI 技术备忘

### 5.1 外部工具（v2）

**实际使用 CustomTkinter**（`customtkinter>=5.2.0`），非 PyQt-SiliconUI。

关键特性：
- 1200×780 窗口，暗色主题（`set_appearance_mode("dark")`）
- 字体缩放：10 档滑块（`FONT_SCALE_MIN=1.30`，每档+0.075）
- Steam 好友弹窗：虚拟滚动 15 行，`FriendPickerDialog`（CTkToplevel）
- 存档格式：CRLF 明文 JSON（`json.dumps` 后 `\n`→`\r\n`）

### 5.2 PyQt-SiliconUI（备选）

详细分析见 `tools/PYQT_SILICON_UI_ANALYSIS.md`，**当前未使用**，保留为未来备选方案。

关键备忘：
- 必须在任何窗口前调用 `reload_scale_factor()`
- 基于 **PyQt5**（非 PySide6），打包用 `--onedir`
- 禁用：`templates/` 模块、`SiLineEdit`（重构中）

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

### 7.2 实际项目进度总览

#### MP_PlayerManager_v2（外部工具）— 进度 95%

| 文件 | 状态 |
|------|------|
| main.py / core.py / save_io.py / characters.py / steam_api.py | ✅ 完成 |
| Steam 好友选择（本地离线，无 API Key） | ✅ 完成 |
| 存档读写（CRLF 明文 JSON 格式） | ✅ 完成 |
| 夺舍 / 添加 / 移除玩家 | ✅ 完成 |
| 备份管理 | ✅ 完成 |
| PyInstaller 打包配置 | ✅ 完成 |
| **Mod 模板加载（读取 FreeLoadout 导出的 JSON）** | ❌ 未实现 |
| **dotnet build 编译并发布** | ❌ 未执行 |

#### FreeLoadout 扩展（Mod 端）— 进度 65%

| 文件 | 状态 |
|------|------|
| TemplateData.cs / TemplateStorage.cs / LoadoutPanel.cs | ✅ 完成 |
| CardBrowserPanel.cs / TemplatesTab.cs | ✅ 完成（完整 UI） |
| F1InputNode.cs / TrainerBootstrap.cs（Postfix） | ✅ 完成 |
| mod_manifest.json / build.ps1 / localization | ✅ 完成 |
| **dotnet build 通过** | ✅ 0 errors，PCK 96KB |

### 7.2.1 编译环境准备

**Godot 已安装**（`K:\杀戮尖塔mod制作\Godot_v4.5.1\`），构建成功。
- 首次构建缺少 `export_presets.cfg`，已从 ControlPanel 复制并修改
- Debug 编译与 Godot Mono 的 obj 目录冲突，dotnet build 前需清理 `obj/` 和 `.godot/mono/temp/obj/`

### 7.2.2 下一步开发任务（按优先级）

| 优先级 | 任务 | 涉及文件 | 状态 |
|--------|------|---------|------|
| **P0** | TemplatesTab UI（完整版） | `src/Tabs/TemplatesTab.cs` | ✅ 已完成（构建成功） |
| **P0** | CardBrowserPanel（搜索弹窗） | `src/CardBrowserPanel.cs` | ✅ 已完成 |
| **P1** | 工具端加载 Mod 模板 | `characters.py` 新增 `load_mod_templates_from_json()` | ❌ 未实现 |
| **P1** | TemplateData 增加 CharacterId 字段 | `TemplateData.cs` | ❌ 未实现 |
| **P2** | 完善 ui.json 国际化文本 | `localization/eng/ui.json` | ❌ 未实现 |
| **P3** | GitHub Release | TAG: MP_PLv_0.1.0 | ❌ 未执行 |

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
- **mods 子目录内禁止放任意非 manifest 的 `.json`**（游戏递归扫描，缺 `id` 即报错红字）。`ui.json` 只打进 PCK；`config.json` 已改为 `%APPDATA%\SlayTheSpire2\mod_settings\MP_PlayerManager\config.json`
- **排查日志**：`%APPDATA%\SlayTheSpire2\logs\godot.log`
- **build.ps1** 同步到 mods 时会删除残留的 `localization/`、`config.json`

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
| 2026-03-24 | v2 Python 工具多项修复 | 修复非房主昵称重复 Bug、移除 friends.json、创建 DEVLOG.md |
| 2026-03-25 | v2 复制玩家与地图历史对齐 v1 | 深拷贝+inject map_point_history、夺舍同步 player_id、列表昵称与角色名去重 |
| 2026-03-25 | Templates Tab 开发 | TemplateData.cs + TemplateStorage.cs + LoadoutPanel.cs 注册 Tab + 国际化文本 |
| 2026-03-25 | 修复存档写入格式错误 | write_save 输出 GZIP 导致游戏无法读档，改为 CRLF 明文 JSON |
| 2026-03-25 | Mod 构建成功 | 添加 `export_presets.cfg`（参考 ControlPanel），清理 obj/bin 冲突后构建成功；PCK 61KB + DLL 43KB 已同步到游戏 mods/ |
| 2026-03-25 | 状态同步 | 发现 v2 外部工具实际已完成 95%，MEMORY 校正：TemplatesTab UI 实际未实现，补充 v2 外部工具完整文件清单 |
| 2026-03-25 | 模组加载红字修复 | godot.log：mods 下 `ui.json`/`config.json` 被当 manifest；Loc 改为 `res://localization/`；配置改 AppData；build.ps1 清理 loose 文件；Godot 候选路径对齐其他 Mod |
| 2026-03-25 | 初始化架构对齐 NCC/RHA | 改为 `[HarmonyPatch]` + 两帧延迟 Postfix（与 NCC/HotkeyNode/RHA 完全一致）；输入改为 `_Process` 轮询 + `Input.IsKeyPressed()`（删 NGameInputPatch，新增 F1InputNode） |

---

*v2 记忆 · 2026-03-25*
