# 历史记录 vs 存档 — 详细程度对比分析

> 本文档基于源码分析，补充说明游戏存档 (SerializableRun) 与历史记录 (RunHistory) 的数据结构差异，为作弊检测提供更精确的参考。

---

## 一、基本概念区分

| 维度 | 存档 `SerializableRun` | 历史记录 `RunHistory` |
|---|---|---|
| **用途** | 中途保存/恢复跑图 | 跑图结束后永久记录 |
| **生成时机** | 任意时刻可保存（手动/自动） | 跑图结束时一次性生成 |
| **文件位置** | `{profileDir}/saves/run/` | `{profileDir}/saves/history/` |
| **序列化格式** | `PacketWriter` 二进制流（`IPacketSerializable`） | `System.Text.Json` JSON |
| **数量** | 同一时刻只有 1 个 | 无限累积（每个跑图一个） |
| **可读性** | 需反序列化工具 | 可直接用文本编辑器打开 |

---

## 二、SerializableRun 完整属性列表

`SerializableRun` 是游戏存档的核心类，位于命名空间 `MegaCrit.Sts2.Core.Saves`。

| JSON 字段名 | 类型 | 说明 |
|---|---|---|
| `schema_version` | `int` | Schema 版本号 |
| `acts` | `List<SerializableActModel>` | 所有章节的完整数据 |
| `modifiers` | `List<SerializableModifier>` | Modifiers |
| `dailyTime` | `DateTimeOffset?` | 每日挑战时间（用于判断是否为 Daily 模式） |
| `current_act_index` | `int` | 当前章节索引 |
| `events_seen` | `List<ModelId>` | 已触发的事件 ID 列表 |
| `pre_finished_room` | `SerializableRoom?` | 预完成的房间 |
| `odds` | `SerializableRunOddsSet` | Run 级别的赔率设置 |
| `shared_relic_grab_bag` | `SerializableRelicGrabBag` | 多人模式共享遗物袋 |
| `players` | `List<SerializablePlayer>` | **所有玩家的完整数据** |
| `rng` | `SerializableRunRngSet` | **Run 级别的随机数生成器状态** |
| `visited_map_coords` | `List<MapCoord>` | 已访问的地图坐标 |
| `map_point_history` | `List<List<MapPointHistoryEntry>>` | 地图节点历史 |
| `save_time` | `long` | 保存时间戳 |
| `start_time` | `long` | 开始时间戳 |
| `run_time` | `long` | 总时长 |
| `win_time` | `long` | 胜利时刻时间戳 |
| `ascension` | `int` | 难度等级 |
| `platform_type` | `PlatformType` | 平台类型 |
| `map_drawings` | `SerializableMapDrawings` | 地图绘制数据 |
| `extra_fields` | `SerializableExtraRunFields` | 扩展字段 |

---

## 三、SerializablePlayer 完整属性列表

`SerializablePlayer` 是 `SerializableRun.Players` 中的每个玩家的数据结构，位于命名空间 `MegaCrit.Sts2.Core.Saves.Runs`。

| JSON 字段名 | 类型 | 说明 |
|---|---|---|
| `net_id` | `ulong` | 玩家网络 ID（Steam ID） |
| `character_id` | `ModelId` | 角色 ID |
| `current_hp` | `int` | **当前生命值（快照）** |
| `max_hp` | `int` | **最大生命值（快照）** |
| `max_energy` | `int` | 最大能量 |
| `max_potion_slot_count` | `int` | 药水栏位数（默认 3） |
| `gold` | `int` | **当前金币（快照）** |
| `base_orb_slot_count` | `int` | 充能球栏位基础数量 |
| `deck` | `List<SerializableCard>` | 牌组 |
| `relics` | `List<SerializableRelic>` | 遗物列表 |
| `potions` | `List<SerializablePotion>` | 药水列表 |
| `rng` | `SerializablePlayerRngSet` | **玩家级别的随机数状态** |
| `odds` | `SerializablePlayerOddsSet` | 玩家赔率设置 |
| `relic_grab_bag` | `SerializableRelicGrabBag` | 遗物获取袋 |
| `extra_fields` | `SerializableExtraPlayerFields` | 扩展字段 |
| `unlock_state` | `SerializableUnlockState` | 解锁状态 |
| `discovered_cards` | `List<ModelId>` | **已发现的卡牌列表** |
| `discovered_enemies` | `List<ModelId>` | **已发现的敌人列表** |
| `discovered_epochs` | `List<string>` | **已发现的 Epoch** |
| `discovered_potions` | `List<ModelId>` | **已发现的药水列表** |
| `discovered_relics` | `List<ModelId>` | **已发现的遗物列表** |

