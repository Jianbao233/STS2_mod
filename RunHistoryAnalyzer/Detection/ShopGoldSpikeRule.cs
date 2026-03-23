using System;
using System.Collections.Generic;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 补充】商店与假商人相关节点的金币增量校验。
/// <list type="bullet">
/// <item>真商店 <c>map_point_type=shop</c>：向商人投掷 <see cref="FoulPotionGoldPerThrow"/> 金币/瓶（源码 <c>FoulPotion</c> 的 <c>GoldVar(100)</c>）。一局最多约三瓶污浊，单节点合法增量为至多三次 100 或（装备圆顶礼帽时）每次额外 +20%。</item>
/// <item>假商人战斗：遭遇 <c>FakeMerchantEventEncounter</c> 胜利金币固定为 300；圆顶礼帽可再 +60（300×0.2 向下取整后再 GainGold）。</item>
/// <item>控制台刷钱仍走 <c>PlayerCmd.GainGold</c>，金币守恒无法检出，故用语义规则补充。</item>
/// </list>
/// </summary>
public class ShopGoldSpikeRule : Models.IAnomalyRule
{
    public string Name => "ShopGoldSpike";
    public string DisplayName => "商店/假商人金币增量";

    /// <summary>污浊药水投掷商人所得，与 <c>FoulPotion</c> 中 <c>GoldVar(100)</c> 一致。</summary>
    private const int FoulPotionGoldPerThrow = 100;

    /// <summary>假商人战战斗奖励金币（<c>FakeMerchantEventEncounter.MinGoldReward/MaxGoldReward</c>）。</summary>
    private const int FakeMerchantCombatGoldReward = 300;

    /// <summary>圆顶礼帽对单次金币收入的加成比例（<c>BowlerHat</c> 使用 0.2m）。</summary>
    private const decimal BowlerHatBonusFraction = 0.2m;

    /// <summary>单节点「污浊 + 礼帽」理论最大：3×(100+20)=360；再留余量防版本微调。</summary>
    private const int ShopNodeGoldSoftCeiling = 450;

    /// <summary>假商人战 300 + 礼帽二次加金约 360；余量同上。</summary>
    private const int FakeMerchantCombatGoldSoftCeiling = 450;

    /// <summary>
    /// 单商店节点内，至多投掷 3 瓶污浊时，每瓶基础 100 或带礼帽 120 的所有可能总和（含 0）。
    /// </summary>
    private static readonly HashSet<int> LegalShopGoldFromFoul = BuildFoulTotals();

    public IReadOnlyList<Models.Anomaly> Check(Models.RunHistoryData history)
    {
        var result = new List<Models.Anomaly>();
        var floorIndex = 0;
        foreach (var act in history.MapPointHistory)
        {
            foreach (var node in act)
            {
                floorIndex++;
                foreach (var stat in node.PlayerStats)
                {
                    if (history.AnalysisPlayerId != 0 && stat.PlayerId != history.AnalysisPlayerId)
                        continue;

                    var g = stat.GoldGained;
                    if (g == 0)
                        continue;

                    if (MapNodeShopUtil.IsShopLikeMapNode(node) || MapNodeShopUtil.HasShopTransaction(node))
                    {
                        var a = CheckRealShop(floorIndex, node, stat, g);
                        if (a != null) result.Add(a);
                    }
                    else if (IsFakeMerchantCombatNode(node))
                    {
                        var a = CheckFakeMerchantCombat(floorIndex, node, stat, g);
                        if (a != null) result.Add(a);
                    }
                    else if (IsFakeMerchantEventOnly(node))
                    {
                        var a = CheckFakeMerchantEventSpendOnly(floorIndex, node, stat, g);
                        if (a != null) result.Add(a);
                    }
                }
            }
        }

        return result;
    }

