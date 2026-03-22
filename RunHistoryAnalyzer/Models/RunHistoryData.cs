using System;
using System.Collections.Generic;
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

    public DateTime GetStartDateTime() => DateTimeOffset.FromUnixTimeSeconds(StartTime).DateTime;

    public string GetDifficulty()
    {
        if (Ascension == 0) return "普通";
        return $"{Ascension}A";
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
        "CHARACTER.HEXAGUARD" => 99,
        "MOD.WATCHER" => 99,
        _ => 99
    };

    /// <summary>该角色初始最大HP。</summary>
    public static int GetStartingMaxHp(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => 80,
        "CHARACTER.SILENT" => 70,
        "CHARACTER.DEFECT" => 75,
        "CHARACTER.NECROMANCER" => 72,
        "CHARACTER.HEXAGUARD" => 75,
        "MOD.WATCHER" => 72,
        _ => 75
    };

    /// <summary>初始牌组（各角色固定初始卡牌ID列表）。</summary>
    public static HashSet<string> GetStarterCardIds(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => new HashSet<string>(new[]
        {
            "STRIKE_IRONCLAD", "STRIKE_IRONCLAD", "STRIKE_IRONCLAD",
            "STRIKE_IRONCLAD", "STRIKE_IRONCLAD",
            "DEFEND_IRONCLAD", "DEFEND_IRONCLAD", "DEFEND_IRONCLAD",
            "DEFEND_IRONCLAD", "DEFEND_IRONCLAD",
            "BASH"
        }),
        "CHARACTER.SILENT" => new HashSet<string>(new[]
        {
            "STRIKE_SILENT", "STRIKE_SILENT", "STRIKE_SILENT",
            "STRIKE_SILENT", "STRIKE_SILENT",
            "DEFEND_SILENT", "DEFEND_SILENT", "DEFEND_SILENT",
            "DEFEND_SILENT", "DEFEND_SILENT",
            "SHIV"
        }),
        "CHARACTER.DEFECT" => new HashSet<string>(new[]
        {
            "STRIKE_DEFECT", "STRIKE_DEFECT", "STRIKE_DEFECT",
            "STRIKE_DEFECT", "STRIKE_DEFECT",
            "DEFEND_DEFECT", "DEFEND_DEFECT", "DEFEND_DEFECT",
            "DEFEND_DEFECT", "DEFEND_DEFECT",
            "STRIKE_DEFECT", "DEFEND_DEFECT", "ORB_SLOT_GOLD"
        }),
        _ => new HashSet<string>(new[]
        {
            "STRIKE_P", "STRIKE_P", "STRIKE_P", "STRIKE_P", "STRIKE_P",
            "DEFEND_P", "DEFEND_P", "DEFEND_P", "DEFEND_P", "DEFEND_P"
        })
    };
}

public class SerializableCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public class SerializableRelic
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public class SerializablePotion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

/// <summary>
/// 地图节点历史条目（每个节点对应一层地图上的一个坐标）。
/// </summary>
public class MapPointHistoryEntry
{
    [JsonPropertyName("map_point_type")]
    public string MapPointType { get; set; } = "";

    [JsonPropertyName("player_stats")]
    public List<PlayerMapPointHistoryEntry> PlayerStats { get; set; } = new();
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