---

## 四、SerializableActModel — 章节存档

`SerializableActModel` 位于 `SerializableRun.Acts` 中：

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | `ModelId` | Act 的 ModelId |
| `SerializableRooms` | `SerializableRoomSet` | 该章节的房间集合 |
| `SavedMap` | `SerializableActMap?` | 存档地图 |

`SerializableRoomSet` 包含：

| 字段 | 类型 | 说明 |
|---|---|---|
| `EventIds` | `List<ModelId>` | 所有事件 ID |
| `EventsVisited` | `int` | 已访问事件数 |
| `NormalEncounterIds` | `List<ModelId>` | 普通遭遇 ID |
| `NormalEncountersVisited` | `int` | 已访问普通遭遇数 |
| `EliteEncounterIds` | `List<ModelId>` | 精英遭遇 ID |
| `EliteEncountersVisited` | `int` | 已访问精英遭遇数 |
| `BossEncountersVisited` | `int` | 已访问 Boss 遭遇数 |
| `BossId` | `ModelId?` | Boss ID |
| `SecondBossId` | `ModelId?` | 第二个 Boss ID（多人模式） |
| `AncientId` | `ModelId?` | 古代祭坛 ID |

---

## 五、SerializableRoom — 房间存档

`SerializableRoom` 是每个房间的详细存档：

| 字段 | 类型 | 说明 |
|---|---|---|
| `RoomType` | `RoomType` | 房间类型 |
| `EncounterId` | `ModelId` | 遭遇 ID（战斗/精英/Boss） |
| `EventId` | `ModelId` | 事件 ID |
| `IsPreFinished` | `bool` | 是否已完成 |
| `GoldProportion` | `float` | 奖励金币比例 |
| `ExtraRewards` | `Dictionary<ulong, List<SerializableReward>>` | 额外奖励（多人模式按玩家） |
| `ParentEventId` | `ModelId` | 父事件 ID |
| `ShouldResumeParentEvent` | `bool` | 是否应恢复父事件 |
| `EncounterState` | `Dictionary<string, string>` | **遭遇状态（关键！）** |

---

## 六、转换函数：存档 → 历史记录

`RunHistoryUtilities.CreateRunHistoryEntry()` 是将 `SerializableRun` 转换为 `RunHistory` 的核心函数：

