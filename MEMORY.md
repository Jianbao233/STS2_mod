# STS2_mod · 工作区总记忆

> ⚠️ **本文件已停更（2026-08-10 标注）**：当前状态与规则以根目录 `AGENTS.md` + `工作区台账.md` 为准；本文保留历史决策与细节，仅作参考。标注后新增/变更不写回本文件。
>
> 本文件为 `STS2_mod/` 工作区的唯一主记忆，每次新对话开始时请先阅读本文。
> 旧的分段记忆（VC_SESSION_MEMORY.md）已合并至本文档，按时间顺序记录在末尾。

---

## 一、工作区概述

| 项目 | 路径 | 说明 |
|------|------|------|
| **主目录** | `K:\杀戮尖塔mod制作\STS2_mod\` | 所有 STS2 Mod 源码的父目录 |
| **仓库** | `https://github.com/Jianbao233/STS2_mod` | GitHub 仓库，git 管理 |
| **目标游戏** | Slay the Spire 2（Steam 正式版） |
| **游戏目录** | `K:\SteamLibrary\steamapps\common\Slay the Spire 2\` |
| **游戏用户数据** | `C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\` |
| **开发环境** | .NET 8 SDK + Godot 4.5.1 Mono + Harmony（游戏内置） |

---

## 二、模组清单

| 模组 | 路径 | 功能 |
|------|------|------|
| **NoClientCheats** | `NoClientCheats\` | 禁止客机作弊（拦截控制台指令 + 历史面板 + 顶栏呼出按钮） |
| **RunHistoryAnalyzer** | `RunHistoryAnalyzer\` | 历史记录作弊检测（金币守恒、HP守恒、卡牌/遗物来源追溯） |
| **RichPing** | `RichPing\` | 多人联机PING文本丰富（参考 DamageMeter） |
| **HostPriority** | `HostPriority\` | 房主权限管理 |
| **ControlPanel** | `ControlPanel\` | F7 控制面板：卡牌/药水/遗物/战斗快捷（参考实现） |
| **MP_PlayerManager** | `MP_PlayerManager\` | 多人玩家管理（v1 归档；v2 FreeLoadout 扩展，开发中） |
| **MP_SavePlayerRemover** | `MP_SavePlayerRemover\` | 多人存档玩家移除工具（已废弃） |
| **ModListHider** | `ModListHider\` | 独立仓库；主仓忽略目录，只在根 README/MEMORY 记录跳转 |
| **LoadOrderManager** | `LoadOrderManager\` | 独立仓库；主仓忽略目录，只在根 README/MEMORY 记录跳转 |
| **PVP_ParallelTurn** | `PVP_ParallelTurn\` | 独立仓库；主仓忽略目录，只在根 README/MEMORY 记录跳转 |
| **RefreshShop** | `RefreshShop\` | 独立仓库；商店免费无限刷新，只刷新本地玩家商店卡牌/遗物 |
| **DimensionalTraveler** | `DimensionalTraveler\` | 独立仓库；次元旅人炼金协作角色，正式运行时仅依赖 RitsuLib；测试适配器仅限本地开发 |
| **AutoModSubscriber** | `AutoModSubscriber\` | 联机进房时遇到 mod 不一致，弹双区块对话框：一键订阅工坊缺失 mod + 勾选禁用本机多余 mod。Workshop 3750485606（public）。本仓直接维护，独立 Git 仓库 `https://github.com/Jianbao233/AutoModSubscriber` |

**补充登记（2026-08-10，原清单缺失）**：

