using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using DimensionalTraveler.Content.Cards.System;

namespace DimensionalTraveler.Alchemy.Backpack;

public static class SystemCardProtection
{
    internal static bool IsSystemCard(CardModel card) => card is PotionSatchel;

    internal static bool IsProtected(CardModel card) =>
        IsSystemCard(card)
        && CombatManager.Instance.IsInProgress
        && !CombatManager.Instance.IsOverOrEnding
        && !card.Owner.Creature.IsDead;

    internal static bool IsLegalPileTransition(CardModel card, CardPile targetPile)
    {
        if (!IsProtected(card))
            return true;

        var sourceType = card.Pile?.Type ?? PileType.None;
        return (sourceType, targetPile.Type) switch
        {
            (PileType.None, PileType.Hand) => true,
            (PileType.Hand, PileType.Hand or PileType.Play) => true,
            (PileType.Play, PileType.Play or PileType.Hand) => true,
            _ => false,
        };
    }

    internal static CardPileAddResult RejectedMove(CardModel card) => new()
    {
        success = false,
        cardAdded = card,
        oldPile = card.Pile,
        modifyingModels = null,
    };

    internal static async Task<IReadOnlyList<CardPileAddResult>> AddAllowedCards(
        IReadOnlyList<CardModel> requestedCards,
        CardPile targetPile,
        CardPilePosition position,
        AbstractModel? clonedBy,
        bool skipVisuals,
        bool isChangingOwners)
    {
        var allowedCards = requestedCards
            .Where(card => IsLegalPileTransition(card, targetPile))
            .ToArray();
        var allowedResults = allowedCards.Length == 0
            ? []
            : (await CardPileCmd.Add(
                allowedCards,
                targetPile,
                position,
                clonedBy,
                skipVisuals,
                isChangingOwners)).ToArray();

        var resultQueue = new Queue<CardPileAddResult>(allowedResults);
        var results = new List<CardPileAddResult>(requestedCards.Count);
        foreach (var card in requestedCards)
        {
            if (IsLegalPileTransition(card, targetPile))
            {
                results.Add(resultQueue.Dequeue());
                continue;
            }

            if (card.Pile?.Type == PileType.Play)
                await CardPileCmd.Add(card, PileType.Hand, skipVisuals: skipVisuals);
            results.Add(RejectedMove(card));
        }

        return results;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsTransformable), MethodType.Getter)]
internal static class SystemCardTransformProtectionPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (SystemCardProtection.IsSystemCard(__instance))
            __result = false;
    }
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
internal static class SystemCardHandSelectionProtectionPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref Func<CardModel, bool>? filter)
    {
        var originalFilter = filter;
        filter = card =>
            !SystemCardProtection.IsSystemCard(card)
            && (originalFilter?.Invoke(card) ?? true);
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
internal static class SystemCardDiscardProtectionPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        ref IEnumerable<CardModel> cardsToDiscard,
        ref int cardsToDraw)
    {
        var requestedCards = cardsToDiscard.ToArray();
        var allowedCards = requestedCards
            .Where(card => !SystemCardProtection.IsProtected(card))
            .ToArray();

        if (cardsToDraw == requestedCards.Length)
            cardsToDraw = allowedCards.Length;
        cardsToDiscard = allowedCards;
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust))]
internal static class SystemCardExhaustProtectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CardModel card, ref Task __result)
    {
        if (!SystemCardProtection.IsProtected(card))
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add),
    typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition),
    typeof(AbstractModel), typeof(bool), typeof(bool))]
internal static class SystemCardPileTransitionProtectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        IEnumerable<CardModel> cards,
        CardPile newPile,
        CardPilePosition position,
        AbstractModel? clonedBy,
        bool skipVisuals,
        bool isChangingOwners,
        ref Task<IReadOnlyList<CardPileAddResult>> __result)
    {
        var requestedCards = cards.ToArray();
        if (requestedCards.All(card => SystemCardProtection.IsLegalPileTransition(card, newPile)))
            return true;

        __result = SystemCardProtection.AddAllowedCards(
            requestedCards,
            newPile,
            position,
            clonedBy,
            skipVisuals,
            isChangingOwners);
        return false;
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromCombat),
    typeof(IEnumerable<CardModel>), typeof(bool))]
internal static class SystemCardCombatRemovalProtectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        IEnumerable<CardModel> cards,
        bool skipVisuals,
        ref Task __result)
    {
        var requestedCards = cards.ToArray();
        var allowedCards = requestedCards
            .Where(card => !SystemCardProtection.IsProtected(card))
            .ToArray();
        if (allowedCards.Length == requestedCards.Length)
            return true;

        __result = allowedCards.Length == 0
            ? Task.CompletedTask
            : CardPileCmd.RemoveFromCombat(allowedCards, skipVisuals);
        return false;
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.GiveToAnotherPlayer))]
internal static class SystemCardOwnershipProtectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CardModel card, ref Task __result)
    {
        if (!SystemCardProtection.IsProtected(card))
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}