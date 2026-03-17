using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 从游戏 ModelDb.AllCardPools 获取官方卡牌归属。参考游戏百科/卡牌图鉴的 CardPool 分类。
/// </summary>
public static class CardPoolHelper
{
    private static Dictionary<string, string> _cache;

    /// <summary>获取卡牌所属角色：IRONCLAD/SILENT/DEFECT/NECROBINDER/REGENT/COLORLESS</summary>
    public static string GetCharacter(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "COLORLESS";
        _cache ??= BuildCache();
        var u = cardId.ToUpperInvariant();
        return _cache.TryGetValue(u, out var c) ? c : InferFromSuffix(u);
    }

    /// <summary>角色筛选：0=全部,1=铁甲,2=寂静,3=故障,4=亡灵,5=储君</summary>
    public static bool MatchesCharacter(string cardId, int charIdx)
    {
        if (charIdx <= 0) return true;
        var c = GetCharacter(cardId);
        return charIdx switch { 1 => c == "IRONCLAD", 2 => c == "SILENT", 3 => c == "DEFECT", 4 => c == "NECROBINDER", 5 => c == "REGENT", _ => true };
    }

    private static string InferFromSuffix(string id)
    {
        if (id.EndsWith("_IRONCLAD")) return "IRONCLAD";
        if (id.EndsWith("_SILENT")) return "SILENT";
        if (id.EndsWith("_DEFECT")) return "DEFECT";
        if (id.EndsWith("_NECROBINDER")) return "NECROBINDER";
        if (id.EndsWith("_REGENT")) return "REGENT";
        return "COLORLESS";
    }

    private static Dictionary<string, string> BuildCache()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var modelDb = asm.GetType("MegaCrit.Sts2.Core.Models.ModelDb") ?? asm.GetType("ModelDb");
                if (modelDb == null) continue;
                var allPools = modelDb.GetProperty("AllCardPools") ?? modelDb.GetProperty("AllSharedCardPools");
                var pools = allPools?.GetValue(null) as System.Collections.IEnumerable;
                if (pools == null) continue;

                foreach (var pool in pools)
                {
                    if (pool == null) continue;
                    var poolType = pool.GetType().Name;
                    var charKey = poolType switch
                    {
                        "IroncladCardPool" => "IRONCLAD",
                        "SilentCardPool" => "SILENT",
                        "DefectCardPool" => "DEFECT",
                        "NecrobinderCardPool" => "NECROBINDER",
                        "RegentCardPool" => "REGENT",
                        "ColorlessCardPool" => "COLORLESS",
                        _ => null
                    };
                    if (charKey == null) continue;
                    var allCards = pool.GetType().GetProperty("AllCards")?.GetValue(pool) as System.Collections.IEnumerable;
                    if (allCards == null) continue;
                    foreach (var card in allCards)
                    {
                        if (card == null) continue;
                        var idProp = card.GetType().GetProperty("Id");
                        var idObj = idProp?.GetValue(card);
                        var entry = idObj?.GetType().GetProperty("Entry")?.GetValue(idObj) as string ?? idObj?.ToString();
                        if (!string.IsNullOrEmpty(entry)) result[entry.ToUpperInvariant()] = charKey;
                    }
                }
            }
        }
        catch { }
        return result;
    }
}