| 模组 | 路径 | 功能 |
|------|------|------|
| **MerchantBlacklist** | `MerchantBlacklist\` | 本机商店遗物/药水黑名单过滤器。**独立 Git 仓库**。workspace `ShopBlacklist`（历史遗留） |
| **MultiplayerTools** | `MultiplayerTools\` | 多人存档/角色模板/备份面板类工具。工坊 3747498878 |
| **UpgradeBugRestore** | `UpgradeBugRestore\` | 恢复火堆升级多选 UI。工坊 3750854057 |
| **ShopCatalog** | `ShopCatalog\` | 商店总览（私人定制，客户：南鸢离梦） |
| **SharedConfig** | `SharedConfig\` | 共享配置框架库（非独立 mod），见其 README |

**已废弃（2026-08-10 标注，明细以根 AGENTS.md 为准）**：`ControlPanel`、`HostPriority`、`RichPing`、`MP_PlayerManager_v1`、`MP_PlayerManager_v2`、`MP_SavePlayerRemover`、`MP_SaveSlotsNative`、`_废弃_Manifest格式修复`。废弃 README 均已在头部标注。

---

## 三项独立仓迁移记录（2026-06-12）

- `ModListHider` 源码以独立仓库 `https://github.com/Jianbao233/ModListHider` 为准；本地开发路径：`K:\杀戮尖塔mod制作\STS2_mod\ModListHider`。
- `LoadOrderManager` 源码以独立仓库 `https://github.com/Jianbao233/STS2-LoadOrderManager` 为准；本地开发路径：`K:\杀戮尖塔mod制作\STS2_mod\LoadOrderManager`。
- `PVP_ParallelTurn` / `ParallelTurnPvp` 源码以独立仓库 `https://github.com/Jianbao233/sts2-parallel-turn-pvp` 为准；本地开发路径：`K:\杀戮尖塔mod制作\STS2_mod\PVP_ParallelTurn`。
- `RefreshShop` 源码以独立仓库 `https://github.com/Jianbao233/RefreshShop` 为准；本地开发路径：`K:\杀戮尖塔mod制作\STS2_mod\RefreshShop`。定位：商店免费无限刷新，只刷新本地玩家自己的商店卡牌/遗物，入口复用删卡服务金币图标。
- `DimensionalTraveler` 源码以独立私有仓库 `https://github.com/Jianbao233/STS2-DimensionalTraveler` 为准；本地开发路径：`K:\杀戮尖塔mod制作\STS2_mod\DimensionalTraveler`。正式包只依赖 `STS2-RitsuLib`；`test-adapter/` 和 KitLib 仅用于本地验收，绝不进入 Workshop staging。
- `STS2_mod` 主仓继续保存未迁移小 mod 源码；上述五项在主仓 `.gitignore` 中按整目录忽略。
- 不使用 submodule，不保留 gitlink，不在三项目录内提交主仓跳转 README；主仓只在根 `README.md` 和本文件记录跳转说明。
- `toRelease/`、`torelease/` 是本地历史版本与发布包存档，保留在本地，不提交到 GitHub。
- `K:\Dev` 是外部/他人仓库克隆区，不作为本人 STS2 Mod 的保存和开发位置；除非用户要求比对外部仓库，否则不要进入。

---

## 三、外部工具与资源

### 3.1 工作区根目录清单（`K:\杀戮尖塔mod制作\`）

