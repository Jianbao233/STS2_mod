using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Cards.System;

namespace DimensionalTraveler.Alchemy.Backpack;

public enum BackpackTransition
{
    Retrieve,
    Purify,
    Sublimate,
    Masterpiece,
}

public enum BackpackFlowPhase
{
    NoCandidates,
    SelectionCanceled,
    SelectionInvalidated,
    Committed,
}

public readonly record struct BackpackFlowResult(
    BackpackFlowPhase Phase,
    AlchemyPotionCard? Selected = null);

public static class BackpackFlow
{
    private sealed record TransitionSpec(
        Func<Player, CardModel, bool, IReadOnlyList<AlchemyPotionCard>> GetCandidates,
        Func<Player, CardModel, AlchemyPotionCard, bool, bool> CanCommit,
        Func<CardModel, AlchemyPotionCard, Task<bool>> Commit);

    private static readonly IReadOnlyDictionary<BackpackTransition, TransitionSpec> Transitions =
        new Dictionary<BackpackTransition, TransitionSpec>
        {
            [BackpackTransition.Retrieve] = new(
                static (player, source, _) => GetRetrievalCandidates(player, source),
                static (player, source, potion, _) => CanRetrieve(player, source, potion),
                Retrieve),
            [BackpackTransition.Purify] = new(
                static (player, _, requireAffordable) => AlchemyBackpack.GetPotions(player)
                    .Where(potion =>
                        !potion.IsUpgraded
                        && (!requireAffordable
                            || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 1)))
                    .ToArray(),
                static (player, _, potion, requireAffordable) =>
                    potion.Pile?.Type == AlchemyBackpack.PileType
                    && !potion.IsUpgraded
                    && (!requireAffordable
                        || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 1)),
                static (_, potion) => AlchemyBackpack.CommitPurification(_, potion)),
            [BackpackTransition.Sublimate] = new(
                static (player, _, requireAffordable) => AlchemyBackpack.GetPotions(player)
                    .Where(potion =>
                        potion.Quality == PotionQuality.Normal
                        && (!requireAffordable
                            || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 2)))
                    .ToArray(),
                static (player, _, potion, requireAffordable) =>
                    potion.Pile?.Type == AlchemyBackpack.PileType
                    && potion.Quality == PotionQuality.Normal
                    && (!requireAffordable
                        || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 2)),
                static (source, potion) => AlchemyBackpack.CommitSublimation(source, potion)),
            [BackpackTransition.Masterpiece] = new(
                static (player, _, requireAffordable) => AlchemyBackpack.GetPotions(player)
                    .Where(potion =>
                        potion.Quality == PotionQuality.Refined
                        && (!requireAffordable
                            || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 4)))
                    .ToArray(),
                static (player, _, potion, requireAffordable) =>
                    potion.Pile?.Type == AlchemyBackpack.PileType
                    && potion.Quality == PotionQuality.Refined
                    && (!requireAffordable
                        || Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 4)),
                static (source, potion) => AlchemyBackpack.CommitMasterpiece(source, potion)),
        };

    public static bool CanStart(
        BackpackTransition transition,
        Player player,
        CardModel source,
        bool requireAffordable = false) =>
        Resolve(transition).GetCandidates(player, source, requireAffordable).Count > 0;

    public static async Task<BackpackFlowResult> Execute(
        BackpackTransition transition,
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel source,
        LocString selectionPrompt)
    {
        var selected = await Select(
            transition,
            choiceContext,
            player,
            source,
            selectionPrompt,
            requireAffordable: false);
        if (selected is null)
            return new BackpackFlowResult(BackpackFlowPhase.NoCandidates);

        if (!CanCommit(transition, player, source, selected, requireAffordable: false))
            return new BackpackFlowResult(BackpackFlowPhase.SelectionInvalidated, selected);

        var committed = await CommitPaid(transition, source, selected);
        return new BackpackFlowResult(
            committed ? BackpackFlowPhase.Committed : BackpackFlowPhase.SelectionInvalidated,
            selected);
    }

    public static async Task<AlchemyPotionCard?> Select(
        BackpackTransition transition,
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel source,
        LocString selectionPrompt,
        bool requireAffordable)
    {
        var candidates = Resolve(transition).GetCandidates(player, source, requireAffordable);
        if (candidates.Count == 0)
            return null;

        return (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates.Cast<CardModel>().ToArray(),
                player,
                new CardSelectorPrefs(selectionPrompt, 1)))
            .OfType<AlchemyPotionCard>()
            .FirstOrDefault();
    }

    public static bool CanCommit(
        BackpackTransition transition,
        Player player,
        CardModel source,
        AlchemyPotionCard potion,
        bool requireAffordable) =>
        Resolve(transition).CanCommit(player, source, potion, requireAffordable);

    public static Task<bool> CommitPaid(
        BackpackTransition transition,
        CardModel source,
        AlchemyPotionCard potion) =>
        Resolve(transition).Commit(source, potion);

    private static TransitionSpec Resolve(BackpackTransition transition) =>
        Transitions.TryGetValue(transition, out var spec)
            ? spec
            : throw new ArgumentOutOfRangeException(nameof(transition), transition, null);

    private static IReadOnlyList<AlchemyPotionCard> GetRetrievalCandidates(Player player, CardModel source) =>
        HasRetrievalCapacity(player, source)
            ? AlchemyBackpack.GetPotions(player)
            : [];

    private static bool CanRetrieve(Player player, CardModel source, AlchemyPotionCard potion) =>
        potion.Pile?.Type == AlchemyBackpack.PileType
        && HasRetrievalCapacity(player, source);

    private static bool HasRetrievalCapacity(Player player, CardModel source)
    {
        var hand = PileType.Hand.GetPile(player);
        if (hand is null)
            return false;

        var sourceMustReturnToHand = source is PotionSatchel && source.Pile?.Type == PileType.Play;
        var requiredSlots = sourceMustReturnToHand ? 2 : 1;
        return hand.Cards.Count <= CardPile.MaxCardsInHand - requiredSlots;
    }

    private static async Task<bool> Retrieve(CardModel _, AlchemyPotionCard potion)
    {
        var result = await CardPileCmd.Add(potion, PileType.Hand);
        return result.success && potion.Pile?.Type == PileType.Hand;
    }
}