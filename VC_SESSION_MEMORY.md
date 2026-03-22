# 分记忆 · 2026-03-22 · NCC 重新打包事故

## 事故概述

目标：用户要求"重新打包，覆盖掉最新的 release"。将本地源码构建的 v1.1.2 DLL 打包并上传 GitHub Release NCC_v1.1.2。

结果：release zip 更新了，但游戏无法识别 NCC，原因是 `mod_manifest.json` 解析失败。

---

## 问题根源

### 1. mod_manifest.json 的 description 字段含未转义换行符

**症状：**

```
System.Text.Json.JsonException: '0x0A' is invalid within a JSON string.
The string should be correctly escaped.
Path: $.description | LineNumber: 6 | BytePositionInLine: 377.
```

**根本原因：** `description` 字段的值包含原始 0x0A（LF）字节，而不是 JSON 合法的 `\n` escape 序列。

**为什么正常 mod 的 manifest 可以有换行：** 其他正常 mod（DamageMeter、RunHistoryAnalyzer）的换行符出现在**文件结构层面**（即 JSON 语法允许的格式换行），而非**字符串值内部**。JSON 规范不允许字符串值内出现 raw LF，必须用 `\n` escape。

**为什么会产生 raw LF：** 源码目录下的 `mod_manifest.json` 里，description 字段的 `\n` 被写成了真实换行（PowerShell 的 `Write` 工具或早期手动编辑时产生）。文件结构换行 + description 内部 raw LF 混在一起，导致 JSON 解析器在第 6 行、字节 377 处（description 字段内）遇到非法字符。

---

## 错误过程复盘

| 步骤 | 操作 | 问题 |
|------|------|------|
| 1 | 用 `dotnet build -c Release` 编译本地源码 | ✅ 正确 |
| 2 | 解压旧 GitHub zip，用新 DLL 替换 | ⚠️ 复用了有问题的 manifest |
| 3 | 用 PowerShell `Write` 工具写 manifest | ❌ 将 `\n` 解释为真实换行 |
| 4 | 用 PowerShell 脚本修复（多次失败） | ❌ PowerShell 对 `$PSItem`、`$_`、转义处理极不稳定 |
| 5 | 最终用 Python `json.dump()` 修复 | ✅ 成功 |

---

## 核心教训

### 教训 1：JSON 文件必须用序列化库生成，不要手动写

JSON 的 escape 序列 `\n`、`\uXXXX` 等必须精确。手动写或用文本工具（Write 工具、PowerShell here-string）极易出错。**正确做法：始终用 Python `json.dump()` 或 C# `JsonSerializer`** 生成 JSON 文件。

### 教训 2：PowerShell 对 `$_`/`$PSItem` 在 heredoc 和 `-c` 命令中表现不一致

在本次会话中，PowerShell 的以下写法全部失败或产生异常结果：
- `Where-Object { $PSItem -eq 10 }` — shell 解析器破坏
- `Where-Object { $_ -eq 10 }` — 同上
- `for (...) { ... $bytes[$i] ... }` — 解析异常

**原则：** 任何超过 10 行的 PowerShell 逻辑必须写入 `.ps1` 文件执行，不能用 `-c` 或 heredoc。

### 教训 3：GitHub Release 打包流程本身有 BUG

查看上传的旧 zip，NoClientCheats.dll 版本是 1.0.0.0，不是源码里的 1.1.2。说明 GitHub Actions 的打包 workflow 没有从源码构建 DLL，而是直接塞了一个提前放好的旧文件。**需要修复 `.github/workflows/` 中的打包步骤，确保每次 release 都执行 `dotnet build` → 打包输出目录。**

### 教训 4：游戏内日志是唯一可靠的调试来源

绕了一大圈才读 `godot.log`，而日志从第一次运行就清楚写明了错误：`'0x0A' is invalid within a JSON string`。遇到"游戏里无法识别"时，**第一步永远先读 godot.log**。

---

## 正确修复流程（本次已手动完成）

```
1. dotnet build -c Release          # 构建新 DLL（AssemblyVersion 1.1.2.0）
2. 用 Python json.dump() 重写 mod_manifest.json  # 生成合法的 JSON（同时修源码目录和 mods 目录）
3. 复制 DLL + manifest + pck 到 mods/
4. 重新打包 zip（25670 bytes），上传 GitHub Release NCC_v1.1.2
```

**GitHub Release NCC_v1.1.2 最终状态（2026-03-22）：**
- Asset: `NoClientCheats-v1.1.2.zip`（25 670 bytes，manifest JSON 合法 ✅）
- mods 目录：`NoClientCheats.dll`（1.1.2.0）+ `mod_manifest.json`（合法 JSON）✅
- 源码目录：同上 ✅

---

## 追加到 VC_GITHUB_WORKFLOW.md 的规则

在第七节末尾追加（见 VC_GITHUB_WORKFLOW.md §7.3）。

---

## 下次改进方向

