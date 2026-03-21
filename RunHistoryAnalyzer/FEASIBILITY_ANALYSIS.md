# RunHistoryAnalyzer — 从历史记录检测异常数据的可行性分析

> 核心问题：从"百科大全 → 历史记录"模块入手，检测玩家通过本地内存修改、控制台作弊、存档文件直接编辑等手段产生的异常记录，可行性有多高？

---

## 一、为什么选择历史记录而非存档

游戏有两套数据体系，特性截然不同：

| 维度 | 存档 `SerializableRun` | 历史记录 `RunHistory` | 历史记录胜出 |
|---|---|---|---|
| **格式** | 二进制（PacketWriter） | **JSON** | ✅ 可直接解析 |
| **记录方式** | 当前状态快照 | **完整流水账** | ✅ 可追溯过程 |
| **金币** | 只有最终值 `Gold` | `GoldGained + Spent + Lost + Stolen + CurrentGold` | ✅ 可验证守恒 |
| **HP** | 只有 `CurrentHp/MaxHp` 快照 | `DamageTaken + HpHealed + MaxHpGained/Lost` | ✅ 可验证守恒 |
| **卡牌来源** | 只存最终 Deck | `CardChoices[wasPicked]` + `CardsGained` + `CardsRemoved` | ✅ 可追溯来源 |
| **遗物来源** | 只存最终 Relics | `RelicChoices[wasPicked]` + `BoughtRelics` + `RelicsRemoved` | ✅ 可追溯来源 |
| **RNG** | 完整随机数状态 | **只存 seed** | ❌ 存档更优 |
| **遭遇状态** | `EncounterState` 完整 | ❌ 无 | ❌ 存档更优 |
| **发现列表** | `DiscoveredCards/Enemies` | ❌ 无 | ❌ 存档更优 |

**结论**：存档存"结果"，历史记录存"过程"。对于检测**金币/HP/卡牌/遗物**的异常，历史记录具有不可替代的优势——它的流水账天然支持守恒定律验证和来源追溯。

---

## 二、核心数据结构回顾

### 2.1 流水账核心：PlayerMapPointHistoryEntry

每个地图节点存储所有玩家的增量数据：

```json
// 来自 map_point_history[act][node][player_stat]
{
  "current_gold"      : 500,   // 当前金币（快照）
  "gold_gained"       : 300,   // 累计获得金币
  "gold_spent"        : 100,   // 累计消耗金币
  "gold_lost"         : 0,     // 累计丢失金币（如事件惩罚）
  "gold_stolen"       : 0,     // 累计偷取金币
  "current_hp"        : 65,    // 当前HP（快照）
  "max_hp"            : 80,    // 最大HP（快照）
  "damage_taken"      : 25,    // 累计受伤
  "hp_healed"         : 10,    // 累计治疗
  "max_hp_gained"     : 0,     // 累计最大HP增加
  "max_hp_lost"       : 0,     // 累计最大HP减少
  "cards_gained"      : [...], // 战斗奖励获得的卡牌
  "card_choices"      : [...], // 卡牌选择（3选1等）
  "cards_removed"     : [...], // 移除的卡牌
  "upgraded_cards"    : [...], // 升级的卡牌
  "downgraded_cards"  : [...], // 降级的卡牌
  "cards_enchanted"   : [...], // 附魔的卡牌
  "cards_transformed"  : [...], // 变形的卡牌
  "relic_choices"    : [...], // 遗物选择
  "relics_removed"    : [...], // 移除的遗物
  "bought_relics"     : [...], // 购买的遗物
  "potion_choices"    : [...], // 药水选择
  "potion_used"       : [...], // 使用的药水
  "potion_discarded" : [...], // 丢弃的药水
  "bought_potions"    : [...], // 购买的药水
  "bought_colorless"  : [...], // 商店购买的无色卡牌
  "ancient_choices"  : [...], // 古代祭坛选择
  "event_choices"    : [...], // 事件选项选择
  "rest_site_choices": [...], // 休息点选择
  "completed_quests"  : [...]   // 完成的任务
}
```

### 2.2 来源追溯关键：ChoiceHistoryEntry

