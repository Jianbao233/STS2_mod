using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
    /// <summary>
    /// 将 TemplateData 应用到游戏中的玩家（HP/Gold/Energy/卡组/遗物/药水）。
    /// 参考 TrainerState.SetHpOnce/SetGoldOnce/SetEnergyOnce 模式。
    /// </summary>
    internal static class TemplateApplier
    {
        /// <summary>将模板应用到当前本地玩家（用于 UI 按钮调用）。</summary>
        internal static async Task ApplyToLocalPlayerAsync(TemplateData template)
        {
            var player = LoadoutPanel.GetPlayer();
            if (player == null)
            {
                GD.Print("[MP_PlayerManager] No local player found, cannot apply template.");
                return;
            }
            await ApplyToPlayerAsync(player, template);
        }

        /// <summary>将模板应用到指定 Player 实例。</summary>
        internal static async Task ApplyToPlayerAsync(Player player, TemplateData template)
        {
            if (player == null || template == null) return;

            GD.Print($"[MP_PlayerManager] Applying template '{template.Name}' to player...");

            // 1. HP（Creature API：SetMaxHpInternal → SetCurrentHpInternal）
            await SetPlayerHpAsync(player, template.MaxHp, template.CurHp > 0 ? template.CurHp : template.MaxHp);

            // 2. Gold
            SetPlayerGold(player, template.Gold);

            // 3. Energy
            SetPlayerEnergy(player, template.Energy);

            // 4. 清空并重建卡组
            await RebuildPlayerDeckAsync(player, template.CardIds);

            // 5. 清空并重建遗物
            await RebuildPlayerRelicsAsync(player, template.RelicIds);

            // 6. 药水（若 API 可用）
            await RebuildPlayerPotionsAsync(player, template.PotionIds);

            GD.Print($"[MP_PlayerManager] Template '{template.Name}' applied successfully.");
        }

        // ── HP ─────────────────────────────────────────────────────────────────

        private static async Task SetPlayerHpAsync(Player player, int maxHp, int currentHp)
        {
            try
            {
                await Task.Yield(); // 让出主线程

                maxHp = Math.Max(1, maxHp);
                currentHp = (int)Math.Clamp(currentHp, 1, maxHp);

                var creature = player.Creature;
                if (creature == null) return;

                creature.SetMaxHpInternal(maxHp);
                creature.SetCurrentHpInternal(currentHp);

                GD.Print($"[MP_PlayerManager] HP set to {currentHp}/{maxHp}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to set HP: {ex.Message}");
            }
        }

        // ── Gold ───────────────────────────────────────────────────────────────

        private static void SetPlayerGold(Player player, int gold)
        {
            try
            {
                player.Gold = Math.Max(0, gold);
                GD.Print($"[MP_PlayerManager] Gold set to {gold}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to set Gold: {ex.Message}");
            }
        }

        // ── Energy ─────────────────────────────────────────────────────────────

        private static void SetPlayerEnergy(Player player, int energy)
        {
            try
            {
                player.MaxEnergy = Math.Max(0, energy);
                if (player.PlayerCombatState != null)
                {
                    player.PlayerCombatState.Energy = Math.Max(0, energy);
                }
                GD.Print($"[MP_PlayerManager] Energy set to {energy}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to set Energy: {ex.Message}");
            }
        }

        // ── 卡组 ───────────────────────────────────────────────────────────────

        private static async Task RebuildPlayerDeckAsync(Player player, List<string> cardIds)
        {
            try
            {
                // 清空现有卡组（Deck）
                var deck = CardPile.Get(PileType.Deck, player);
                if (deck != null)
                {
                    var existing = deck.Cards.ToList();
                    foreach (var card in existing)
                    {
                        try { CardPileCmd.RemoveFromDeck(card, false); }
                        catch { }
                    }
                }

                // 按 cardIds 逐张添加
                foreach (var cardId in cardIds)
                {
                    await AddCardToPlayerDeckAsync(player, cardId);
                }

                GD.Print($"[MP_PlayerManager] Deck rebuilt with {cardIds.Count} card(s).");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to rebuild deck: {ex.Message}");
            }
        }

        private static async Task AddCardToPlayerDeckAsync(Player player, string cardId)
        {
            try
            {
                await Task.Yield();

                var cardModel = ModelDb.AllCards.FirstOrDefault(c =>
                    string.Equals(c.Id?.Entry, cardId, StringComparison.OrdinalIgnoreCase));

                if (cardModel == null)
                {
                    GD.PrintErr($"[MP_PlayerManager] Card not found in ModelDb: {cardId}");
                    return;
                }

                var mutable = cardModel.ToMutable();
                await CardPileCmd.Add(mutable, PileType.Deck, CardPilePosition.Bottom, null, false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to add card {cardId}: {ex.Message}");
            }
        }

        // ── 遗物 ───────────────────────────────────────────────────────────────

        private static async Task RebuildPlayerRelicsAsync(Player player, List<string> relicIds)
        {
            try
            {
                // 清空现有遗物（通过 RelicCmd 逐个移除）
                try
                {
                    var held = player.HeldRelics;
                    if (held != null)
                    {
                        foreach (var r in held.ToList())
                        {
                            try { RelicCmd.Remove(r); }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[MP_PlayerManager] Error clearing relics (may use game API): {ex.Message}");
                }

                // 按 relicIds 逐个获取
                foreach (var relicId in relicIds)
                {
                    await ObtainRelicForPlayerAsync(player, relicId);
                }

                GD.Print($"[MP_PlayerManager] Relics rebuilt with {relicIds.Count} relic(s).");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to rebuild relics: {ex.Message}");
            }
        }

        private static async Task ObtainRelicForPlayerAsync(Player player, string relicId)
        {
            try
            {
                await Task.Yield();

                var relicModel = ModelDb.AllRelics.FirstOrDefault(r =>
                    string.Equals(r.Id?.Entry, relicId, StringComparison.OrdinalIgnoreCase));

                if (relicModel == null)
                {
                    GD.PrintErr($"[MP_PlayerManager] Relic not found in ModelDb: {relicId}");
                    return;
                }

                var mutable = relicModel.ToMutable();
                RelicCmd.Obtain(mutable, player);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] Failed to obtain relic {relicId}: {ex.Message}");
            }
        }

        // ── 药水 ───────────────────────────────────────────────────────────────

        private static Task RebuildPlayerPotionsAsync(Player player, List<string> potionIds)
        {
            if (potionIds.Count == 0) return Task.CompletedTask;
            GD.Print($"[MP_PlayerManager] Potion rebuild requested for {potionIds.Count} potion(s) — not yet implemented (API TBD).");
            return Task.CompletedTask;
        }
    }
}