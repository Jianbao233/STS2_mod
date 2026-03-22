# 先古之民（Ancient）数据与规则库 — 建设计划

> **状态：待确认后执行（用户已确认：一次性完成含遗物与事件，中英双语）。**  
> **游戏源码路径：** `K:\SteamLibrary\steamapps\common\Slay the Spire 2\extracted\`  
> **最后更新：** 2026-03-22（对应 SL2 最新版）

---

## 1. 背景与目标 / Background & Goals

先古之民（Ancient / 祭坛）相关节点与遗物在 `.run` 中产生的流水形态与普通战斗/商店差异显著，曾导致以下误报：

| 现象 | 涉及的检测器 | 已修复 |
|------|------------|--------|
| 先古拾起可变金币遗物（CURSED_PEARL / SIGNET_RING 等）→ 大额 gold_gained | `NonShopLargeGold` | ✅ ancient 节点整段跳过 |
| TOY_BOX 拾起再给多件蜡制遗物 → 多次 was_picked | `RelicMultiPick` | ✅ ancient 放宽至 10 |
| PAELS_WING 献祭卡包换遗物 → monster 节点出现 relic_choices | `RelicMultiPick` | ✅ monster 放宽至 1 |
| SEA_GLASS 选异色牌入组 → 卡组带他职业后缀 | `CharacterCardAffinity` + `CardSourceTrace` | ✅ 白名单 |

当前硬编码特例难以维护，需建立**可版本化的权威数据源**，支撑：
1. 新遗物/新事件的快速注册（无需改 C# 代码）
2. 白名单阈值的数据驱动化（替代启发式固定常量）
3. 跨版本回归测试（游戏更新后重新扫描对比 diff）

---

## 2. 数据来源 / Data Sources

| 优先级 | 路径 | 内容 | 用途 |
|--------|------|------|------|
| **P0** | `extracted/localization/{lang}/relics.json` | 所有遗物 id / 描述 / 事件描述 | 提取「拾起时给金币/卡牌/遗物/变量 Gold」类遗物 |
| **P0** | `extracted/localization/{lang}/ancients.json` | 先古 NPC（DARV / NEOW / PAELS 等）对话与选项 | 提取先古节点行为模式 |
| **P0** | `extracted/localization/{lang}/events.json` | 所有事件 id / 页面描述 | 提取可能给金币/多遗物的普通事件 |
| **P0** | `mods/RunHistoryAnalyzer/history/*.run` | 大量实测存档 | 验证流水组合（gold_gained 分位数、relic_choices 次数分布） |
| **P1** | `VC_STS2_FULL_IDS.json` | 统一 ID ↔ 类名映射 | 统一白名单字段命名 |
| **P2** | 游戏 Godot 源码 / 反编译类 | 精确数值上界、条件分支 | 精确化变量上界（如 {Gold} 最大值） |

---

## 3. 数据库形态 / Database Schema

存放路径：`RunHistoryAnalyzer/Data/ancient_peoples_rules.json`

### 3.1 顶层结构

```jsonc
{
  "schema_version": 1,           // 每次结构性修改 +1
  "game_version_range": ">=0.99", // 适用的游戏版本范围
  "generated_at": "2026-03-22",   // 自动填充
  "note": "zh-CN / en-US"         // 摘要说明
}
```

### 3.2 遗物级条目 / Relic Effects

```jsonc
{
  "relic_effects": [
    {
      "id": "TOY_BOX",
      "title_zh": "玩具盒",
      "title_en": "Toy Box",
      "on_pickup": {
        "grant_relic_choices_min": 1,
        "grant_relic_choices_max": 5,
        "note_zh": "拾起时授予多件蜡制遗物（主遗物 + 蜡制遗物），relic_choices 多次 was_picked",
        "note_en": "Grants multiple wax relics on pickup; multiple was_picked in single node"
      },
      "tags": ["ancient_eligible", "multi_relic_same_node"]
    }
  ]
}
```

### 3.3 事件级条目 / Event Effects

```jsonc
{
  "event_effects": [
    {
      "id": "COLORFUL_PHILOSOPHERS",
      "title_zh": "色彩哲学家",
      "title_en": "Colorful Philosophers",
      "rewards": {
        "foreign_character_cards": true,
        "note_zh": "选择另一角色获得 1 张卡牌入组",
        "note_en": "Choose 1 card from another character to add to deck"
      },
      "tags": ["cross_character_cards", "choice_event"]
    }
  ]
}
```

### 3.4 节点类型覆盖 / Node-Type Overrides

```jsonc
{
  "node_type_overrides": [
    {
      "match": { "map_point_type": "ancient" },
      "non_shop_gold_rule": "skip",
      "relic_pick_ceiling": 10,
      "note_zh": "先古祭坛多段遗物/金币与变量 Gold 共存",
      "note_en": "Ancient nodes host variable gold and multi-relic flows; full skip recommended"
    }
  ]
}
```

---

## 4. 提取与校验流程 / Extraction & Validation Pipeline

```
tools/extract_ancient_candidates.py
    ├── scan relics.json        → relics_on_pickup_candidates.json
    ├── scan ancients.json      → ancient_npc_behaviors.json
    ├── scan events.json         → event_relic_gold_candidates.json
    └── merge + deduplicate       → Data/ancient_peoples_rules.json (draft)

tools/analyze_run_ancient_stats.py
    └── history/*.run (sampled)
        ├── group by map_point_type=ancient
        ├── percentile(gold_gained)
        ├── distribution(relic_choices[].was_picked)
        └── cross-validate with JSON draft → anomaly_report.json
```

### 4.1 提取规则（Python 扫描脚本）

**关键词正则（中文 / 英文双轨扫描）：**

| 字段 | 中文关键词 | 英文关键词 | 对应 JSON 字段 |
|------|-----------|-----------|--------------|
| 金币 | `金币`, `{Gold}`, `拾起时.*金` | `gold`, `Gold}`, `pickup.*gold` | `gold_variable` |
| 异色卡 | `其他角色`, `{Character}`, `来自.*的牌` | `another character`, `{Character}`, `foreign.*cards` | `foreign_character_cards` |
| 多遗物 | `{Relics}`, `件.*遗物` | `Relics}`, `relics` | `multi_relic` |
| 卡包献祭 | `献祭`, `牺牲.*卡` | `sacrifice`, `Sacrifices}` | `card_sacrifice_relic` |
| 拾起时 | `拾起时`, `upon pickup` | `upon pickup`, `pickup` | — |

---

## 5. 与现有检测器的映射 / Mapping to Existing Rules

| 规则类 | 现有策略 | 数据库化后 |
|--------|---------|-----------|
| `NonShopLargeGoldGainRule` | `ancient` 节点全跳 | 改为读取 `relic_effects[].gold_variable` 列表 + 变量 Gold 上界 |
| `RelicMultiPickRule` | `ancient→10`, `monster→1` | 改为读取 `node_type_overrides[].relic_pick_ceiling` |
| `CardSourceTraceRule` | 硬编码 `CrossCharacterEventIds` | 改为读取 `event_effects[].foreign_character_cards` |
| `CharacterCardAffinityRule` | `SEA_GLASS` 硬编码白名单 | 改为读取 `relic_effects[].cross_character_cards` |

---

## 6. 风险与原则 / Risks & Principles

- **避免过宽白名单**：以「遗物 ID + 节点类型」组合为最小粒度，不单独按 `ancient` 全盘跳过所有检测。
- **版本漂移**：数据库需带 `game_version_range`；游戏更新后重新运行扫描脚本 diff 报告。
- **联机多玩家**：所有提取均按 `player_id` / `AnalysisPlayerId` 隔离，与检测器一致。

---

## 7. 交付物清单 / Deliverables

| # | 文件 | 说明 |
|---|------|------|
| 1 | `Data/ancient_peoples_rules.json` | 遗物 + 事件 + 节点覆盖初版，中英双语 note |
| 2 | `tools/extract_ancient_candidates.py` | 从游戏源码 JSON 生成候选条目 |
| 3 | `tools/analyze_run_ancient_stats.py` | 从 .run 存档输出统计 JSON |
| 4 | `Detection/AncientRuleLoader.cs` | C# 可选加载 JSON；无 JSON 时回退当前硬编码常量 |
| 5 | `Detection/AncientRuleLoader.cs` → 各规则 | 各规则读取 `AncientRuleLoader` 配置，移除零散 hardcode |

---

## 8. 执行顺序 / Execution Order

```
① 扫描 relics.json（全量拾起时效果，生成初稿）
② 扫描 ancients.json + events.json（补充事件与 NPC）
③ 合并 → Data/ancient_peoples_rules.json（人工校对版）
④ 编写 extract_ancient_candidates.py（自动化脚本）
⑤ 编写 analyze_run_ancient_stats.py（统计验证脚本）
⑥ 实现 AncientRuleLoader.cs（C# 加载层）
⑦ 各检测器改造：引用 AncientRuleLoader 而非硬编码
⑧ dotnet build 验证
⑨ （可选）人工加注 note_zh / note_en 细节
```
