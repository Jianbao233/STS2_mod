# Slay the Spire 2 存档文件完整解析

> 基于源码分析 + 实际存档文件交叉验证。面向制作存档修改工具。

---

## 一、存档文件总览

### 1.1 文件清单

| 文件名 | 类型 | 格式 | Schema版本 | 说明 |
|--------|------|------|-----------|------|
| `profile.save` | 配置 | JSON | v2 | 全局配置（最后使用的Profile ID） |
| `settings.save` | 设置 | JSON | v21 | 视频/音频/按键/Mod启用状态 |
| `prefs.save` | 偏好 | JSON | v2 | 快速模式/屏幕震动/文字特效等 |
| `progress.save` | 进度 | JSON | v21 | **核心**：全局解锁/统计/成就 |
| `current_run.save` | 当前跑图 | JSON | v14 | **核心**：单人当前对局 |
| `current_run_mp.save` | 当前跑图(多人) | JSON | v14 | **核心**：多人当前对局 |
| `*.mcr` | 回放 | 二进制 | - | 游戏回放文件 |

### 1.2 存档目录结构

```
%APPDATA%\SlayTheSpire2\
└── steam\
    └── {SteamId}\
        ├── profile.save                      ← 全局配置
        ├── settings.save                     ← 全局设置
        ├── backups\                          ← 自动/手动备份
        │   ├── normal_p1_auto_before_copy_*
        │   ├── modded_p1_auto_before_copy_*
        │   └── normal_p1_manual_*
        ├── profile1\saves\
        │   ├── progress.save                 ← Profile1 进度（普通）
        │   ├── prefs.save
        │   └── current_run.save              ← Profile1 当前单人局
        ├── profile2\saves\
        │   ├── progress.save                 ← Profile2 进度（普通）
        │   └── current_run.save
        ├── profile3\saves\
        │   ├── progress.save
        │   └── current_run.save
        ├── modded\profile1\saves\
        │   ├── progress.save                 ← Profile1 进度（Mod模式）
        │   ├── prefs.save
        │   ├── current_run.save              ← Profile1 当前单人局（Mod模式）
        │   └── current_run_mp.save           ← Profile1 当前多人局（Mod模式）
        ├── modded\profile2\saves\
        │   ├── progress.save
        │   ├── prefs.save
        │   ├── current_run.save
        │   └── current_run_mp.save
        └── modded\profile3\saves\
            ├── progress.save
            ├── prefs.save
            ├── current_run.save
            └── current_run_mp.save
```

**多人存档** 的判断标准：
1. 文件名含 `_mp` 后缀 → `current_run_mp.save`
2. JSON 内含 `"is_multiplayer": true`
3. `players` 数组内含多个玩家

---

## 二、核心存档详解

---

### 2.1 profile.save — 全局配置（最简）

