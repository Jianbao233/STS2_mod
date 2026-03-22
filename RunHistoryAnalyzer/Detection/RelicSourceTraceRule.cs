using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1 - 遗物来源追溯】
/// 最终遗物列表中的每件遗物必须来自以下合法来源之一：
/// - 初始遗物
/// - 遗物选择（RelicChoices 中 wasPicked=true 的）
/// - 商店购买（BoughtRelics）
/// - 移除的遗物（RelicsRemoved，移除后不计入最终）
///
/// 若存在无法追溯来源的遗物 → 标记为 [高] 异常。
/// </summary>
public class RelicSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "RelicSourceTrace";
    public string DisplayName => "遗物来源追溯";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            var allAcquiredRelicIds = new HashSet<string>();
            var acquiredByChoice = new HashSet<string>();

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                // 遗物选择
                foreach (var choice in stat.RelicChoices)
                {
                    if (choice.WasPicked && !string.IsNullOrEmpty(choice.Choice))
                        acquiredByChoice.Add(choice.Choice);
                }

                // 商店购买（JSON 为 ModelId 字符串）
                foreach (var relicId in stat.BoughtRelics)
                    allAcquiredRelicIds.Add(relicId);
            }

            // 最终遗物
            var finalRelics = player.Relics;
            var untracedRelics = new List<string>();

            // 初始遗物由角色决定（无法从数据推断，仅检查是否存在）
            // 排除初始遗物后的检查：只有选择和购买可追溯
            foreach (var relic in finalRelics)
            {
                string id = relic.Id;
                bool isFromChoice = acquiredByChoice.Contains(id);
                bool isFromShop = allAcquiredRelicIds.Contains(id);

                if (!isFromChoice && !isFromShop)
                {
                    untracedRelics.Add(id);
                }
            }

            if (untracedRelics.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("无法追溯来源的遗物：");
                foreach (var r in untracedRelics)
                    sb.Append($"{r}  ");
                sb.AppendLine();
                sb.Append("(遗物必须来自：遗物选择 / 商店购买 / 初始遗物)");

                return new Models.Anomaly(
                    Models.AnomalyLevel.Medium,
                    Name,
                    "遗物来源追溯",
                    $"发现 {untracedRelics.Count} 件无法追溯来源的遗物",
                    sb.ToString(),
                    "可能原因：relic 控制台作弊 / 存档直接添加"
                );
            }
        }

        return null;
    }
}
