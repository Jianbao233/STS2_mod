using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace RunHistoryAnalyzer.Models;

/// <summary>
/// 反序列化游戏 .run JSON 文件得到的根对象。
/// 字段名严格与游戏存档 JSON 对应，大小写与 json_tags 匹配。
/// </summary>
public class RunHistoryData
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("platform_type")]
    [JsonConverter(typeof(PlatformTypeJsonConverter))]
    public int PlatformType { get; set; }

    [JsonPropertyName("game_mode")]
    public string GameMode { get; set; } = "";

    [JsonPropertyName("win")]
    public bool Win { get; set; }

    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("start_time")]
    public long StartTime { get; set; }

    [JsonPropertyName("run_time")]
    public double RunTime { get; set; }

    [JsonPropertyName("ascension")]
    public int Ascension { get; set; }

    [JsonPropertyName("build_id")]
    public string BuildId { get; set; } = "";

    [JsonPropertyName("was_abandoned")]
    public bool WasAbandoned { get; set; }

    [JsonPropertyName("killed_by_encounter")]
    public string? KilledByEncounter { get; set; }

    [JsonPropertyName("killed_by_event")]
    public string? KilledByEvent { get; set; }

    [JsonPropertyName("players")]
    public List<RunHistoryPlayerData> Players { get; set; } = new();

    [JsonPropertyName("acts")]
    public List<string> Acts { get; set; } = new();

    [JsonPropertyName("map_point_history")]
    public List<List<MapPointHistoryEntry>> MapPointHistory { get; set; } = new();

    /// <summary>
    /// 非 JSON 字段：分析时指定的玩家 ID（0 = 分析所有玩家）。
    /// 各检测规则用此字段过滤到对应玩家。
    /// </summary>
    [JsonIgnore]
    public ulong AnalysisPlayerId { get; set; }

    public DateTime GetStartDateTime() => DateTimeOffset.FromUnixTimeSeconds(StartTime).DateTime;

    public string GetDifficulty()
    {
        if (Ascension == 0) return "普通";
        return $"{Ascension}A";
    }

    /// <summary>
    /// 返回 AnalysisPlayerId 对应的玩家（如果有的话），否则返回 Players[0]。
    /// </summary>
    public RunHistoryPlayerData GetTargetPlayer()
    {
        if (AnalysisPlayerId != 0)
        {
            foreach (var p in Players)
                if (p.Id == AnalysisPlayerId)
                    return p;
        }
        return Players.Count > 0 ? Players[0] : null!;
    }
}

