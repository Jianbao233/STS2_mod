using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "TEMPORARY_STRENGTH_POTION")]
public sealed class TemporaryStrengthPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<FlexPotionPower>(2m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public TemporaryStrengthPotion()
        : base(PotionFamily.TemporaryStrength, PotionQuality.Normal, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<FlexPotionPower>(
            choiceContext,
            target,
            DynamicVars["FlexPotionPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["FlexPotionPower"].UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_TEMPORARY_STRENGTH_POTION")]
public sealed class RefinedTemporaryStrengthPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FlexPotionPower>(6m),
        new DynamicVar("Amplification", 30m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public RefinedTemporaryStrengthPotion()
        : base(PotionFamily.TemporaryStrength, PotionQuality.Refined, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<FlexPotionPower>(
            choiceContext,
            target,
            DynamicVars["FlexPotionPower"].BaseValue,
            Owner.Creature,
            this);
        await AttackAmplificationPower.Apply(
            choiceContext,
            target,
            DynamicVars["Amplification"].IntValue,
            charges: 1,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() =>
        DynamicVars["Amplification"].UpgradeValueBy(20m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_TEMPORARY_STRENGTH_POTION")]
public sealed class MasterpieceTemporaryStrengthPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FlexPotionPower>(10m),
        new DynamicVar("Amplification", 50m),
        new DynamicVar("Charges", 2m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public MasterpieceTemporaryStrengthPotion()
        : base(PotionFamily.TemporaryStrength, PotionQuality.Masterpiece, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<FlexPotionPower>(
            choiceContext,
            target,
            DynamicVars["FlexPotionPower"].BaseValue,
            Owner.Creature,
            this);
        await AttackAmplificationPower.Apply(
            choiceContext,
            target,
            DynamicVars["Amplification"].IntValue,
            DynamicVars["Charges"].IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FlexPotionPower"].UpgradeValueBy(2m);
        DynamicVars["Charges"].UpgradeValueBy(1m);
    }
}