| 目录/文件 | 路径 | 说明 |
|-----------|------|------|
| **STS2_mod/** | `STS2_mod\` | 所有 Mod 源码（git 管理） |
| **Manager/** | `Manager\` | 皮皮模组管理器 v2.3.1 |
| **Tools/** | `Tools\` | 反编译/解包/提取工具集 |
| **SL2_v0.110.1/** | `SL2_v0.110.1\` | **当前游戏 v0.110.1 的 GDRE 还原 Godot 项目**。2026-07-31 解包，当前源码/资源基线；详见 §3.2。 |
| **SL2_v0.109.0/** | `SL2_v0.109.0\` | 上一代 GDRE 快照（2026-07-24 解包），仅用于版本差异对照（2026-08-10 起）。 |
| **SL2_v0.108/** | `SL2_v0.108\` | 最近上一版完整快照，仅用于版本差异对照。 |
| **Godot_v4.5.1/** | `Godot_v4.5.1\` | Godot 4.5.1 安装包 |
| **GDRE_tools-*/** | `GDRE_tools-*/` | Godot 资源提取工具 |
| **历代版本源码/** | `历代版本源码\` | 历史完整快照；`SL2_v0.107.1` 已移入该目录。
| **modmanager.json** | 根目录 | 皮皮模组管理器配置文件 |
| **addons.zip** | 根目录 | Godot 插件包 |

### 3.2 Tools 目录详情（`K:\杀戮尖塔mod制作\Tools\`）

#### ID 提取与本地化工具

| 文件/目录 | 路径 | 说明 |
|-----------|------|------|
| **extract_sts2_ids.py** | `Tools/extract_sts2_ids.py` | 从反编译 Models 目录爬取完整 ID 列表（Cards/Potions/Relics/Powers/Enchantments/Afflictions），输出 JSON + Markdown |
| **extract_sts2_ids.ps1** | `Tools/extract_sts2_ids.ps1` | 同上 PowerShell 版，含 zhs 本地化合并 |
| **extract_card_characters.py** | `Tools/extract_card_characters.py` | 从 CardPool 文件提取卡牌角色归属（IRONCLAD/SILENT/DEFECT/NECROBINDER/REGENT）→ 输出 `ControlPanel/card_characters.json` |
| **extract_localization_from_pck.md** | `Tools/extract_localization_from_pck.md` | 从游戏 .pck 解包官方中文翻译的方法说明 |

#### 反编译工具

| 文件/目录 | 路径 | 说明 |
|-----------|------|------|
| **dnSpy-net-win64/** | `Tools/dnSpy-net-win64/` | dnSpyEx 反编译器 v6.5.1（.NET 反编译，参考 VC_DNSPY_SETUP.md） |
| **VC_DNSPY_SETUP.md** | `Tools/VC_DNSPY_SETUP.md` | dnSpyEx 部署与使用说明 |

#### 当前 SL2 完整项目（`K:\杀戮尖塔mod制作\SL2_v0.109.0\`）

> ⚠️ **历史快照标注（2026-08-10）**：以下路径与数据均为 v0.109.0 快照。当前基线为 `SL2_v0.110.1/`（2026-07-31 解包；0.110.1 仅 SL 修复、无资产变化，结构基本一致）。下方场景/文件数量如与 `SL2_v0.110.1/` 有出入，以实际为准。

> **核心定位**：由 GDRE v2.5.0-beta.3 从当前 `SlayTheSpire2.pck` 还原出的**完整 Godot 4.5.1 Mono 项目**（v0.109.0 / 2026-07-24 解包）。GDRE 日志确认提取 15,705 个文件、导入转换 4,002 项、提取阶段无错误；23 个脚本反编译失败，因此涉及其逻辑时必须回到运行时或 DLL 证据复核。**不是**单纯资源目录，而是包含 C# 源码、Godot 场景、UID 引用、资源/本地化的可打开工程。
>
> **首选用途**：理解当前游戏原生 UI 工作机制、复用原生 UI、查节点路径、追"场景 → 挂的脚本类 → C# 逻辑"链路、查信号连线。`SL2_v0.108` 仅用于差异比较；`历代版本源码/SL2_v0.107.1` 是历史归档，不可用于证明当前行为。

体量速览：

| 类型 | 数量 | 用途 |
|------|------|------|
| `.tscn` 场景 | ~975 | UI 树、节点层级、脚本绑定、信号连线 |
| `.cs` 源码 | ~1741 | 完整后端类（含 Screens、Multiplayer、Nodes 等分层） |
| `.uid` UID | ~1736 | Godot 4 资源 UID 索引，配 `.cs`/`.tscn` 用 |
| `.tres` 资源 | ~153 | 主题、字体、材质、配置 |
| 本地化 JSON | ~645 | 14 种语言全套 |

关键子目录（UI 复用必查）：

| 子目录 | 内容 |
|--------|------|
| `SL2_v0.109.0/scenes/screens/` | 121 个 UI 屏幕场景（main_menu、char_select、settings_screen、run_history_screen、map、potion_lab、shops、card_library 等） |
| `SL2_v0.109.0/scenes/ui/` | 65 个通用 UI 组件场景（含 `multiplayer/`、`top_bar/`、`character_icons/`） |
| `SL2_v0.109.0/scenes/backgrounds/`、`scenes/vfx/`、`scenes/encounters/` 等 | 战斗背景、特效、事件、奖励等其他场景 |
| `SL2_v0.109.0/src/Core/Nodes/Screens/` | 573 个文件，**每个 UI 屏幕的 C# 后端类**按子目录分好（`Shops/`、`Settings/`、`MainMenu/`、`Map/`、`CardLibrary/` 等） |
| `SL2_v0.109.0/src/Core/Nodes/Screens/Settings/` | 71 个设置界面控件，**任何"mod 设置面板"都应模仿这里**的布局/绑定模式 |
| `SL2_v0.109.0/src/Core/Multiplayer/` | 147 个文件，按 `Connection/`、`Game/Lobby/`、`Game/PeerInput/`、`Messages/Game/`、`Messages/Lobby/`、`Transport/ENet/`、`Transport/Steam/` 分层 |
| `SL2_v0.109.0/src/Core/Models/Relics/`、`Models/Powers/` 等 | 完整数据模型（Relics 298 个、Powers 212 个） |
| `SL2_v0.109.0/addons/mega_text/`、`addons/megacontentcreator/` 等 | 游戏自带 Godot 插件，含 `MegaLabel.cs`、`MegaRichTextLabel.cs` 等可直接被 Mod 复用的控件 |
| `SL2_v0.109.0/themes/`、`SL2_v0.109.0/materials/` | 字体（含中文 `zhs/`）、卡牌/特效材质、shader |
| `SL2_v0.109.0/localization/<lang>/` | 14 种语言完整文案（`zhs/`、`eng/` 等）+ `eng/patch_notes/`（122 篇 `.md` 补丁说明） |

UI 复用工作流（推荐套路）：

1. 在 `SL2_v0.109.0/scenes/screens/<name>/` 找一个最接近你想做的面板的 `.tscn` 当模板。
2. 看 `.tscn` 里 `[ext_resource type="Script" uid="..."]` 指向哪个 `.cs`，再到 `SL2_v0.109.0/src/Core/Nodes/Screens/<name>/` 找对应的后端类，了解它的字段、信号、生命周期。
3. 在 Mod 里 `GD.Load<PackedScene>("res://scenes/screens/<name>/<file>.tscn")` 实例化原生场景，或者按它的节点树**抄一份**自己的 `.tscn`。
4. 用 `GetNode<T>("VBox/...")` 拿原生控件、`Connect` 原生信号；遇到没暴露的成员就 `AccessTools.TypeByName(...)` 反射或 Harmony Patch。

`.cs` ↔ `.tscn` 配对规则：每个 `.cs` 旁有 `.cs.uid`，`.tscn` 里 `ExtResource` 用同一个 UID 引用——这套关系 GDRE 已经还原好，AI 可以直接追。

#### 历史 DLL 反编译（`Tools/sts.dll历史存档/`）

| 文件/目录 | 路径 | 说明 |
|-----------|------|------|
| `sts.dll历史存档/` | `Tools/sts.dll历史存档/` | sts2 主程序集的纯 C# 反编译存档 |
| `sts2_decompiled_v0.109.0_20260724/` | `Tools/sts.dll历史存档/sts2_decompiled_v0.109.0_20260724/` | **当前版本（v0.109.0）**，ILSpy `9.1.0.7988` 从实际游戏 `sts2.dll` 导出，3,494 个 `.cs`；无 `.tscn`/UID/资源。 |
| 旧版存档 | `sts2_decompiled20260619/` 及更早日期目录 | 历史版本快照，仅用于版本对比。 |

**与 `SL2_v0.109.0/` 的关系**：`Tools/sts.dll历史存档/sts2_decompiled_v0.109.0_20260724/` 是当前程序集的纯 C# 视图，不含场景和资源。只在以下场景退到这里：

- 只需要符号/类层级/调用链，不需要场景信息
- 做版本对比（拿当前 `sts2_decompiled_v0.109.0_20260724` 跟旧存档 diff）
- `SL2_v0.109.0/` 暂时不在手边时的轻量参考

> **用法**：`extract_sts2_ids.py` 的输入路径需显式指向当前 `Tools/sts.dll历史存档/sts2_decompiled_v0.109.0_20260724/`；不得再假定不存在的 `Tools/sts2_decompiled/` 或旧版目录。

#### 其他 Mod 源码存档

| 文件/目录 | 路径 | 说明 |
|-----------|------|------|
| **KaylaMod** | `Tools/清野控制台解包/KaylaMod解包2026年3月23日205613/` | **强力控制台 Mod 源码**（作者：清野），含 ConsoleCommands/LoadoutSupport/UI/Patches 等 39 个文件，参考价值极高 |
| **freeloadout解包/** | `Tools/freeloadout解包/` | FreeLoadout Mod 源码（含 FreeLoadout.sln + FreeLoadout-STS2_0.99-0.2.0/） |
| **modconfig解包/** | `Tools/modconfig解包/` | ModConfig Mod 源码（含 GodotPlugins/、ConfigEntry.cs、ModConfigManager.cs 等 14 个文件） |
| **DamageMeter解包/** | `Tools/DamageMeter解包/` | DamageMeter Mod 源码（含 2026-03-22 新版 + old/ 旧版） |
| **sts2-heybox-support/** | `Tools/sts2-heybox-support/` | 黑盒适配 Mod 源码（sts2-heybox-support.dll + .pck） |
| **sts2_decompiled/** | `Tools/sts2_decompiled/` | sts2.dll 反编译源码（extract_sts2_ids.py 默认读取路径，指向最新版本） |

#### Godot 工具

| 文件 | 路径 | 说明 |
|------|------|------|
| **godotpcktool.exe** | `Tools/godotpcktool.exe` | Godot .pck 打包/解包工具（1.7MB），用法：`godotpcktool <file.pck> -a e -o extracted -i "\.json"` |
| **addons.zip** | `K:\杀戮尖塔mod制作\addons.zip` | Godot 插件压缩包（75MB） |

#### SL2 资源占位（旧描述已迁移）

> 见上文 §3.2 的 "SL2 游戏完整项目目录"。本节保留只是为了导航。

相关文档：
- `VC_MOD_GUIDE.md` — Steam 正式版 Mod 制作完整指南
- `VC_DEVELOPMENT_SETUP.md` — Mod 开发环境配置说明
- `docs/analysis/模组加载问题分析报告.md` — Mod 加载失败排查文档

### 3.3 Manager 模组管理器详情（`K:\杀戮尖塔mod制作\Manager\`）

| 项目 | 说明 |
|------|------|
| **版本** | 2.3.1（皮一下就很凡 @Bilibili） |
| **主文件** | `ModManager.ps1`（PowerShell GUI，2986 行）+ `bootstrap.ps1`（自更新引导） |
| **功能** | Mod 下载/安装/更新/卸载，支持 Steam Workshop + 本地 mod |
| **上传** | 静默上传 mods 到 COS CDN（`https://sts2-mods-1323919747.cos.ap-shanghai.myqcloud.com`） |
| **游戏 AppID** | 2868840 |
| **安装目录** | `K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\` |
| **配置** | `modmanager.json`（含 TelemetryId + GameDir） |

### 3.4 游戏 mods 安装目录（`K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\`）

| Mod ID | 路径 | 作者 |
|--------|------|------|
| NoClientCheats | `mods/NoClientCheats/` | 煎包 |
| RunHistoryAnalyzer | `mods/RunHistoryAnalyzer/` | - |
| ControlPanel | `mods/ControlPanel/` | - |
| RichPing | `mods/RichPing/` | - |
| HostPriority | `mods/HostPriority/` | - |
| FreeLoadout | `mods/FreeLoadout-STS2_0.99-0.2.0/` | Boninall (@BravoBon) |
| DamageMeter | `mods/DamageMeter_v1.8.4/` | - |
| ModConfig | `mods/ModConfig_v0.1.8/` | 皮一下就很凡 |
| SpeedX | `mods/SpeedX_v0.8.6/` | - |
| Watcher | `mods/【0.99+版本支持】Watcher-STS2_0.99-0.4.6/` | - |
| quickRestart2 | `mods/quickRestart2/` | - |
| RemoveMultiplayerPlayerLimit | `mods/RemoveMultiplayerPlayerLimit/` | - |

### 3.5 游戏日志路径与「mods 下任意 .json」陷阱

| 路径 | 说明 |
|------|------|
| `%APPDATA%\SlayTheSpire2\logs\godot.log` | 当前会话主日志（启动后滚动写入） |
| `%APPDATA%\SlayTheSpire2\logs\godot*.log` | 按时间戳归档的历史日志 |

**重要**：`ModManager` 会**递归扫描** `游戏/mods/` 下**所有** `.json` 文件并尝试按 **mod manifest** 解析。凡文件名被扫到且**缺少顶层 `id` 字段**即记一条 `[ERROR] ... missing the 'id' field`，并计入「已加载 N 个模组但检测到错误」的红字提示。

因此：

- **切勿**在单个 Mod 子目录里放 `localization/**/ui.json`、`config.json`、数据用 JSON 等松散文件（除非该文件本身就是合法 manifest）。
- 本地化、配置应 **打进 .pck**（`res://...` 读取），或放到 **`%APPDATA%\SlayTheSpire2\`** 等非 `mods/` 路径。
- 皮皮模组管理器写入的 `modmanager.json`、`telemetry_cache.json` 等若放在 `mods/` 根目录，同样会触发误扫（属管理器与游戏扫描策略叠加问题）。

