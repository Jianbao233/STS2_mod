using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Content.Powers;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Operations;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "DIRECTED_HAND_ROUTING")]
public sealed class DirectedHandRouting : ModCardTemplate
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        PileType.Draw.GetPile(Owner)?.Cards.Count > 0;

    public DirectedHandRouting() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        if (drawPile is null || drawPile.Cards.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            drawPile,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected is null)
            return;

        var addResult = await CardPileCmd.Add(selected, PileType.Hand);
        if (!addResult.success || selected.Pile?.Type != PileType.Hand)
            throw new InvalidOperationException("定向手牌调度未能将所选牌置入手牌。");

        var discarded = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            new CardSelectorPrefs(
                new LocString("cards", Id.Entry + ".discardSelectionScreenPrompt"),
                1),
            static card => !SystemCardProtection.IsProtected(card),
            this)).FirstOrDefault();
        if (discarded is null)
            throw new InvalidOperationException("定向手牌调度没有返回合法弃牌目标。");

        await CardCmd.Discard(choiceContext, discarded);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PERPETUAL_ENERGY")]
public sealed class PerpetualEnergy : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<PerpetualEnergyPower>(1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public PerpetualEnergy() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        var costs = this.SecondaryCosts();
        costs.Set(AlchemyPrinciples.Vitality.Id, 2);
        costs.Set(AlchemyPrinciples.Volatility.Id, 2);
        costs.Set(AlchemyPrinciples.Corruption.Id, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PowerCmd.Apply<PerpetualEnergyPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["PerpetualEnergyPower"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}