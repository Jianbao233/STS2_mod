using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Production;
using DimensionalTraveler.Alchemy.Resolution;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Formulas;

namespace DimensionalTraveler.Content.Powers;

[RegisterPower]
public sealed class ProductionFormulaRoutingPower : ModPowerTemplate, IAlchemyProductionListener
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("DRAW_CARDS_NEXT_TURN_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterExplicitProduction(
        Player player,
        ProductionSnapshot snapshot,
        CardModel source)
    {
        if (player != Owner.Player)
            return;

        var state = AlchemyCombatState.Require(player);
        if (state.Snapshot.ProductionFormulaFetchTriggered)
            return;

        state.Update(static turn => turn.ProductionFormulaFetchTriggered = true);
        var drawPile = PileType.Draw.GetPile(player);
        var formulas = drawPile?.Cards
            .Where(static card => card is IAlchemyFormulaCard { IsTemporaryCopy: false })
            .Take(Amount)
            .ToArray() ?? [];
        if (formulas.Length == 0)
            return;

        Flash();
        await CardPileCmd.Add(formulas, PileType.Hand);
    }
}

[RegisterPower]
public sealed class DiffusionRewardPower : ModPowerTemplate, IAlchemyPotionResolutionListener
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("DRAW_CARDS_NEXT_TURN_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterOriginalPotionResolved(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source)
    {
        if (player != Owner.Player
            || result.DiffusionMode == DiffusionMode.None
            || !result.AffectedMultipleTargets)
        {
            return;
        }

        var state = AlchemyCombatState.Require(player);
        if (state.Snapshot.DiffusionRewardTriggered)
            return;

        state.Update(static turn => turn.DiffusionRewardTriggered = true);
        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, player);
        await PlayerCmd.GainEnergy(Amount, player);
    }
}