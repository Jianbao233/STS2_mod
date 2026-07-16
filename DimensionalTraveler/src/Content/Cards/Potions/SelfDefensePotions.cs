using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
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

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
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