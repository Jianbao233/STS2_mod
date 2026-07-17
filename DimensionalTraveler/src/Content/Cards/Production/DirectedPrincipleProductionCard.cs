using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

public abstract class DirectedPrincipleProductionCard(SecondaryResourceDefinition principle)
    : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 2m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        AlchemyPrinciples.Gain(Owner, principle, DynamicVars["Gain"].IntValue, this);

    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}