所有选择类历史条目都有 `wasPicked` 字段，精确记录哪张卡/遗物/药水被选中：

```json
// CardChoiceHistoryEntry
{ "card": {"id": "STRIKE_P"}, "was_picked": true }

// ModelChoiceHistoryEntry（遗物/药水）
{ "choice": "RELIC.BURNING_BLOOD", "was_picked": true }
```

**关键点**：`wasPicked=true` 的项 = 实际获得的来源；`wasPicked=false` = 被拒绝的选项，可用于交叉验证。

### 2.3 数据流：游戏过程 → 历史记录

```
游戏过程（运行中）
  └─ SerializablePlayer（实时快照）
      ├─ CurrentHp / MaxHp / Gold     ← 随时被内存修改
      └─ Deck / Relics / Potions      ← 随时被内存修改

  节点切换时
  └─ RunManager.UpdatePlayerStatsInMapPointHistory()
      └─ 将快照写入 PlayerMapPointHistoryEntry 的流水账字段

  跑图结束（胜利/死亡/放弃）
  └─ RunHistoryUtilities.CreateRunHistoryEntry()
      ├─ Deck/Relics/Potions ← 直接复制 SerializablePlayer 最终快照
      └─ MapPointHistory     ← 直接引用存档中的流水账对象（同一引用）
```

---

## 三、可检测的异常类型

### 3.1 等级一：数据守恒定律（证据链最强）

#### A. 金币守恒

**定律**：`初始金币 + ΣGoldGained - ΣGoldSpent - ΣGoldLost = 最终CurrentGold`

```csharp
int initialGold = 99; // 初始金币
int totalGained = 0, totalSpent = 0, totalLost = 0;
int finalGold = 0;

foreach (var act in history.MapPointHistory)
foreach (var node in act)
foreach (var stat in node.PlayerStats)
{
    totalGained += stat.GoldGained;
    totalSpent   += stat.GoldSpent;
    totalLost    += stat.GoldLost;
    finalGold     = stat.CurrentGold; // 取最后一个节点的值
}

int expectedGold = initialGold + totalGained - totalSpent - totalLost;
if (Math.Abs(expectedGold - finalGold) > 1) // 允许1金币误差
    → 异常：金币不守恒，疑似存档编辑或内存修改
```

**可检测的作弊**：
- 直接编辑 `CurrentGold` 提高金币
- `gold N` 控制台作弊（获得金币无对应来源）
- 内存修改 `Player.Gold` 超过合理范围

#### B. HP守恒

**定律 1**（最大HP）：`初始MaxHp + ΣMaxHpGained - ΣMaxHpLost = 最终MaxHp`

**定律 2**（当前HP）：`初始CurrentHp - ΣDamageTaken + ΣHpHealed = 最终CurrentHp`

```csharp
int initialMaxHp = 角色初始MaxHp;
int initialHp = 角色初始CurrentHp;
int totalDamageTaken = 0, totalHealed = 0, totalMaxHpGained = 0, totalMaxHpLost = 0;

foreach (var stat in allPlayerStats)
{
    totalDamageTaken  += stat.DamageTaken;
    totalHealed       += stat.HpHealed;
    totalMaxHpGained  += stat.MaxHpGained;
    totalMaxHpLost    += stat.MaxHpLost;
}

int expectedMaxHp = initialMaxHp + totalMaxHpGained - totalMaxHpLost;
int expectedHp    = initialHp - totalDamageTaken + totalHealed;

// 边界检测
if (expectedHp > expectedMaxHp) expectedHp = expectedMaxHp; // 正常上限

if (Math.Abs(expectedHp - finalCurrentHp) > 2)   → 异常
if (Math.Abs(expectedMaxHp - finalMaxHp) > 0)    → 异常
```

**可检测的作弊**：
- 直接编辑 HP 相关字段
- `heal N` 控制台瞬间满血
- 内存修改 `Player.CurrentHp`

#### C. 边界检测（简单有效）