```csharp
// 来自 RunHistoryUtilities.cs
public static void CreateRunHistoryEntry(SerializableRun run, bool victory, bool isAbandoned, PlatformType platformType)
{
    // 1. 提取击杀信息（最后一节点判断）
    if (!victory && lastMapPoint != null)
    {
        if (lastRoomType.IsCombatRoom())
            killedByEncounter = lastMapPoint.Rooms.First().ModelId;
        else if (lastRoomType == RoomType.Event)
            killedByEvent = lastMapPoint.Rooms.First().ModelId;
    }

    // 2. 转换玩家数据（只提取这6个字段）
    foreach (SerializablePlayer sp in run.Players)
    {
        RunHistoryPlayer rhp = new RunHistoryPlayer
        {
            Id          = sp.NetId,           // ← steam ID
            Character   = sp.CharacterId,     // ← 角色
            Deck        = sp.Deck,            // ← 最终卡组
            Relics      = sp.Relics,          // ← 最终遗物
            Potions     = sp.Potions,         // ← 最终药水
            MaxPotionSlotCount = sp.MaxPotionSlotCount
        };
    }

    // 3. 提取 seed
    runHistory.Seed = run.SerializableRng.Seed;

    // 4. 提取时间
    runHistory.RunTime = (run.WinTime > 0) ? run.WinTime : run.RunTime;

    // 5. 直接引用 map_point_history（不做转换）
    runHistory.MapPointHistory = run.MapPointHistory;

    // 6. 章节只保存 ModelId，不保存完整 SerializableActModel
    runHistory.Acts = run.Acts.Select(a => a.Id).ToList<ModelId>();

    // 7. 游戏模式通过 DailyTime 判断
    runHistory.GameMode = (run.DailyTime != null) ? GameMode.Daily : GameMode.Standard;
}
```

---

## 七、字段对比总表

### 7.1 顶层字段

| 字段 | SerializableRun | RunHistory | 差异说明 |
|---|---|---|---|
| `schema_version` | ✅ | ✅ | 名称相同 |
| `platform_type` | ✅ | ✅ | 名称相同 |
| `game_mode` | ❌（通过 `DailyTime` 推断） | ✅ | 历史记录有独立字段 |
| `win` | ❌（通过 `win_time > 0` 推断） | ✅ | 历史记录有独立字段 |
| `was_abandoned` | ❌ | ✅ | 存档需通过逻辑推断 |
| `seed` | ❌（在 `SerializableRng.Seed`） | ✅ | 历史记录直接暴露 |
| `start_time` | ✅ | ✅ | 名称相同 |
| `run_time` | ✅ | ✅ | 名称相同 |
| `win_time` | ✅ | ❌ | 存档有，历史记录只有 `run_time` |
| `save_time` | ✅ | ❌ | 存档有，历史记录无保存时间 |
| `ascension` | ✅ | ✅ | 名称相同 |
| `build_id` | ❌（在 `ReleaseInfo.Version`） | ✅ | 历史记录从 ReleaseInfo 提取 |
| `killed_by_encounter` | ❌ | ✅ | 历史记录从最后一个 MapPoint 推断 |
| `killed_by_event` | ❌ | ✅ | 同上 |
| `players` | ✅（完整 SerializablePlayer） | ✅（简化版 RunHistoryPlayer） | 见 7.2 |
| `acts` | ✅（完整 SerializableActModel） | ✅（只存 ModelId） | 存档更详细 |
| `map_point_history` | ✅（完整版） | ✅（引用同一对象） | 存档版本更详细 |
| `modifiers` | ✅ | ✅ | 名称相同 |
| `rng` | ✅（完整状态） | ❌（只存 seed） | 存档有完整 RNG 状态 |
| `visited_map_coords` | ✅ | ❌ | 存档独有 |
| `map_drawings` | ✅ | ❌ | 存档独有（地图绘制） |
| `extra_fields` | ✅ | ❌ | 存档独有 |
| `dailyTime` | ✅ | ❌ | 存档独有（用于判断 Daily 模式） |

### 7.2 玩家数据字段

