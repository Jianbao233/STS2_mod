using System;

using RunHistoryAnalyzer;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 补充】非商店、非假商人节点出现单节点大额 <c>gold_gained</c> 时提示（控制台刷金常落在战斗/事件节点）。
/// 真商店（含问号房内嵌 <c>room_type=shop</c>）与假商人由 <see cref="ShopGoldSpikeRule"/> 覆盖；藏宝图任务完成节点见 <see cref="IsSpoilsMapPayoutNode"/>。
/// </summary>
public sealed class NonShopLargeGoldGainRule : Models.IAnomalyRule
{
    public string Name => "NonShopLargeGold";
    public string DisplayName => "非商店大额金币";

    /// <summary>「低」档：小额度控制台加金。</summary>
    private const int LowThreshold = 250;

    private const int MediumThreshold = 450;

    private const int HighThreshold = 850;

    /// <summary>
    /// 藏宝图 <c>CARD.SPOILS_MAP</c> 在宝藏房完成时，金币为卡牌描述的变量值（常见数百）；留足上界防版本/加成。
    /// 参考：卡牌文案「下一阶段额外 X 金币」、实测存档 gold_gained=763。
    /// </summary>
    private const int SpoilsMapGoldMaxLegit = 1600;

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        var floorIndex = 0;
        foreach (var act in history.MapPointHistory)
        {
            foreach (var node in act)
            {
                floorIndex++;
                if (ShouldSkipNodeType(node))
                    continue;

                foreach (var stat in node.PlayerStats)
                {
                    if (history.AnalysisPlayerId != 0 && stat.PlayerId != history.AnalysisPlayerId)
                        continue;

                    var g = stat.GoldGained;
                    if (g < LowThreshold)
                        continue;

                    if (IsSpoilsMapPayoutNode(stat, g))
                        continue;

                    var level = g >= HighThreshold
                        ? Models.AnomalyLevel.High
                        : g >= MediumThreshold
                            ? Models.AnomalyLevel.Medium
                            : Models.AnomalyLevel.Low;
                    var tag = level == Models.AnomalyLevel.High ? "高" : level == Models.AnomalyLevel.Medium ? "中" : "低";
                    return new Models.Anomaly(
                        level,
                        Name,
                        $"【{tag}】非商店节点金币增量异常",
                        $"第 {floorIndex} 个地图节点：gold_gained={g}（≥{LowThreshold}，且非藏宝图合法结算/非商店语境）。",
                        $"map_point_type={node.MapPointType}；current_gold={stat.CurrentGold}；gold_spent={stat.GoldSpent}",
                        "参考：问号房内真商人（rooms 含 room_type=shop）走 ShopGoldSpike；藏宝图 SPOILS_MAP 完成见 completed_quests / cards_removed。"
                    );
                }
            }
        }

        return null;
    }

    /// <summary>本节点为藏宝图任务宝藏结算：completed_quests 或移除的 SPOILS_MAP，且金币在合理上界内。</summary>
    private static bool IsSpoilsMapPayoutNode(Models.PlayerMapPointHistoryEntry stat, int goldGained)
    {
        if (goldGained <= 0 || goldGained > SpoilsMapGoldMaxLegit)
            return false;
        foreach (var q in stat.CompletedQuests)
        {
            if (!string.IsNullOrEmpty(q) && q.Contains("SPOILS_MAP", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var c in stat.CardsRemoved)
        {
            if (!string.IsNullOrEmpty(c.Id) && c.Id.Contains("SPOILS_MAP", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ShouldSkipNodeType(Models.MapPointHistoryEntry node)
    {
        if (MapNodeShopUtil.IsShopLikeMapNode(node))
            return true;
        // 先古之民祭坛（由 AncientRuleLoader 统一管理，含 JSON 配置回退）
        if (AncientRuleLoader.ShouldSkipNonShopGold(node.MapPointType ?? ""))
            return true;
        foreach (var r in node.Rooms)
        {
            if (r.RoomType != null && AncientRuleLoader.ShouldSkipNonShopGold(r.RoomType))
                return true;
        }
        // 含商店购买行为（bought_relics / bought_colorless）的节点走 ShopGoldSpike 规则
        if (MapNodeShopUtil.HasShopTransaction(node))
            return true;
        if (IsFakeMerchantCombatNode(node))
            return true;
        if (IsFakeMerchantEventOnly(node))
            return true;
        return false;
    }

    private static bool IsFakeMerchantCombatNode(Models.MapPointHistoryEntry node)
    {
        foreach (var r in node.Rooms)
        {
            if (r.RoomType == null || !r.RoomType.Equals("monster", StringComparison.OrdinalIgnoreCase))
                continue;
            var mid = r.ModelId ?? "";
            if (mid.Contains("FAKE_MERCHANT", StringComparison.OrdinalIgnoreCase))
                return true;
            if (r.MonsterIds == null)
                continue;
            foreach (var m in r.MonsterIds)
            {
                if (!string.IsNullOrEmpty(m) && m.Contains("FAKE_MERCHANT", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool IsFakeMerchantEventOnly(Models.MapPointHistoryEntry node)
    {
        var hasFakeMerchantEvent = false;
        var hasMonster = false;
        foreach (var r in node.Rooms)
        {
            var mid = r.ModelId ?? "";
            if (mid.Contains("EVENT.FAKE_MERCHANT", StringComparison.OrdinalIgnoreCase))
                hasFakeMerchantEvent = true;
            if (r.RoomType != null && r.RoomType.Equals("monster", StringComparison.OrdinalIgnoreCase))
                hasMonster = true;
        }

        return hasFakeMerchantEvent && !hasMonster;
    }
}
