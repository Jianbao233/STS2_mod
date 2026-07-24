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

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "TEMPORARY_DEXTERITY_POTION")]
public sealed class TemporaryDexterityPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<SpeedPotionPower>(2m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public TemporaryDexterityPotion()
        : base(PotionFamily.TemporaryDexterity, PotionQuality.Normal, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<SpeedPotionPower>(
            choiceContext,
            target,
            DynamicVars["SpeedPotionPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["SpeedPotionPower"].UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_TEMPORARY_DEXTERITY_POTION")]
public sealed class RefinedTemporaryDexterityPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpeedPotionPower>(6m),
        new DynamicVar("Amplification", 30m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public RefinedTemporaryDexterityPotion()
        : base(PotionFamily.TemporaryDexterity, PotionQuality.Refined, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<SpeedPotionPower>(
            choiceContext,
            target,
            DynamicVars["SpeedPotionPower"].BaseValue,
            Owner.Creature,
            this);
        await BlockAmplificationPower.Apply(
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

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_TEMPORARY_DEXTERITY_POTION")]
public sealed class MasterpieceTemporaryDexterityPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<IntangiblePower>(1m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public MasterpieceTemporaryDexterityPotion()
        : base(
            PotionFamily.TemporaryDexterity,
            PotionQuality.Masterpiece,
            Anyone,
            energyCost: 1)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<IntangiblePower>(
            choiceContext,
            target,
            DynamicVars["IntangiblePower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() =>
        DynamicVars["IntangiblePower"].UpgradeValueBy(1m);
}