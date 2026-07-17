using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Content.Cards.Formulas;

public interface IAlchemyFormulaCard
{
    PotionFamily PotionFamily { get; }
}

public abstract class AlchemyFormulaCard(
    PotionFamily family,
    params (string ResourceId, int Amount)[] costs)
    : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.Self), IAlchemyFormulaCard
{
    public PotionFamily PotionFamily { get; } = family;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected void ConfigureCosts()
    {
        var secondaryCosts = this.SecondaryCosts();
        foreach (var (resourceId, amount) in costs)
            secondaryCosts.Set(resourceId, amount);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        AlchemyBackpack.Brew(
            Owner,
            PotionFamily,
            PotionQuality.Normal,
            upgraded: IsUpgraded);
}