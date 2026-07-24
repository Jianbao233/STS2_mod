using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.Production;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Cards.Production;

public abstract class SpecialPrincipleBridgeCard : ModCardTemplate
{
    protected SpecialPrincipleBridgeCard() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected async Task ProduceChosenSpecial(
        PlayerChoiceContext choiceContext,
        int amount)
    {
        var principle = await AlchemyPrincipleChoices.ChooseSpecial(choiceContext, Owner);
        await AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(principle, amount));
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SPECIAL_PRINCIPLE_BRIDGE")]
public sealed class SpecialPrincipleBridge : SpecialPrincipleBridgeCard
{
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ProduceChosenSpecial(choiceContext, 1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "EXPERIMENT_CONVERSION")]
public sealed class ExperimentConversion : SpecialPrincipleBridgeCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Minimum", 1m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new HoverTip(
            new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXPERIMENT_RECORD.title"),
            new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXPERIMENT_RECORD.description")),
    ];

    protected override bool IsPlayable =>
        AlchemyCombatState.Require(Owner).Snapshot.ExperimentCount >= DynamicVars["Minimum"].IntValue;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ProduceChosenSpecial(
            choiceContext,
            AlchemyCombatState.Require(Owner).Snapshot.ExperimentCount);
}