| 字段 | SerializablePlayer | RunHistoryPlayer | 差异说明 |
|---|---|---|---|
| 玩家标识 | `NetId` | `Id` | 同为 Steam ID |
| 角色 | `CharacterId` | `Character` | 同为 ModelId |
| **当前 HP** | ✅ `CurrentHp` | ❌ | 存档有快照，历史记录无 |
| **最大 HP** | ✅ `MaxHp` | ❌ | 存档有快照，历史记录无 |
| **当前金币** | ✅ `Gold` | ❌ | 存档有快照，历史记录无 |
| 最大能量 | ✅ `MaxEnergy` | ❌ | 存档独有 |
| 充能球栏位 | ✅ `BaseOrbSlotCount` | ❌ | 存档独有 |
| 最终卡组 | ✅ `Deck` | ✅ `Deck` | 同为快照 |
| 最终遗物 | ✅ `Relics` | ✅ `Relics` | 同为快照 |
| 最终药水 | ✅ `Potions` | ✅ `Potions` | 同为快照 |
| 药水栏位 | ✅ `MaxPotionSlotCount` | ✅ `MaxPotionSlotCount` | 同名同义 |
| 随机数状态 | ✅ `Rng` | ❌ | 存档有完整状态 |
| 赔率设置 | ✅ `Odds` | ❌ | 存档独有 |
| 遗物获取袋 | ✅ `RelicGrabBag` | ❌ | 存档独有 |
| 扩展字段 | ✅ `ExtraFields` | ❌ | 存档独有 |
| 解锁状态 | ✅ `UnlockState` | ❌ | 存档独有 |
| 已发现卡牌 | ✅ `DiscoveredCards` | ❌ | 存档独有 |
| 已发现敌人 | ✅ `DiscoveredEnemies` | ❌ | 存档独有 |
| 已发现 Epoch | ✅ `DiscoveredEpochs` | ❌ | 存档独有 |
| 已发现药水 | ✅ `DiscoveredPotions` | ❌ | 存档独有 |
| 已发现遗物 | ✅ `DiscoveredRelics` | ❌ | 存档独有 |

### 7.3 玩家统计字段（PlayerStats）

| 字段 | SerializableRun 中的 MapPointHistoryEntry | RunHistory 中的 PlayerMapPointHistoryEntry | 差异说明 |
|---|---|---|---|
| `CurrentGold` | ❌（在 `SerializablePlayer.Gold`） | ✅ | 历史记录每个节点快照 |
| `GoldGained` | ❌ | ✅ | 历史记录独有（累加） |
| `GoldSpent` | ❌ | ✅ | 历史记录独有 |
| `GoldLost` | ❌ | ✅ | 历史记录独有 |
| `GoldStolen` | ❌ | ✅ | 历史记录独有 |
| `CurrentHp` | ❌（在 `SerializablePlayer.CurrentHp`） | ✅ | 历史记录每个节点快照 |
| `MaxHp` | ❌（在 `SerializablePlayer.MaxHp`） | ✅ | 历史记录每个节点快照 |
| `DamageTaken` | ❌ | ✅ | 历史记录独有 |
| `HpHealed` | ❌ | ✅ | 历史记录独有 |
| `MaxHpGained` | ❌ | ✅ | 历史记录独有 |
| `MaxHpLost` | ❌ | ✅ | 历史记录独有 |
| `CardsGained` | ❌ | ✅ | 历史记录独有 |
| `CardChoices` | ❌ | ✅ | 历史记录独有 |
| `CardsRemoved` | ❌ | ✅ | 历史记录独有 |
| `CardsEnchanted` | ❌ | ✅ | 历史记录独有 |
| `CardsTransformed` | ❌ | ✅ | 历史记录独有 |
| `UpgradedCards` | ❌ | ✅ | 历史记录独有 |
| `DowngradedCards` | ❌ | ✅ | 历史记录独有 |
| `RelicChoices` | ❌ | ✅ | 历史记录独有 |
| `RelicsRemoved` | ❌ | ✅ | 历史记录独有 |
| `BoughtRelics` | ❌ | ✅ | 历史记录独有 |
| `PotionChoices` | ❌ | ✅ | 历史记录独有 |
| `PotionUsed` | ❌ | ✅ | 历史记录独有 |
| `PotionDiscarded` | ❌ | ✅ | 历史记录独有 |
| `BoughtPotions` | ❌ | ✅ | 历史记录独有 |
| `BoughtColorless` | ❌ | ✅ | 历史记录独有 |
| `AncientChoices` | ❌ | ✅ | 历史记录独有 |
| `EventChoices` | ❌ | ✅ | 历史记录独有 |
| `RestSiteChoices` | ❌ | ✅ | 历史记录独有 |
| `CompletedQuests` | ❌ | ✅ | 历史记录独有 |
| **EncounterState** | ✅ | ❌ | 存档独有 |

