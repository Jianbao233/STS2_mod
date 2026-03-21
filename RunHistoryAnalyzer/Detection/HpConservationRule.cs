using System;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 - 生命守恒定律】
/// 初始 MaxHp + Σ(MaxHpGained) - Σ(MaxHpLost) = 最终 MaxHp
/// 当前 HP = 初始 HP - Σ(DamageTaken) + Σ(HpHealed)
/// 允许 ±1 误差（游戏内舍入问题）。
/// </summary>
public class HpConservationRule : Models.IAnomalyRule
{
    public string Name => "HpConservation";
    public string DisplayName => "HP守恒定律";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            string character = player.Character;
            int initialMaxHp = Models.RunHistoryPlayerData.GetStartingMaxHp(character);

            int totalMaxHpGained = 0;
            int totalMaxHpLost = 0;
            int totalDamageTaken = 0;
            int totalHpHealed = 0;
            int finalMaxHp = initialMaxHp;
            int finalCurrentHp = initialMaxHp;

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.CurrentHp <= 0) continue;
                totalMaxHpGained += stat.MaxHpGained;
                totalMaxHpLost += stat.MaxHpLost;
                totalDamageTaken += stat.DamageTaken;
                totalHpHealed += stat.HpHealed;
                finalMaxHp = stat.MaxHp;
                finalCurrentHp = stat.CurrentHp;
            }

            int expectedMaxHp = initialMaxHp + totalMaxHpGained - totalMaxHpLost;
            int maxHpDeviation = Math.Abs(expectedMaxHp - finalMaxHp);

            // 当前HP = 初始HP（满血）- 总受伤 + 总治疗
            int expectedCurrentHp = initialMaxHp - totalDamageTaken + totalHpHealed;
            expectedCurrentHp = Math.Min(expectedCurrentHp, expectedMaxHp); // 不超过上限
            int hpDeviation = Math.Abs(expectedCurrentHp - finalCurrentHp);

            // 允许误差：HP波动1点，MaxHP误差1点
            if (maxHpDeviation > 1)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "MaxHP不守恒",
                    $"预期最大HP：{expectedMaxHp}，实际最大HP：{finalMaxHp}，偏差：{maxHpDeviation}",
                    $"初始={initialMaxHp}  + 获得={totalMaxHpGained}  - 失去={totalMaxHpLost}",
                    "可能原因：内存修改 / 存档直接编辑"
                );
            }

            if (hpDeviation > 1)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "HP不守恒",
                    $"预期当前HP：{expectedCurrentHp}，实际当前HP：{finalCurrentHp}，偏差：{hpDeviation}",
                    $"初始HP={initialMaxHp}  - 受伤={totalDamageTaken}  + 治疗={totalHpHealed}",
                    "可能原因：内存修改 / heal 控制台作弊 / 存档直接编辑"
                );
            }
        }

        return null;
    }
}
