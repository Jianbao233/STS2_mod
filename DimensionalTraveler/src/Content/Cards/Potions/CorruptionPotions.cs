using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "CORRUPTION_POTION")]
public sealed class CorruptionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m),
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public CorruptionPotion() : base(PotionFamily.Corruption, PotionQuality.Normal, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            target,
            DynamicVars.Vulnerable.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_CORRUPTION_POTION")]
public sealed class RefinedCorruptionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new PowerVar<VulnerablePower>(2m),
        new PowerVar<DebilitatePower>(1m),
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<DebilitatePower>(),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public RefinedCorruptionPotion() : base(PotionFamily.Corruption, PotionQuality.Refined, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            target,
            DynamicVars.Vulnerable.BaseValue,
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
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
        DynamicVars["DebilitatePower"].UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_CORRUPTION_POTION")]
public sealed class MasterpieceCorruptionPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16m, ValueProp.Move),
        new PowerVar<CorrosionPower>(3m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public MasterpieceCorruptionPotion()
        : base(PotionFamily.Corruption, PotionQuality.Masterpiece, Anyone)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await PowerCmd.Apply<CorrosionPower>(
            choiceContext,
            target,
            DynamicVars["CorrosionPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["CorrosionPower"].UpgradeValueBy(2m);
    }
}