using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.Payment;

public abstract class DynamicBackpackPaymentCard : ModCardTemplate
{
    private const string DynamicPrinciplePaymentUseId =
        "dimensional_traveler_dynamic_backpack_principle";

    private AlchemyPotionCard? _preparedPotion;

    protected DynamicBackpackPaymentCard(
        int energyCost,
        CardRarity rarity,
        BackpackTransition transition,
        int principleCost)
        : base(energyCost, CardType.Skill, rarity, TargetType.Self)
    {
        Transition = transition;
        PrincipleCost = principleCost;
    }

    internal BackpackTransition Transition { get; }

    internal int PrincipleCost { get; }

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        BackpackFlow.CanStart(Transition, Owner, this, requireAffordable: true);

    internal async Task<bool> PrepareManualPayment(PlayerChoiceContext choiceContext)
    {
        var selected = await BackpackFlow.Select(
            Transition,
            choiceContext,
            Owner,
            this,
            SelectionScreenPrompt,
            requireAffordable: true);
        if (selected is null
            || !BackpackFlow.CanCommit(Transition, Owner, this, selected, requireAffordable: true))
        {
            return false;
        }

        _preparedPotion = selected;
        this.SecondaryResourceUses().Require(
            DynamicPrinciplePaymentUseId,
            selected.MainPrinciple.Id,
            new SecondaryResourceCost(PrincipleCost),
            SecondaryResourceCostDuration.UntilPlayed);
        return true;
    }

    internal bool CanCommitPreparedPayment() =>
        _preparedPotion is not null
        && BackpackFlow.CanCommit(
            Transition,
            Owner,
            this,
            _preparedPotion,
            requireAffordable: true);

    internal void ClearPreparedPayment()
    {
        _preparedPotion = null;
        if (this.TryGetSecondaryResourceUses(out var uses))
            uses.Clear(DynamicPrinciplePaymentUseId);
    }

    protected sealed override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var selected = _preparedPotion;
        if (selected is null)
        {
            selected = await BackpackFlow.Select(
                Transition,
                choiceContext,
                Owner,
                this,
                SelectionScreenPrompt,
                requireAffordable: false);
        }

        if (selected is null
            || !BackpackFlow.CanCommit(
                Transition,
                Owner,
                this,
                selected,
                requireAffordable: false))
        {
            return;
        }

        if (!await BackpackFlow.CommitPaid(Transition, this, selected))
        {
            throw new InvalidOperationException(
                $"{Id.Entry} 已完成原子支付，但目标药剂在提交效果时失效。");
        }
    }
}

[HarmonyPatch(typeof(PlayCardAction), "ExecuteAction")]
internal static class DynamicBackpackPaymentPlayCardPatch
{
    private static readonly PropertyInfo PlayerChoiceContextProperty =
        typeof(PlayCardAction).GetProperty(nameof(PlayCardAction.PlayerChoiceContext))
        ?? throw new MissingMemberException(typeof(PlayCardAction).FullName, nameof(PlayCardAction.PlayerChoiceContext));

    [HarmonyPrefix]
    private static bool Prefix(PlayCardAction __instance, ref Task __result)
    {
        var card = __instance.NetCombatCard.ToCardModelOrNull();
        if (card is not DynamicBackpackPaymentCard dynamicCard)
            return true;

        __result = Execute(__instance, dynamicCard);
        return false;
    }

    private static async Task Execute(
        PlayCardAction action,
        DynamicBackpackPaymentCard card)
    {
        NCardPlayQueue.Instance?.UpdateCardBeforeExecution(action);
        var combatState = action.Player.Creature.CombatState;
        if (combatState is null || card.Pile?.Type != PileType.Hand)
        {
            CancelQueuedCard(action);
            return;
        }

        var target = await combatState.GetCreatureAsync(action.TargetId, 10.0);
        if (!card.CanPlay(out _, out _) || !card.IsValidTarget(target))
        {
            CancelQueuedCard(action);
            return;
        }

        var choiceContext = new GameActionPlayerChoiceContext(action);
        PlayerChoiceContextProperty.SetValue(action, choiceContext);
        var paymentCommitted = false;
        try
        {
            if (!await card.PrepareManualPayment(choiceContext)
                || !card.CanCommitPreparedPayment()
                || !card.CanPlay(out _, out _)
                || !card.IsValidTarget(target))
            {
                CancelQueuedCard(action);
                return;
            }

            var (energySpent, starsSpent) = await card.SpendResources();
            paymentCommitted = true;
            await card.OnPlayWrapper(
                choiceContext,
                target,
                isAutoPlay: false,
                new ResourceInfo
                {
                    EnergySpent = energySpent,
                    EnergyValue = energySpent,
                    StarsSpent = starsSpent,
                    StarValue = starsSpent,
                });
        }
        finally
        {
            card.ClearPreparedPayment();
            if (!paymentCommitted && card.Pile?.Type == PileType.Hand)
                CancelQueuedCard(action);
        }
    }

    private static void CancelQueuedCard(PlayCardAction action) =>
        NCardPlayQueue.Instance?.RemoveCardFromQueueForCancellation(action);
}