```csharp
// HP 绝不可能超过最大HP
if (finalCurrentHp > finalMaxHp)
    → 异常：CurrentHp > MaxHp

// 金币正常上限约 9999（根据游戏设计）
if (finalGold > 9999)
    → 异常：金币超出合理范围

// 每节点的金币变动不应过大（正常单次奖励上限）
foreach (var stat in allPlayerStats)
{
    if (stat.GoldGained > 200)  → 警告：单次获得金币异常
    if (stat.GoldSpent > 500)   → 警告：单次消耗金币异常
}
```

---

### 3.2 等级二：来源可追溯性（证据链强）

#### A. 卡牌来源追溯

**原则**：每张非初始卡牌必须来自以下来源之一：
- `CardsGained`：战斗奖励
- `CardChoices[wasPicked=true]`：卡牌选择（如篝火升级）
- `BoughtColorless`：商店购买无色卡牌
- 初始卡组（固定列表）

```csharp
HashSet<string> acquiredCardIds = new HashSet<string>();

// 收集所有获得来源
foreach (var stat in allPlayerStats)
{
    foreach (var card in stat.CardsGained)
        acquiredCardIds.Add(card.Id.Entry);
    foreach (var choice in stat.CardChoices)
        if (choice.WasPicked)
            acquiredCardIds.Add(choice.Card.Id.Entry);
    foreach (var id in stat.BoughtColorless)
        acquiredCardIds.Add(id.Entry);
}

// 收集所有消耗（移除/变形后的卡）
HashSet<string> removedCardIds = new HashSet<string>();
foreach (var stat in allPlayerStats)
{
    foreach (var card in stat.CardsRemoved)
        removedCardIds.Add(card.Id.Entry);
    foreach (var transform in stat.CardsTransformed)
        removedCardIds.Add(transform.OriginalCard.Id.Entry);
}

// 最终卡组 = (初始卡组 ∪ 获得) - 移除
// 检查最终 Deck 中每张卡是否可追溯
foreach (var card in finalDeck)
{
    bool fromAcquired = acquiredCardIds.Contains(card.Id.Entry);
    bool fromInitial  = IsStarterCard(card);
    bool wasRemoved    = removedCardIds.Contains(card.Id.Entry) &&
                         !fromAcquired; // 曾被移除但没有再次获得

    if (!fromAcquired && !fromInitial && !wasRemoved)
        → 异常：卡牌"{card.Id.Entry}"无法追溯来源，疑似作弊
}
```

**可检测的作弊**：
- `card X` 控制台获得卡牌（不在任何来源列表中）
- 存档直接添加卡牌到 Deck

#### B. 遗物来源追溯

```csharp
// 遗物来源：初始遗物 + RelicChoices[wasPicked] + BoughtRelics + 事件奖励
// 遗物移除：RelicsRemoved + 特定事件替换
// 最终 Relics 应 = 初始 + 获得 - 移除

HashSet<string> acquiredRelics = new HashSet<string>();
HashSet<string> removedRelics = new HashSet<string>();

foreach (var stat in allPlayerStats)
{
    foreach (var choice in stat.RelicChoices)
        if (choice.WasPicked)
            acquiredRelics.Add(choice.Choice.Entry);
    foreach (var id in stat.BoughtRelics)
        acquiredRelics.Add(id.Entry);
    foreach (var id in stat.RelicsRemoved)
        removedRelics.Add(id.Entry);
}

foreach (var relic in finalRelics)
{
    bool fromAcquired = acquiredRelics.Contains(relic.Id.Entry);
    bool fromInitial  = IsStarterRelic(relic);
    bool wasRemoved   = removedRelics.Contains(relic.Id.Entry);

    if (!fromAcquired && !fromInitial && !wasRemoved)
        → 异常：遗物"{relic.Id.Entry}"无法追溯来源
}
```

**可检测的作弊**：`relic X` 控制台作弊

#### C. 药水来源追溯

