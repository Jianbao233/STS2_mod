using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Payment;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.Formulas;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Cards.Operations;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FORMULA_RETRIEVAL")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class FormulaRetrieval : ModCardTemplate
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        PileType.Draw.GetPile(Owner)?.Cards.Any(IsFormalFormula) == true;

    public FormulaRetrieval() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        var candidates = drawPile?.Cards.Where(IsFormalFormula).ToArray() ?? [];
        if (candidates.Length == 0)
            return;

        var selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Add(selected, PileType.Hand);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static bool IsFormalFormula(CardModel card) =>
        card is IAlchemyFormulaCard { IsTemporaryCopy: false };
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FORMULA_RETENTION")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class FormulaRetention : ModCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        PileType.Hand.GetPile(Owner)?.Cards.Any(card => card != this && IsFormalFormula(card)) == true;

    public FormulaRetention() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = PileType.Hand.GetPile(Owner)?.Cards
            .Where(card => card != this && IsFormalFormula(card))
            .ToArray() ?? [];
        if (candidates.Length == 0)
            return;

        var selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected is not null)
            CardCmd.ApplyKeyword(selected, CardKeyword.Retain);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);

    private static bool IsFormalFormula(CardModel card) =>
        card is IAlchemyFormulaCard { IsTemporaryCopy: false };
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PURIFICATION")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class Purification : DynamicBackpackPaymentCard
{
    public Purification()
        : base(
            1,
            CardRarity.Basic,
            BackpackTransition.Purify,
            principleCost: 1)
    {
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SUBLIMATION")]
public sealed class Sublimation : DynamicBackpackPaymentCard
{
    public Sublimation()
        : base(
            1,
            CardRarity.Uncommon,
            BackpackTransition.Sublimate,
            principleCost: 2)
    {
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}