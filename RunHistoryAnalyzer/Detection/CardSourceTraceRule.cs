using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RunHistoryAnalyzer;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1 - 卡牌来源追溯】
/// 最终卡组中的每张卡必须来自以下合法来源之一：
/// - 初始牌组
/// - 卡牌选择（CardChoices 中 wasPicked=true 的那张）
/// - 战斗奖励（CardsGained）
/// - 商店购买（BoughtColorless）
/// - 移除的卡牌（CardsRemoved，已从最终卡组中移除）
/// - 卡牌转化/进化（CardsTransformed 的 final_card，含先古之民、问号房等事件）
/// - 色彩哲学家事件（COLORFUL_PHILOSOPHERS：选择另一角色获得卡牌）
///
/// 若存在无法追溯来源的卡 → 标记为 [高] 异常。
/// </summary>
public class CardSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "CardSourceTrace";
    public string DisplayName => "卡牌来源追溯";

    /// <summary>提供异色卡牌的事件 id（子串匹配 rooms.model_id）。</summary>
    private static readonly string[] CrossCharacterEventIds =
    {
        "COLORFUL_PHILOSOPHERS"
    };

    public IReadOnlyList<Models.Anomaly> Check(Models.RunHistoryData history)
    {
        var result = new List<Models.Anomaly>();
        foreach (var player in history.Players)
        {
            if (history.AnalysisPlayerId != 0 && player.Id != history.AnalysisPlayerId) continue;

            string character = player.Character;

            var starterRemaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var sid in Models.RunHistoryPlayerData.GetStarterCardShortIds(character))
                starterRemaining[sid] = starterRemaining.GetValueOrDefault(sid, 0) + 1;
            if (history.Ascension > 0)
                starterRemaining["ASCENDERS_BANE"] = starterRemaining.GetValueOrDefault("ASCENDERS_BANE", 0) + 1;

            var allAcquiredCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var acquiredByChoice = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var removedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 跨角色事件中实际获得的卡（chose → cards_gained 对应）
            var crossCharacterAcquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.PlayerId != player.Id) continue;

                // 战斗奖励
                foreach (var c in stat.CardsGained)
                    allAcquiredCardIds.Add(NormalizeCardId(c.Id));

                // 卡牌选择：was_picked=true
                foreach (var choice in stat.CardChoices)
                {
                    if (choice.WasPicked && !string.IsNullOrEmpty(choice.Card.Id))
                        acquiredByChoice.Add(NormalizeCardId(choice.Card.Id));
                }

                // 商店购买
                foreach (var cardId in stat.BoughtColorless)
                    allAcquiredCardIds.Add(NormalizeCardId(cardId));

                // 移除
                foreach (var c in stat.CardsRemoved)
                    removedCardIds.Add(NormalizeCardId(c.Id));

                // 转化/进化
                foreach (var xf in stat.CardsTransformed)
                {
                    if (!string.IsNullOrEmpty(xf.FinalCard.Id))
                        allAcquiredCardIds.Add(NormalizeCardId(xf.FinalCard.Id));
                }

                // 跨角色事件（CARD.CARD.XXX 格式是游戏的 bug，直接接受 cards_gained）
                if (HasCrossCharacterEvent(node))
                {
                    foreach (var c in stat.CardsGained)
                        crossCharacterAcquired.Add(NormalizeCardId(c.Id));
                }

                // 海玻璃遗物：拾起后从其它角色池选任意张入组；同节点 cards_gained 视为合法
                if (StatPickedSeaGlassRelic(stat))
                {
                    foreach (var c in stat.CardsGained)
                        crossCharacterAcquired.Add(NormalizeCardId(c.Id));
                }
            }

            // 最终卡组
            var finalDeck = player.Deck;
            var untracedCards = new List<string>();

            foreach (var card in finalDeck)
            {
                var norm = NormalizeCardId(card.Id);
                bool wasRemoved = removedCardIds.Contains(norm);
                if (wasRemoved) continue;

                bool fromStarter = TryTakeStarterSlot(starterRemaining, norm);
                bool fromReward = allAcquiredCardIds.Contains(norm);
                bool fromChoice = acquiredByChoice.Contains(norm);
                bool fromCross = crossCharacterAcquired.Contains(norm);

                if (!fromStarter && !fromReward && !fromChoice && !fromCross)
                    untracedCards.Add(card.Id);
            }

            if (untracedCards.Count > 0)
            {
                foreach (var cardId in untracedCards)
                {
                    result.Add(new Models.Anomaly(
                        Models.AnomalyLevel.High,
                        Name,
                        "卡牌来源追溯",
                        $"卡牌 {cardId} 无法追溯来源",
                        "来源必须为：初始牌组 / 卡牌选择 / 战斗奖励 / 商店无色卡 / 卡牌转化 / 色彩哲学家 / 海玻璃 SEA_GLASS",
                        "可能原因：card 控制台作弊 / 存档直接添加"
                    ));
                }
            }
        }

        return result;
    }

    private static bool HasCrossCharacterEvent(Models.MapPointHistoryEntry node)
    {
        foreach (var r in node.Rooms)
        {
            var mid = r.ModelId ?? "";
            if (string.IsNullOrEmpty(mid))
                continue;
            // 从 AncientRuleLoader 查询（JSON 配置 > 硬编码回退）
            var eventId = mid;
            if (eventId.StartsWith("EVENT.", StringComparison.OrdinalIgnoreCase))
                eventId = eventId.Substring("EVENT.".Length);
            if (AncientRuleLoader.IsForeignCharacterCardEvent(eventId))
                return true;
        }

        return false;
    }

    private static bool StatPickedSeaGlassRelic(Models.PlayerMapPointHistoryEntry stat)
    {
        foreach (var rc in stat.RelicChoices)
        {
            if (!rc.WasPicked || string.IsNullOrEmpty(rc.Choice))
                continue;
            if (AncientRuleLoader.IsForeignCharacterCardRelic(rc.Choice))
                return true;
        }

        foreach (var ac in stat.AncientChoices)
        {
            if (!ac.WasChosen)
                continue;
            if (!string.IsNullOrEmpty(ac.TextKey) && AncientRuleLoader.IsForeignCharacterCardRelic(ac.TextKey))
                return true;
        }

        return false;
    }

    /// <summary>将各种格式的卡牌 id 规范化为统一格式（去 CARD. 前缀）。</summary>
    private static string NormalizeCardId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        const string p = "CARD.";
        return id.StartsWith(p, StringComparison.OrdinalIgnoreCase)
            ? id.Substring(p.Length)
            : id;
    }

    private static bool TryTakeStarterSlot(Dictionary<string, int> remaining, string shortId)
    {
        if (!remaining.TryGetValue(shortId, out var n) || n <= 0)
            return false;
        remaining[shortId] = n - 1;
        return true;
    }
}
