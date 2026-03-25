# current_run_mp.save 结构分析报告

> 来源：`C:\Users\Administrator\AppData\Roaming\SlayTheSpire2\steam\76561198679823594\modded\profile1\saves\current_run_mp.save`
> 分析时间：2026-03-20
> 游戏版本：当前版本（对应 schema_version = 14）
> 对局状态：第1幕（ACT.UNDERDOCKS），已完成 Neow 事件 + 1场战斗（SEAPUNK_WEAK），2名玩家

---

## 一、顶层字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `players` | list[dict] | **核心**：玩家列表 |
| `map_point_history` | list[list] | 各层玩家统计（每层的房间节点+玩家数据） |
| `map_drawings` | string | Base64 + Gzip 编码的地图涂鸦 |
| `acts` | list[dict] | 3个幕的配置（ACT.UNDERDOCKS/HIVE/GLORY） |
| `shared_relic_grab_bag` | dict | 共享遗物获取袋 |
| `rng` | dict | 全局随机数状态 |
| `odds` | dict | 全局概率参数 |
| `pre_finished_room` | dict | 预完成房间（用于重连恢复） |
| `visited_map_coords` | list | 已访问地图坐标 |
| `ascension` | int | 进阶等级 |
| `current_act_index` | int | 当前幕索引（0=第1幕） |
| `schema_version` | int | 存档格式版本号 = **14** |
| `platform_type` | string | `"none"`（当前存档）|
| `start_time / save_time / run_time` | int | 时间戳（秒） |
| `win_time` | int | 通关时间，未通关为0 |

---

## 二、players[] 玩家数据结构

> **重要结论**：当前版本使用**字符串格式**（`"CHARACTER.SILENT"`），**不是** C# ModelId 的 `{entry: "SILENT"}` 对象格式。

### 2.1 完整字段列表

```json
{
  "net_id": 76561198679823594,           // Steam 64位ID（数值）
  "character_id": "CHARACTER.SILENT",   // 角色ID（字符串，非 ModelId）
  "current_hp": 70,                     // 当前生命
  "max_hp": 70,                         // 最大生命
  "gold": 99,                           // 金币
  "max_energy": 3,                      // 最大能量
  "max_potion_slot_count": 3,           // 药水栏位上限
  "base_orb_slot_count": 0,             // 基础充能球槽数（Defect相关）
  "deck": [...],                         // 牌组
  "relics": [...],                       // 遗物
  "potions": [...],                      // 药水
  "rng": {...},                          // 玩家随机数状态
  "odds": {...},                         // 玩家概率状态
  "relic_grab_bag": {...},              // 玩家遗物获取袋
  "discovered_cards": [],                // 已发现卡牌
  "discovered_relics": [],               // 已发现遗物
  "discovered_enemies": [],              // 已发现敌人
  "discovered_epochs": [],               // 已发现 Epoch
  "unlock_state": {...},                 // 解锁状态
  "extra_fields": {}                    // 额外字段
}
```

### 2.2 牌组 deck[]

```json
{
  "id": "CARD.STRIKE_SILENT",           // 卡牌ID（字符串，非 ModelId）
  "floor_added_to_deck": 1,             // 加入牌组的层数
  "current_upgrade_level": 0             // 升级等级（0=无升级）
}
```

> **注意**：`current_upgrade_level` 字段存在但本存档为0（初始牌组）。升级卡牌后会变为1/2/3。

### 2.3 遗物 relics[]

```json
{
  "id": "RELIC.RING_OF_THE_SNAKE",     // 遗物ID（字符串）
  "floor_added_to_deck": 1             // 获得遗物的层数
}
```

### 2.4 药水 potions[]

```json
{
  "id": "POTION.FLEX_POTION",           // 药水ID
  "slot_index": 0                       // 栏位索引（0/1/2）
}
```

### 2.5 随机数 rng

