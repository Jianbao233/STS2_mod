using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Formulas;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Cards.Operations;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FORMULA_RECOVERY")]
public sealed class FormulaRecovery : ModCardTemplate
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        PileType.Discard.GetPile(Owner)?.Cards.Any(IsFormalFormula) == true;

    public FormulaRecovery() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var discardPile = PileType.Discard.GetPile(Owner);
        if (discardPile is null)
            return;

        var selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            discardPile,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            IsFormalFormula)).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Add(selected, PileType.Hand);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static bool IsFormalFormula(CardModel card) =>
        card is IAlchemyFormulaCard { IsTemporaryCopy: false };
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FORMULA_REPLICATION")]
public sealed class FormulaReplication : ModCardTemplate
{
    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable =>
        PileType.Hand.GetPile(Owner)?.Cards.Any(card => card != this && IsFormalFormula(card)) == true;

    public FormulaReplication() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            card => card != this && IsFormalFormula(card),
            this)).FirstOrDefault();
        if (selected is null || CombatState is null)
            return;

        var copy = (AlchemyFormulaCard)CombatState.CreateCard(
            ModelDb.GetById<CardModel>(selected.Id),
            Owner);
        if (selected.IsUpgraded)
            CardCmd.Upgrade(copy, CardPreviewStyle.None);
        copy.MarkTemporaryCopy();
        CardCmd.ApplyKeyword(copy, CardKeyword.Ethereal, CardKeyword.Exhaust);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static bool IsFormalFormula(CardModel card) =>
        card is IAlchemyFormulaCard { IsTemporaryCopy: false };
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "FORMULA_DISCOUNT")]
public sealed class FormulaDiscount : ModCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public FormulaDiscount() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        AlchemyCombatState.Require(Owner).Update(
            static turn => turn.NextFormulaEnergyDiscount += 1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "POTION_REPACK")]
public sealed class PotionRepack : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override bool IsPlayable => GetCandidates().Count > 0;

    public PotionRepack() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = GetCandidates();
        if (candidates.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates.Cast<CardModel>().ToArray(),
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1)))
            .OfType<AlchemyPotionCard>()
            .FirstOrDefault();
        if (selected is null || !CanRepack(selected))
            return;

        var result = await CardPileCmd.Add(selected, AlchemyBackpack.PileType);
        if (!result.success || selected.Pile?.Type != AlchemyBackpack.PileType)
            throw new InvalidOperationException("药剂重新装包提交失败。");

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);

    private IReadOnlyList<AlchemyPotionCard> GetCandidates()
    {
        if (!AlchemyBackpack.HasSpace(Owner))
            return [];

        return PileType.Hand.GetPile(Owner)?.Cards
            .OfType<AlchemyPotionCard>()
            .Where(CanRepack)
            .ToArray() ?? [];
    }

    private bool CanRepack(AlchemyPotionCard potion) =>
        potion.Origin != PotionOrigin.EchoDerived
        && potion.Pile?.Type == PileType.Hand
        && AlchemyBackpack.HasSpace(Owner)
        && AlchemyBackpack.CanStore(Owner, potion.Quality);
}