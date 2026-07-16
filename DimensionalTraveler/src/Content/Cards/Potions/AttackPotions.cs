using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ATTACK_POTION")]
public sealed class AttackPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public AttackPotion() : base(PotionFamily.Attack, PotionQuality.Normal, Anyone)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_ATTACK_POTION")]
public sealed class RefinedAttackPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("Corruption", 1m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Corruption;

    public RefinedAttackPotion() : base(PotionFamily.Attack, PotionQuality.Refined, Anyone)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await AlchemyPrinciples.Gain(
            Owner,
            AlchemyPrinciples.Corruption,
            DynamicVars["Corruption"].IntValue,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Corruption"].UpgradeValueBy(1m);
    }
}