```json
{
  "seed": 2189922105,                   // 随机种子（数值）
  "counters": {
    "rewards": 1,                       // 奖励计数器
    "shops": 0,                         // 商店计数器
    "transformations": 0                 // 变形计数器
  }
}
```

### 2.6 概率 odds（玩家级）

```json
{
  "card_rarity_odds_value": -0.05,      // 卡牌稀有度偏移
  "potion_reward_odds_value": 0.4       // 药水奖励概率
}
```

### 2.7 遗物获取袋 relic_grab_bag

```json
{
  "relic_id_lists": {
    "common":   [...],   // 普通遗物列表（约22-25件）
    "uncommon": [...],   // 非普通遗物列表（约25-30件）
    "rare":     [...],   // 稀有遗物列表（约28-35件）
    "shop":     [...]    // 商店遗物列表（约25-26件）
  }
}
```

> 遗物袋**各玩家独立**，不影响其他玩家。这是 v1 已有结论，当前版本确认仍然如此。

### 2.8 unlock_state

```json
{
  "number_of_runs": 78,                 // 总游戏局数
  "unlocked_epochs": [                  // 已解锁 Epoch 列表（约36个）
    "COLORLESS1_EPOCH", "RELIC1_EPOCH", ...
  ],
  "encounters_seen": [...]               // 已遭遇敌人列表（约83种）
}
```

### 2.9 discovered_* 字段

| 字段 | 含义 | 本存档值 |
|------|------|---------|
| `discovered_cards` | 已发现卡牌 | 0（初始时为空）|
| `discovered_relics` | 已发现遗物 | 0（初始时为空）|
| `discovered_enemies` | 已发现敌人 | 0（初始时为空）|
| `discovered_epochs` | 已发现 Epoch | 0（初始时为空）|

> **关键**：`discovered_epochs` 在早期玩家中为空，但较旧存档的玩家2中有 `discovered_epochs: ["REGENT3_EPOCH"]`。新玩家添加时应初始化为空列表 `[]`。

---

## 三、顶层 rng（全局随机数）

```json
{
  "seed": "0KRZSR53TG",    // 种子（字符串形式）
  "counters": {
    "up_front": 497,
    "shuffle": 21,
    "unknown_map_point": 0,
    "combat_card_generation": 0,
    "combat_potion_generation": 0,
    "combat_card_selection": 0,
    "combat_energy_costs": 0,
    "combat_targets": 0,
    "monster_ai": 0,
    "niche": 1,
    "combat_orbs": 0,
    "treasure_room_relics": 0
  }
}
```

> 顶层 rng 由游戏全局管理，**添加/移除玩家不需要修改**。

---

## 四、顶层 odds（全局概率）

```json
{
  "unknown_map_point_elite_odds_value": -1,
  "unknown_map_point_monster_odds_value": 0.1,
  "unknown_map_point_shop_odds_value": 0.03,
  "unknown_map_point_treasure_odds_value": 0.02
}
```

> 顶层 odds 控制地图随机事件概率，**添加/移除玩家不需要修改**。

---

## 五、shared_relic_grab_bag（共享遗物袋）

```json
{
  "relic_id_lists": {
    "common":   [...],   // 约23件
    "uncommon": [...],   // 约29件
    "rare":     [...],   // 约33件
    "shop":     [...],   // 约25件
    "event":    ["RELIC.FRESNEL_LENS"],    // 1件
    "ancient":  ["RELIC.VERY_HOT_COCOA", "RELIC.LOOMING_FRUIT"]  // 2件
  }
}
```

> **共享遗物袋**在所有玩家间共享，移除玩家时**不需要修改**（因为拾取后从列表移除，而非按玩家隔离）。

---

## 六、map_point_history 地图统计

### 结构

