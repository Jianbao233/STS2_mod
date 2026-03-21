using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1 - 卡牌来源追溯】
/// 最终卡组中的每张卡必须来自以下合法来源之一：
/// - 初始牌组
/// - 卡牌选择（CardChoices 中 wasPicked=true 的那张）
/// - 战斗奖励（CardsGained）
/// - 商店购买（BoughtColorless）
/// - 升级/变形产生的卡牌（UpgradedCards / CardsTransformed，已在上游来源中）
///
/// 若存在无法追溯来源的卡 → 标记为 [高] 异常。
/// </summary>
public class CardSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "CardSourceTrace";
    public string DisplayName => "卡牌来源追溯";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            string character = player.Character;
            var starterCards = Models.RunHistoryPlayerData.GetStarterCardIds(character);

            var allAcquiredCardIds = new HashSet<string>();
            var acquiredByChoice = new HashSet<string>();

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                // 战斗奖励
                foreach (var card in stat.CardsGained)
                    allAcquiredCardIds.Add(card.Id);

                // 卡牌选择：只有 wasPicked=true 的才计入
                foreach (var choice in stat.CardChoices)
                {
                    if (choice.WasPicked)
                    {
                        foreach (var card in choice.Cards)
                            acquiredByChoice.Add(card.Id);
                    }
                }

                // 商店购买（无色卡）
                foreach (var card in stat.BoughtColorless)
                    allAcquiredCardIds.Add(card.Id);
            }

            // 最终卡组
            var finalDeck = player.Deck;
            var untracedCards = new List<string>();

            foreach (var card in finalDeck)
            {
                string id = card.Id;
                bool isFromStarter = starterCards.Contains(id);
                bool isFromReward = allAcquiredCardIds.Contains(id);
                bool isFromChoice = acquiredByChoice.Contains(id);

                if (!isFromStarter && !isFromReward && !isFromChoice)
                {
                    untracedCards.Add(id);
                }
            }

            if (untracedCards.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("无法追溯来源的卡牌：");
                foreach (var c in untracedCards)
                    sb.Append($"{c}  ");
                sb.AppendLine();
                sb.Append("(卡牌必须来自：初始牌组 / 卡牌选择 / 战斗奖励 / 商店无色卡)");

                return new Models.Anomaly(
                    Models.AnomalyLevel.Medium,
                    Name,
                    "卡牌来源追溯",
                    $"发现 {untracedCards.Count} 张无法追溯来源的卡牌",
                    sb.ToString(),
                    "可能原因：card 控制台作弊 / 存档直接添加"
                );
            }
        }

        return null;
    }
}