```json
{
  "last_profile_id": 1,
  "schema_version": 2
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `last_profile_id` | int | 最后使用的存档位（1/2/3） |
| `schema_version` | int | 配置文件版本号 |

---

### 2.2 settings.save — 全局设置

```json
{
  "schema_version": 21,
  "fps_limit": 144,
  "language": "zhs",
  "window_position": [-1, -1],
  "window_size": [1920, 1080],
  "fullscreen": false,
  "aspect_ratio": "sixteen_by_nine",
  "target_display": -1,
  "resize_windows": true,
  "vsync": "adaptive",
  "msaa": 2,
  "volume_master": 0.8,
  "volume_bgm": 0.5,
  "volume_sfx": 0.7,
  "volume_ambience": 0.5,
  "skip_intro_logo": false,
  "full_console": false,
  "mute_in_background": true,
  "limit_fps_in_background": true,
  "keyboard_mapping": {
    "up": "key_up",
    "down": "key_down",
    "left": "key_left",
    "right": "key_right",
    "attack": "key_z",
    ...
  },
  "controller_mapping_type": "default",
  "controller_mapping": { ... },
  "mod_settings": {
    "mod_list": [
      {
        "id": "Heybox",
        "is_enabled": true,
        "source": "steam_workshop",
        "source_id": "1234567890"
      },
      {
        "id": "DamageMeter",
        "is_enabled": true,
        "source": "local",
        "local_path": "DamageMeter"
      }
    ]
  }
}
```

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `fps_limit` | int | 60 | FPS上限 |
| `language` | string | "zhs" | 语言（zhs/eng等） |
| `window_position` | [int,int] | [-1,-1] | 窗口位置，-1=居中 |
| `window_size` | [int,int] | [1920,1080] | 窗口尺寸 |
| `fullscreen` | bool | true | 全屏模式 |
| `aspect_ratio` | string | "sixteen_by_nine" | 宽高比 |
| `vsync` | string | "adaptive" | 垂直同步（off/on/adaptive） |
| `msaa` | int | 2 | 抗锯齿等级（0/2/4/8） |
| `volume_*` | float | 0.5 | 各音量（0.0~1.0） |
| `full_console` | bool | false | 启用完整控制台 |
| `mod_settings.mod_list` | array | [] | 各Mod启用状态 |

---

### 2.3 prefs.save — 偏好设置

```json
{
  "schema_version": 2,
  "fast_mode": "fast",
  "long_press": false,
  "mute_in_background": true,
  "screenshake": 2,
  "show_card_indices": true,
  "show_run_timer": true,
  "text_effects_enabled": true,
  "upload_data": true
}
```

| 字段 | 类型 | 可选值 | 说明 |
|------|------|--------|------|
| `fast_mode` | string | "off"/"fast"/"instant" | 快速模式 |
| `long_press` | bool | - | 启用长按 |
| `screenshake` | int | 0/1/2 | 屏幕震动强度 |
| `show_card_indices` | bool | - | 显示卡牌编号 |
| `show_run_timer` | bool | - | 显示游戏计时 |
| `text_effects_enabled` | bool | true | 文字动画特效 |
| `upload_data` | bool | true | 上传游戏数据 |

---

### 2.4 progress.save — 全局进度（最重要！）

**文件大、结构复杂**（约 500-600KB），记录所有角色/卡牌/敌人的全局统计数据和全局解锁状态。

#### 顶层结构

```json
{
  "schema_version": 21,
  "unique_id": "SBDBQP1",
  "total_playtime": 215561,
  "total_unlocks": 18,
  "floors_climbed": 1458,
  "architect_damage": 23530,
  "wongo_points": 32,
  "max_multiplayer_ascension": 4,
  "preferred_multiplayer_ascension": 0,
  "test_subject_kills": 13,
  "pending_character_unlock": "NONE.NONE",
  "ftue_completed": [
    "multiplayer_warning",
    "accept_tutorials_ftue",
    "map_select_ftue",
    "...": ...
  ],
  "card_stats": [ ... ],
  "character_stats": [ ... ],
  "encounter_stats": [ ... ],
  "enemy_stats": [ ... ],
  "ancient_stats": [ ... ],
  "epoch_stats": [ ... ],
  "unlocked_achievements": [ ... ],
  "discovered_cards": [ ... ],
  "discovered_relcis": [ ... ],
  "discovered_potions": [ ... ],
  "discovered_events": [ ... ],
  "discovered_acts": [ ... ]
}
```

#### 字段详解

| 字段 | 类型 | 说明 |
|------|------|------|
| `schema_version` | int | 进度存档版本号 |
| `unique_id` | string | 玩家唯一标识符 |
| `total_playtime` | long | 总游戏时间（秒） |
| `total_unlocks` | int | 总解锁数 |
| `floors_climbed` | long | 已爬过的总层数（含死亡重开） |
| `architect_damage` | long | 对建筑师的总伤害 |
| `wongo_points` | int | WongoBoss点数（击败Wongo累计） |
| `max_multiplayer_ascension` | int | 多人模式最高难度 |
| `preferred_multiplayer_ascension` | int | 多人模式首选难度 |
| `test_subject_kills` | int | 测试对象击杀数 |
| `pending_character_unlock` | string | 待解锁角色ID |
| `ftue_completed` | string[] | 已完成的新手教程列表 |
| `card_stats` | CardStat[] | 所有卡牌统计 |
| `character_stats` | CharacterStat[] | 所有角色统计 |
| `encounter_stats` | EncounterStat[] | 所有遭遇战统计 |
| `enemy_stats` | EnemyStat[] | 所有敌人统计 |
| `ancient_stats` | AncientStat[] | 古神统计 |
| `epoch_stats` | EpochStat[] | 时代/赛季统计 |
| `unlocked_achievements` | UnlockedAchievement[] | 已解锁成就 |
| `discovered_cards` | string[] | 已发现的卡牌ID |
| `discovered_relics` | string[] | 已发现的遗物ID |
| `discovered_potions` | string[] | 已发现的药水ID |
| `discovered_events` | string[] | 已发现的事件ID |
| `discovered_acts` | string[] | 已发现的剧情/章节ID |

#### CardStat — 卡牌统计

```json
{
  "id": "CARD.STRIKE_IRONCLAD",
  "times_picked": 0,
  "times_skipped": 0,
  "times_won": 0,
  "times_lost": 9
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 卡牌ID（格式：`CARD.XXX`） |
| `times_picked` | long | 被选取的总次数 |
| `times_skipped` | long | 战斗奖励时跳过的次数 |
| `times_won` | long | 胜利时牌组中保留该牌的次数 |
| `times_lost` | long | 失败时牌组中该牌的次数 |

#### CharacterStat — 角色统计

```json
{
  "id": "CHARACTER.IRONCLAD",
  "preferred_ascension": 20,
  "max_ascension": 20,
  "total_wins": 15,
  "total_losses": 37,
  "fastest_win_time": 1234567,
  "best_win_streak": 5,
  "current_win_streak": 1,
  "playtime": 54321
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 角色ID（`CHARACTER.IRONCLAD`等） |
| `preferred_ascension` | int | 首选难度 |
| `max_ascension` | int | 该角色达到的最高难度 |
| `total_wins` | int | 胜利总次数 |
| `total_losses` | int | 失败总次数 |
| `fastest_win_time` | long | 最快胜利时间（毫秒），-1=无胜利 |
| `best_win_streak` | long | 最佳连胜 |
| `current_win_streak` | long | 当前连胜 |
| `playtime` | long | 该角色游戏总时间（秒） |

**已知角色ID：**

| ID | 角色名 |
|----|--------|
| `CHARACTER.IRONCLAD` | 铁甲战士 |
| `CHARACTER.SILENT` | 静默猎手 |
| `CHARACTER.DEFECT` | 故障机器人 |
| `CHARACTER.NECROBINDER` | 亡灵契约师 |
| `CHARACTER.REGENT` | 储君 |
| `CHARACTER.DEPRIVED` | 剥夺者 |
| `CHARACTER.RANDOM_CHARACTER` | 随机角色 |

#### EncounterStat — 遭遇战统计

```json
{
  "id": "ENCOUNTER.SENTINELS",
  "fight_stats": [
    { "character": "CHARACTER.IRONCLAD", "wins": 5, "losses": 2 },
    { "character": "CHARACTER.SILENT", "wins": 3, "losses": 1 }
  ]
}
```

#### EnemyStat — 敌人统计

```json
{
  "id": "MONSTER.SENTINEL",
  "fight_stats": [ ... ]
}
```

#### AncientStat — 古神统计

```json
{
  "ancient_id": "EVENT.NEOW",
  "character_stats": [
    { "character": "CHARACTER.NECROBINDER", "wins": 15, "losses": 37 }
  ]
}
```

#### EpochStat — 时代/赛季统计

```json
{
  "id": "NEOW_EPOCH",
  "state": "revealed",
  "obtain_date": 1773059058
}
```

| 状态值 | 说明 |
|--------|------|
| `not_obtained` | 未获得 |
| `revealed` | 已揭示（可见但未激活） |
| `obtained` | 已获得并激活 |

#### UnlockedAchievement — 已解锁成就

```json
[
  { "achievement": "WIN_1", "unlock_time": 1773059058 },
  { "achievement": "ASCEND_0_IRONCLAD", "unlock_time": 1773059059 }
]
```

**常见成就ID：**

| 成就ID | 说明 |
|--------|------|
| `WIN_1` | 首次胜利 |
| `WIN_5` | 胜利5次 |
| `WIN_10` | 胜利10次 |
| `ASCEND_0_*` | 通关基础难度（无难度） |
| `ASCEND_20_*` | 通关最高难度（Ascension 20） |
| `FULL_DECK_*` | 用完整牌组通关 |
| `PERFECT_*` | 完美通关（不受伤） |

---

### 2.5 current_run.save — 当前跑图存档（单人）

**约 200-400KB**，记录当前正在进行中的对局所有数据。

#### 顶层结构

```json
{
  "schema_version": 14,
  "is_multiplayer": false,
  "players": [ ... ],                  // 单人只有一个玩家
  "acts": [ ... ],
  "current_act_index": 0,
  "current_map_room": { ... },
  "floor": 8,
  "rng": { ... },
  "shared_relic_grab_bag": { ... },
  "epoch": "NEOW_EPOCH",
  "visited_map_coords": [ ... ],
  "events_seen": [ ... ],
  "start_time": 1773948181,
  "run_time": 5,
  "save_time": 1773948186,
  "map_drawings": { ... },
  "modifiers": [ ... ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `schema_version` | int | 跑图存档版本号（固定为14） |
| `is_multiplayer` | bool | 是否多人模式 |
| `players` | Player[] | 玩家列表（单人=1人） |
| `acts` | ActModel[] | 各章节数据 |
| `current_act_index` | int | 当前章节索引（0=第一章） |
| `current_map_room` | MapRoom | 当前所在房间 |
| `floor` | int | 当前层数 |
| `rng` | RunRngSet | 全局随机数生成器状态 |
| `shared_relic_grab_bag` | GrabBag | 共享遗物获取袋 |
| `epoch` | string | 当前时代ID |
| `visited_map_coords` | MapCoord[] | 已访问的地图坐标 |
| `events_seen` | string[] | 已触发过的事件ID |
| `start_time` | long | 开始时间戳（秒） |
| `run_time` | long | 累计游戏时长（秒） |
| `save_time` | long | 本次保存时间戳 |
| `map_drawings` | MapDrawings | 地图绘制（手绘/涂鸦） |
| `modifiers` | Modifier[] | 激活的修改器 |

#### Player — 玩家数据

```json
{
  "player_id": "76561198679823594",
  "character": "CHARACTER.IRONCLAD",
  "display_data": {
    "character_name": "铁甲战士",
    "player_name": "Player1",
    "steam_name": "煎包"
  },
  "is_local": true,
  "is_ready": true,
  "gold": 76,
  "max_hp": 80,
  "current_hp": 76,
  "max_energy": 3,
  "base_orb_slot_count": 0,
  "max_potion_slot_count": 3,
  "net_id": 1,
  "steam_id": "76561198679823594",
  "deck": {
    "cards": [ ... ]
  },
  "relics": [ ... ],
  "potions": [ ... ],
  "rng": { ... },
  "odds": { ... },
  "relic_grab_bag": { ... },
  "extra_fields": { ... },
  "unlock_state": { ... },
  "discovered_cards": [ ... ],
  "discovered_enemies": [ ... ],
  "discovered_epochs": [ ... ],
  "discovered_potions": [ ... ],
  "discovered_relics": [ ... ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `player_id` | string | 玩家ID（Steam64位ID或生成ID） |
| `character` | string | 角色ID |
| `display_data.character_name` | string | 角色名（本地化） |
| `display_data.player_name` | string | 玩家显示名 |
| `display_data.steam_name` | string | Steam昵称 |
| `is_local` | bool | 是否本地玩家 |
| `is_ready` | bool | 是否已准备（多人） |
| `gold` | int | 当前金币 |
| `max_hp` | int | 最大生命值 |
| `current_hp` | int | 当前生命值 |
| `max_energy` | int | 最大能量 |
| `base_orb_slot_count` | int | 基础充能球槽数（Defect专属） |
| `max_potion_slot_count` | int | 药水槽数量 |
| `net_id` | ulong | 网络ID（多人同步用） |
| `steam_id` | string | Steam 64位ID |
| `deck` | Deck | 牌组 |
| `relics` | Relic[] | 持有遗物 |
| `potions` | Potion[] | 持有药水 |
| `rng` | PlayerRngSet | 玩家随机数状态 |
| `odds` | PlayerOddsSet | 玩家概率状态 |
| `relic_grab_bag` | GrabBag | 遗物获取袋（Neow祝福等） |
| `extra_fields` | ExtraPlayerFields | 额外字段 |
| `unlock_state` | UnlockState | 解锁状态 |
| `discovered_*` | string[] | 各类已发现物 |

#### Deck — 牌组

```json
{
  "cards": [
    {
      "id": "CARD.STRIKE_IRONCLAD",
      "current_upgrade_level": 0,
      "floor_added_to_deck": 0,
      "enchantment": null,
      "props": {
        "ints": [
          { "name": "times_played", "value": 3 }
        ]
      }
    }
  ]
}
```

#### Relic — 持有遗物

```json
{
  "id": "RELIC.VAJRA",
  "floor_added_to_deck": 0,
  "props": {
    "ints": [
      { "name": "counter", "value": 0 }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 遗物ID |
| `floor_added_to_deck` | int | 获得该遗物的层数 |
| `props.ints[].name` | - | 属性名（如 `counter`=使用次数） |
| `props.ints[].value` | int | 属性值 |

**常见遗物属性：**

| 属性名 | 适用遗物 | 说明 |
|--------|----------|------|
| `counter` | 大多数有计数的遗物 | 使用次数/充能 |
| `turns` | 临时增益遗物 | 剩余回合 |
| `damage_dealt` | 记录伤害的遗物 | 累计伤害 |

#### Potion — 药水

```json
{
  "id": "POTION.ENTROPIC_BREW",
  "room_id": 0
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 药水ID |
| `room_id` | int | 获得的房间ID |

#### ActModel — 章节数据

```json
{
  "id": "ACT.OVERGROWTH",
  "saved_map": {
    "nodes": [
      {
        "coord": { "x": 0, "y": 0 },
        "type": "event",
        "connected_to": [{ "x": 1, "y": 0 }]
      }
    ]
  },
  "rooms": {
    "boss_id": "ENCOUNTER.VANTOM_BOSS",
    "second_boss_id": null,
    "normal_encounter_ids": [
      "ENCOUNTER.SENTINELS",
      "ENCOUNTER.LOTS_OF_SLIMES"
    ],
    "elite_encounter_ids": [
      "ENCOUNTER.GREMLIN_NOB"
    ],
    "event_ids": [
      "EVENT.RELIC_TRADER",
      "EVENT.BUGSLAYER"
    ],
    "boss_encounters_visited": 1,
    "boss_id_visited": false,
    "elite_encounters_visited": 2,
    "events_visited": 3,
    "normal_encounters_visited": 4,
    "ancient_id": "EVENT.NEOW"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 章节ID |
| `saved_map.nodes` | Node[] | 地图节点 |
| `rooms.boss_id` | string | Boss遭遇ID |
| `rooms.second_boss_id` | string | 第二Boss（如有） |
| `rooms.normal_encounter_ids` | string[] | 普通遭遇ID列表 |
| `rooms.elite_encounter_ids` | string[] | 精英遭遇ID列表 |
| `rooms.event_ids` | string[] | 事件ID列表 |
| `rooms.ancient_id` | string | 古神事件ID |

#### MapRoom — 当前地图房间

```json
{
  "map_coord": { "x": 1, "y": 0 },
  "room_type": "monster",
  "encounter_id": "ENCOUNTER.SENTINELS",
  "event_id": null,
  "relic_encounter_id": null,
  "is_finished": false,
  "rewards_pending": false,
  "options_chosen": 0,
  "event": null
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `map_coord` | {x,y} | 坐标 |
| `room_type` | string | 房间类型 |
| `encounter_id` | string | 遭遇ID（战斗房） |
| `event_id` | string | 事件ID（事件房） |
| `relic_encounter_id` | string | 遗物遭遇ID |
| `is_finished` | bool | 是否已完成 |
| `rewards_pending` | bool | 奖励是否待领取 |
| `options_chosen` | int | 已选择的选项数 |

**RoomType 房间类型：**

| 值 | 说明 |
|----|------|
| `monster` | 普通战斗 |
| `elite` | 精英战斗 |
| `boss` | Boss战斗 |
| `rest` | 休息点 |
| `shop` | 商店 |
| `treasure` | 宝箱 |
| `event` | 事件 |
| `unknown` | 未知 |

#### GrabBag — 遗物获取袋

```json
{
  "ids": [
    "RELIC.BURNING_BLOOD",
    "RELIC.CRACKED_CORE",
    "RELIC.RING_OF_SERPENTS"
  ],
  "taken": false,
  "taken_id": null
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `ids` | string[] | 袋中遗物ID列表 |
| `taken` | bool | 是否已抽取 |
| `taken_id` | string | 已抽取的遗物ID |

#### ExtraPlayerFields — 玩家额外字段

```json
{
  "ascension": 0,
  "neow_bonus": "NONE.NONE",
  "boss_relics": { ... },
  "colorless_cards": { ... },
  "starter_cards": { ... }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `ascension` | int | 当前难度等级 |
| `neow_bonus` | string | Neow祝福ID |
| `boss_relics` | GrabBag | Boss遗物袋 |
| `colorless_cards` | GrabBag | 无色卡牌袋 |
| `starter_cards` | GrabBag | 初始卡牌袋 |

#### UnlockState — 解锁状态

```json
{
  "card_unlock_ids": ["CARD.APOTHEOSIS"],
  "relic_unlock_ids": ["RELIC.PUNCHOUT"],
  "potion_unlock_ids": ["POTION.GROWTH_POTION"]
}
```

---

### 2.6 current_run_mp.save — 多人跑图存档

与 `current_run.save` 结构相同，**关键区别**：

```json
{
  "schema_version": 14,
  "is_multiplayer": true,
  "lobby_id": "LOBBY_ABC123",
  "mp_game_mode": "standard",
  "players": [
    {
      "player_id": "76561198679823594",
      "character": "CHARACTER.IRONCLAD",
      "is_local": true,
      "is_ready": true,
      "steam_id": "76561198679823594",
      ...
    },
    {
      "player_id": "76561198765432109",
      "character": "CHARACTER.SILENT",
      "is_local": false,
      "is_ready": true,
      "steam_id": "76561198765432109",
      ...
    }
  ],
  ...
}
```

| 额外字段 | 类型 | 说明 |
|----------|------|------|
| `is_multiplayer` | bool | 固定为 `true` |
| `lobby_id` | string | Steam Lobby ID |
| `mp_game_mode` | string | 多人游戏模式（standard/casual等） |
| `players[].is_local` | bool | 是否本地玩家 |
| `players[].is_ready` | bool | 是否已准备 |
| `players[].steam_id` | string | Steam 64位ID |

---

## 三、存档修改工具设计要点

### 3.1 关键原则

1. **JSON格式可直接编辑** — 所有 `.save` 文件都是标准JSON
2. **保持Schema Version** — 修改时不要改变 `schema_version` 值
3. **保留文件结构** — 字段类型必须匹配
4. **多人存档修改** — 修改 `current_run_mp.save` 时注意多玩家数据同步

### 3.2 常用修改场景

| 场景 | 修改文件 | 修改位置 |
|------|----------|----------|
| 解锁所有卡牌 | `progress.save` | `discovered_cards` 添加所有卡牌ID |
| 全角色全胜 | `progress.save` | `character_stats` 所有角色 `total_wins` |
| 改金币 | `current_run*.save` | `players[].gold` |
| 改血量 | `current_run*.save` | `players[].current_hp` / `max_hp` |
| 改牌组 | `current_run*.save` | `players[].deck.cards` |
| 改遗物 | `current_run*.save` | `players[].relics` |
| 改药水 | `current_run*.save` | `players[].potions` |
| 改层数 | `current_run*.save` | `floor` |
| 改能量 | `current_run*.save` | `players[].max_energy` |
| 解锁成就 | `progress.save` | `unlocked_achievements` |
| 清空跑图（重开） | `current_run*.save` | 删除或置空 |
| 移除断线玩家 | `current_run_mp.save` | 从 `players[]` 删除对应条目 |

### 3.3 多人存档特殊注意

修改 `current_run_mp.save` 时：
- 每个玩家在 `players[]` 数组中有一条独立记录
- 移除玩家时需同时清理：
  - 该玩家的 `deck.cards`
  - 该玩家的 `relics`
  - 该玩家的 `potions`
  - `shared_relic_grab_bag` 中该玩家的引用（如有）
  - `visited_map_coords` 中该玩家的历史（如有）
- **建议保留最后一个玩家**作为单人继续游戏

### 3.4 存档备份机制

游戏在以下时机自动备份：
- 进入Mod模式前：`modded_p{profile}_auto_before_copy_*`
- 进入普通模式前：`normal_p{profile}_auto_before_copy_*`
- 手动存档：按日期时间命名

---

## 四、参考：源码对应关系

| 存档类 | C# 类名 | 源码文件 |
|--------|---------|----------|
| `profile.save` | `ProfileSave` | `MegaCrit.Sts2.Core.Saves` |
| `settings.save` | `SettingsSave` | `MegaCrit.Sts2.Core.Saves` |
| `prefs.save` | `PrefsSave` | `MegaCrit.Sts2.Core.Saves` |
| `progress.save` | `SerializableProgress` | `MegaCrit.Sts2.Core.Saves` |
| `current_run.save` | `SerializableRun` | `MegaCrit.Sts2.Core.Saves.Runs` |
| `current_run_mp.save` | `SerializableRun` (is_multiplayer=true) | `MegaCrit.Sts2.Core.Saves.Runs` |

---

*文档基于 v0.99 反编译源码 + 实际存档文件分析整理。*
