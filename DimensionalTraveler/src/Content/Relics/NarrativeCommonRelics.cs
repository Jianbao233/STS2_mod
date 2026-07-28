using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.Formulas;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Relics;

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "FIRST_FORMULA_PRINCIPLE_DISCOUNT")]
public sealed class FirstFormulaPrincipleDiscount : ModRelicTemplate, ISecondaryResourceHookListener
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("BAG_OF_PREPARATION");

    public override RelicRarity Rarity => RelicRarity.Common;

    public decimal ModifySecondaryResourceCost(SecondaryResourceCostContext context, decimal cost)
    {
        return context.Card is AlchemyFormulaCard formula
            && CanDiscount(formula, context.Player, context.Definition.Id)
            && cost > 0m
                ? Math.Max(0m, cost - 1m)
                : cost;
    }

    public bool ConsumeAfterSuccessfulBrew(AlchemyFormulaCard formula, CardPlay cardPlay)
    {
        var mainPrinciple = PotionMainPrinciples.For(formula.PotionFamily);
        var payment = cardPlay.SecondaryResources();
        if (payment.IsFree
            || payment.Spent(mainPrinciple.Id) != formula.MainPrincipleCost - 1
            || !CanDiscount(formula, formula.Owner, mainPrinciple.Id))
        {
            return false;
        }

        var consumed = AlchemyCombatState.Require(Owner).TryConsumeFirstFormulaPrincipleDiscount();
        if (consumed)
            Flash();
        return consumed;
    }

    private bool CanDiscount(
        AlchemyFormulaCard formula,
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        string resourceId)
    {
        return player == Owner
            && formula.Owner == Owner
            && !AlchemyCombatState.Require(Owner).FirstFormulaPrincipleDiscountConsumed
            && PotionMainPrinciples.For(formula.PotionFamily).Id == resourceId;
    }
}

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "POTION_SATCHEL_EXPANSION")]
public sealed class PotionSatchelExpansion : ModRelicTemplate, IAlchemyBackpackCapacityModifier
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("MATRYOSHKA");

    public override RelicRarity Rarity => RelicRarity.Common;

    public int CapacityModifier => 1;
}