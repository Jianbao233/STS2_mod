using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.Resolution;

public static class PotionResolution
{
    public static async Task<PotionResolutionResult> ResolvePlayedPotion(
        PlayerChoiceContext choiceContext,
        AlchemyPotionCard potion,
        CardPlay cardPlay)
    {
        var initialTarget = cardPlay.Target ?? potion.Owner.Creature;
        var state = AlchemyCombatState.Require(potion.Owner);
        var turn = state.Snapshot;
        var origin = potion.Origin;
        var diffusion = origin == PotionOrigin.Original
            ? turn.PendingDiffusion
            : DiffusionMode.None;
        if (origin == PotionOrigin.Original)
            state.Update(static turnState => turnState.PendingDiffusion = DiffusionMode.None);

        IReadOnlyList<uint> frozenIds;
        var resolvedIds = new List<uint>();
        if (diffusion == DiffusionMode.AdditionalTarget)
        {
            var initialTargetId = RequireCombatId(initialTarget);
            if (await ResolveFrozenTarget(choiceContext, potion, cardPlay, initialTargetId))
                resolvedIds.Add(initialTargetId);

            var candidates = GetSameSideTargets(initialTarget)
                .Where(target => target != initialTarget)
                .ToArray();
            var additional = await AlchemyTargetChoices.ChooseAdditionalTarget(
                choiceContext,
                potion.Owner,
                candidates);
            frozenIds = additional is null
                ? [initialTargetId]
                : [initialTargetId, RequireCombatId(additional)];

            if (additional is not null
                && await ResolveFrozenTarget(
                    choiceContext,
                    potion,
                    cardPlay,
                    RequireCombatId(additional)))
            {
                resolvedIds.Add(RequireCombatId(additional));
            }
        }
        else
        {
            var targets = diffusion == DiffusionMode.WholeSide
                ? GetSameSideTargets(initialTarget)
                : [initialTarget];
            frozenIds = FreezeTargets(targets);
            foreach (var combatId in frozenIds)
            {
                if (await ResolveFrozenTarget(choiceContext, potion, cardPlay, combatId))
                    resolvedIds.Add(combatId);
            }
        }

        var frozenSnapshot = new TargetSnapshot(frozenIds.ToArray());
        var resolvedSnapshot = new TargetSnapshot(resolvedIds.Distinct().ToArray());
        var descriptor = potion.Describe();
        var result = new PotionResolutionResult(
            descriptor,
            frozenSnapshot,
            resolvedSnapshot,
            diffusion);
        if (origin == PotionOrigin.Original)
        {
            state.Update(turnState =>
            {
                turnState.Record(ExperimentRecord.UsedOriginalPotion);
                turnState.LatestOriginalPotion = new PotionResolutionSnapshot(
                    descriptor,
                    frozenSnapshot.Copy());
            });
            await NotifyOriginalPotionListeners(choiceContext, potion.Owner, result, potion);
        }

        return result;
    }

    public static bool CanReplayLatest(Player player)
    {
        var snapshot = AlchemyCombatState.Require(player).Snapshot.LatestOriginalPotion;
        var combatState = player.Creature.CombatState;
        return snapshot is not null
            && combatState is not null
            && snapshot.Targets.CombatIds.Any(
                combatId => IsValidFrozenTarget(combatState.GetCreature(combatId)));
    }

    public static async Task<PotionResolutionResult> ReplayLatest(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel source)
    {
        var snapshot = AlchemyCombatState.Require(player).Snapshot.LatestOriginalPotion
            ?? throw new InvalidOperationException("本回合没有可重放的原始药剂目标快照。");
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("回响重放只能在战斗中执行。");
        var potion = (AlchemyPotionCard)combatState.CreateCard(
            PotionCatalog.GetCanonical(snapshot.Descriptor.Family, snapshot.Descriptor.Quality),
            player);
        if (snapshot.Descriptor.IsUpgraded)
            MegaCrit.Sts2.Core.Commands.CardCmd.Upgrade(
                potion,
                MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
        potion.SetOrigin(PotionOrigin.EchoDerived);

        try
        {
            var resolvedIds = new List<uint>();
            foreach (var combatId in snapshot.Targets.CombatIds)
            {
                var target = combatState.GetCreature(combatId);
                if (!IsValidFrozenTarget(target))
                    continue;
                await potion.ResolveSingleTarget(
                    choiceContext,
                    target!,
                    CreateDerivedCardPlay(potion, target!));
                resolvedIds.Add(combatId);
            }

            return new PotionResolutionResult(
                potion.Describe(),
                snapshot.Targets.Copy(),
                new TargetSnapshot(resolvedIds.ToArray()),
                DiffusionMode.None);
        }
        finally
        {
            potion.RemoveFromState();
        }
    }

    private static IReadOnlyList<Creature> GetSameSideTargets(Creature initialTarget)
    {
        var combatState = initialTarget.CombatState
            ?? throw new InvalidOperationException("药剂目标不在活动战斗中。");
        return combatState
            .GetCreaturesOnSide(initialTarget.Side)
            .Where(IsValidFrozenTarget)
            .OrderBy(static target => target.CombatId)
            .ToArray();
    }

    private static uint[] FreezeTargets(IEnumerable<Creature> targets) =>
        targets
            .Select(RequireCombatId)
            .Distinct()
            .ToArray();

    private static uint RequireCombatId(Creature target) =>
        target.CombatId
        ?? throw new InvalidOperationException("药剂目标缺少稳定 CombatId，不能提交冻结目标快照。");

    private static bool IsValidFrozenTarget(Creature? target) =>
        target is { IsAlive: true, IsPet: false, CombatState: not null }
        && target.CombatState.ContainsCreature(target);

    private static async Task<bool> ResolveFrozenTarget(
        PlayerChoiceContext choiceContext,
        AlchemyPotionCard potion,
        CardPlay originalPlay,
        uint combatId)
    {
        var combatState = potion.CombatState
            ?? throw new InvalidOperationException("药剂不在活动战斗中。");
        var target = combatState.GetCreature(combatId);
        if (!IsValidFrozenTarget(target))
            return false;

        await potion.ResolveSingleTarget(
            choiceContext,
            target!,
            CloneCardPlay(originalPlay, target!));
        return true;
    }

    private static CardPlay CloneCardPlay(CardPlay source, Creature target) => new()
    {
        Card = source.Card,
        Player = source.Player,
        Target = target,
        ResultPile = source.ResultPile,
        Resources = source.Resources,
        IsAutoPlay = source.IsAutoPlay,
        PlayIndex = source.PlayIndex,
        PlayCount = source.PlayCount,
    };

    private static CardPlay CreateDerivedCardPlay(AlchemyPotionCard potion, Creature target) => new()
    {
        Card = potion,
        Player = potion.Owner,
        Target = target,
        ResultPile = PileType.None,
        Resources = new ResourceInfo
        {
            EnergySpent = 0,
            EnergyValue = 0,
            StarsSpent = 0,
            StarValue = 0,
        },
        IsAutoPlay = true,
        PlayIndex = 0,
        PlayCount = 1,
    };

    private static async Task NotifyOriginalPotionListeners(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("原始药剂结算只能在战斗中通知监听器。");
        foreach (var listener in combatState
                     .IterateHookListeners()
                     .OfType<IAlchemyPotionResolutionListener>()
                     .ToArray())
        {
            await listener.AfterOriginalPotionResolved(choiceContext, player, result, source);
        }
    }
}