/// <summary>
/// 单个玩家的历史记录数据（最终快照）。
/// </summary>
public class RunHistoryPlayerData
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("character")]
    public string Character { get; set; } = "";

    [JsonPropertyName("deck")]
    public List<SerializableCard> Deck { get; set; } = new();

    [JsonPropertyName("relics")]
    public List<SerializableRelic> Relics { get; set; } = new();

    [JsonPropertyName("potions")]
    public List<SerializablePotion> Potions { get; set; } = new();

    [JsonPropertyName("max_potion_slot_count")]
    public int MaxPotionSlotCount { get; set; }

    /// <summary>该角色初始金币（铁甲=99等），用于守恒定律起点。</summary>
    public static int GetStartingGold(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => 99,
        "CHARACTER.SILENT" => 99,
        "CHARACTER.DEFECT" => 99,
        "CHARACTER.NECROMANCER" => 99,
        "CHARACTER.NECROBINDER" => 99,
        "CHARACTER.HEXAGUARD" or "CHARACTER.REGENT" => 99,
        "MOD.WATCHER" => 99,
        _ => 99
    };

    /// <summary>该角色初始最大HP。</summary>
    public static int GetStartingMaxHp(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => 80,
        "CHARACTER.SILENT" => 70,
        "CHARACTER.DEFECT" => 75,
        "CHARACTER.NECROMANCER" => 66,
        "CHARACTER.NECROBINDER" => 66,
        "CHARACTER.HEXAGUARD" or "CHARACTER.REGENT" => 75,
        "MOD.WATCHER" => 72,
        _ => 75
    };

    /// <summary>
    /// 初始牌组短 id 列表（与游戏 StartingDeck 一致，含重复张数；卡牌追溯须用多重集合而非 HashSet）。
    /// id 不含 CARD. 前缀。
    /// </summary>
    public static IReadOnlyList<string> GetStarterCardShortIds(string characterId) => characterId switch
    {
        // 与 MegaCrit.Sts2 反编译 Ironclad.StartingDeck 一致：5 打击 + 4 防御 + 痛击
        "CHARACTER.IRONCLAD" => Repeat("STRIKE_IRONCLAD", 5)
            .Concat(Repeat("DEFEND_IRONCLAD", 4))
            .Append("BASH")
            .ToList(),
        // Silent：5+5 + 中和 + 生存者
        "CHARACTER.SILENT" => Repeat("STRIKE_SILENT", 5)
            .Concat(Repeat("DEFEND_SILENT", 5))
            .Append("NEUTRALIZE")
            .Append("SURVIVOR")
            .ToList(),
        // Defect：4+4 + 闪电 + 双发
        "CHARACTER.DEFECT" => Repeat("STRIKE_DEFECT", 4)
            .Concat(Repeat("DEFEND_DEFECT", 4))
            .Append("ZAP")
            .Append("DUALCAST")
            .ToList(),
        "CHARACTER.NECROBINDER" or "CHARACTER.NECROMANCER" => Repeat("STRIKE_NECROBINDER", 4)
            .Concat(Repeat("DEFEND_NECROBINDER", 4))
            .Append("BODYGUARD")
            .Append("UNLEASH")
            .ToList(),
        // 储君：存档多为 CHARACTER.REGENT；部分数据可能为 CHARACTER.HEXAGUARD
        "CHARACTER.HEXAGUARD" or "CHARACTER.REGENT" => Repeat("STRIKE_REGENT", 4)
            .Concat(Repeat("DEFEND_REGENT", 4))
            .Append("FALLING_STAR")
            .Append("VENERATE")
            .ToList(),
        "MOD.WATCHER" => Repeat("STRIKE_P", 5)
            .Concat(Repeat("DEFEND_P", 5))
            .ToList(),
        _ => Repeat("STRIKE_P", 5)
            .Concat(Repeat("DEFEND_P", 5))
            .ToList()
    };

    private static IEnumerable<string> Repeat(string id, int count)
    {
        for (var i = 0; i < count; i++)
            yield return id;
    }
}

    /// <summary>古遗物祭坛选择条目（与游戏 AncientChoiceHistoryEntry 一致）。</summary>
    public class AncientChoiceHistoryEntry
    {
        /// <summary>遗物短名（如 "NEW_LEAF"，无 RELIC. 前缀）。存档字段为 PascalCase <c>TextKey</c>。</summary>
        [JsonPropertyName("TextKey")]
        public string TextKey { get; set; } = "";

        [JsonPropertyName("title")]
        public LocString Title { get; set; } = new();

        [JsonPropertyName("was_chosen")]
        public bool WasChosen { get; set; }
    }

    /// <summary>事件选项历史（遗物/药水等事件奖励，与 AncientChoiceHistoryEntry 结构相同）。</summary>
    public class EventOptionHistoryEntry
    {
        [JsonPropertyName("TextKey")]
        public string TextKey { get; set; } = "";

        [JsonPropertyName("title")]
        public LocString Title { get; set; } = new();

        [JsonPropertyName("was_chosen")]
        public bool WasChosen { get; set; }
    }

    /// <summary>本地化字符串（与游戏 LocString 一致）。</summary>
    public class LocString
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("table")]
        public string Table { get; set; } = "";
    }

    /// <summary>卡牌附魔历史条目（与游戏 CardEnchantmentHistoryEntry：card + enchantment）。</summary>
    public class CardEnchantmentHistoryEntry
    {
        [JsonPropertyName("card")]
        public SerializableCard Card { get; set; } = new();

        /// <summary>附魔 ModelId（JSON 可能为字符串或对象）。</summary>
        [JsonPropertyName("enchantment")]
        public object? Enchantment { get; set; }
    }

    public class SerializableCard
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("current_upgrade_level")]
        public int? CurrentUpgradeLevel { get; set; }

        /// <summary>附魔数据（仅部分存档字段存在）。</summary>
        [JsonPropertyName("enchantment")]
        public object? Enchantment { get; set; }

        /// <summary>该卡牌加入牌组的层数（仅在最终快照中出现）。</summary>
        [JsonPropertyName("floor_added_to_deck")]
        public int? FloorAddedToDeck { get; set; }
    }

