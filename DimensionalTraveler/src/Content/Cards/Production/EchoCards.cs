using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.Production;
using DimensionalTraveler.Alchemy.Resolution;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ECHO_PRODUCTION")]
public sealed class EchoProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public EchoProduction() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var amount = DynamicVars["Gain"].IntValue
            + (AlchemyCombatState.Require(Owner).Snapshot.HasBrewedOrUsedOriginalPotion ? 1 : 0);
        return AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.Resource(AlchemyPrinciples.Echo, amount));
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "COMPOSITE_ECHO_PRODUCTION")]
public sealed class CompositeEchoProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Echo", 1m),
        new DynamicVar("Basic", 1m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public CompositeEchoProduction() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var principle = await AlchemyPrincipleChoices.ChooseBasic(choiceContext, Owner);
        await AlchemyProduction.Execute(
            Owner,
            this,
            ProductionPlan.ResourcesOf(
                (AlchemyPrinciples.Echo, DynamicVars["Echo"].IntValue),
                (principle, DynamicVars["Basic"].IntValue)));
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

public abstract class EchoSnapshotCard(int echoCost)
    : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected bool HasLatestSnapshot =>
        AlchemyCombatState.Require(Owner).Snapshot.LatestOriginalPotion is not null;

    protected void ConfigureCost() =>
        this.SecondaryCosts().Set(AlchemyPrinciples.Echo.Id, echoCost);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ECHO_REPLAY")]
public sealed class EchoReplay : EchoSnapshotCard
{
    protected override bool IsPlayable => PotionResolution.CanReplayLatest(Owner);

    public EchoReplay() : base(echoCost: 2)
    {
        ConfigureCost();
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PotionResolution.ReplayLatest(choiceContext, Owner, this);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ECHO_POTION_CREATION")]
public sealed class EchoPotionCreation : EchoSnapshotCard
{
    protected override bool IsPlayable => HasLatestSnapshot;

    public EchoPotionCreation() : base(echoCost: 1)
    {
        ConfigureCost();
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var descriptor = AlchemyCombatState.Require(Owner).Snapshot.LatestOriginalPotion!.Descriptor;
        return AlchemyBackpack.Brew(
            Owner,
            descriptor.Family,
            descriptor.Quality,
            descriptor.IsUpgraded,
            PotionOrigin.EchoDerived,
            recordAsBrewed: false);
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ORIGINAL_POTION_INSIGHT")]
public sealed class OriginalPotionInsight : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public OriginalPotionInsight() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var extra = AlchemyCombatState.Require(Owner).Snapshot.Experiments
            .HasFlag(ExperimentRecord.UsedOriginalPotion)
            ? 1
            : 0;
        return CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue + extra, Owner);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}