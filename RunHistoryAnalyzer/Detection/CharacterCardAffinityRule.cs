using System;
using System.Collections.Generic;

using RunHistoryAnalyzer;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1】最终牌组中含明显带异色角色后缀的卡牌（如 <c>CARD.DEFEND_DEFECT</c> 出现在亡灵存档）。
/// 无色/通用牌无此后缀，不在此规则范围；控制台加牌常带原职业后缀。
/// </summary>
public sealed class CharacterCardAffinityRule : Models.IAnomalyRule
{
    public string Name => "CharacterCardAffinity";
    public string DisplayName => "角色专属卡牌后缀";

    private static readonly string[] ClassTags =
    {
        "IRONCLAD", "SILENT", "DEFECT", "NECROBINDER", "NECROMANCER", "REGENT", "HEXAGUARD"
    };

    public IReadOnlyList<Models.Anomaly> Check(Models.RunHistoryData history)
    {
        var result = new List<Models.Anomaly>();
        foreach (var player in history.Players)
        {
            if (history.AnalysisPlayerId != 0 && player.Id != history.AnalysisPlayerId)
                continue;

            var character = player.Character;
            // 海玻璃：可任选数量异色牌入组；带后缀的它职业牌为合法来源
            if (PlayerHasSeaGlassRelic(player))
                continue;

            var bad = new List<string>();
            foreach (var card in player.Deck)
            {
                var id = card.Id ?? "";
                var shortId = StripCardPrefix(id);
                if (!TryGetLockedClassTag(shortId, out var tag))
                    continue;
                if (TagMatchesCharacter(tag, character))
                    continue;
                bad.Add(id);
            }

            if (bad.Count == 0)
                continue;

            foreach (var cardId in bad)
            {
                result.Add(new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "卡组中存在异色角色卡牌",
                    $"卡牌 {cardId} 带有其它职业后缀，角色为 {character}。",
                    $"卡牌ID：{cardId}",
                    "可能原因：控制台 card 命令 / 存档直接编辑；正常掉落不应含他职业带后缀的牌。"
                ));
            }
        }

        return result;
    }

    private static bool PlayerHasSeaGlassRelic(Models.RunHistoryPlayerData player)
    {
        foreach (var r in player.Relics)
        {
            var id = r.Id ?? "";
            if (AncientRuleLoader.IsForeignCharacterCardRelic(id))
                return true;
        }

        return false;
    }

    private static string StripCardPrefix(string id)
    {
        const string p = "CARD.";
        return id.StartsWith(p, StringComparison.OrdinalIgnoreCase) ? id.Substring(p.Length) : id;
    }

    private static bool TryGetLockedClassTag(string shortId, out string tag)
    {
        foreach (var t in ClassTags)
        {
            if (shortId.EndsWith("_" + t, StringComparison.OrdinalIgnoreCase))
            {
                tag = t;
                return true;
            }
        }

        tag = "";
        return false;
    }

    private static bool TagMatchesCharacter(string tag, string characterId)
    {
        var c = characterId.ToUpperInvariant();
        return tag switch
        {
            "IRONCLAD" => c.Contains("IRONCLAD"),
            "SILENT" => c.Contains("SILENT"),
            "DEFECT" => c.Contains("DEFECT"),
            "NECROBINDER" or "NECROMANCER" => c.Contains("NECRO"),
            "REGENT" or "HEXAGUARD" => c.Contains("REGENT") || c.Contains("HEXAGUARD"),
            _ => false
        };
    }
}
