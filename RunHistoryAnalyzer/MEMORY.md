# RunHistoryAnalyzer · 项目记忆

> 本文件为 RunHistoryAnalyzer 项目的专属记忆，每次新对话开始时请先阅读本文。

---

## 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\RunHistoryAnalyzer\` |
| **目标** | 从"百科大全 → 历史记录"入手，检测玩家作弊产生的异常记录 |
| **部署** | 单机 Mod，仅本机可见 |
| **数据源** | 历史记录 JSON 文件（`saves/history/*.run`），不依赖存档 |

---

## 分析文档清单

| 文件 | 内容概要 |
|------|----------|
| `FEASIBILITY_ANALYSIS.md` | **核心报告**：可行性分析 + 代码示例 + 优先级矩阵 |
| `UI_DESIGN.md` | **UI设计方案**：按需检测 + 分析按钮 + 结果窗口 + TXT导出 |
| `ANALYSIS_REPORT.md` | 早期报告：作弊手段分类、守恒定律算法 |
| `ARCHIVE_VS_HISTORY_COMPARISON.md` | 存档vs历史记录字段对比，SerializableRun完整表 |

---

## 核心设计决策

### 为什么选历史记录而非存档

- 历史记录存**流水账**（GoldGained/Spent, DamageTaken等），存档只存**最终快照**
- 流水账天然支持**守恒定律验证**和**来源追溯**
- 历史记录是 **JSON 格式**，存档是二进制Packet格式

### 检测模式：按需检测

- **不自动检测**：历史记录列表加载时不检测，避免卡顿
- **玩家主动点**：在 `NRunHistory` 详情面板底部添加【🔍 分析】按钮
- **结果呈现**：弹出结果窗口，高/中/低三级分类显示所有异常
- **导出报告**：结果窗口底部【导出报告】按钮，导出 .txt 文件

### 检测优先级（P0 → P2，行为类已废弃）

```
P0（数学等式，零模糊）
  ├─ GoldConservationRule     金币守恒
  ├─ HpConservationRule       HP守恒
  └─ HpBoundaryRule           CurrentHp > MaxHp（数学不可能）

P1（规则明确，低误报）
  ├─ CardSourceTraceRule     卡牌来源追溯
  └─ RelicSourceTraceRule    遗物来源追溯

P2
  └─ PotionSourceTraceRule   药水来源追溯

~~无敌检测~~ → 已废弃（高手无伤难以区分）
~~异常通关时间~~ → 已废弃（阈值难以设定）
~~路线合理性~~ → 已废弃（特殊情况多）
```

### 局限性

- 完美伪造（同时修改所有字段一致）无法检测
- 只能事后标记，无实时阻止能力

---

## 关键数据结构速查

### 流水账（PlayerMapPointHistoryEntry）

```
金币  GoldGained + GoldSpent + GoldLost + GoldStolen + CurrentGold
HP    CurrentHp + MaxHp + DamageTaken + HpHealed + MaxHpGained + MaxHpLost
卡牌  CardsGained + CardChoices[wasPicked] + CardsRemoved + UpgradedCards + CardsTransformed
遗物  RelicChoices[wasPicked] + BoughtRelics + RelicsRemoved
药水  PotionChoices[wasPicked] + BoughtPotions + PotionUsed + PotionDiscarded
```

### 守恒定律

```
金币：初始99 + ΣGoldGained - ΣGoldSpent - ΣGoldLost = 最终CurrentGold
HP：  初始HP - ΣDamageTaken + ΣHpHealed = 最终CurrentHp
MaxHP：初始MaxHp + ΣMaxHpGained - ΣMaxHpLost = 最终MaxHp
```

### 来源追溯关键字段

- `CardChoiceHistoryEntry.wasPicked` → 卡牌是否被选中
- `ModelChoiceHistoryEntry.wasPicked` → 遗物/药水是否被选中

---

## 源码索引

| 文件 | 路径 |
|------|------|
| `PlayerMapPointHistoryEntry` | `MegaCrit.Sts2.Core.Runs.PlayerMapPointHistoryEntry` |
| `RunHistory` | `MegaCrit.Sts2.Core.Runs.RunHistory` |
| `RunHistoryPlayer` | `MegaCrit.Sts2.Core.Runs.RunHistoryPlayer` |
| `RunHistoryUtilities` | `MegaCrit.Sts2.Core.Runs.RunHistoryUtilities` |
| `NRunHistory` | `MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory` |
| `RunHistorySaveManager` | `MegaCrit.Sts2.Core.Saves.Managers.RunHistorySaveManager` |
| `SerializablePlayer` | `MegaCrit.Sts2.Core.Saves.Runs.SerializablePlayer` |
| `SerializableRun` | `MegaCrit.Sts2.Core.Saves.SerializableRun` |

**源码前缀**：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260318\sts2\`

---

## 开发备忘

- **当前阶段**：需求设计阶段，UI设计方案见 `UI_DESIGN.md`
- **检测时机**：玩家选定历史记录 → 显示分析按钮 → 点击后检测 → 弹出结果窗口
- **缓存策略**：检测结果缓存内存中，文件修改时间变化则失效重检
- **Hook 点**：`NRunHistory` 详情面板底部按钮栏，添加分析按钮
- **导出**：结果窗口→【导出报告】→ FileDialog → `.txt` 文件
- **构建**：dotnet build + Godot --export-pack，参考 build.ps1
- **ModConfig**：可简化为仅一个总开关，暂不实现

---

## 提示词（快速承接）

| 场景 | 提示词 |
|------|--------|
| 继续开发 | "继续 RunHistoryAnalyzer 项目" |
| 查看UI设计 | "给我看看 UI_DESIGN.md" |
| 开始编码 | "开始实现第一阶段：数据层+检测层" |
| 查看数据结构 | "PlayerMapPointHistoryEntry 有哪些字段" |
