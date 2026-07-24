using System.Reflection;
using System.Text.Json.Nodes;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DimensionalTraveler.TestAdapter;

internal sealed class DimensionalTravelerSnapshotContributor
{
    public static DimensionalTravelerSnapshotContributor Instance { get; } = new();

    public string ExtensionKey => "dimensionalTravelerTest";

    public void Enrich(JsonObject snapshot, Player player, object gamePhase)
    {
        if (!AlchemyCombatState.IsTraveler(player))
            return;

        var extensions = snapshot["extensions"] as JsonObject ?? new JsonObject();
        snapshot["extensions"] = extensions;
        var local = player.Creature.CombatState is null
            ? CaptureTravelerRosterEntry(player, gamePhase)
            : Capture(player, gamePhase);
        local["travelers"] = CaptureTravelers(gamePhase);
        extensions[ExtensionKey] = local;
    }

    private static JsonObject Capture(Player player, object gamePhase)
    {
        var result = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["playerNetId"] = player.NetId.ToString(),
            ["gamePhase"] = gamePhase.ToString(),
            ["principles"] = CapturePrinciples(player),
            ["playerCombat"] = CapturePlayerCombat(player),
            ["backpack"] = CaptureBackpack(player),
            ["piles"] = CapturePiles(player),
            ["combatants"] = CaptureCombatants(player),
            ["payments"] = PaymentAudit.Capture(),
            ["choices"] = ChoiceAudit.Capture(),
            ["targeting"] = TargetingControl.Capture(),
        };

