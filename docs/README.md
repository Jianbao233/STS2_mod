# STS2_mod · docs 文档索引

> 本索引是 `STS2_mod/docs/` 的导航与状态总表（2026-08-10 文档整理建立）。
>
> **状态标记**：🟢 现行 · 🟡 过时/待复核（基于旧版本，内容可能失效）· 🟠 待迁移（将移出主仓）· ⚪ 已归档（历史，仅参考）
>
> **版本基线**：当前游戏 v0.110.1（2026-07-31）。涉及旧反编译（v0.99/v0.109.0）的分析结论一律视为 🟡，引用前先与 `SL2_v0.110.1/` 或 `Tools/sts.dll历史存档/sts2_decompiled_v0.110.1_20260809/` 核对。

## 目录职责

| 目录 | 职责 |
|---|---|
| `analysis/` | 分析报告、根因分析、决策评估、外部 mod 源码研究 |
| `guides/` | 操作指南、工作流说明 |
| `reference/` | 速查、数据、解析区（含 `存档结构解析/`、`ID数据源/` 两个带版本头注的专区） |
| `_归档/` | 已合并/已废弃/过程性文档的历史留档 |
| docs 根 | 仅保留待迁移文件（见下），迁移完成前不要新增根级文件 |

## 文件清单

### analysis/

| 文件 | 定位 | 状态 |
|---|---|---|
| `模组加载问题分析报告.md` | manifest/加载失败根因分析 + 修复指引 | 🟡 基于 v0.99 反编译，结论方向仍有效，细节待复核 |
| `VC_BASELIB_ANALYSIS_REPORT.md` | 外部库 BaseLib-StS2 架构分析 | 🟡 基于旧版 BaseLib（2026-03） |
| `VC_STS2_前置方案评估报告.md` | self-hook/BaseLib/RitsuLib 三选一评估 | 🟡 结论推荐 BaseLib，但实际项目（DimensionalTraveler）选了 RitsuLib；作为历史决策依据保留 |
| `ForkedRoad_Analysis.md` | 外部 mod Forked Road 源码研究（联机参考） | 🟢 可参考（配套私仓内 `NCC_NetId_思路分析.md` 阅读） |

### guides/

| 文件 | 定位 | 状态 |
|---|---|---|
| `安卓端开发与调试工作流.md` | 安卓真机调试流程 | 🟢 部分内容引用旧 mod 名，使用前核对 |
| `VC_GITHUB_RELEASE_GUIDE.md` | GitHub Release 发布指南 | 🟡 **发布流程已被工坊发布体系取代**，仅历史参考；发布走 `创意工坊/` |
| `VC_GITHUB_WORKFLOW.md` | GitHub 仓库管理准则（含"不另建仓库"旧原则） | 🟡 与独立仓现实矛盾处，以根 AGENTS.md 为准 |
| `VC_CONTROL_PANEL_WORK_LOG.md` | ControlPanel 开发日志 | 🟠 ControlPanel 已废弃，文档待归档 |

### reference/

| 文件 | 定位 | 状态 |
|---|---|---|
| `VC_STS2_CONSOLE_AND_COMMANDS_REFERENCE.md` | 控制台命令 + 指令场景 + IsNetworked 合并稿 | 🟢 2026-08-10 合并自两份旧文档（原稿在 `_归档/`） |
| `VC_MOD_CHARACTER_TEMPLATE_FIELDS.md` | 角色初始模板字段参考 | 🟡 混有 STS1/第三方角色数据（WATCHER），引用前核对 |
| `存档结构解析/VC_STS2_SAVE_FILE_ANALYSIS.md` | 存档文件结构全解 | 🟡 基于 v0.99，带版本头注；**游戏更新后回到本区重解析** |
| `ID数据源/` | 完整 ID 数据（md + json + README） | 🟡 基于 v0.99，带版本头注；**游戏更新后回到本区重解析**（工具 `Tools/extract_sts2_ids.py`） |

### docs 根（已清空，勿再放根级文件）

> 2026-08-10：根级 4 份文件（`NCC_NetId_思路分析.md`、`MythicPyre_Bug_Analysis.md`、PVP 计划 ×2）已迁入私有文档仓 `STS2_PrivateDocs/`（按项目分子目录）。今后私有文档一律放私仓，docs 根不落文件。

### _归档/

| 文件 | 说明 |
|---|---|
| `VC_STS2_CONSOLE_GUIDE.md` | ⚪ 2026-08-10 合并入 `VC_STS2_CONSOLE_AND_COMMANDS_REFERENCE.md` |
| `VC_STS2_IDS_AND_COMMANDS_REFERENCE.md` | ⚪ 同上 |

## 维护约定

- 新增分析/指南/参考文档时按上述分区落位，**不要**放在 docs 根。
- 文档内容基于旧游戏版本时，头部加版本头注（格式参考 `存档结构解析/`、`ID数据源/`）。
- 合并、归档、迁移动作完成后同步更新本索引与根目录 `工作区台账.md`。