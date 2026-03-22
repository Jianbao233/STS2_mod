# RunHistoryAnalyzer | 历史记录异常检测

> 分析单机/联机模式中的异常跑图记录，检测玩家作弊行为。  
> Analyzes abnormal run records in single-player and multiplayer modes, detecting potential cheating.

---

## 功能 | Features

**按需检测**：在游戏内历史记录面板底部显示【分析】按钮，点击后分析当前存档。

**10 条检测规则（覆盖 P0 → P2）：**

| 等级 | 规则 | 说明 |
|------|------|------|
| P0 | `GoldConservationRule` | 金币守恒（初始 + Σ收入 - Σ支出 = 结余） |
| P0 | `ShopGoldSpikeRule` | 商店金币尖刺（一次性大额买入） |
| P0 | `NonShopLargeGoldGainRule` | 非商店大额金币（≥250金币且非战利品地图奖励） |
| P0 | `HpConservationRule` | HP守恒 |
| P0 | `HpBoundaryRule` | HP越界（当前HP > 最大HP） |
| P1 | `CardSourceTraceRule` | 卡牌来源追溯（未通过合法途径获得的卡牌） |
| P1 | `CharacterCardAffinityRule` | 角色卡牌亲和性（异色卡检测） |
| P1 | `RelicSourceTraceRule` | 遗物来源追溯 |
| P1 | `RelicMultiPickRule` | 单节点多遗物选取（同一节点 was_picked 异常） |
| P2 | `PotionSourceTraceRule` | 药水来源追溯 |

**支持联机模式**：按玩家 ID 分别分析，支持 4 人联机存档。

**导出报告**：分析结果可导出为 `.txt` 文本，便于存档对比。

---

## 安装 | Installation

### 从 Release 安装（推荐）

1. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `RunHistoryAnalyzer-v0.x.x.zip`
2. 解压到游戏目录 `Steam\steamapps\common\Slay the Spire 2\mods\`
3. 启动游戏，在"历史记录"面板底部点击【分析】

### 从源码构建

```powershell
cd K:\杀戮尖塔mod制作\STS2_mod\RunHistoryAnalyzer
dotnet build  # Debug（自动同步到 mods 目录）

# 或 Release
dotnet publish -c Release -o ./releases/vA.B.C
```

**依赖**：.NET 8 SDK、Godot 4.5.1 Mono

---

## 规则与 JSON 数据

检测参数（遗物效果、事件奖励、节点上限）由 `Data/ancient_peoples_rules.json` 驱动，无需修改 C# 代码即可新增/调整规则。

当前支持的 JSON 覆盖场景：
- `ancient` 节点：跳过 NonShopLargeGold 检测，relic_pick_ceiling=10
- `monster` 节点：relic_pick_ceiling=1（含 PAELS_WING 献祭换遗物的特殊情况）
- `EVENT.TRIAL`（mpt=unknown）：relic_pick_ceiling=4
- `mpt=monster + room_type=ancient`：relic_pick_ceiling=5
- `boss` 节点：relic_pick_ceiling=2（联机多人各拿1件）
- 110+ 遗物效果描述（中英双语）
- 8 先古 NPC + 34 事件效果

---

## 项目结构

```
RunHistoryAnalyzer/
├── RunHistoryAnalyzerCore.cs          # 分析引擎：加载 .run JSON，执行全部规则
├── AncientRuleLoader.cs               # JSON 规则加载器（支持三键查表）
├── Detection/
│   ├── GoldConservationRule.cs
│   ├── ShopGoldSpikeRule.cs
│   ├── NonShopLargeGoldGainRule.cs
│   ├── HpConservationRule.cs
│   ├── HpBoundaryRule.cs
│   ├── CardSourceTraceRule.cs
│   ├── CharacterCardAffinityRule.cs
│   ├── RelicSourceTraceRule.cs
│   ├── RelicMultiPickRule.cs
│   ├── PotionSourceTraceRule.cs
│   └── MapNodeShopUtil.cs
├── Models/
│   ├── RunHistoryData.cs             # .run JSON 完整数据模型（与游戏反序列化对齐）
│   ├── Anomaly.cs / AnomalyLevel.cs
│   └── IAnomalyRule.cs
├── Data/
│   └── ancient_peoples_rules.json    # 遗物/事件/节点覆盖规则（中英双语）
├── tools/
│   ├── extract_ancient_candidates.py # 从游戏源码 JSON 提取候选规则
│   ├── analyze_run_ancient_stats.py # 从 .run 存档统计验证
│   └── inspect_neow_boss.py          # 调试工具：打印 NEoW/Boss 节点详情
└── releases/                          # 打包好的发布版本
```

---

## 版本说明

| 版本 | 说明 |
|------|------|
| 0.2.0 | Demo：内置 10 条检测规则 + JSON 规则库（110+遗物、34事件、8先古NPC）；三键节点匹配（mpt + model_id + room_type）|
| 0.1.7 | 首次功能版本：初版检测规则框架 |

---

## License

MIT
