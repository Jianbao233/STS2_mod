using System.Collections.Generic;
using System.Text;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P2 - 药水来源追溯】
/// 最终药水列表中的每瓶药水必须来自以下合法来源之一：
/// - 药水选择（PotionChoices 中 wasPicked=true 的）
/// - 商店购买（BoughtPotions）
/// - 消耗的药水（PotionUsed，从最终列表中移除）
///
/// 注意：药水可以消耗后消失，最终列表中可能少于获得数量。
/// 若存在无法追溯来源的药水 → 标记为 [低] 异常。
/// </summary>
public class PotionSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "PotionSourceTrace";
    public string DisplayName => "药水来源追溯";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            var acquiredPotionIds = new HashSet<string>();
            int maxPotionSlots = player.MaxPotionSlotCount;

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                // 药水选择
                foreach (var choice in stat.PotionChoices)
                {
                    if (choice.WasPicked && !string.IsNullOrEmpty(choice.ChosenId))
                        acquiredPotionIds.Add(choice.ChosenId);
                }

                // 商店购买
                foreach (var potion in stat.BoughtPotions)
                    acquiredPotionIds.Add(potion.Id);
            }

            var finalPotions = player.Potions;

            // 药水可消耗，最终数量 <= 获得数量 + 初始栏位
            // 此处仅做宽松检查：最终药水数量不超过（获得数量 + 栏位上限）
            // 精确追溯需要跟踪每瓶药水的消耗，这里做简化处理
            if (finalPotions.Count > acquiredPotionIds.Count + maxPotionSlots)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.Low,
                    Name,
                    "药水数量异常",
                    $"最终药水：{finalPotions.Count}，可获得上限：{acquiredPotionIds.Count + maxPotionSlots}",
                    $"（实际数量可能因消耗而少于获得数量，此检测仅作参考）",
                    "可能原因：potion 控制台作弊 / 存档直接添加"
                );
            }
        }

        return null;
    }
}
