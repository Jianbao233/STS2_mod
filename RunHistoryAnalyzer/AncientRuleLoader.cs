using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace RunHistoryAnalyzer;

/// <summary>
/// 读取并提供先古之民相关规则数据（<c>Data/ancient_peoples_rules.json</c>）。
/// 若 JSON 不存在或解析失败，回退到硬编码默认值，保证各检测器始终正常运行。
///
/// 文件格式（schema_version=1）：
/// <code>
/// {
///   "schema_version": 1,
///   "game_version_range": ">=0.99",
///   "relic_effects": [
///     { "id": "SEA_GLASS", "effects": { "foreign_character_cards": true }, "tags": [...] }
///   ],
///   "event_effects": [
///     { "id": "COLORFUL_PHILOSOPHERS", "effects": { "foreign_character_cards": true } }
///   ],
///   "ancient_npcs": [
///     { "id": "DARV", "tags": ["offers_gold", "offers_relic"] }
///   ],
///   "node_type_overrides": [
///     { "match": { "map_point_type": "ancient" }, "non_shop_gold_rule": "skip", "relic_pick_ceiling": 10 }
///   ]
/// }
/// </code>
/// </summary>
public static class AncientRuleLoader
{
    // ---------------------------------------------------------------------------
    // 硬编码默认值（JSON 不存在时的回退）
    // ---------------------------------------------------------------------------

