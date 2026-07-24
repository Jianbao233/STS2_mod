using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.Production;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

public abstract class BurstPrincipleProductionCard(SecondaryResourceDefinition principle)
    : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Gain", 3m),
        new CardsVar(0),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(principle, DynamicVars["Gain"].IntValue),
            ProductionKind.DirectedBasic);
        if (DynamicVars.Cards.IntValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "VITALITY_BURST")]
public sealed class VitalityBurst : BurstPrincipleProductionCard
{
    public VitalityBurst() : base(AlchemyPrinciples.Vitality)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "VOLATILITY_BURST")]
public sealed class VolatilityBurst : BurstPrincipleProductionCard
{
    public VolatilityBurst() : base(AlchemyPrinciples.Volatility)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "CORRUPTION_BURST")]
public sealed class CorruptionBurst : BurstPrincipleProductionCard
{
    public CorruptionBurst() : base(AlchemyPrinciples.Corruption)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FLEXIBLE_BASIC_PRODUCTION")]
public sealed class FlexibleBasicProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 2m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public FlexibleBasicProduction() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var principle = await AlchemyPrincipleChoices.ChooseBasic(choiceContext, Owner);
        await AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(principle, DynamicVars["Gain"].IntValue),
            ProductionKind.FlexibleBasic);
    }

    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}