using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Alchemy;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Potions;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "VOLATILE_DRAW_POTION")]
public sealed class VolatileDrawPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Volatility;

    public VolatileDrawPotion()
        : base(PotionFamily.VolatileDraw, PotionQuality.Normal, AlchemyTargetTypes.FriendlyCreature)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var player = cardPlay.Target.Player
            ?? throw new InvalidOperationException("挥发过牌药剂只能对玩家角色使用。");
        return CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "REFINED_VOLATILE_DRAW_POTION")]
public sealed class RefinedVolatileDrawPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(4),
        new EnergyVar(1),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Volatility;

    public RefinedVolatileDrawPotion()
        : base(PotionFamily.VolatileDraw, PotionQuality.Refined, AlchemyTargetTypes.FriendlyCreature)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var player = cardPlay.Target.Player
            ?? throw new InvalidOperationException("挥发过牌药剂只能对玩家角色使用。");
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}