**已修复的 manifest（`mods/` 下）**：
- `DamageMeter_v1.8.4/mod_manifest.json` — 补 `id: "DamageMeter"`
- `ModConfig_v0.1.8/mod_manifest.json` — 补 `id: "ModConfig"`
- `RemoveMultiplayerPlayerLimit/mod_manifest.json` — 补 `id: "RemoveMultiplayerPlayerLimit"`
- `SpeedX_v0.8.6/mod_manifest.json` — 补 `id: "SpeedX"`

**残留触发报错的非 manifest JSON**（暂不处理）：
- `RunHistoryAnalyzer/Data/ancient_peoples_rules.json`
- `sts2_lan_connect/lobby-defaults.json`
- `【0.99+版本支持】Watcher-STS2_0.99-0.4.6/player_template.json`

---

## 四、通用技术决策

### 4.1 框架栈

```
Slay the Spire 2
  └─ Godot 4.5.1 Mono（游戏引擎）
       └─ Harmony 2.x（内置，无需额外安装）
            └─ 各 Mod（PatchAll() 自动发现 [HarmonyPatch] 类）
```

### 4.2 ModConfig 集成模式（所有模组通用）

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

### 4.3 初始化三重保险（所有主动作 Mod 通用）

> ⚠️ `[ModuleInitializer]` 方案（推荐）：
> STS2 Android 加载器只调用 `Harmony.PatchAll()`，**不触发** `[ModInitializer]` / `static constructor` / Harmony static field initializer。
> 使用 C# 9 `[ModuleInitializer]` 可确保 DLL 加载时必定执行初始化代码。
> 条件：`TargetFramework >= net8.0`，需 `System.Runtime.CompilerServices` 引用。

