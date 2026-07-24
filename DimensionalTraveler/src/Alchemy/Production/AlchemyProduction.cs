using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Alchemy.Production;

public static class AlchemyProduction
{
    public static async Task<ProductionSnapshot> Execute(
        Player player,
        CardModel source,
        ProductionPlan plan,
        ProductionKind kind = ProductionKind.Other,
        bool derived = false)
    {
        var state = AlchemyCombatState.Require(player);
        var turn = state.Snapshot;
        var requested = derived
            ? ToSnapshot(plan)
            : BuildFinalSnapshot(player, turn, plan, kind);
        var actualResources = new List<ResourceProduction>(requested.Resources.Count);
        foreach (var item in requested.Resources.Where(static item => item.Amount > 0))
        {
            var before = SecondaryResourceCmd.Get(player, item.ResourceId);
            var after = await SecondaryResourceCmd.Gain(player, item.ResourceId, item.Amount, source);
            var actualAmount = Math.Max(0, after - before);
            if (actualAmount > 0)
                actualResources.Add(new ResourceProduction(item.ResourceId, actualAmount));
        }

        var playerCombatState = player.PlayerCombatState
            ?? throw new InvalidOperationException("显式生产只能由处于战斗中的玩家执行。");
        var energyBefore = playerCombatState.Energy;
        if (requested.Energy > 0)
            await PlayerCmd.GainEnergy(requested.Energy, player);

        var actual = new ProductionSnapshot(
            actualResources.ToArray(),
            Math.Max(0, playerCombatState.Energy - energyBefore));
        if (derived)
            return actual;

        state.Update(turnState =>
        {
            turnState.ProductionBoostCatalysisSnapshot = null;
            turnState.LatestProduction = actual.Copy();
        });
        await NotifyListeners(player, actual, source);
        return actual;
    }

    public static Task<ProductionSnapshot> RepeatLatest(Player player, CardModel source)
    {
        var snapshot = AlchemyCombatState.Require(player).Snapshot.LatestProduction
            ?? throw new InvalidOperationException("本回合没有可重复的显式生产快照。");
        return Execute(
            player,
            source,
            new ProductionPlan(snapshot.Resources, snapshot.Energy),
            derived: true);
    }

    private static ProductionSnapshot BuildFinalSnapshot(
        Player player,
        AlchemyTurnState turn,
        ProductionPlan plan,
        ProductionKind kind)
    {
        var catalyst = turn.ProductionBoostCatalysisSnapshot
            ?? AlchemyPrinciples.Get(player, AlchemyPrinciples.Catalysis);
        var multiplier = turn.ProductionBoostCatalysisSnapshot.HasValue ? 2 : 1;

        var amounts = plan.Resources
            .GroupBy(static item => item.ResourceId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group => group.Sum(static item => item.Amount) * multiplier,
                StringComparer.Ordinal);

        ApplyCatalysisPassive(amounts, kind, catalyst);

        return new ProductionSnapshot(
            amounts
                .Where(static pair => pair.Value > 0)
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new ResourceProduction(pair.Key, pair.Value))
                .ToArray(),
            plan.Energy * multiplier);
    }

    private static ProductionSnapshot ToSnapshot(ProductionPlan plan) => new(
        plan.Resources
            .Where(static item => item.Amount > 0)
            .GroupBy(static item => item.ResourceId, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new ResourceProduction(group.Key, group.Sum(static item => item.Amount)))
            .ToArray(),
        Math.Max(0, plan.Energy));

    private static void ApplyCatalysisPassive(
        IDictionary<string, int> amounts,
        ProductionKind kind,
        int catalyst)
    {
        if (catalyst <= 0)
            return;

        if (kind == ProductionKind.DirectedBasic)
        {
            var bonus = catalyst >= 3 ? 2 : 1;
            var basicResource = amounts.Keys.SingleOrDefault(IsBasicResource);
            if (basicResource is not null)
                amounts[basicResource] += bonus;
            return;
        }

        if (kind != ProductionKind.FlexibleBasic || catalyst < 2)
            return;

        foreach (var resource in AlchemyPrinciples.Basic)
        {
            if (amounts.ContainsKey(resource.Id))
                amounts[resource.Id] += 1;
        }
    }

    private static bool IsBasicResource(string resourceId) =>
        AlchemyPrinciples.Basic.Any(resource => resource.Id == resourceId);

    private static async Task NotifyListeners(
        Player player,
        ProductionSnapshot snapshot,
        CardModel source)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("显式生产只能在战斗中通知监听器。");
        foreach (var listener in combatState
                     .IterateHookListeners()
                     .OfType<IAlchemyProductionListener>()
                     .ToArray())
        {
            await listener.AfterExplicitProduction(player, snapshot, source);
        }
    }
}