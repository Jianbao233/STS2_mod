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

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "WEAKNESS_POTION")]
public sealed class WeaknessPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<WeakPower>(3m)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public WeaknessPotion() : base(PotionFamily.Weakness, PotionQuality.Normal, Anyone)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        PowerCmd.Apply<WeakPower>(
            choiceContext,
            target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_WEAKNESS_POTION")]
public sealed class RefinedWeaknessPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(5m),
        new PowerVar<DebilitatePower>(1m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public RefinedWeaknessPotion()
        : base(PotionFamily.Weakness, PotionQuality.Refined, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DebilitatePower>(
            choiceContext,
            target,
            DynamicVars["DebilitatePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(2m);
        DynamicVars["DebilitatePower"].UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_WEAKNESS_POTION")]
public sealed class MasterpieceWeaknessPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(8m),
        new PowerVar<DebilitatePower>(3m),
        new DynamicVar("WeakPerStrengthReduction", 4m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public MasterpieceWeaknessPotion()
        : base(PotionFamily.Weakness, PotionQuality.Masterpiece, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DebilitatePower>(
            choiceContext,
            target,
            DynamicVars["DebilitatePower"].BaseValue,
            Owner.Creature,
            this);

        var strengthReduction = target.GetPowerAmount<WeakPower>()
            / DynamicVars["WeakPerStrengthReduction"].IntValue;
        if (strengthReduction > 0)
        {
            await PowerCmd.Apply<StrengthReductionPower>(
                choiceContext,
                target,
                strengthReduction,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(2m);
        DynamicVars["DebilitatePower"].UpgradeValueBy(1m);
        DynamicVars["WeakPerStrengthReduction"].UpgradeValueBy(-1m);
    }
}