1. **`[ModuleInitializer]`**（推荐）：程序集加载时必定执行，不依赖游戏调用
2. **静态构造**：Harmony `PatchAll` 时尝试（Engine 可能为 null，静默跳过）
3. **Postfix**：`ModManager.Initialize` 的 Harmony Postfix（Engine 应该就绪）
4. **懒触发**：业务逻辑首次触发时兜底调用

### 4.4 Godot 节点与静态字段

Godot 节点作为静态类字段时，必须在每次访问前检查 `GodotObject.IsInstanceValid(node)`——Godot 会静默使已释放的节点失效。

### 4.5 Godot 输入与游戏输入兼容

**禁止**在 Mod 节点覆盖 `_Input` 并调用 `SetInputAsHandled()`——这会阻断游戏自身的输入处理。

正确做法：
- 纯轮询：`Node._Process` + `Input.IsKeyPressed()` + edge detection（`prevDown`）
- `ProcessMode = ProcessModeEnum.Always` 让暂停时也继续

### 4.6 ModConfig 操作按钮防递归

`OnChanged` 在每次值变化时触发。在 `OnChanged` 内调用 `ModConfig.SetValue` 会触发新的 `OnChanged`，导致死循环。

解决：用私有 `ModConfigManager.SetValue`（反射）绕过 ModConfig 的 `OnChanged` 回调链，直接重置值。

