using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Potions;
using STS2RitsuLib.Cards;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Events;
using DimensionalTraveler.Alchemy.Resolution;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Formulas;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Relics;

internal static class RelicTriggerState
{
    public static bool TryConsume(Player player, RelicTurnTrigger trigger)
    {
        var state = AlchemyCombatState.Require(player);
        var turn = state.Snapshot;
        if (turn.RelicTriggers.HasFlag(trigger))
            return false;

        state.Update(current => current.RelicTriggers |= trigger);
        return true;
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "ORIGINAL_BREW_DRAW")]
public sealed class OriginalBrewDraw : ModRelicTemplate, IAlchemyOriginalPotionBrewListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("INK_BOTTLE");

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public async Task AfterOriginalPotionBrewed(Player player, AlchemyPotionCard potion, AbstractModel? source)
    {
        if (source is not AlchemyFormulaCard
            || player != Owner
            || !RelicTriggerState.TryConsume(player, RelicTurnTrigger.BrewDraw))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, player);
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "QUALITY_UPGRADE_REFUND")]
public sealed class QualityUpgradeRefund : ModRelicTemplate, IAlchemyExistingPotionQualityListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("MANGO");

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public async Task AfterExistingPotionQualityChanged(Player player, AlchemyPotionCard potion, AbstractModel source)
    {
        if (player != Owner || !RelicTriggerState.TryConsume(player, RelicTurnTrigger.QualityRefund))
            return;

        Flash();
        await AlchemyPrinciples.Gain(player, potion.MainPrinciple, 1, this);
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "ORIGINAL_POTION_REFUND")]
public sealed class OriginalPotionRefund : ModRelicTemplate, IAlchemyPotionResolutionListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("BIRD_FACED_URN");

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public async Task AfterOriginalPotionResolved(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source)
    {
        if (player != Owner || !RelicTriggerState.TryConsume(player, RelicTurnTrigger.OriginalPotionRefund))
            return;

        var amount = result.FrozenTargets.CombatIds.FirstOrDefault() == player.Creature.CombatId ? 1 : 2;
        var principle = PotionMainPrinciples.For(result.Descriptor.Family);
        Flash();
        await AlchemyPrinciples.Gain(player, principle, amount, this);
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "CATALYSIS_REFUND")]
public sealed class CatalysisRefund : ModRelicTemplate, ISecondaryResourceHookListener, ICardOnPlayHookListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("MUMMIFIED_HAND");

    public override RelicRarity Rarity => RelicRarity.Rare;

    public Task AfterSecondaryResourceSpent(SecondaryResourceSpendContext context)
    {
        if (context.Player != Owner
            || context.Definition.Id != AlchemyPrinciples.Catalysis.Id
            || context.Card is null
            || context.Amount <= 0
            || AlchemyCombatState.Require(context.Player).Snapshot.RelicTriggers
                .HasFlag(RelicTurnTrigger.CatalysisRefund))
        {
            return Task.CompletedTask;
        }

        if (!NetCombatCardDb.Instance.TryGetCardId(context.Card, out var cardNetId))
            return Task.CompletedTask;

        AlchemyCombatState.Require(context.Player).Update(turn =>
            turn.PendingCatalysisPayment = new CatalysisPaymentReceipt(
                cardNetId,
                context.Card.Id.Entry,
                context.Amount));
        return Task.CompletedTask;
    }

    public async Task AfterCardOnPlay(AfterCardOnPlayContext context)
    {
        if (!context.OriginalOnPlayRan || context.CardPlay.Card.Owner != Owner)
            return;

        var state = AlchemyCombatState.Require(Owner);
        var receipt = state.Snapshot.PendingCatalysisPayment;
        if (receipt is not { Amount: > 0 }
            || !NetCombatCardDb.Instance.TryGetCardId(context.CardPlay.Card, out var cardNetId)
            || receipt.Value.CardNetId != cardNetId
            || !RelicTriggerState.TryConsume(Owner, RelicTurnTrigger.CatalysisRefund))
        {
            return;
        }

        state.Update(static turn => turn.PendingCatalysisPayment = null);
        Flash();
        await AlchemyPrinciples.Gain(Owner, AlchemyPrinciples.Catalysis, 1, this);
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "DIFFUSION_REFUND")]
public sealed class DiffusionRefund : ModRelicTemplate, IAlchemyPotionResolutionListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("STRANGE_SPOON");

    public override RelicRarity Rarity => RelicRarity.Rare;

    public async Task AfterOriginalPotionResolved(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source)
    {
        if (player != Owner
            || result.DiffusionMode == DiffusionMode.None
            || !result.AffectedMultipleTargets
            || !RelicTriggerState.TryConsume(player, RelicTurnTrigger.DiffusionRefund))
        {
            return;
        }

        Flash();
        await AlchemyPrinciples.Gain(player, AlchemyPrinciples.Diffusion, 1, this);
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "ORIGINAL_POTION_ECHO")]
public sealed class OriginalPotionEcho : ModRelicTemplate, IAlchemyPotionResolutionListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("BRANCH_OF_BURNING_BLOOD");

    public override RelicRarity Rarity => RelicRarity.Rare;

    public async Task AfterOriginalPotionResolved(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source)
    {
        if (player != Owner || !RelicTriggerState.TryConsume(player, RelicTurnTrigger.EchoGain))
            return;

        Flash();
        await AlchemyPrinciples.Gain(player, AlchemyPrinciples.Echo, 1, this);
    }
}