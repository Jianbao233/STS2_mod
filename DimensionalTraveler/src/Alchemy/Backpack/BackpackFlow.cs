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
        Func<Player, CardModel, IReadOnlyList<AlchemyPotionCard>> GetCandidates,
        Func<Player, CardModel, AlchemyPotionCard, bool> CanCommit,
        Func<CardModel, AlchemyPotionCard, Task<bool>> Commit);

    private static readonly IReadOnlyDictionary<BackpackTransition, TransitionSpec> Transitions =
        new Dictionary<BackpackTransition, TransitionSpec>
        {
            [BackpackTransition.Retrieve] = new(
                GetRetrievalCandidates,
                CanRetrieve,
                Retrieve),
            [BackpackTransition.Purify] = new(
                static (player, _) => AlchemyBackpack.GetPurificationCandidates(player),
                static (player, _, potion) =>
                    potion.Pile?.Type == AlchemyBackpack.PileType
                    && !potion.IsUpgraded
                    && Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 1),
                static (source, potion) => AlchemyBackpack.Purify(potion, source)),
            [BackpackTransition.Sublimate] = new(
                static (player, _) => AlchemyBackpack.GetSublimationCandidates(player),
                static (player, _, potion) =>
                    potion.Pile?.Type == AlchemyBackpack.PileType
                    && potion.Quality == PotionQuality.Normal
                    && Resources.AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 2),
                static async (source, potion) =>
                    await AlchemyBackpack.Sublimate(potion, source) is not null),
        };

    public static bool CanStart(BackpackTransition transition, Player player, CardModel source) =>
        Resolve(transition).GetCandidates(player, source).Count > 0;

    public static async Task<BackpackFlowResult> Execute(
        BackpackTransition transition,
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel source,
        LocString selectionPrompt)
    {
        var spec = Resolve(transition);
        var candidates = spec.GetCandidates(player, source);
        if (candidates.Count == 0)
            return new BackpackFlowResult(BackpackFlowPhase.NoCandidates);

        var selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates.Cast<CardModel>().ToArray(),
                player,
                new CardSelectorPrefs(selectionPrompt, 1)))
            .OfType<AlchemyPotionCard>()
            .FirstOrDefault();
        if (selected is null)
            return new BackpackFlowResult(BackpackFlowPhase.SelectionCanceled);

        if (!spec.CanCommit(player, source, selected))
            return new BackpackFlowResult(BackpackFlowPhase.SelectionInvalidated, selected);

        var committed = await spec.Commit(source, selected);
        return new BackpackFlowResult(
            committed ? BackpackFlowPhase.Committed : BackpackFlowPhase.SelectionInvalidated,
            selected);
    }

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