### 4.7 Harmony Patch 游戏节点类

游戏内置类型（如 `NTopBar`）无编译期引用。用 `AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar")` 运行时解析。搜索目标方法时 `BindingFlags` 必须含 `AccessTools.all`。

---

## 五、通用源码索引

| 类型/方法 | 来源 | 说明 |
|-----------|------|------|
| `ModManager.Initialize` | 游戏主程序集 | Mod 加载入口 |
| `ActionQueueSynchronizer.HandleRequestEnqueueActionMessage` | 游戏主程序集 | 多人联机动作入队，是作弊指令拦截点 |
| `ModManager.GetGameplayRelevantModNameList` | 游戏主程序集 | 联机时发给客户端的 Mod 列表 |
| `NTopBar._Ready` | 游戏内置节点 | 游戏顶栏加载完毕事件（注入按钮的时机） |
| `LocString` | 游戏内置 | 文本本地化字符串 |
| `RunState.CurrentRun.Players` | 游戏内置 | 当前跑动的玩家列表 |
| `CombatManager.Instance.IsInProgress` | 游戏内置 | 当前是否在战斗中 |
| `CombatManager.Instance.DebugOnlyGetState()` | 游戏内置 | 获取战斗状态（可从中取当前玩家） |
| `ModelDb.AllCards / AllRelics / AllCharacters` | 游戏内置 | 游戏数据库（运行时访问） |
| `LocalContext.GetMe(state)` | 游戏内置 | 从战斗状态获取当前玩家 |