```csharp
// 药水来源：BoughtPotions + PotionChoices[wasPicked] + 事件赠送
// 最终药水 = 初始 + 获得 - 使用 - 丢弃

// 检测使用/丢弃的药水是否在获得列表中
HashSet<string> acquiredPotions = new HashSet<string>();
HashSet<string> usedPotions = new HashSet<string>();

foreach (var stat in allPlayerStats)
{
    foreach (var choice in stat.PotionChoices)
        if (choice.WasPicked)
            acquiredPotions.Add(choice.Choice.Entry);
    foreach (var id in stat.BoughtPotions)
        acquiredPotions.Add(id.Entry);
    foreach (var id in stat.PotionUsed)
        usedPotions.Add(id.Entry);
    foreach (var id in stat.PotionDiscarded)
        usedPotions.Add(id.Entry); // 丢弃也算消耗
}

foreach (var used in usedPotions)
    if (!acquiredPotions.Contains(used))
        → 异常：使用了未获得的药水
```

---

### 3.3 ~~等级三：行为异常模式~~（已废弃 · 误报率过高）

> ⚠️ 以下检测项均已废弃，不纳入实现方案。
> 纯击杀流/防御流高手极难与作弊区分，异常通关时间阈值难以设定，路线合理性存在大量特殊情况。

#### ~~A. 无敌检测~~ → 已废弃

~~战斗累计受伤为0但非放弃且非胜利 → 疑似无敌作弊~~
~~注意：纯击杀流/防御流高手可能确实无伤，误报率极高~~

#### ~~B. 异常通关时间~~ → 已废弃

~~通关时间过短 → 疑似跳过战斗~~
~~问题：需设定合理阈值，高手和作弊难以区分~~

#### ~~C. 路线合理性~~ → 已废弃

~~无战斗通关 → 疑似作弊~~
~~问题：存在大量特殊情况（如只打Boss跳过战斗），误报率高~~

---

## 四、各作弊手段的可检测性矩阵

| 作弊手段 | 存档修改 | 历史记录检测 | 证据链强度 | 说明 |
|---|---|---|---|---|
| 直接编辑 `CurrentGold` | ✅ | ✅ 金币守恒 | **强** | 最终金币与流水账矛盾 |
| `gold N` 控制台作弊 | ❌ 无法阻止 | ✅ 金币守恒 + 无来源 | **强** | 金币增加但无对应交易记录 |
| 直接编辑 `CurrentHp/MaxHp` | ✅ | ✅ HP守恒 | **强** | HP快照与累计受伤/治疗矛盾 |
| `heal N` 控制台作弊 | ❌ 无法阻止 | ✅ HP守恒 | **强** | 治疗增加但无休息点记录 |
| `godmode` 无敌作弊 | ❌ 无法阻止 | ~~✅ 无敌检测~~ → ~~已废弃~~ | ~~**中**~~ | 行为检测已废弃 |
| 内存修改战斗伤害 | ❌ 运行时不检测 | ~~✅ 无敌检测~~ → ~~已废弃~~ | ~~**中**~~ | 行为检测已废弃 |
| 直接编辑 `win=true` | ✅ | ~~⚠️ 部分检测~~ → ~~已废弃~~ | ~~**弱**~~ | 行为检测已废弃 |
| 内存修改 `win=true` | ❌ 无法阻止 | ⚠️ 路线矛盾检测 | **弱** | 无战斗通关/时间过短可检测部分 |
| 修改 `run_time` | ✅ | ✅ 可绕过 | — | 如果同时修改则无法检测 |
| 修改所有字段一致 | ❌ | ❌ 无法检测 | — | 完美伪造无法从数据检测 |

---

## 五、可行性结论与优先级

### 5.1 总体结论

**完全可行，历史记录是检测作弊的最优数据源。**

原因：
1. **数据天然支持守恒定律**：金币/HP 的增量字段（`_Gained/Spent/DamageTaken`）与最终快照天然形成验证对
2. **来源可追溯**：`ChoiceHistoryEntry[wasPicked]` 精确记录每次选择的来源
3. **JSON 格式**：无需二进制解析，可直接读取分析
4. **客户端可实施**：无需服务器支持，单机即可运行

### 5.2 检测优先级

