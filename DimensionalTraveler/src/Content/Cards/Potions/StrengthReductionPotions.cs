using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "STRENGTH_REDUCTION_POTION")]
public sealed class StrengthReductionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthReductionPower>(2m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public StrengthReductionPotion()
        : base(PotionFamily.StrengthReduction, PotionQuality.Normal, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<StrengthReductionPower>(
            choiceContext,
            target,
            DynamicVars["StrengthReductionPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["StrengthReductionPower"].UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_STRENGTH_REDUCTION_POTION")]
public sealed class RefinedStrengthReductionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthReductionPower>(6m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public RefinedStrengthReductionPotion()
        : base(PotionFamily.StrengthReduction, PotionQuality.Refined, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<StrengthReductionPower>(
            choiceContext,
            target,
            DynamicVars["StrengthReductionPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["StrengthReductionPower"].UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_STRENGTH_REDUCTION_POTION")]
public sealed class MasterpieceStrengthReductionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthReductionPower>(10m),
        new DynamicVar("ReductionPerExtra", 5m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public MasterpieceStrengthReductionPotion()
        : base(PotionFamily.StrengthReduction, PotionQuality.Masterpiece, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthReductionPower>(
            choiceContext,
            target,
            DynamicVars["StrengthReductionPower"].BaseValue,
            Owner.Creature,
            this);

        var extra = target.GetPowerAmount<StrengthReductionPower>()
            / DynamicVars["ReductionPerExtra"].IntValue;
        if (extra > 0)
        {
            await PowerCmd.Apply<StrengthReductionPower>(
                choiceContext,
                target,
                extra,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade() =>
        DynamicVars["ReductionPerExtra"].UpgradeValueBy(-2m);
}