```json
map_point_history: [
  [  // 第1层（已通过的房间）
    {  // 节点0：古代神庙（Neow事件）
      "map_point_type": "ancient",
      "player_stats": [
        {
          "player_id": 76561198679823594,
          "current_gold": 99,
          "current_hp": 70,
          "max_hp": 70,
          "damage_taken": 0,
          "gold_gained": 0,
          "gold_lost": 0,
          "gold_spent": 0,
          "gold_stolen": 0,
          "hp_healed": 70,
          "max_hp_gained": 0,
          "max_hp_lost": 0,
          "cards_gained": [{"id": "CARD.ABRASIVE"}],
          "relic_choices": [{"choice": "RELIC.ARCANE_SCROLL", "was_picked": true}],
          "event_choices": [{"title": {...}}],
          "ancient_choice": [  // 仅 ancient 类型节点有
            {"TextKey": "ARCANE_SCROLL", "was_chosen": true, "title": {...}},
            ...
          ]
        },
        {
          "player_id": 76561199718354550,
          "current_gold": 249,
          "current_hp": 80,
          "max_hp": 80,
          ...
        }
      ],
      "rooms": [
        {"room_type": "event", "model_id": "EVENT.NEOW", "turns_taken": 0}
      ]
    },
    {  // 节点1：战斗
      "map_point_type": "monster",
      "player_stats": [...],
      "rooms": [
        {"room_type": "monster", "model_id": "ENCOUNTER.SEAPUNK_WEAK",
         "monster_ids": ["MONSTER.SEAPUNK"], "turns_taken": 1}
      ]
    }
  ]
]
```

### 添加新玩家时 map_point_history 处理

当添加新玩家时，**必须在所有已有 `map_point_history` 条目的 `player_stats` 中注入该玩家的初始统计**，否则游戏在 `LoadIntoLatestMapCoord` 时会因查不到玩家 stats 而崩溃。

注入模板：

```json
{
  "player_id": <new_net_id>,
  "current_gold": <new_player.gold>,
  "current_hp": <new_player.current_hp>,
  "max_hp": <new_player.max_hp>,
  "damage_taken": 0,
  "gold_gained": 0,
  "gold_lost": 0,
  "gold_spent": 0,
  "gold_stolen": 0,
  "hp_healed": <new_player.max_hp>,
  "max_hp_gained": 0,
  "max_hp_lost": 0,
  "cards_gained": [],
  "relic_choices": [],
  "event_choices": []
}
```

> **注意**：对于 `map_point_type == "ancient"` 的节点，还需添加 `ancient_choice: []` 字段。

---

## 七、map_drawings 地图涂鸦

格式：`Base64(gzip(二进制))`，其中二进制为：

```
02 00 00 00           # 小端 int32 = 2（绘制数量）
02 00 00 00           # 小端 int32 = 2（？版本？）
...
```

二进制内容编码了每个玩家的涂鸦数据，包含 `playerId` 和 `points` 坐标数组。

> **编码格式未完全解析**。但移除玩家时需同步清理 map_drawings 中对应 `playerId` 的绘制记录，否则旧玩家退出后涂鸦仍可能残留。当前 v1 代码使用 gzip 解压后修改 JSON，再重新 gzip+base64 编码，但本存档解压后格式与预期不同，需要进一步确认。

**临时方案**：移除玩家时忽略 map_drawings（游戏中通常不影响）。

---

## 八、pre_finished_room

```json
{
  "is_pre_finished": true,
  "room_type": "monster",
  "encounter_id": "ENCOUNTER.SEAPUNK_WEAK",
  "event_id": null,
  "parent_event_id": null,
  "should_resume_parent_event": true,
  "reward_proportion": 1
}
```

> 表示上一场已完成战斗（SEAPUNK_WEAK），用于重连时恢复状态。
> **添加/移除玩家不需要修改**。

---

## 九、acts 三幕配置

每幕包含：

| 子字段 | 说明 |
|--------|------|
| `id` | 幕ID（如 `ACT.UNDERDOCKS`）|
| `rooms.ancient_id` | 古代神庙事件ID |
| `rooms.boss_id` | Boss战ID |
| `rooms.elite_encounter_ids` | 精英敌人池（约15个）|
| `rooms.event_ids` | 事件池（约25-28个）|
| `rooms.normal_encounter_ids` | 普通敌人池（约12-14个）|
| `rooms.boss_encounters_visited` | Boss战已访问数 |
| `rooms.elite_encounters_visited` | 精英已访问数 |
| `rooms.events_visited` | 事件已访问数 |
| `rooms.second_boss_id` | 第二Boss（部分幕有）|
| `saved_map` | 已生成地图的完整结构 |

