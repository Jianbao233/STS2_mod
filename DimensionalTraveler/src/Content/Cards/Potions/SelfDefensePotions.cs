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

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SELF_DEFENSE_POTION")]
public sealed class SelfDefensePotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10m, ValueProp.Move)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public SelfDefensePotion() : base(PotionFamily.SelfDefense, PotionQuality.Normal, TargetType.Self)
    {
    }

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay) =>
        CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_SELF_DEFENSE_POTION")]
public sealed class RefinedSelfDefensePotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new PowerVar<PlatingPower>(3m),
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PlatingPower>()];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public RefinedSelfDefensePotion() : base(PotionFamily.SelfDefense, PotionQuality.Refined, TargetType.Self)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            target,
            DynamicVars["PlatingPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["PlatingPower"].UpgradeValueBy(1m);
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_SELF_DEFENSE_POTION")]
public sealed class MasterpieceSelfDefensePotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(16m, ValueProp.Move),
        new PowerVar<PlatingPower>(3m),
        new PowerVar<EtherealPower>(2m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Vitality;

    public override bool GainsBlock => true;

    public MasterpieceSelfDefensePotion()
        : base(PotionFamily.SelfDefense, PotionQuality.Masterpiece, TargetType.Self)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(target, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            target,
            DynamicVars["PlatingPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<EtherealPower>(
            choiceContext,
            target,
            DynamicVars["EtherealPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars["PlatingPower"].UpgradeValueBy(2m);
        DynamicVars["EtherealPower"].UpgradeValueBy(1m);
    }
}