---

## 八、核心结论

### SerializableRun 像"完整的 Save 文件"
- **存结果**：当前 HP、金币、最大 HP、能量、充能球
- **存过程**：通过 `MapPointHistory` 记录节点历史，但节点内的玩家统计远不如历史记录详细
- **存完整 RNG**：可以精确恢复任何时刻的游戏状态
- **存发现列表**：已见过哪些卡牌/敌人/药水/遗物/Epoch
- **存遭遇状态**：`EncounterState` 保存战斗/事件的中间状态
- **存地图绘制**：`MapDrawings` 保存地图绘制信息
- **不可直接读取**：二进制格式，需工具解析

### RunHistory 像"跑完全程的故事"
- **只存最终快照**：卡组/遗物/药水的最终状态（不是每个节点的快照）
- **存完整流水账**：每个节点记录所有操作的累加值
- **存操作来源**：`CardChoices[wasPicked]` 等记录每张卡牌/遗物/药水的来源
- **只存 seed**：没有完整 RNG，无法恢复中途状态
- **没有发现列表**：历史记录不关心"你见过什么"
- **可直接读取**：JSON 格式，任何文本编辑器可打开

### 作弊检测意义

| 检测目标 | 最佳数据来源 | 理由 |
|---|---|---|
| 金币作弊 | **历史记录** | 历史记录有 `GoldGained/Spent/Lost/Stolen` 全流程 |
| 生命作弊 | **历史记录** | 历史记录有 `DamageTaken/HpHealed/MaxHpGained/MaxHpLost` |
| 卡牌来源 | **历史记录** | `CardChoices[wasPicked]` 精确记录每次选择 |
| 遗物来源 | **历史记录** | `RelicChoices[wasPicked]` 精确记录每次选择 |
| 遭遇状态篡改 | **存档** | `EncounterState` 只存在于存档中 |
| RNG 种子验证 | **历史记录** | seed 直接暴露在 JSON 中 |
| 发现列表作弊 | **存档** | `DiscoveredCards/Enemies/Potions/Relics` 只存在于存档 |
| 实时状态篡改 | **存档** | `CurrentHp/Gold/MaxHp` 只存在于存档快照 |

---

## 九、关键源码文件索引

| 文件 | 路径 |
|---|---|
| SerializableRun | `...\sts2\MegaCrit\sts2\Core\Saves\SerializableRun.cs` |
| SerializablePlayer | `...\sts2\MegaCrit\sts2\Core\Saves\Runs\SerializablePlayer.cs` |
| SerializableActModel | `...\sts2\MegaCrit\sts2\Core\Saves\Runs\SerializableActModel.cs` |
| SerializableRoom | `...\sts2\MegaCrit\sts2\Core\Saves\Runs\SerializableRoom.cs` |
| SerializableRoomSet | `...\sts2\MegaCrit\sts2\Core\Saves\Runs\SerializableRoomSet.cs` |
| RunHistoryUtilities | `...\sts2\MegaCrit\sts2\Core\Runs\RunHistoryUtilities.cs` |
| RunHistory | `...\sts2\MegaCrit\sts2\Core\Runs\RunHistory.cs` |
| PlayerMapPointHistoryEntry | `...\sts2\MegaCrit\sts2\Core\Runs\PlayerMapPointHistoryEntry.cs` |
| MapPointHistoryEntry | `...\sts2\MegaCrit\sts2\Core\Runs\History\MapPointHistoryEntry.cs` |