> **acts 在添加/移除玩家时不需要修改**。

---

## 十、关键结论

### 10.1 存档格式版本

- **schema_version = 14**，当前游戏版本使用**字符串格式**（`"CHARACTER.SILENT"`）
- **不是** C# ModelId 的 `{entry: "SILENT"}` 对象格式
- v1 分析文档中提到的 ModelId 格式可能适用于更晚期的版本或有版本差异

### 10.2 v1 工具兼容性

v1 `manage_players.py` **可以直接复用**，无需修改数据层：

- `character_id` → 字符串 `"CHARACTER.SILENT"` ✅ 与 v1 一致
- `deck[].id` → 字符串 `"CARD.STRIKE_SILENT"` ✅ 与 v1 一致
- `relics[].id` → 字符串 `"RELIC.RING_OF_THE_SNAKE"` ✅ 与 v1 一致
- `current_upgrade_level` 字段存在但不影响操作 ✅
- `discovered_epochs` 字段新增，添加玩家时应初始化为 `[]` ✅

### 10.3 新增玩家时需处理的字段

| 字段 | 初始值 |
|------|--------|
| `discovered_cards` | `[]` |
| `discovered_relics` | `[]` |
| `discovered_enemies` | `[]` |
| `discovered_epochs` | `[]` |
| `extra_fields` | `{}` |
| `discovered_relics`（如设初始遗物）| `[<starter_relic>]` |

### 10.4 待确认

1. **map_drawings 二进制格式**：当前存档 gzip 解压后内容与旧存档不同，需进一步分析编码格式
2. **新版本 ModelId 格式**：如游戏后续版本切换到 `{entry: "SILENT"}` 格式，需要重写数据层
3. **player_template.json 对应关系**：Mod 角色使用 `MOD.XXX` 前缀，需要确认游戏中 `character_id` 字段如何存储

---

## 十一、map_point_history 语义深度解析

### 11.1 `current_hp` / `current_gold` 的真实含义

**这两个字段表示进入该房间时的快照，不是战斗结果。**

证据：
- 玩家1（静默猎手）进入 SEAPUNK_WEAK 前：`gold=99`
- SEAPUNK_WEAK 节点 `player_stats`：`current_gold=115`（= 99 + gold_gained=16，进入时快照）
- 玩家1当前 `gold=115`（战斗后真实值，存于 `players[]`）
- 下一节点 CORPSE_SLUGS_WEAK：`current_gold=0`（进入该房间前的快照值）

> **注入新玩家时**，`current_hp`/`current_gold` 应设为玩家的**实际当前值**（而非0），避免游戏读取到错误快照。

### 11.2 不同节点类型的 `player_stats` 字段差异

#### 战斗房间（monster）

```json
{
  "player_id": 76561198679823594,
  "current_hp": 0,           // 进入房间时的HP快照（战斗后不更新）
  "current_gold": 0,         // 进入房间时的金币快照
  "max_hp": 70,
  "damage_taken": 0,         // 本场战斗受到的伤害
  "gold_gained": 16,         // 本场战斗获得的金币
  "gold_spent": 0,
  "gold_lost": 0,
  "gold_stolen": 0,
  "hp_healed": 0,            // 本场战斗治疗量
  "max_hp_gained": 0,
  "max_hp_lost": 0,
  "card_choices": [           // 战斗奖励卡牌选项
    {"card": {"id": "CARD.BLUR", "floor_added_to_deck": 2}, "was_picked": true},
    {"card": {"id": "CARD.RICOCHET"}, "was_picked": false}
  ],
  "cards_gained": [          // 战斗获得的所有卡牌（包含未被选择的）
    {"id": "CARD.BLUR"}
  ]
}
```

