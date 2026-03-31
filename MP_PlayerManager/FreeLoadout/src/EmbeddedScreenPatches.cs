using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using System.Collections.Generic;
using System.Linq;

namespace MP_PlayerManager
{
    /// <summary>
    /// 游戏 UI 补丁：改造卡牌/遗物/药水库的交互行为。
    /// 
    /// 支持：
    /// - Shift+点击卡牌库 → 追加到当前模板（批量选卡弹窗）
    /// - Shift+右键点击卡牌 → 从当前模板移除该卡牌
    /// - Ctrl+点击卡牌 → 直接获取单张卡牌（待实现）
    /// </summary>
    [HarmonyPatch]
    internal static class EmbeddedScreenPatches
    {
        private static readonly HashSet<string> _pendingShiftRightCards = new();

        /// <summary>
        /// Shift+Left 点击卡牌库中的单张卡牌：追加到当前模板的 CardIds。
        /// 普通左键保留游戏原有行为（打开检查界面）。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NCardLibrary), "OnCardPressed")]
        private static void OnCardLibraryCardPressed(NCardLibrary __instance, CardModel card)
        {
            try
            {
                if (!Input.IsKeyPressed(Key.Shift))
                    return;

                if (card?.Id?.Entry == null) return;

                // Shift+Left：追加到模板
                TemplatesTab.AppendCardToTemplate(card);
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] NCardLibrary.OnCardPressed patch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shift+右键点击卡牌库中的单张卡牌：从当前模板的 CardIds 移除。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NCardLibrary), "OnCardRightClicked")]
        private static void OnCardLibraryCardRightClicked(NCardLibrary __instance, CardModel card)
        {
            try
            {
                if (!Input.IsKeyPressed(Key.Shift))
                    return;

                if (card?.Id?.Entry == null) return;

                var cardId = card.Id.Entry;
                TemplatesTab.RemoveCardFromTemplate(cardId);

                // 记录以防止重复触发
                _pendingShiftRightCards.Add(cardId);
                Callable.From(() =>
                {
                    _pendingShiftRightCards.Remove(cardId);
                }).CallDeferred();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] NCardLibrary.OnCardRightClicked patch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shift+Left 点击遗物库条目：追加到当前模板的 RelicIds。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection.NRelicCollectionCategory), "OnRelicEntryPressed")]
        private static void OnRelicCollectionEntryPressed(
            MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection.NRelicCollectionCategory __instance,
            object relicModel)
        {
            try
            {
                if (!Input.IsKeyPressed(Key.Shift))
                    return;

                if (relicModel is RelicModel r && r.Id?.Entry != null)
                {
                    TemplatesTab.AppendRelicToTemplate(r);
                }
                else if (relicModel is IReadOnlyDictionary<string, object> dict && dict.TryGetValue("Id", out var idObj))
                {
                    var entryId = idObj?.ToString();
                    if (!string.IsNullOrEmpty(entryId))
                        TemplatesTab.AppendRelicToTemplate(entryId);
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] NRelicCollectionCategory.OnRelicEntryPressed patch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Shift+右键点击遗物库：从当前模板移除该遗物。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection.NRelicCollectionCategory), "OnRelicEntryRightClicked")]
        private static void OnRelicCollectionEntryRightClicked(
            MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection.NRelicCollectionCategory __instance,
            object relicModel)
        {
            try
            {
                if (!Input.IsKeyPressed(Key.Shift))
                    return;

                string? relicId = null;
                if (relicModel is RelicModel r)
                    relicId = r.Id?.Entry;
                else if (relicModel is IReadOnlyDictionary<string, object> dict && dict.TryGetValue("Id", out var idObj))
                    relicId = idObj?.ToString();

                if (!string.IsNullOrEmpty(relicId))
                    TemplatesTab.RemoveRelicFromTemplate(relicId);
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] NRelicCollectionCategory.OnRelicEntryRightClicked patch error: {ex.Message}");
            }
        }
    }
}