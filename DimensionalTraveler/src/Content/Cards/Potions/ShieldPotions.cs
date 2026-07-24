using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SHIELD_POTION")]
public sealed class ShieldPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public ShieldPotion() : base(PotionFamily.Shield, PotionQuality.Normal, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_SHIELD_POTION")]
public sealed class RefinedShieldPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("Reduction", 30m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public RefinedShieldPotion() : base(PotionFamily.Shield, PotionQuality.Refined, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<NextDamageReductionPower>(
            choiceContext,
            target,
            DynamicVars["Reduction"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Reduction"].UpgradeValueBy(20m);
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_SHIELD_POTION")]
public sealed class MasterpieceShieldPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(20m, ValueProp.Move),
        new PowerVar<MegaCrit.Sts2.Core.Models.Powers.PlatingPower>(4m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public MasterpieceShieldPotion() : base(PotionFamily.Shield, PotionQuality.Masterpiece, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.PlatingPower>(
            choiceContext,
            target,
            DynamicVars["PlatingPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars["PlatingPower"].UpgradeValueBy(3m);
    }
}