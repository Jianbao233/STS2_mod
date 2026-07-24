using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Cards.System;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "POTION_SATCHEL")]
public sealed class PotionSatchel : ModCardTemplate, IAlchemyBackpackCapacityProvider
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Eternal, CardKeyword.Retain];

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public int Capacity => IsUpgraded ? 4 : 3;

    public PotionQuality MaximumQuality =>
        IsUpgraded ? PotionQuality.Masterpiece : PotionQuality.Refined;

    protected override bool IsPlayable =>
        BackpackFlow.CanStart(BackpackTransition.Retrieve, Owner, this);

    public PotionSatchel() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        BackpackFlow.Execute(
            BackpackTransition.Retrieve,
            choiceContext,
            Owner,
            this,
            SelectionScreenPrompt);

    protected override CardLocation GetResultLocationForCardPlay() =>
        new(Owner, PileType.Hand, CardPilePosition.Bottom);
}