> `card_choices` 记录了战斗奖励卡牌的选择过程，`cards_gained` 记录本场战斗实际获得的卡牌。

#### 古代神庙房间（ancient）

```json
{
  "player_id": 76561198679823594,
  "current_hp": 70,
  "current_gold": 99,
  "max_hp": 70,
  "damage_taken": 0,
  "gold_gained": 0,
  "hp_healed": 70,           // Neow 事件给了 10% 最大生命治疗
  "max_hp_gained": 0,
  "max_hp_lost": 0,
  "relic_choices": [         // 遗物选择
    {"choice": "RELIC.ARCANE_SCROLL", "was_picked": true}
  ],
  "event_choices": [          // 事件选项
    {"title": {"key": "ARCANE_SCROLL.title", "table": "relics"}}
  ],
  "ancient_choice": [         // 古代神庙特有选项
    {"TextKey": "ARCANE_SCROLL", "was_chosen": true, "title": {...}},
    {"TextKey": "PRECISE_SCISSORS", "was_chosen": false, "title": {...}}
  ]
}
```

> **关键**：古代房间有 `relic_choices`、`event_choices`、`ancient_choice`，战斗房间有 `card_choices`、`cards_gained`。注入新玩家时，必须包含房间类型对应的全部字段，缺字段可能导致游戏读取异常。

### 11.3 新玩家注入模板

```python
def build_player_stat_entry(new_player: dict, map_point_type: str) -> dict:
    """为新玩家构建 player_stats 条目"""
    stat = {
        "player_id": new_player["net_id"],
        "current_gold": new_player.get("gold", 0),
        "current_hp": new_player.get("current_hp", new_player.get("max_hp", 0)),
        "max_hp": new_player.get("max_hp", 0),
        "damage_taken": 0,
        "gold_gained": 0,
        "gold_lost": 0,
        "gold_spent": 0,
        "gold_stolen": 0,
        "hp_healed": new_player.get("max_hp", 0),
        "max_hp_gained": 0,
        "max_hp_lost": 0,
        "cards_gained": [],
        "relic_choices": [],
        "event_choices": [],
    }
    if map_point_type == "ancient":
        stat["ancient_choice"] = []
    return stat
```

## 十二、pre_finished_room 语义

```json
{
  "is_pre_finished": true,
  "room_type": "monster",
  "encounter_id": "ENCOUNTER.SEAPUNK_WEAK",
  "event_id": null,
  "parent_event_id": null,
  "should_resume_parent_event": true,
  "reward_proportion": 1
}
```

- `is_pre_finished: true` 表示该房间已被完成，重连时游戏会自动完成并发放奖励
- 每次完成战斗后，`pre_finished_room` 更新为该战斗
- **添加/移除玩家时不需要修改此字段**（游戏会自动重置）

## 十三、card_choices 完整结构

战斗奖励卡牌的选项结构：

```json
"card_choices": [
  {
    "card": {
      "id": "CARD.BLUR",
      "floor_added_to_deck": 2,   // 如果选择此卡，加入牌组的层数
      "current_upgrade_level": 0  // 初始升级等级
    },
    "was_picked": true
  },
  {
    "card": {"id": "CARD.RICOCHET"},
    "was_picked": false
  }
]
```

> `floor_added_to_deck` 只出现在被选中的卡牌中。

## 十四、卡牌升级字段 `current_upgrade_level`

```json
{
  "id": "CARD.STRIKE_NECROBINDER",
  "floor_added_to_deck": 1,
  "current_upgrade_level": 1,    // 0=无升级, 1=+, 2=++, 3=+++

  // 部分卡牌还有附魔信息（铭文）
  "enchantment": {
    "id": "ENCHANTMENT.SWIFT",
    "amount": 2
  },

  // Tinker Time 特殊卡牌有额外属性
  "props": {
    "ints": [
      {"name": "TinkerTimeType", "value": 3},
      {"name": "TinkerTimeRider", "value": 8}
    ]
  }
}
```

