using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using Loc = MultiplayerTools.Loc;

namespace MultiplayerTools
{
    /// <summary>
    /// Minimal template data structure (matches JSON serialization).
    /// </summary>
    internal class TemplateData
    {
        internal string Id { get; set; } = Guid.NewGuid().ToString();
        internal string Name { get; set; } = Loc.Get("tmpl.new_template", "New Template");
        internal string CharacterId { get; set; } = "";
        internal string CharacterName { get; set; } = "";
        internal int MaxHp { get; set; }
        internal int CurHp { get; set; }
        internal int Energy { get; set; }
        internal int Gold { get; set; }
        internal List<string> CardIds { get; set; } = new();
        internal List<string> RelicIds { get; set; } = new();
        internal List<string> PotionIds { get; set; } = new();
    }
}

namespace MultiplayerTools.Core
{
    /// <summary>
    /// Apply a character template (HP/Gold/Deck/Relics) to a player.
    /// Uses reflection to resolve game API classes (PlayerCmd, CardPileCmd, RelicCmd).
    /// </summary>
    internal static class TemplateApplier
    {
        private static readonly Type? PlayerCmdType;
        private static readonly Type? CardPileCmdType;
        private static readonly Type? RelicCmdType;

        static TemplateApplier()
        {
            // Resolve game command types via reflection from sts2.dll
            PlayerCmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.Cmd.PlayerCmd");
            CardPileCmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.Cmd.CardPileCmd");
            RelicCmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.Cmd.RelicCmd");
            if (PlayerCmdType == null) GD.PrintErr("[MultiplayerTools] PlayerCmd type not found");
            if (CardPileCmdType == null) GD.PrintErr("[MultiplayerTools] CardPileCmd type not found");
            if (RelicCmdType == null) GD.PrintErr("[MultiplayerTools] RelicCmd type not found");
        }

        /// <summary>Apply a template to the local player.</summary>
        internal static async Task ApplyToLocalAsync(TemplateData template)
        {
            var player = MpPanel.GetLocalPlayer();
            if (player == null)
            {
                GD.PrintErr("[MultiplayerTools] No local player available");
                return;
            }
            await ApplyToPlayerAsync(player, template);
        }

        /// <summary>Apply a template to a specific player object.</summary>
        internal static async Task ApplyToPlayerAsync(Player player, TemplateData template)
        {
            try
            {
                GD.Print($"[MultiplayerTools] Applying template '{template.Name}' to player");
                var runState = RunManager.Instance?.DebugOnlyGetState();
                if (runState == null) { GD.PrintErr("[MultiplayerTools] No run state"); return; }

                // 1. HP
                await SetHpAsync(player, template.MaxHp, template.CurHp > 0 ? template.CurHp : template.MaxHp);

                // 2. Gold
                if (PlayerCmdType != null)
                {
                    var setGold = AccessTools.Method(PlayerCmdType, "SetGold", new[] { typeof(int), typeof(Player) });
                    setGold?.Invoke(null, new object[] { template.Gold, player });
                }

                // 3. Energy
                player.MaxEnergy = template.Energy > 0 ? template.Energy : 3;

                // 4. Clear + Rebuild deck
                await RebuildDeckAsync(player, template);

                // 5. Clear + Rebuild relics
                await RebuildRelicsAsync(player, template);

                GD.Print($"[MultiplayerTools] Template '{template.Name}' applied successfully");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ApplyToPlayerAsync failed: " + ex.Message);
            }
        }

        private static async Task SetHpAsync(Player player, int maxHp, int curHp)
        {
            try
            {
                var creature = player.Creature;
                if (creature == null) return;
                creature.SetMaxHpInternal(maxHp);
                creature.SetCurrentHpInternal(Math.Min(curHp, maxHp));
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] SetHpAsync failed: " + ex.Message);
            }
        }

        private static async Task RebuildDeckAsync(Player player, TemplateData template)
        {
            try
            {
                // Clear existing deck
                var deck = player.Deck?.Cards?.ToList() ?? new List<CardModel>();
                if (deck.Count > 0 && CardPileCmdType != null)
                {
                    var removeMethod = AccessTools.Method(CardPileCmdType, "RemoveFromDeck", new[] { typeof(IEnumerable<CardModel>), typeof(bool) });
                    removeMethod?.Invoke(null, new object[] { deck, false });
                }

                // Add new cards
                foreach (var cardId in template.CardIds)
                {
                    var cardModel = FindCard(cardId);
                    if (cardModel == null) continue;
                    if (CardPileCmdType != null)
                    {
                        var addMethod = AccessTools.Method(CardPileCmdType, "Add", new[]
                        {
                            typeof(CardModel), typeof(PileType),
                            typeof(CardPilePosition), typeof(CardPile), typeof(bool)
                        });
                        addMethod?.Invoke(null, new object[] { cardModel, PileType.Deck, CardPilePosition.Bottom, null, true });
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] RebuildDeckAsync failed: " + ex.Message);
            }
            await Task.CompletedTask;
        }

        private static async Task RebuildRelicsAsync(Player player, TemplateData template)
        {
            try
            {
                if (RelicCmdType == null) return;

                // Remove existing relics
                foreach (var relic in (player.Relics ?? Enumerable.Empty<RelicModel>()).ToList())
                {
                    var removeMethod = AccessTools.Method(RelicCmdType, "Remove", new[] { typeof(RelicModel) });
                    removeMethod?.Invoke(null, new object[] { relic });
                }

                // Add template relics
                foreach (var relicId in template.RelicIds)
                {
                    var relicModel = FindRelic(relicId);
                    if (relicModel == null) continue;
                    var mutable = relicModel.ToMutable();
                    if (mutable == null) continue;
                    var obtainMethod = AccessTools.Method(RelicCmdType, "Obtain", new[]
                    {
                        typeof(RelicModel), typeof(Player), typeof(int)
                    });
                    obtainMethod?.Invoke(null, new object[] { mutable, player, -1 });
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] RebuildRelicsAsync failed: " + ex.Message);
            }
            await Task.CompletedTask;
        }

        private static CardModel? FindCard(string cardId)
        {
            try
            {
                var allCards = ModelDb.AllCards;
                return allCards.FirstOrDefault(c => c.Id?.Entry == cardId)
                    ?? allCards.FirstOrDefault(c => c.Id?.Entry.Contains(cardId, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch { return null; }
        }

        private static RelicModel? FindRelic(string relicId)
        {
            try
            {
                var allRelics = ModelDb.AllRelics;
                return allRelics.FirstOrDefault(r => r.Id?.Entry == relicId)
                    ?? allRelics.FirstOrDefault(r => r.Id?.Entry.Contains(relicId, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch { return null; }
        }
    }
}
