using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Payment;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Cards.Operations;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_TRANSFORMATION")]
public sealed class MasterpieceTransformation : DynamicBackpackPaymentCard
{
    public MasterpieceTransformation()
        : base(
            1,
            CardRarity.Rare,
            BackpackTransition.Masterpiece,
            principleCost: 4)
    {
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRE_PURIFICATION")]
public sealed class PrePurification : ModCardTemplate
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public PrePurification() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        AlchemyCombatState.Require(Owner).Update(
            static turn => turn.PrePurificationCharges += 1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "QUALITY_INSIGHT")]
public sealed class QualityInsight : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(1),
    ];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public QualityInsight() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var qualities = AlchemyBackpack.GetPotions(Owner).Select(static potion => potion.Quality).ToArray();
        var highestQuality = qualities.Length == 0 ? PotionQuality.Normal : qualities.Max();
        var drawAmount = DynamicVars.Cards.IntValue
            + (highestQuality == PotionQuality.Masterpiece ? 1 : 0);

        await CardPileCmd.Draw(choiceContext, drawAmount, Owner);
        if (highestQuality >= PotionQuality.Refined)
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}