**游戏源码参考目录**：
- 首选 `K:\杀戮尖塔mod制作\SL2\src\Core\`（完整 Godot 项目，含场景）
- 退选 `K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled_v0.109.0_20260724\`（当前 `v0.109.0` 纯 C# 反编译）

**依赖 lib 源码（K:\Dev，外部仓库 fork）**：
- `K:\Dev\STS2-RitsuLib\`（远端 `https://github.com/BAKAOLC/STS2-RitsuLib`，约 1068 个 `.cs`）—— RitsuLib 官方源码，比本地反编译版 (`Tools/RitsuLib_0.107.0_decompiled/`) 完整很多，写依赖 RitsuLib 的 mod 时优先看这里。
- `K:\Dev\STS2-KitLib\`（远端 `https://github.com/WRXinYue/STS2-KitLib`，约 726 个 `.cs`）—— KitLib 官方源码，比本地反编译版 (`Tools/KitLib_AI_decompiled/`) 完整很多。

### 5.x CodeGraph 代码索引（MCP）

工作区已为下面 3 个仓库建立了 CodeGraph 知识图谱索引（`.codegraph/` 目录），并挂到 Cursor MCP（见 `K:\杀戮尖塔mod制作\.cursor\mcp.json`）：

| MCP server | 索引目录 | 文件 / 节点 / 边 |
|---|---|---|
| `codegraph-sl2` | `K:\杀戮尖塔mod制作\SL2` | 3,440 / 71,318 / 173,508 |
| `codegraph-ritsulib` | `K:\Dev\STS2-RitsuLib` | 1,118 / 25,203 / 63,187 |
| `codegraph-kitlib` | `K:\Dev\STS2-KitLib` | 784 / 14,472 / 39,143 |

CodeGraph CLI 在 `J:\Tools\MCPControl\npm-global\codegraph.cmd`。常用：
- `codegraph status <path>`：看某仓库索引统计
- `codegraph sync <path>`：手动同步（默认 MCP 启动时会自动监听 + 重建）
- `codegraph query <symbol> -p <path>`：精确符号搜索
- `codegraph callers <symbol> -p <path>` / `callees`：调用关系
- `codegraph explore "<question>" -p <path>`：自然语言探索，一次返回相关源码 + 调用图 + blast radius