    private static readonly Dictionary<string, int> DefaultRelicPickCeiling = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monster"]  = 1,   // PAELS_WING 献祭卡包换遗物：最多 1 次；1 例 monster gold=1000 极可能是作弊
        ["elite"]    = 2,   // 极少数精英可能因叠加效果达到 2 次（p99=2）
        ["treasure"] = 1,
        ["ancient"]  = 5,   // TEZCATARA 事件赠 TOY_BOX + 4 件蜡制遗物；实测 max=5
        ["event"]    = 4,   // FAKE_MERCHANT 可选多件遗物；实测 max=4（ RANWID_THE_ELDER=2）
        ["rest"]     = 1,   // 休息站实测 max=1
        ["boss"]     = 4,   // 多玩家存档：每人最多拿 1 件 boss_relic，4 人上限为 4
        ["shop"]     = 999,
        ["unknown"]  = 5,   // 未知节点含 EVENT.TRIAL 等，1 个玩家可合法选 3 件；设上限 5
    };

    private static readonly string[] DefaultForeignCharacterCardRelics = new[]
    {
        "SEA_GLASS"
    };

    private static readonly string[] DefaultForeignCharacterCardEvents = new[]
    {
        "COLORFUL_PHILOSOPHERS"
    };

    // ---------------------------------------------------------------------------
    // 内部状态
    // ---------------------------------------------------------------------------

    private static bool _initialized;
    private static bool _jsonLoaded;
    private static string? _jsonPath;

    private static JsonData? _data;

    // ---------------------------------------------------------------------------
    // JSON 反序列化模型
    // ---------------------------------------------------------------------------

    private sealed class JsonData
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("game_version_range")]
        public string? GameVersionRange { get; set; }

        [JsonPropertyName("generated_at")]
        public string? GeneratedAt { get; set; }

        [JsonPropertyName("generated_by")]
        public string? GeneratedBy { get; set; }

        [JsonPropertyName("relic_effects")]
        public List<RelicEffectEntry> RelicEffects { get; set; } = new();

        [JsonPropertyName("event_effects")]
        public List<EventEffectEntry> EventEffects { get; set; } = new();

        [JsonPropertyName("ancient_npcs")]
        public List<AncientNpcEntry> AncientNpcs { get; set; } = new();

        [JsonPropertyName("node_type_overrides")]
        public List<NodeTypeOverride> NodeTypeOverrides { get; set; } = new();
    }

    private sealed class RelicEffectEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("effects")]
        public EffectsEntry Effects { get; set; } = new();
    }

    private sealed class EventEffectEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }

    private sealed class AncientNpcEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }

    private sealed class EffectsEntry
    {
        [JsonPropertyName("gold_variable")]
        public bool GoldVariable { get; set; }

        [JsonPropertyName("cards_on_pickup")]
        public bool CardsOnPickup { get; set; }

        [JsonPropertyName("relics_on_pickup")]
        public bool RelicsOnPickup { get; set; }

        [JsonPropertyName("foreign_character_cards")]
        public bool ForeignCharacterCards { get; set; }

        [JsonPropertyName("card_sacrifice_reward")]
        public bool CardSacrificeReward { get; set; }

        [JsonPropertyName("starter_to_ancient")]
        public bool StarterToAncient { get; set; }
    }

    private sealed class NodeTypeOverride
    {
        [JsonPropertyName("match")]
        public MatchEntry Match { get; set; } = new();

        [JsonPropertyName("non_shop_gold_rule")]
        public string NonShopGoldRule { get; set; } = "";

        [JsonPropertyName("relic_pick_ceiling")]
        public int RelicPickCeiling { get; set; } = -1;
    }

    private sealed class MatchEntry
    {
        [JsonPropertyName("map_point_type")]
        public string? MapPointType { get; set; }

        /// <summary>
        /// 可选项：精确匹配 rooms[].model_id。
        /// 用于 mpt=unknown 但有明确 event/boss ID 的节点（如 EVENT.TRIAL）。
        /// </summary>
        [JsonPropertyName("model_id")]
        public string? ModelId { get; set; }

        /// <summary>
        /// 可选项：精确匹配 rooms[].room_type。
        /// 用于 mpt=monster/elite 但 rooms 含 room_type=ancient 的特殊节点（如 EVENT.TOADPOLES 所在的 ancient 语义节点）。
        /// </summary>
        [JsonPropertyName("room_type")]
        public string? RoomType { get; set; }
    }

    // ---------------------------------------------------------------------------
    // 初始化
    // ---------------------------------------------------------------------------

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;

        // mods 目录下的 DLL 路径示例：K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\RunHistoryAnalyzer\
        // 向上两级得到 mods\ 根目录，再拼接 RunHistoryAnalyzer\Data\
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        var modsRoot = Path.GetDirectoryName(Path.GetDirectoryName(asmDir)) ?? "";
        var jsonInMods = Path.Combine(modsRoot, "RunHistoryAnalyzer", "Data", "ancient_peoples_rules.json");

        // 源码目录（构建输出），同时检查
        var srcDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(asmDir)));
        var jsonInSrc = srcDir != null
            ? Path.Combine(srcDir, "RunHistoryAnalyzer", "Data", "ancient_peoples_rules.json")
            : null;

        var candidates = new List<string>();
        if (File.Exists(jsonInMods)) candidates.Add(jsonInMods);
        if (jsonInSrc != null && File.Exists(jsonInSrc)) candidates.Add(jsonInSrc);
        candidates.Add(Directory.GetCurrentDirectory());

        foreach (var candidate in candidates)
        {
            var fullPath = Path.IsPathRooted(candidate)
                ? candidate
                : Path.Combine(Directory.GetCurrentDirectory(), candidate);
            if (File.Exists(fullPath))
            {
                _jsonPath = fullPath;
                break;
            }
        }

        if (_jsonPath == null)
        {
            GD.Print("[AncientRuleLoader] ancient_peoples_rules.json not found — using hardcoded defaults.");
            return;
        }

        try
        {
            var json = File.ReadAllText(_jsonPath);
            _data = JsonSerializer.Deserialize<JsonData>(json);
            _jsonLoaded = true;
            GD.Print($"[AncientRuleLoader] Loaded {_jsonPath} (schema={_data?.SchemaVersion}, "
                     + $"relics={_data?.RelicEffects.Count}, events={_data?.EventEffects.Count}, "
                     + $"npcs={_data?.AncientNpcs.Count}).");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AncientRuleLoader] Failed to parse ancient_peoples_rules.json: {ex.Message}");
            _data = null;
        }
    }

    // ---------------------------------------------------------------------------
    // 公开 API
    // ---------------------------------------------------------------------------

    /// <summary>手动指定 JSON 文件路径（通常由 Mod 主程序调用）。</summary>
    public static void SetJsonPath(string path)
    {
        _initialized = false;
        _jsonPath = path;
        _data = null;
        _jsonLoaded = false;
        EnsureInitialized();
    }

    /// <summary>JSON 是否成功加载。</summary>
    public static bool IsJsonLoaded => _jsonLoaded;

    /// <summary>当前 JSON 文件路径（加载失败时为 null）。</summary>
    public static string? JsonPath => _jsonPath;

    /// <summary>数据库版本（加载失败时为 0）。</summary>
    public static int SchemaVersion => _data?.SchemaVersion ?? 0;

    // ---- 非商店大额金币 --------------------------------------------------------

    /// <summary>
    /// 给定节点类型，是否应整段跳过 NonShopLargeGold 检测。
    /// 对 <c>ancient</c> 节点返回 true（先古祭坛存在变量 Gold）。
    /// </summary>
    public static bool ShouldSkipNonShopGold(string mapPointType)
    {
        EnsureInitialized();

        if (_data != null)
        {
            foreach (var ov in _data.NodeTypeOverrides)
            {
                if (string.Equals(ov.Match.MapPointType, mapPointType, StringComparison.OrdinalIgnoreCase)
                    && ov.NonShopGoldRule == "skip")
                {
                    return true;
                }
            }
        }

        // 回退：ancient 节点全跳
        return mapPointType.Equals("ancient", StringComparison.OrdinalIgnoreCase);
    }

    // ---- 遗物多选上限 ---------------------------------------------------------

    /// <summary>
    /// 给定节点类型，返回合法 relic_choices was_picked 次数上限。
    /// </summary>
    public static int MaxLegitRelicPicks(string mapPointType)
        => MaxLegitRelicPicks(mapPointType, null, null);

    /// <summary>
    /// 给定节点类型和首个房间的 model_id，返回合法 relic_choices was_picked 次数上限。
    /// modelId 为 null 时退化为仅按 mapPointType 查表。
    /// </summary>
    public static int MaxLegitRelicPicks(string mapPointType, string? modelId)
        => MaxLegitRelicPicks(mapPointType, modelId, null);

    /// <summary>
    /// 给定节点类型、首个房间 model_id、首个房间 room_type，返回合法 relic_choices was_picked 次数上限。
    ///
    /// 查表优先级（三键精确 > 双键精确 > 单键）：
    /// <list type="number">
    ///   <item>mpt + model_id（最精确）</item>
    ///   <item>mpt + room_type（次精确，用于 ancient 语义节点 mpt≠ancient）</item>
    ///   <item>mpt（仅按节点类型）</item>
    /// </list>
    /// </summary>
    public static int MaxLegitRelicPicks(string mapPointType, string? modelId, string? roomType)
    {
        EnsureInitialized();

        var mpt = mapPointType ?? "";
        var upperModelId = modelId?.ToUpperInvariant();
        var upperRoomType = roomType?.ToUpperInvariant();

        if (_data != null)
        {
            // 优先级 1：mpt + model_id
            if (!string.IsNullOrEmpty(upperModelId))
            {
                foreach (var ov in _data.NodeTypeOverrides)
                {
                    if (string.Equals(ov.Match.MapPointType, mpt, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(ov.Match.ModelId, modelId, StringComparison.OrdinalIgnoreCase)
                        && ov.RelicPickCeiling >= 0)
                    {
                        return ov.RelicPickCeiling;
                    }
                }
            }

            // 优先级 2：mpt + room_type
            if (!string.IsNullOrEmpty(upperRoomType))
            {
                foreach (var ov in _data.NodeTypeOverrides)
                {
                    if (string.Equals(ov.Match.MapPointType, mpt, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(ov.Match.RoomType, roomType, StringComparison.OrdinalIgnoreCase)
                        && ov.RelicPickCeiling >= 0)
                    {
                        return ov.RelicPickCeiling;
                    }
                }
            }

            // 优先级 3：仅 mpt
            foreach (var ov in _data.NodeTypeOverrides)
            {
                if (string.Equals(ov.Match.MapPointType, mpt, StringComparison.OrdinalIgnoreCase)
                    && ov.RelicPickCeiling >= 0)
                {
                    return ov.RelicPickCeiling;
                }
            }
        }

        return DefaultRelicPickCeiling.TryGetValue(mpt, out var ceiling) ? ceiling : 2;
    }

    // ---- 异色卡来源 ------------------------------------------------------------

    /// <summary>该遗物是否可提供其他角色的卡牌（SEA_GLASS 等）。</summary>
    public static bool IsForeignCharacterCardRelic(string relicId)
    {
        EnsureInitialized();

        if (_data != null)
        {
            foreach (var r in _data.RelicEffects)
            {
                if (r.Id.Equals(relicId, StringComparison.OrdinalIgnoreCase)
                    && r.Effects.ForeignCharacterCards)
                {
                    return true;
                }
            }
            return false;
        }

        // 回退
        return DefaultForeignCharacterCardRelics.Any(id =>
            relicId.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>该事件是否可提供其他角色的卡牌（COLORFUL_PHILOSOPHERS 等）。</summary>
    public static bool IsForeignCharacterCardEvent(string eventId)
    {
        EnsureInitialized();

        if (_data != null)
        {
            foreach (var e in _data.EventEffects)
            {
                if (e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase))
                {
                    // 事件没有 effects 字段时，尝试从 RelicEffects 中找同名 relic
                    foreach (var r in _data.RelicEffects)
                    {
                        if (r.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase)
                            && r.Effects.ForeignCharacterCards)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // 回退
        return DefaultForeignCharacterCardEvents.Any(id =>
            eventId.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    // ---- 变量金币遗物 ---------------------------------------------------------

    /// <summary>该遗物是否在拾起时授予可变金币（{Gold} 变量）。</summary>
    public static bool IsGoldVariableRelic(string relicId)
    {
        EnsureInitialized();

        if (_data != null)
        {
            foreach (var r in _data.RelicEffects)
            {
                if (r.Id.Equals(relicId, StringComparison.OrdinalIgnoreCase)
                    && r.Effects.GoldVariable)
                {
                    return true;
                }
            }
            return false;
        }

        // 回退：已在 NonShopLargeGold 中按 ancient 节点全跳，此处仅作提示用
        return false;
    }

    // ---- 先古 NPC ------------------------------------------------------------

    /// <summary>该 ID 是否为先古 NPC（Darv / Neow / Paels 家族等）。</summary>
    public static bool IsAncientNpc(string npcId)
    {
        EnsureInitialized();

        if (_data != null)
        {
            foreach (var n in _data.AncientNpcs)
            {
                if (n.Id.Equals(npcId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // 回退：按前缀
        var upper = npcId.ToUpperInvariant();
        return upper.StartsWith("DARV") || upper.StartsWith("NEOW") || upper.StartsWith("PAELS");
    }
}