1. **修复 GitHub Actions 打包 workflow**：确保 `dotnet build -c Release` → 打包 `.godot/mono/temp/bin/Release/` 的输出
2. **源码 `mod_manifest.json` 同样用 Python 生成**：放在项目构建脚本中，而非手动维护
3. **mods 文件夹内不保留源码的 manifest 副本**：构建时从源码复制，或 mods 目录直接指向源码输出

---

# 分记忆 · 2026-03-22 续 · 先古之民数据库建设

## 概述

用户要求对"先古之民"相关内容做系统梳理，建立数据库（一次性含遗物与事件，中英双语）。

## 关键文件路径

- `.run` 存档目录：`C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history\`（共 67 个存档）
- 游戏源码（最新 SL2）：`K:\SteamLibrary\steamapps\common\Slay the Spire 2\extracted\localization\`
- 项目根目录：`K:\杀戮尖塔mod制作\STS2_mod\RunHistoryAnalyzer\`

## 已完成交付物

| 文件 | 说明 |
|------|------|
| `Data/ancient_peoples_rules.json` | 遗物 100 条 + 先古 NPC 11 条 + 事件 34 条 + 节点覆盖 3 条 |
| `Data/ancient_stats_report.json` | 67 个存档实测统计（1260 个节点） |
| `tools/extract_ancient_candidates.py` | 从游戏源码自动提取候选条目 |
| `tools/analyze_run_ancient_stats.py` | 从 .run 存档统计（自动修复节点类型识别） |
| `AncientRuleLoader.cs` | C# 统一加载层（JSON > 硬编码回退） |

## 实测校准结论（67 存档，1260 节点）

| 节点 | gold_gained 正常上限 | relic_picks 正常上限 | 说明 |
|------|---------------------|---------------------|------|
| monster | ~28（p99=51）；异常值 1000 = 1 例极可能作弊 | **1**（PAELS_WING 献祭） | 1 例 monster gold=1000 极可能作弊 |
| elite | 60 | **2**（p99=2） | 最高 2 可能含稀有情况 |
| boss | 120（p99=115） | **1** | boss_relic 极稀有 |
| treasure | 49（p99=646 为 SPOILS_MAP，已被规则过滤） | **1** | ✅ |
| ancient | **999**（SIGNET_RING/CURSED_PEARL 拾起时） | **5**（TEZCATARA 事件赠 TOY_BOX + 4 蜡制遗物） | 整段跳过 NonShopLargeGold |
| event | 162（RANWID_THE_ELDER） | **4**（FAKE_MERCHANT 实测） | 1 例作弊=999 |
| shop | 由 ShopGoldSpikeRule 覆盖 | 999 | ✅ |

### 关键发现：map_point_type vs rooms[].room_type

- 实测存档 `map_point_type=ancient` 对应 `rooms[].room_type=event`
- `get_map_point_type()` 优先级：先取 `map_point_type`（非 unknown），否则取 `rooms[].room_type`
- `NonShopLargeGoldGainRule.ShouldSkipNodeType()` 已接入 `AncientRuleLoader.ShouldSkipNonShopGold()`

## AncientRuleLoader 校准默认值

```csharp
DefaultRelicPickCeiling:
  monster=1, elite=2, treasure=1, ancient=5, event=4, rest=1, boss=1, shop=999
DefaultForeignCharacterCardRelics: ["SEA_GLASS"]
DefaultForeignCharacterCardEvents: ["COLORFUL_PHILOSOPHERS"]
```

## 已接入 AncientRuleLoader 的规则

1. `NonShopLargeGoldGainRule.ShouldSkipNodeType()` → `ShouldSkipNonShopGold()`
2. `RelicMultiPickRule.MaxLegitRelicPicksForNodeType()` → `MaxLegitRelicPicks()`
3. `CardSourceTraceRule.HasCrossCharacterEvent()` → `IsForeignCharacterCardEvent()`
4. `CardSourceTraceRule.StatPickedSeaGlassRelic()` → `IsForeignCharacterCardRelic()`
5. `CharacterCardAffinityRule.PlayerHasSeaGlassRelic()` → `IsForeignCharacterCardRelic()`

## .run 文件节点数据结构关键字段

- `map_point_history[act_idx][node_idx].map_point_type`（如 "ancient"、"monster"）
- `map_point_history[act_idx][node_idx].rooms[].room_type`（如 "event"、"monster"）
- `map_point_history[act_idx][node_idx].rooms[].model_id`（如 "EVENT.TEZCATARA"）
- `player_stats[].gold_gained`（单节点增量，非累积）
- `player_stats[].relic_choices[].choice`（遗物 ID，如 "RELIC.TOY_BOX"）
- `player_stats[].relic_choices[].was_picked`（是否被选中）

## 后续维护

1. SL2 更新后：重跑 `tools/extract_ancient_candidates.py`
2. 新存档累积后：重跑 `tools/analyze_run_ancient_stats.py --run-dir "C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\history"`
3. 若检测器有新误报：在 `Data/ancient_peoples_rules.json` 中注册，无需改 C#
