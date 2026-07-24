using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Production;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "DIFFUSION_PRODUCTION")]
public sealed class DiffusionProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public DiffusionProduction() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(AlchemyPrinciples.Diffusion, DynamicVars["Gain"].IntValue));

    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "COMPOSITE_DIFFUSION_PRODUCTION")]
public sealed class CompositeDiffusionProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Diffusion", 1m),
        new DynamicVar("Basic", 2m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public CompositeDiffusionProduction() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var basic = DynamicVars["Basic"].IntValue;
        return AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.ResourcesOf(
                (AlchemyPrinciples.Diffusion, DynamicVars["Diffusion"].IntValue),
                (AlchemyPrinciples.Vitality, basic),
                (AlchemyPrinciples.Volatility, basic),
                (AlchemyPrinciples.Corruption, basic)),
            ProductionKind.FlexibleBasic);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public abstract class DiffusionPreparationCard(DiffusionMode mode, int energyCost, CardRarity rarity, int diffusionCost)
    : ModCardTemplate(energyCost, CardType.Skill, rarity, TargetType.Self)
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected void ConfigureCost() =>
        this.SecondaryCosts().Set(AlchemyPrinciples.Diffusion.Id, diffusionCost);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        AlchemyCombatState.Require(Owner).Update(turn => turn.PendingDiffusion = mode);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "LOCAL_DIFFUSION")]
public sealed class LocalDiffusion : DiffusionPreparationCard
{
    public LocalDiffusion()
        : base(DiffusionMode.AdditionalTarget, 1, CardRarity.Uncommon, diffusionCost: 1)
    {
        ConfigureCost();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FULL_DIFFUSION")]
public sealed class FullDiffusion : DiffusionPreparationCard
{
    public FullDiffusion()
        : base(DiffusionMode.WholeSide, 1, CardRarity.Rare, diffusionCost: 2)
    {
        ConfigureCost();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "DIFFUSION_REWARD")]
public sealed class DiffusionReward : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<DiffusionRewardPower>(1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public DiffusionReward() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<DiffusionRewardPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DiffusionRewardPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}