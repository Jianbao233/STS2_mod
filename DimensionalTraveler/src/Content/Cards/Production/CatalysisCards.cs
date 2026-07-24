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
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "CATALYSIS_PRODUCTION")]
public sealed class CatalysisProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public CatalysisProduction() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(AlchemyPrinciples.Catalysis, DynamicVars["Gain"].IntValue));

    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "COMPOSITE_CATALYSIS_PRODUCTION")]
public sealed class CompositeCatalysisProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Catalysis", 1m),
        new DynamicVar("Basic", 2m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public CompositeCatalysisProduction() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var principle = await AlchemyPrincipleChoices.ChooseBasic(choiceContext, Owner);
        await AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.ResourcesOf(
                (AlchemyPrinciples.Catalysis, DynamicVars["Catalysis"].IntValue),
                (principle, DynamicVars["Basic"].IntValue)),
            ProductionKind.DirectedBasic);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRODUCTION_AMPLIFICATION")]
public sealed class ProductionAmplification : ModCardTemplate
{
    private const int CatalysisCost = 2;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public ProductionAmplification() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(AlchemyPrinciples.Catalysis.Id, CatalysisCost);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var catalysisBeforePayment = AlchemyPrinciples.Get(Owner, AlchemyPrinciples.Catalysis) + CatalysisCost;
        AlchemyCombatState.Require(Owner).Update(
            turn => turn.ProductionBoostCatalysisSnapshot = catalysisBeforePayment);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REPEAT_PRODUCTION")]
public sealed class RepeatProduction : ModCardTemplate
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        AlchemyCombatState.Require(Owner).Snapshot.LatestProduction is not null;

    public RepeatProduction() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(AlchemyPrinciples.Catalysis.Id, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        AlchemyProduction.RepeatLatest(Owner, this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "IMMEDIATE_CONCOCTION")]
public sealed class ImmediateConcoction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new CardsVar(1),
    ];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public ImmediateConcoction() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        this.SecondaryCosts().Set(AlchemyPrinciples.Catalysis.Id, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await AlchemyProduction.Execute(
            Owner,
            this,
            new ProductionPlan([], DynamicVars.Energy.IntValue));
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRODUCTION_FORMULA_ROUTING")]
public sealed class ProductionFormulaRouting : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ProductionFormulaRoutingPower>(1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public ProductionFormulaRouting() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<ProductionFormulaRoutingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ProductionFormulaRoutingPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}