| 优先级 | 检测项 | 难度 | 误报率 | 理由 |
|---|---|---|---|---|
| **P0** | 金币守恒定律 | ⭐ | 极低 | 数学等式，零模糊性 |
| **P0** | HP守恒定律 | ⭐ | 极低 | 数学等式，零模糊性 |
| **P0** | HP边界（CurrentHp > MaxHp） | ⭐ | 零 | 数学不可能 |
| **P1** | 卡牌来源追溯 | ⭐⭐ | 低 | 初始卡组固定，来源明确 |
| **P1** | 遗物来源追溯 | ⭐⭐ | 低 | 初始遗物固定，来源明确 |
| **P2** | 药水来源追溯 | ⭐⭐ | 低 | 逻辑清晰 |
| **P1** | ~~无敌检测（零伤害通关）~~ → ~~已废弃~~ | ~~⭐⭐~~ | ~~中~~ | 高手无伤难以区分 |
| **P2** | ~~异常通关时间~~ → ~~已废弃~~ | ~~⭐⭐~~ | ~~高~~ | 阈值难以设定 |
| **P3** | ~~路线合理性（无战斗通关）~~ → ~~已废弃~~ | ~~⭐⭐⭐~~ | ~~中~~ | 特殊情况多 |
| ~~P3~~ | ~~控制台日志关联~~ → ~~已废弃~~ | ~~⭐⭐⭐~~ | ~~低~~ | 依赖 godot.log |

### 5.3 局限性承认

1. **完美伪造无法检测**：若作弊者同时修改所有相关字段使其一致，任何检测都无效
2. **行为类检测已废弃**：无敌检测/异常通关时间/路线合理性因误报率过高，不纳入本方案
3. **无实时阻止能力**：只能事后标记，无法在游戏中实时拦截作弊

---

## 六、实现方案建议

### 6.1 三层架构

```
UI 层（显示）
  └─ 历史记录界面（NRunHistory）添加异常标记图标

检测层（分析）
  ├─ GoldConservationRule       → P0
  ├─ HpConservationRule        → P0
  ├─ CardSourceTraceRule        → P1
  ├─ RelicSourceTraceRule       → P1
  └─ PotionSourceTraceRule      → P2

数据层（读取）
  └─ RunHistorySaveManager → JSON 解析 → RunHistory 对象
```

### 6.2 关键 Hook 点

| 位置 | 用途 |
|---|---|
| `NRunHistory.RefreshAndSelectRun()` | 加载历史记录后立即检测 |
| `SaveManager.SaveRunHistory()` | 跑图结束时检测当前记录 |
| `NRunHistory.DisplayRun()` | 在 UI 中添加异常标记 |

### 6.3 输出形式

1. **历史记录列表**：异常记录旁显示红色/橙色标记
2. **详情弹窗**：悬停显示异常原因列表（高/中/低三级）
3. **日志输出**：`[RunHistoryAnalyzer] 检测到异常 #N：[高] 金币不守恒...`

---

## 七、关键源码索引

| 源码文件 | 路径 |
|---|---|
| RunHistory 主类 | `MegaCrit.Sts2.Core.Runs.RunHistory` |
| 玩家历史数据 | `MegaCrit.Sts2.Core.Runs.RunHistoryPlayer` |
| **节点统计（流水账）** | `MegaCrit.Sts2.Core.Runs.PlayerMapPointHistoryEntry` |
| **转换函数** | `MegaCrit.Sts2.Core.Runs.RunHistoryUtilities` |
| 历史记录界面 | `MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory` |
| 存档管理器 | `MegaCrit.Sts2.Core.Saves.Managers.RunHistorySaveManager` |
| SerializableCard | `MegaCrit.Sts2.Core.Saves.Runs.SerializableCard` |
| SerializableRelic | `MegaCrit.Sts2.Core.Saves.Runs.SerializableRelic` |
| SerializablePotion | `MegaCrit.Sts2.Core.Saves.Runs.SerializablePotion` |
| CardChoiceHistoryEntry | `MegaCrit.Sts2.Core.Runs.History.CardChoiceHistoryEntry` |
| ModelChoiceHistoryEntry | `MegaCrit.Sts2.Core.Runs.History.ModelChoiceHistoryEntry` |

**源码前缀**：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260318\sts2\`
