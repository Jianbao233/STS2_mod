using System;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 - 金币守恒定律】
/// 初始金币 + Σ(GoldGained) - Σ(GoldSpent) - Σ(GoldLost) - Σ(GoldStolen) = 最终 CurrentGold
/// 允许 ±1 金币误差（浮点运算精度问题）。
/// </summary>
public class GoldConservationRule : Models.IAnomalyRule
{
    public string Name => "GoldConservation";
    public string DisplayName => "金币守恒定律";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            int initialGold = Models.RunHistoryPlayerData.GetStartingGold(player.Character);
            int totalGained = 0;
            int totalSpent = 0;
            int totalLost = 0;
            int totalStolen = 0;
            int finalGold = initialGold; // 初始值作为基准

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.CurrentHp <= 0) continue;
                totalGained += stat.GoldGained;
                totalSpent += stat.GoldSpent;
                totalLost += stat.GoldLost;
                totalStolen += stat.GoldStolen;
                finalGold = stat.CurrentGold;
            }

            int expectedGold = initialGold + totalGained - totalSpent - totalLost - totalStolen;
            int deviation = Math.Abs(expectedGold - finalGold);

            if (deviation > 1)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "金币不守恒",
                    $"预期金币：{expectedGold}，实际金币：{finalGold}，偏差：{deviation}",
                    $"初始={initialGold}  + 获得={totalGained}  - 花费={totalSpent}  - 丢失={totalLost}  - 被偷={totalStolen}",
                    "可能原因：直接编辑存档 / gold 控制台作弊 / 内存修改"
                );
            }
        }

        return null;
    }
}