public class SerializableRelic
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>附加属性（部分遗物特有，如 WHETSTONE 的 CardsAdded）。</summary>
    [JsonPropertyName("props")]
    public SavedProperties? Props { get; set; }

    /// <summary>该遗物加入的层数（仅在最终快照中出现）。</summary>
    [JsonPropertyName("floor_added_to_deck")]
    public int? FloorAddedToDeck { get; set; }
}

/// <summary>
/// 遗物附加属性（部分遗物以 Key-Value 对记录额外数据）。
/// </summary>
public class SavedProperties
{
    [JsonPropertyName("ints")]
    public List<SavedPropertyInt>? Ints { get; set; }
}

public class SavedPropertyInt
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

public class SerializablePotion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>药水槽索引（仅在最终快照中出现）。</summary>
    [JsonPropertyName("slot_index")]
    public int? SlotIndex { get; set; }
}

/// <summary>
/// 地图节点历史条目（每个节点对应一层地图上的一个坐标）。
/// </summary>
public class MapPointHistoryEntry
{
    [JsonPropertyName("map_point_type")]
    public string MapPointType { get; set; } = "";

    /// <summary>该地图节点内的房间流水（含 model_id，如 EVENT.FAKE_MERCHANT、遭遇战 id）。</summary>
    [JsonPropertyName("rooms")]
    public List<MapPointRoomHistoryEntry> Rooms { get; set; } = new();

    [JsonPropertyName("player_stats")]
    public List<PlayerMapPointHistoryEntry> PlayerStats { get; set; } = new();
}

/// <summary>与游戏 <c>MapPointRoomHistoryEntry</c> 一致的单房间摘要。</summary>
public class MapPointRoomHistoryEntry
{
    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    [JsonPropertyName("room_type")]
    public string? RoomType { get; set; }

    [JsonPropertyName("monster_ids")]
    public List<string>? MonsterIds { get; set; }
}

/// <summary>
/// 某玩家在某个节点上的统计（GoldGained/Spent/Hp/卡牌/遗物等流水账）。
/// 字段类型与游戏 <c>PlayerMapPointHistoryEntry</c> 一致（如 upgraded_cards 为 ModelId 字符串列表，不是 SerializableCard）。
/// </summary>
public class PlayerMapPointHistoryEntry
{
    [JsonPropertyName("player_id")]
    public ulong PlayerId { get; set; }

    // 金币
    [JsonPropertyName("current_gold")]
    public int CurrentGold { get; set; }

    [JsonPropertyName("gold_gained")]
    public int GoldGained { get; set; }

    [JsonPropertyName("gold_spent")]
    public int GoldSpent { get; set; }

    [JsonPropertyName("gold_lost")]
    public int GoldLost { get; set; }

    [JsonPropertyName("gold_stolen")]
    public int GoldStolen { get; set; }

    // HP
    [JsonPropertyName("current_hp")]
    public int CurrentHp { get; set; }