> **复制玩家时必须保留** `current_upgrade_level`、`enchantment`、`props` 等字段，否则升级信息会丢失。

## 十五、重要结论汇总

### 15.1 存档格式版本

- **schema_version = 14**，当前游戏版本使用**字符串格式**（`"CHARACTER.SILENT"`）
- **不是** C# ModelId 的 `{entry: "SILENT"}` 对象格式
- v1 分析文档中提到的 ModelId 格式可能适用于更晚期的版本

### 15.2 v1 工具数据层兼容性

v1 `manage_players.py` **大部分逻辑可直接复用**，核心数据结构一致：

| 字段 | 格式 | v1 兼容性 |
|------|------|---------|
| `character_id` | 字符串 | ✅ |
| `deck[].id` | 字符串 | ✅ |
| `relics[].id` | 字符串 | ✅ |
| `current_upgrade_level` | 整数 | ✅ 需保留 |
| `enchantment` | 对象 | ⚠️ 新发现，需保留 |
| `props` | 对象 | ⚠️ 新发现（Tinker卡牌），需保留 |
| `discovered_epochs` | 列表 | ✅ 新增字段，需初始化 |

### 15.3 新增玩家时需处理的字段

| 字段 | 初始值 |
|------|--------|
| `discovered_cards` | `[]` |
| `discovered_relics` | `[<starter_relic>]`（如有初始遗物）|
| `discovered_enemies` | `[]` |
| `discovered_epochs` | `[]` |
| `extra_fields` | `{}` |

### 15.4 map_point_history 注入要点

1. **必须包含房间类型对应的全部字段**：战斗房间需要 `card_choices`、`cards_gained`；古代房间需要 `ancient_choice`、`relic_choices`、`event_choices`
2. `current_hp`/`current_gold` 设为玩家的**实际当前值**
3. `max_hp` 必须正确设置
4. `cards_gained`、`relic_choices`、`event_choices` 初始化为空列表 `[]`

### 15.5 复制玩家时需保留的完整卡牌信息

```python
new_card = {
    "id": source_card["id"],
    "floor_added_to_deck": source_card.get("floor_added_to_deck", 1),
    "current_upgrade_level": source_card.get("current_upgrade_level", 0),
}
# 如果源卡牌有附魔/特殊属性，也需要复制
if "enchantment" in source_card:
    new_card["enchantment"] = copy.deepcopy(source_card["enchantment"])
if "props" in source_card:
    new_card["props"] = copy.deepcopy(source_card["props"])
```

### 15.6 待确认

1. **map_drawings 二进制格式**：当前存档 gzip 解压后是二进制（非 JSON），旧存档是 JSON。两种格式同时存在于游戏中，需要确认编码格式
2. **新版本 ModelId 格式**：如游戏后续版本切换到 `{entry: "SILENT"}` 格式，需要重写数据层
3. **`card_choices` 中未选中卡牌的 `floor_added_to_deck`**：未选中卡牌没有此字段，复制时需要判断
4. **部分战斗房间节点缺少 `card_choices`**：可能是游戏版本差异或数据结构不完整，注入时建议都加上

---

## 十六、v2 工具开发计划

### P0（必须修改）
1. 新增字段初始化：`discovered_cards`, `discovered_relics`, `discovered_enemies`, `discovered_epochs`, `extra_fields`
2. 修复 `map_point_history` 注入逻辑：包含房间类型对应的全部字段
3. 保留卡牌升级信息：`current_upgrade_level`、`enchantment`、`props`

### P1（重要优化）
4. 正确处理 `card_choices` 中未选中卡牌缺少 `floor_added_to_deck` 的情况
5. map_drawings 二进制格式解析（如需支持移除玩家时的涂鸦清理）

### P2（低优先级）
6. 支持 Mod 角色完整生命周期
7. 处理 `props` 字段的完整复制（Tinker Time 卡牌）
