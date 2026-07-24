using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Alchemy;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
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

    internal override Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        var player = target.Player
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

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        var player = target.Player
            ?? throw new InvalidOperationException("挥发过牌药剂只能对玩家角色使用。");
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_VOLATILE_DRAW_POTION")]
public sealed class MasterpieceVolatileDrawPotion : AlchemyPotionCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(2),
        new PowerVar<AcceleratedRotationPower>(3m),
    ];

    public override SecondaryResourceDefinition MainPrinciple => AlchemyPrinciples.Volatility;

    public MasterpieceVolatileDrawPotion()
        : base(PotionFamily.VolatileDraw, PotionQuality.Masterpiece, AlchemyTargetTypes.FriendlyCreature)
    {
    }

    internal override async Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay)
    {
        var player = target.Player
            ?? throw new InvalidOperationException("杰作轮转药剂只能对玩家角色使用。");
        var drawPile = PileType.Draw.GetPile(player)
            ?? throw new InvalidOperationException("目标玩家没有活动抽牌堆。");
        var selectCount = Math.Min(DynamicVars.Cards.IntValue, drawPile.Cards.Count);
        if (selectCount > 0)
        {
            var selected = await CardSelectCmd.FromCombatPile(
                choiceContext,
                drawPile,
                player,
                new CardSelectorPrefs(SelectionScreenPrompt, selectCount)
                {
                    RequireManualConfirmation = drawPile.Cards.Count >= 2,
                });
            await CardPileCmd.Add(selected, PileType.Hand);
        }

        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
        await PowerCmd.Apply<AcceleratedRotationPower>(
            choiceContext,
            target,
            DynamicVars["AcceleratedRotationPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() =>
        DynamicVars["AcceleratedRotationPower"].UpgradeValueBy(1m);
}