        var state = player.Creature.GetPower<AlchemyCombatStatePower>();
        result["combatStateAttached"] = state is not null;
        if (state is not null)
            result["turn"] = CaptureTurn(state.Snapshot);
        return result;
    }

    private static JsonArray CaptureTravelers(object gamePhase)
    {
        var result = new JsonArray();
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state is null)
            return result;

        foreach (var traveler in state.Players
                     .Where(AlchemyCombatState.IsTraveler)
                     .OrderBy(static traveler => traveler.NetId))
        {
            result.Add(traveler.Creature.CombatState is null
                ? CaptureTravelerRosterEntry(traveler, gamePhase)
                : Capture(traveler, gamePhase));
        }
        return result;
    }

    private static JsonObject CaptureTravelerRosterEntry(Player player, object gamePhase) => new()
    {
        ["schemaVersion"] = 2,
        ["playerNetId"] = player.NetId.ToString(),
        ["gamePhase"] = gamePhase.ToString(),
        ["characterId"] = player.Character.Id.Entry,
        ["inCombat"] = false,
        ["combatStateAttached"] = false,
    };

    private static JsonObject CapturePrinciples(Player player)
    {
        var principles = new JsonObject();
        foreach (var definition in AlchemyPrinciples.All)
        {
            principles[definition.LocalId] = new JsonObject
            {
                ["id"] = definition.Id,
                ["amount"] = AlchemyPrinciples.Get(player, definition),
                ["max"] = STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCmd.GetMax(
                    player,
                    definition.Id),
            };
        }
        return principles;
    }

    private static JsonObject CapturePlayerCombat(Player player) => new()
    {
        ["baseMaxEnergy"] = player.MaxEnergy,
        ["effectiveMaxEnergy"] = player.PlayerCombatState?.MaxEnergy,
        ["currentEnergy"] = player.PlayerCombatState?.Energy,
        ["turnNumber"] = player.PlayerCombatState?.TurnNumber,
        ["phase"] = player.PlayerCombatState?.Phase.ToString(),
    };

    private static JsonObject CaptureBackpack(Player player)
    {
        try
        {
            var cards = new JsonArray();
            var potions = AlchemyBackpack.GetPotions(player);
            for (var index = 0; index < potions.Count; index++)
            {
                var potion = potions[index];
                cards.Add(new JsonObject
                {
                    ["index"] = index,
                    ["cardId"] = potion.Id.Entry,
                    ["family"] = potion.Family.ToString(),
                    ["quality"] = potion.Quality.ToString(),
                    ["upgraded"] = potion.IsUpgraded,
                    ["origin"] = potion.Origin.ToString(),
                    ["mainPrincipleId"] = potion.MainPrinciple.Id,
                });
            }

            return new JsonObject
            {
                ["attached"] = true,
                ["capacity"] = AlchemyBackpack.GetCapacity(player),
                ["maximumQuality"] = AlchemyBackpack.GetMaximumQuality(player).ToString(),
                ["count"] = potions.Count,
                ["cards"] = cards,
            };
        }
        catch (Exception exception)
        {
            return new JsonObject
            {
                ["attached"] = false,
                ["error"] = exception.Message,
                ["cards"] = new JsonArray(),
            };
        }
    }

    private static JsonObject CapturePiles(Player player)
    {
        var piles = new JsonObject();
        foreach (var pileType in new[]
                 {
                     PileType.Hand,
                     PileType.Draw,
                     PileType.Discard,
                     PileType.Exhaust,
                     PileType.Play,
                 })
        {
            var cards = new JsonArray();
            var pile = pileType.GetPile(player);
            if (pile is not null)
            {
                var index = 0;
                foreach (var card in pile.Cards)
                {
                    var item = new JsonObject
                    {
                        ["index"] = index++,
                        ["cardId"] = card.Id.Entry,
                        ["upgradeLevel"] = card.CurrentUpgradeLevel,
                        ["cardType"] = card.Type.ToString(),
                        ["costForTurn"] = TryGetCost(card),
                    };
                    if (card is AlchemyPotionCard potion)
                    {
                        item["family"] = potion.Family.ToString();
                        item["quality"] = potion.Quality.ToString();
                        item["upgraded"] = potion.IsUpgraded;
                        item["origin"] = potion.Origin.ToString();
                    }
                    cards.Add(item);
                }
            }
            piles[pileType.ToString().ToLowerInvariant()] = cards;
        }
        return piles;
    }

    private static JsonArray CaptureCombatants(Player player)
    {
        var result = new JsonArray();
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return result;

        foreach (var creature in combatState.Creatures.OrderBy(static creature => creature.CombatId))
        {
            result.Add(new JsonObject
            {
                ["combatId"] = creature.CombatId,
                ["side"] = creature.Side.ToString(),
                ["currentHp"] = creature.CurrentHp,
                ["maxHp"] = creature.MaxHp,
                ["block"] = creature.Block,
                ["isAlive"] = creature.IsAlive,
                ["isPlayer"] = creature.IsPlayer,
                ["powers"] = CapturePowers(creature),
            });
        }
        return result;
    }

    private static JsonArray CapturePowers(Creature creature)
    {
        var result = new JsonArray();
        foreach (var power in creature.Powers.OrderBy(static power => power.Id.Entry))
        {
            var item = new JsonObject
            {
                ["id"] = power.Id.Entry,
                ["type"] = power.GetType().Name,
                ["amount"] = power.Amount,
            };
            if (power is AttackAmplificationPower or BlockAmplificationPower)
            {
                item["charges30"] = ReadPrivateCounter(power, "_charges30");
                item["charges50"] = ReadPrivateCounter(power, "_charges50");
            }
            result.Add(item);
        }
        return result;
    }

    private static int ReadPrivateCounter(PowerModel power, string fieldName) =>
        power.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(power) as int? ?? 0;

    private static int? TryGetCost(CardModel card)
    {
        try
        {
            return card.EnergyCost.GetWithModifiers(CostModifiers.All);
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject CaptureTurn(AlchemyTurnState turn) => new()
    {
        ["experiments"] = turn.Experiments.ToString(),
        ["experimentCount"] = turn.ExperimentCount,
        ["hasBrewedOrUsedOriginalPotion"] = turn.HasBrewedOrUsedOriginalPotion,
        ["nextFormulaEnergyDiscount"] = turn.NextFormulaEnergyDiscount,
        ["prePurificationCharges"] = turn.PrePurificationCharges,
        ["productionBoostCatalysisSnapshot"] = turn.ProductionBoostCatalysisSnapshot,
        ["latestProduction"] = CaptureProduction(turn.LatestProduction),
        ["pendingDiffusion"] = turn.PendingDiffusion.ToString(),
        ["latestOriginalPotion"] = CapturePotionResolution(turn.LatestOriginalPotion),
        ["productionFormulaFetchTriggered"] = turn.ProductionFormulaFetchTriggered,
        ["diffusionRewardTriggered"] = turn.DiffusionRewardTriggered,
    };

    private static JsonNode? CaptureProduction(ProductionSnapshot? production)
    {
        if (production is null)
            return null;

        var resources = new JsonArray();
        foreach (var resource in production.Resources)
        {
            resources.Add(new JsonObject
            {
                ["resourceId"] = resource.ResourceId,
                ["amount"] = resource.Amount,
            });
        }
        return new JsonObject
        {
            ["energy"] = production.Energy,
            ["resources"] = resources,
        };
    }

    private static JsonNode? CapturePotionResolution(PotionResolutionSnapshot? resolution)
    {
        if (resolution is null)
            return null;

        var targets = new JsonArray();
        foreach (var combatId in resolution.Targets.CombatIds)
            targets.Add(combatId);

        return new JsonObject
        {
            ["family"] = resolution.Descriptor.Family.ToString(),
            ["quality"] = resolution.Descriptor.Quality.ToString(),
            ["upgraded"] = resolution.Descriptor.IsUpgraded,
            ["origin"] = resolution.Descriptor.Origin.ToString(),
            ["targetCombatIds"] = targets,
        };
    }
}