    [JsonPropertyName("max_hp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("damage_taken")]
    public int DamageTaken { get; set; }

    [JsonPropertyName("hp_healed")]
    public int HpHealed { get; set; }

    [JsonPropertyName("max_hp_gained")]
    public int MaxHpGained { get; set; }

    [JsonPropertyName("max_hp_lost")]
    public int MaxHpLost { get; set; }

    // 卡牌
    [JsonPropertyName("cards_gained")]
    public List<SerializableCard> CardsGained { get; set; } = new();

    [JsonPropertyName("card_choices")]
    public List<CardChoiceHistoryEntry> CardChoices { get; set; } = new();

    [JsonPropertyName("cards_removed")]
    public List<SerializableCard> CardsRemoved { get; set; } = new();

    /// <summary>升级卡牌（JSON 为 ModelId 字符串，如 CARD.xxx）。</summary>
    [JsonPropertyName("upgraded_cards")]
    public List<string> UpgradedCards { get; set; } = new();

    [JsonPropertyName("downgraded_cards")]
    public List<string> DowngradedCards { get; set; } = new();

    [JsonPropertyName("cards_transformed")]
    public List<CardTransformationHistoryEntry> CardsTransformed { get; set; } = new();

    // 遗物
    [JsonPropertyName("relic_choices")]
    public List<ModelChoiceHistoryEntry> RelicChoices { get; set; } = new();

    [JsonPropertyName("bought_relics")]
    public List<string> BoughtRelics { get; set; } = new();

    [JsonPropertyName("relics_removed")]
    public List<string> RelicsRemoved { get; set; } = new();

    // 药水
    [JsonPropertyName("potion_choices")]
    public List<ModelChoiceHistoryEntry> PotionChoices { get; set; } = new();

    [JsonPropertyName("potion_used")]
    public List<string> PotionUsed { get; set; } = new();

    [JsonPropertyName("potion_discarded")]
    public List<string> PotionDiscarded { get; set; } = new();

    [JsonPropertyName("bought_potions")]
    public List<string> BoughtPotions { get; set; } = new();

    [JsonPropertyName("bought_colorless")]
    public List<string> BoughtColorless { get; set; } = new();

    /// <summary>古遗物祭坛选项（ancient relic 三选一）。</summary>
    [JsonPropertyName("ancient_choice")]
    public List<AncientChoiceHistoryEntry> AncientChoices { get; set; } = new();

    /// <summary>事件选项（事件奖励的遗物/药水等）。</summary>
    [JsonPropertyName("event_choices")]
    public List<EventOptionHistoryEntry> EventChoices { get; set; } = new();

    /// <summary>休息地选择（休息/强化/冥想等）。</summary>
    [JsonPropertyName("rest_site_choices")]
    public List<string> RestSiteChoices { get; set; } = new();

    /// <summary>完成的委托（遗物相关）。</summary>
    [JsonPropertyName("completed_quests")]
    public List<string> CompletedQuests { get; set; } = new();

    /// <summary>卡牌附魔历史。</summary>
    [JsonPropertyName("cards_enchanted")]
    public List<CardEnchantmentHistoryEntry> CardsEnchanted { get; set; } = new();
}

/// <summary>
/// 卡牌选择历史（与游戏一致：单张 card + was_picked）。
/// </summary>
public class CardChoiceHistoryEntry
{
    [JsonPropertyName("was_picked")]
    public bool WasPicked { get; set; }

    [JsonPropertyName("card")]
    public SerializableCard Card { get; set; } = new();
}

/// <summary>
/// 遗物/药水选择历史（字段名为 choice，值为 ModelId 字符串）。
/// </summary>
public class ModelChoiceHistoryEntry
{
    [JsonPropertyName("was_picked")]
    public bool WasPicked { get; set; }

    [JsonPropertyName("choice")]
    public string Choice { get; set; } = "";
}
