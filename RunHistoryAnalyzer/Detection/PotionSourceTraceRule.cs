using System.Collections.Generic;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P2 - 药水来源追溯】
/// 最终药水列表中的每瓶药水必须来自以下合法来源之一：
/// - 药水选择（PotionChoices 中 wasPicked=true）
/// - 商店购买（BoughtPotions）
/// - 消耗的药水（PotionUsed，从最终列表中移除）
///
/// 若存在无法追溯来源的药水 → 标记为 [中] 异常。
/// </summary>
public class PotionSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "PotionSourceTrace";
    public string DisplayName => "药水来源追溯";

    public IReadOnlyList<Models.Anomaly> Check(Models.RunHistoryData history)
    {
        var result = new List<Models.Anomaly>();
        foreach (var player in history.Players)
        {
            var acquiredPotionIds = new HashSet<string>();
            var consumedPotionIds = new HashSet<string>();

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.PlayerId != player.Id) continue;

                // 药水选择
                foreach (var choice in stat.PotionChoices)
                {
                    if (choice.WasPicked && !string.IsNullOrEmpty(choice.Choice))
                        acquiredPotionIds.Add(NormalizePotionId(choice.Choice));
                }

                // 商店购买
                foreach (var potionId in stat.BoughtPotions)
                    acquiredPotionIds.Add(NormalizePotionId(potionId));

                // 已消耗的药水（从最终列表中排除）
                foreach (var potionId in stat.PotionUsed)
                    consumedPotionIds.Add(NormalizePotionId(potionId));
            }

            var finalPotions = player.Potions;

            foreach (var potion in finalPotions)
            {
                string normalizedId = NormalizePotionId(potion.Id);
                bool isAcquired = acquiredPotionIds.Contains(normalizedId);
                bool wasConsumed = consumedPotionIds.Contains(normalizedId);

                // 已消耗的药水不出现在最终列表中，忽略
                if (wasConsumed) continue;

                if (!isAcquired)
                    result.Add(new Models.Anomaly(
                        Models.AnomalyLevel.Medium,
                        Name,
                        "药水来源追溯",
                        $"药水 {potion.Id} 无法追溯来源",
                        "药水必须来自：药水选择 / 商店购买",
                        "可能原因：potion 控制台作弊 / 存档直接添加"
                    ));
            }
        }

        return result;
    }

    private static string NormalizePotionId(string id)
    {
        // "POTION.STRENGTH_POTION_L" → "STRENGTH_POTION_L"
        if (string.IsNullOrEmpty(id)) return id;
        if (id.StartsWith("POTION.", System.StringComparison.OrdinalIgnoreCase))
            return id.Substring("POTION.".Length);
        return id;
    }
}
