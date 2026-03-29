# STS2_mod · 工作区总记忆

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

---

## 三、外部工具与资源

### 3.1 工作区根目录清单（`K:\杀戮尖塔mod制作\`）

| 目录/文件 | 路径 | 说明 |
|-----------|------|------|
| **STS2_mod/** | `STS2_mod\` | 所有 Mod 源码（git 管理） |
| **Manager/** | `Manager\` | 皮皮模组管理器 v2.3.1 |
| **Tools/** | `Tools\` | 反编译/解包/提取工具集 |
| **SL2/** | `SL2\` | 游戏资源提取目录（含 Godot 源码、场景、贴图、音频等） |
| **Godot_v4.5.1/** | `Godot_v4.5.1\` | Godot 4.5.1 安装包 |
| **GDRE_tools-*/** | `GDRE_tools-*/` | Godot 资源提取工具 |
| **历代版本源码/** | `历代版本源码\` | sts2.dll 历史存档（两个版本） |
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

#### 游戏 DLL 历史存档

| 文件/目录 | 路径 | 说明 |
|-----------|------|------|
| **sts.dll历史存档/** | `Tools/sts.dll历史存档/` | sts2.dll 反编译存档，含 3 个时间点：20260316、20260318、20260322 |
| **sts2_decompiled20260322/** | `Tools/sts.dll历史存档/sts2_decompiled20260322/` | 最新存档（含 `sts2/` 子目录，2026-03-22 解包） |
| **sts2_decompiled20260318/** | `Tools/sts.dll历史存档/sts2_decompiled20260318/` | 中期存档 |
| **sts2_decompiled20260316/** | `Tools/sts.dll历史存档/sts2_decompiled20260316/` | 早期存档 |

> **用法**：`extract_sts2_ids.py` 默认读取 `Tools/sts2_decompiled/sts2.dll/...`，需要指向正确路径（最新为 `sts2_decompiled20260322`）

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

#### SL2 游戏资源目录（`K:\杀戮尖塔mod制作\SL2\`）

> **说明**：由 GDRE 从游戏 .pck 提取，C# 源码 decompile 失败但资源结构完整。可作为**资源路径、场景、贴图、音频、卡牌/遗物 ID、本地化 key** 的参考。

| 子目录 | 说明 |
|--------|------|
| `scenes/` | 游戏场景 (.tscn) |
| `scripts/` | Godot GDScript（部分有效） |
| `images/` | 美术资源（.png/.webp） |
| `animations/` | 动画数据 |
| `localization/` | 本地化资源（.csv/.json） |
| `audio/` | 音效/BGM 资源 |
| `fonts/` | 字体文件 |
| `.godot/` | Godot 编辑器缓存 |
| `project.godot` | Godot 项目配置 |
| `packages.lock.json` | .NET 包依赖锁文件 |

相关文档：
- `VC_MOD_GUIDE.md` — Steam 正式版 Mod 制作完整指南
- `VC_DEVELOPMENT_SETUP.md` — Mod 开发环境配置说明
- `模组加载问题分析报告.md` — Mod 加载失败排查文档

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

1. **静态构造**：Harmony `PatchAll` 时尝试（Engine 可能为 null，静默跳过）
2. **Postfix**：`ModManager.Initialize` 的 Harmony Postfix（Engine 应该就绪）
3. **懒触发**：业务逻辑首次触发时兜底调用

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

**游戏源码参考目录**：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260318\sts2\`

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

详见 `VC_GITHUB_WORKFLOW.md`，关键规则：

- **不另建仓库**：所有 Mod 统一存放在 STS2_mod 主仓库
- **构建成功才提交**：`dotnet build` 通过后才 push
- **提交格式**：`<type>(<scope>): <描述>`（feat/fix/docs/ui/chore）
- **mod_manifest.json**：必须用 `json.dump()` 生成，禁止手动写

---

## 十一、会话历史（按时间顺序）

---

### 会话记录 2026-03-22 · NCC v1.1.2 重新打包事故

**背景**：用户要求"重新打包，覆盖掉最新的 release"。将本地源码构建的 v1.1.2 DLL 打包并上传 GitHub Release NCC_v1.1.2。

**问题**：`mod_manifest.json` 的 `description` 字段含原始 0x0A（LF）字节而非合法的 `\n` escape 序列，导致游戏 JSON 解析失败：

```
System.Text.Json.JsonException: '0x0A' is invalid within a JSON string.
Path: $.description | LineNumber: 6 | BytePositionInLine: 377.
```

**核心教训**：

1. JSON 文件必须用序列化库生成（Python `json.dump()` / C# `JsonSerializer`）
2. PowerShell 对 `$_`/`$PSItem` 在 heredoc 和 `-c` 命令中表现不一致，复杂逻辑必须写入 `.ps1` 文件
3. 游戏内日志（`godot.log`）是唯一可靠的调试来源
4. GitHub Actions 打包 workflow 可能有 BUG（上次复用了旧 DLL）

**正确流程**：
```
1. dotnet build -c Release
2. python json.dump() 重写 mod_manifest.json（同时修源码目录和 mods 目录）
3. 复制 DLL + manifest + pck 到 mods/
4. 重新打包 zip，上传 GitHub Release NCC_v1.1.2
```

---

### 会话记录 2026-03-22 · 先古之民数据库建设

**背景**：用户要求对"先古之民"相关内容做系统梳理，建立数据库（含遗物与事件，中英双语）。

**关键文件路径**：
- 历史存档目录：`C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history\`（共 67 个存档）
- 游戏源码：`K:\SteamLibrary\steamapps\common\Slay the Spire 2\extracted\localization\`
- 项目根目录：`K:\杀戮尖塔mod制作\STS2_mod\RunHistoryAnalyzer\`

**已完成交付物**：

| 文件 | 说明 |
|------|------|
| `Data/ancient_peoples_rules.json` | 遗物 100 条 + 先古 NPC 11 条 + 事件 34 条 + 节点覆盖 3 条 |
| `Data/ancient_stats_report.json` | 67 个存档实测统计（1260 个节点） |
| `tools/extract_ancient_candidates.py` | 从游戏源码自动提取候选条目 |
| `tools/analyze_run_ancient_stats.py` | 从 .run 存档统计（自动修复节点类型识别） |
| `AncientRuleLoader.cs` | C# 统一加载层（JSON > 硬编码回退） |

**实测校准结论（67 存档，1260 节点）**：

| 节点 | gold_gained 正常上限 | relic_picks 正常上限 |
|------|---------------------|---------------------|
| monster | ~28（p99=51） | **1** |
| elite | 60 | **2** |
| boss | 120 | **1** |
| treasure | 49 | **1** |
| ancient | **999**（拾起时异常值） | **5** |
| event | 162 | **4** |
| shop | 覆盖 | 999 |

**后续维护**：
1. SL2 更新后：重跑 `tools/extract_ancient_candidates.py`
2. 新存档累积后：重跑 `tools/analyze_run_ancient_stats.py`
3. 若有新误报：在 `Data/ancient_peoples_rules.json` 中注册，无需改 C#

---

### 会话记录 2026-03-23 · MP_PlayerManager v2 FreeLoadout 扩展搭建

**背景**：用户要求继续 MP_PlayerManager v2 项目流程，在 README 里致谢 FreeLoadout 作者（哔哩哔哩 @BravoBon）。

**完成工作**：

1. **FreeLoadout 扩展项目搭建**（`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\`）
   - 复制源码（`Tools/freeloadout解包/FreeLoadout/` → `FreeLoadout/src/`）
   - 批量重命名命名空间：`FreeLoadout` → `MP_PlayerManager`（48/49 文件成功）
   - 清理反编译残留（NullableContextAttribute、NullableAttribute 等，24 文件）
   - 清理资源路径：`res://FreeLoadout/` → `res://MP_PlayerManager/`
   - 创建 `project.godot`、`mod_manifest.json`、`build.ps1`、`README.md`

2. **README.md 致谢**：`FreeLoadout/README.md` 已创建，含致谢 @BravoBon

**编译卡点（待解决）**：
- 系统只有 .NET 8 SDK，`sts2.dll` 为 .NET 9，编译冲突
- `GodotSharp.dll` 需从 Godot 编辑器安装获取
- 13 个文件含编译器生成名（`<>c__DisplayClass*`），被排除编译

**解决方案（二选一）**：
- **方案 A（推荐）**：安装 Godot 4.5.1 Mono 编辑器 → 打开项目后关闭 → `dotnet build` 即可
- **方案 B**：安装 .NET 9 SDK：`winget install Microsoft.DotNet.SDK.9`

**已确认关键路径**：
- `GodotSharp.dll` / `sts2.dll` / `0Harmony.dll`：`K:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\`
- `Godot.NET.Sdk` 来自 NuGet，仅含 `GodotSharp.xml`（文档），实际 DLL 来自编辑器安装

**下一步**：
1. 安装 Godot 4.5.1 Mono 或 .NET 9 SDK 以完成构建
2. 补写 `TrainerBuffUi.cs` 中残留的 4 处 `NullableAttribute`
3. 重写 13 个被排除的 UI 文件（Tabs/Inspect）
4. 开始 TemplatesTab.cs 开发

---

### 会话记录 2026-03-23 · KaylaMod 控制台 Mod 源码解析

**来源**：`K:\杀戮尖塔mod制作\Tools\清野控制台解包\KaylaMod解包2026年3月23日205613\`
**性质**：十分强力的控制台 Mod（作者：清野，QQ群 1059957432），对理解游戏底层架构和 Mod 开发有极高参考价值。

#### 1. 模组概述

| 项目 | 值 |
|------|-----|
| 模组名 | KaylaMod |
| 版本 | 1.0 |
| 功能定位 | 控制台工具 + 运行时卡组/遗物/属性编辑器 + UI浮层面板 |
| affects_gameplay | true（影响游戏玩法） |
| 编译目标 | .NET 9（v9.0） |

#### 2. 代码架构

```
KaylaMod/
├── MainFile.cs                          入口节点，[ModInitializer] + Godot脚本绑定
├── GodotPlugins/Game/Main.cs            Native入口（UnmanagedCallersOnly，DLL初始化）
├── Sts2ApiCompat.cs                     跨版本兼容层：Creature HP/CurrentHp/Card操作
├── ConsoleCommands/
│   ├── LoadoutConsoleCmd.cs             控制台命令：loadout list/save/show/apply/delete
│   ├── LoadoutPreset.cs                卡组预设数据结构（Gold/HP/Deck/Relics）
│   └── LoadoutPresetStore.cs           预设持久化（JSON → D:/mod/KaylaMod/data/）
├── LoadoutSupport/
│   ├── LoadoutCardEntry.cs             单张卡牌条目（ID/Count/UpgradeLevel/Template）
│   ├── LoadoutPresetSupport.cs         预设序列化/反序列化/Normalize
│   ├── LoadoutDeckSummary.cs           卡组统计摘要（升级/模板/附魔数量）
│   └── CardTemplateRuntimeHelper.cs    卡牌模板核心：捕获差量 + 回放应用
├── UI/
│   ├── LoadoutOverlay.cs               浮层UI主类（~22000行，Godot Control）
│   ├── OfficialConsoleBridge.cs         官方控制台反射访问层
│   ├── OfficialConsoleCommandInfo.cs   官方命令元信息包装
│   ├── CardEditTemplate.cs             卡牌模板编辑数据结构
│   ├── RuntimeStatModifiers.cs         战斗内实时属性修改器
│   ├── LoadoutUiMode.cs                UI模式枚举
│   ├── LoadoutFeature.cs                功能特性注册
│   ├── LoadoutActionService.cs         操作服务（抽牌/取消出牌等）
│   └── [其他UI支持类]
└── Patches/
    ├── Run/AttachLoadoutOverlayPatch.cs 浮层挂载补丁
    └── Multiplayer/
        ├── MultiplayerModSyncCompatPatch.cs          Mod列表过滤（自签名前缀）
        ├── MultiplayerCompatRules.cs                 多人联机兼容性规则
        └── JoinFlowInitialGameInfoCompatPatch.cs     入局握手信息规范化
```

#### 3. 核心入口：MainFile.cs 初始化流程

```csharp
[ModInitializer("Initialize")]  // 游戏加载时自动调用
public class MainFile : Node {
    public static Logger Logger = new Logger("KaylaMod", LogType.Generic);

    public static void Initialize() {
        Harmony harmony = new Harmony("KaylaMod");
        // 手动遍历 PatchTypes 列表打 Harmony 补丁
        foreach (Type type in PatchTypes) {
            harmony.CreateClassProcessor(type).Patch();
        }
        // 立即尝试挂载浮层
        AttachLoadoutOverlayPatch.TryAttachOverlayNow();
    }
}
```

关键点：**手动 PatchAll** 而非自动 PatchAll；**Build marker** 写死在日志里便于版本追溯。

#### 4. Sts2ApiCompat — 跨版本 HP/卡牌操作兼容层

这是整个模组最有价值的部分之一，提供了一个**三级降级策略**：

**设置 MaxHp**（`SetMaxHpAsync`）：
1. 优先尝试 `CreatureCmd.SetMaxHp`（官方命令）
2. 降级：`CreatureCmd.GainMaxHp`（正值）→ `LoseMaxHp`（负值）
3. 最终兜底：反射调用 `SetMaxHpInternal`（私有方法）

**设置 CurrentHp**：`CreatureCmd.SetCurrentHp` → `SetCurrentHpInternal` 反射

**关键泛型参数自动推导**（`TryBuildCreatureCommandArguments`）：
- 自动检测目标方法参数类型（decimal/int/float/double/bool）
- decimal 统一传入，按需转换类型，避免签名不匹配

#### 5. OfficialConsoleBridge — 反射读写官方控制台

```csharp
// 读取官方已注册命令列表
NDevConsole.Instance → 反射 _devConsole 字段 → DevConsole._commands 字典
// 执行命令
devConsole.ProcessCommand(rawCommand) → CmdResult{success, msg}
```

- `OfficialConsoleCommandInfo.FromCommand`：判断是官方还是 Mod 命令（检测命名空间前缀）
- 反射路径：`NDevConsole._devConsole` / `DevConsole._commands`

#### 6. LoadoutOverlay UI — 战斗内实时编辑器

关键 Godot 节点路径和方法：
- `LoadoutOverlay.ApplyCoreStatsAsync`：设置金币/HP/能量/星能/充能球槽位
- `LoadoutOverlay.ApplyOrbSlotsAsync`：战斗内动态增删充能球槽位
- `LoadoutOverlay.TryGetCurrentPlayingCard`：多路径搜索当前选中的卡（`_currentCardPlay`/`_selectedCards`/PlayContainer）
- `LoadoutOverlay.DrawToHandLimitOnceAsync`：抽牌到上限（单次）；多人时走官方 `draw N` 命令，单人时走 `CardPileCmd.Draw`
- `LoadoutOverlay.ForcePlayerTurnOnceAsync`：切换到玩家回合
- `LoadoutOverlay.CancelSelectedCardPlayAsync`：取消当前出牌选择（`PlayQueue.RemoveCardFromQueueForCancellation` + `Hand.TryCancelCardPlay`）

#### 7. CardTemplateRuntimeHelper — 卡牌模板差量捕获与回放

这是**最精密的部分**，完整实现了卡牌所有动态属性的快照与恢复：

**捕获（CaptureDeltaTemplate）**：
- 对比 `ModelDb.AllCards` 基准卡，计算差量
- 支持：BaseCost/ReplayCount/DynamicVars（Damage/Block/ExtraDamage/IncreasedBlock/LastStarsSpent）
- 支持：Keyword差量（Exhaust/Ethereal/Unplayable）
- 支持：附魔类型+数量、文本覆盖（NameOverride/DescriptionOverride）
- 支持：ExhaustOnNextPlay/SingleTurnRetain/SingleTurnSly

**应用（ApplyTemplate）**：
- 三层附魔路径：`CardCmd.Enchant` → 泛型版 `CardCmd.Enchant<T>` → `Activator.CreateInstance` + `EnchantInternal`（强制）
- 动态数值写入：`DynamicVar.BaseValue = PreviewValue = ResetToBase` 链式更新
- 私有成员兜底：`TrySetNumericMember` / `TrySetBoolMember` 多名称反射

#### 8. Patches — 多人联机兼容性

**自签名过滤**：`IgnoredModNamePrefixes = ["KaylaMod", "清野的控制台"]`
- `MultiplayerModSyncCompatPatch`：拦截 `ModManager.GetGameplayRelevantModNameList`，将 KaylaMod 从联机Mod列表中移除
- `JoinFlowInitialGameInfoCompatPatch`：拦截入局握手 `InitialGameInfoMessage`，过滤 mod 列表并**标准化 ModelDb Hash**

**ModelDb Hash 兼容性**：检测自身是否注册了自定义 Model 类型，若无则允许覆盖客户端的 `idDatabaseHash` 以绕过版本差异。

#### 9. 参考价值总结

| 类别 | 具体价值 |
|------|---------|
| **命令注册** | 自定义 `AbstractConsoleCmd` 派生类实现完整 Tab 补全 |
| **官方控制台集成** | 反射读写 `DevConsole._commands`，执行官方命令 |
| **跨版本兼容** | 三级降级 + 反射兜底 + 类型自动转换 |
| **HP 操作** | CreatureCmd vs 反射内部Setter 的完整链路 |
| **卡牌模板** | 完整差量捕获 + 多路径附魔应用，业界罕见 |
| **Godot UI** | `CallDeferred` 异步挂载、SpinBox编辑、RichTextLabel状态提示 |
| **多人 Mod 兼容性** | Mod列表过滤 + ModelDb Hash标准化 |
| **存档持久化** | JSON文件存储，含 `Normalize()` 防止脏数据 |
| **反射技巧** | 多名称字段/属性反射（`TryGetMemberValue`）、私有方法调用、类型全扫描 |

---

---

### 会话记录 2026-03-26 · 紧急修复 i18n + 本地化导入

**背景**：i18n 本地化改造后程序无法启动，发现多个编译/运行时错误。

**修复的 bug（共 4 个）：**

1. **`main.py` · `_render_user_group` 折叠箭头引用错误**
   - 问题：`self._group_arrow[group.steam_id] = arrow` 缺失，导致折叠/展开时箭头元素引用丢失
   - 修复：折叠头注册时追加 `self._group_arrow[group.steam_id] = arrow`

2. **`main.py` · `_render_backup_card` 的 `card.info` 参数数量错误**
   - 问题：调用 `_("card.info", ...)` 时传了 6 个参数，但 `card.info` 的中英文字符串只各定义了 5 个 `{}` 占位符
   - 修复：简化为 5 个明确参数 `be.player_count, "?", 0, "?", save_time_str`

3. **`core.py` / `characters.py` · 绝对导入路径导致 `ModuleNotFoundError`**
   - 问题：模块使用 `from i18n import ...`（绝对导入），但 PyInstaller 打包后 `i18n` 不在 `sys.path`
   - 修复：`core.py` 改为 `from .i18n import _`；`characters.py` 改为 `from .i18n import _ as _i18n`

4. **`main.py` · `CTkRadioButton` 的 `fg_color`/`bg_color` 不接受 `"transparent"`**
   - 问题：CustomTkinter 禁止 `fg_color`/`bg_color` 使用 `"transparent"` 字符串值
   - 修复：改为与父框架一致的暗色背景值 `("#0D1117", "#0D1117")`

5. **`main.py` · 备份浏览器的 mod 模式判断错误**
   - 问题：`is_modded = profile_key.startswith("modded/")`，但 `profile_key` 格式为 `steam_id/modded/profile1`，前导 Steam ID 导致永远不匹配
   - 修复：改为 `"modded" in profile_parts`（`profile_parts = profile_key.split("/")`）

**验证**：`python run.py` 可正常启动 GUI，无异常输出。

**后续计划**（来自 `language_selector_+_backup_ui_improvement_71c479ad.plan.md`）：
1. ✅ 导航栏底部语言切换（已完成，运行时正常）
2. ⏳ 备份管理页重构为全局备份浏览器（`save_io.scan_all_backups()` 已实现，UI 待完善）
3. ⏳ 构建 exe：`pyinstaller MP_PlayerManager_v2.spec --clean`

---

*工作区总记忆 · 2026-03-26*

---

## 2026-03-20 BaseLib-StS2 调研

### 本次完成内容

1. **调研 BaseLib**：访问 [Alchyr/BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)（⭐105，v0.1.8 2026-03-19）
2. **生成报告**：`VC_BASELIB_ANALYSIS_REPORT.md`（7 大模块详解 + 借鉴优先级）
3. **模板字段整理**：`VC_MOD_CHARACTER_TEMPLATE_FIELDS.md`（Mod 角色初始遗物/卡组字段参考）

### 核心结论

BaseLib `CustomCharacterModel` **不直接处理初始遗物/初始牌组**（由游戏 `CharacterModel` 原生管理），BaseLib 专注于视觉/动画/能量/音效资源路径。

### BaseLib CustomCharacterModel 提供的虚属性

| 类别 | 属性 | 说明 |
|------|------|------|
| 视觉 | `CustomVisualPath`、`CustomTrailPath`、`CustomIconTexturePath`、`CustomIconPath` | 角色立绘、拖尾、小/大图标 |
| 选角 | `CustomCharacterSelectBg`、`CustomCharacterSelectIconPath`、`CustomCharacterSelectLockedIconPath`、`CustomCharacterSelectTransitionPath` | 选角界面资源 |
| 地图 | `CustomMapMarkerPath` | 地图标记 |
| 休息/商店 | `CustomRestSiteAnimPath`、`CustomMerchantAnimPath` | 休息站/商店动画 |
| 能量 | `CustomEnergyCounter`（结构体）、`CustomEnergyCounterPath` | 自定义能量计数器 |
| 手势 | `CustomArm*TexturePath`（Rock/Paper/Scissors/Pointing） | 猜拳手势 |
| 音效 | `CustomAttackSfx`、`CustomCastSfx`、`CustomDeathSfx` | `event:/sfx/...` 格式 |
| 数值 | `StartingGold`、`AttackAnimDelay`、`CastAnimDelay` | 可覆盖默认值 |

### 游戏源码 CharacterModel 初始模板字段

| 字段 | 说明 |
|------|------|
| `StarterRelic` / `StarterRelicId` | 初始遗物（实例/ID） |
| `StarterDeck` / `StarterDeckIds` | 初始牌组（CardModel[]/string[]） |
| `MaxHP` | 最大生命值 |

### player_template.json 标准格式（MP_PlayerManager 已采纳）

```json
{
    "character_id": "CHARACTER.WATCHER",
    "name": "观者",
    "max_hp": 72,
    "starter_relic": "RELIC.PURE_WATER",
    "starter_deck": ["CARD.STRIKE_P", "CARD.DEFEND_P", ...]
}
```

ID 命名约定：`CHARACTER.XXX` / `RELIC.XXX` / `CARD.XXX`（大写），卡牌后缀 `_I/S/D/P` 表示角色归属。

### 最高优先级借鉴

`SimpleModConfig` 配置系统（代码量减少 70%），详细见 `VC_BASELIB_ANALYSIS_REPORT.md`。

---

*工作区总记忆 · 2026-03-20*