AI 在写依赖 STS2 原版 / RitsuLib / KitLib 的 mod 时，优先通过 MCP 的 `codegraph_explore` / `codegraph_callers` 工具查源码结构，不要 grep 后再 read 文件。

---

## 六、通用开发备忘

- **构建**：各模组目录下 `build.ps1`（参考 `NoClientCheats\build.ps1`）
- **构建流程**：`dotnet build -c Debug` → `Godot --export-pack --headless` → 复制 DLL+PCK+manifest 到 mods/
- **构建前提**：.NET 8 SDK + Godot 4.5.1 Mono 编辑器（用于 PCK 导出）
- **发布**：`git push` 后手动 `gh release create` 并上传 DLL
- **调试日志**：用 `GD.Print("[ModName] ...")` 输出，在游戏安装目录日志中查看
- **GitHub Releases**：`https://github.com/Jianbao233/STS2_mod/releases`

### mod_manifest.json 规范（必须用序列化库生成）

```python
# ✅ 正确：用 Python json.dump
import json
with open('mod_manifest.json', 'w', encoding='utf-8') as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)
# ❌ 错误：手动写 JSON，\n 会被解释为真实换行符
```

**验证方法**：
```python
import json
with open('mod_manifest.json') as f:
    json.load(f)  # 若抛出异常则 JSON 不合法
```

--- 
    
## 七、存档文件结构速查

| 文件 | 路径 | 说明 |
|------|------|------|
| `current_run_mp.save` | `AppData/Roaming/SlayTheSpire2/steam/{SteamId}/modded/profile{N}/saves/` | 多人当前存档（JSON，players[] 数组） |
| `progress.save` | 同上 | 全局进度（金币/解锁/统计） |
| `settings.save` | 同上 | 全局设置 |
| `.run` | `history/` 目录 | 历史存档（含 map_point_history、player_stats 等） |

**关键存档 ID 格式**：
- 卡牌：`CARD.STRIKE_IRONCLAD`
- 遗物：`RELIC.VAJRA`
- 药水：`POTION.ENTROPIC_BREW`
- 角色：`CHARACTER.IRONCLAD`

---

## 八、各模组专属记忆

| 模组 | 记忆文件 |
|------|----------|
| NoClientCheats | `NoClientCheats\MEMORY.md` |
| RunHistoryAnalyzer | `RunHistoryAnalyzer\MEMORY.md` |
| MP_PlayerManager | `MP_PlayerManager\MEMORY.md`（含 v2 FreeLoadout 扩展详情） |

---

## 九、提示词（快速承接）

| 场景 | 提示词 |
|------|--------|
| 继续 NoClientCheats | "继续 NoClientCheats 项目" |
| 继续 RunHistoryAnalyzer | "继续 RunHistoryAnalyzer 项目" |
| 继续 MP_PlayerManager v2 | "继续 MP_PlayerManager v2 开发" |
| 构建发布 | "发布 NoClientCheats 新版本" |
| 查看项目清单 | "工作区有哪些模组" |
| 理解作弊拦截 | "NoClientCheats 是怎么拦截作弊指令的" |
| 查看存档结构 | "current_run_mp.save 有哪些字段" |
| 查看工具清单 | "工作区有哪些工具" |
| 提取游戏 ID | "运行 extract_sts2_ids.py" |
| 反编译游戏 DLL | "用 dnSpy 分析 sts2.dll" |

---

## 十、GitHub 管理准则（摘要）

详见 `docs/guides/VC_GITHUB_WORKFLOW.md`，关键规则：

- **不另建仓库**：所有 Mod 统一存放在 STS2_mod 主仓库
- **构建成功才提交**：`dotnet build` 通过后才 push
- **提交格式**：`<type>(<scope>): <描述>`（feat/fix/docs/ui/chore）
- **mod_manifest.json**：必须用 `json.dump()` 生成，禁止手动写

---
*工作区总记忆 · 2026-04-07*