    private static Models.Anomaly? CheckRealShop(int floorIndex, Models.MapPointHistoryEntry node, Models.PlayerMapPointHistoryEntry stat, int g)
    {
        if (g > ShopNodeGoldSoftCeiling)
        {
            return AnomalyFor(floorIndex, node, stat, g,
                "真商店节点金币增量过高",
                $"gold_gained={g}，超过污浊药水（每瓶 {FoulPotionGoldPerThrow}）与圆顶礼帽加成下的合理上限（约≤{ShopNodeGoldSoftCeiling}）");
        }

        if (!LegalShopGoldFromFoul.Contains(g))
        {
            return AnomalyFor(floorIndex, node, stat, g,
                "真商店节点金币增量不符合污浊规则",
                $"gold_gained={g}。合法值应为至多三瓶污浊的组合：无礼帽仅 100/200/300；有礼帽时每瓶 120，对应总和见规则集（0,100,120,…,360）。其它数额或来自修改器/控制台。");
        }

        return null;
    }

    private static Models.Anomaly? CheckFakeMerchantCombat(int floorIndex, Models.MapPointHistoryEntry node, Models.PlayerMapPointHistoryEntry stat, int g)
    {
        if (g > FakeMerchantCombatGoldSoftCeiling)
        {
            return AnomalyFor(floorIndex, node, stat, g,
                "假商人战斗节点金币异常",
                $"gold_gained={g}。源码 FakeMerchantEventEncounter 胜利基础金币为 {FakeMerchantCombatGoldReward}，圆顶礼帽二次加金后常见约 360；单节点进账超过 {FakeMerchantCombatGoldSoftCeiling} 视为异常（战斗中其它卡牌/遗物也可能改变数额，故仅做上限检出）。");
        }

        return null;
    }

    /// <summary>假商人事件房（购物）通常 gold_gained 为 0；若出现大额获得多为作弊。</summary>
    private static Models.Anomaly? CheckFakeMerchantEventSpendOnly(int floorIndex, Models.MapPointHistoryEntry node, Models.PlayerMapPointHistoryEntry stat, int g)
    {
        if (g > ShopNodeGoldSoftCeiling)
        {
            return AnomalyFor(floorIndex, node, stat, g,
                "假商人事件节点异常进账",
                $"该节点含 EVENT.FAKE_MERCHANT，通常为购物消费；gold_gained={g} 过高，疑似控制台刷金。");
        }

        return null;
    }

    private static Models.Anomaly AnomalyFor(int floorIndex, Models.MapPointHistoryEntry node, Models.PlayerMapPointHistoryEntry stat, int g, string title, string desc)
    {
        return new Models.Anomaly(
            Models.AnomalyLevel.High,
            "ShopGoldSpike",
            title,
            $"第 {floorIndex} 个地图节点：{desc}",
            $"map_point_type={node.MapPointType}；current_gold={stat.CurrentGold}；gold_spent={stat.GoldSpent}",
            "参考：FoulPotion GoldVar(100)；FakeMerchantEventEncounter 战斗金币 300；BowlerHat +20% 二次 GainGold。"
        );
    }

    /// <summary>
    /// 假商人相关战斗：rooms 中含 <c>room_type=monster</c>，且 model_id / monster_ids 带有 FAKE_MERCHANT（含 ENCOUNTER.* 等）。
    /// 与事件房 <c>EVENT.FAKE_MERCHANT</c> 可能分属不同地图节点，故不强制同节点出现事件 id。
    /// </summary>
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

    /// <summary>仅假商人事件房（无同节点战斗记录），多为逛街买假货。</summary>
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

    private static HashSet<int> BuildFoulTotals()
    {
        var set = new HashSet<int> { 0 };
        var perThrow = new[] { FoulPotionGoldPerThrow, FoulPotionGoldPerThrow + (int)Math.Floor(FoulPotionGoldPerThrow * BowlerHatBonusFraction) };
        foreach (var a in perThrow)
        {
            set.Add(a);
            foreach (var b in perThrow)
            {
                set.Add(a + b);
                foreach (var c in perThrow)
                    set.Add(a + b + c);
            }
        }

        return set;
    }
}
