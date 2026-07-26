using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Relics;

namespace DimensionalTraveler.Content.Cards.Formulas;

public interface IAlchemyFormulaCard
{
    PotionFamily PotionFamily { get; }

    PotionQuality ProductQuality { get; }

    bool IsTemporaryCopy { get; }
}

public abstract class AlchemyFormulaCard : ModCardTemplate, IAlchemyFormulaCard
{
    private readonly (string ResourceId, int Amount)[] _costs;

    protected AlchemyFormulaCard(
        PotionFamily family,
        params (string ResourceId, int Amount)[] costs)
        : this(family, PotionQuality.Normal, 1, CardRarity.Basic, costs)
    {
    }

    protected AlchemyFormulaCard(
        PotionFamily family,
        PotionQuality productQuality,
        int energyCost,
        CardRarity rarity,
        params (string ResourceId, int Amount)[] costs)
        : base(energyCost, CardType.Skill, rarity, TargetType.Self)
    {
        PotionFamily = family;
        ProductQuality = productQuality;
        _costs = costs;
    }

    public PotionFamily PotionFamily { get; }

    public PotionQuality ProductQuality { get; }

    [SavedProperty]
    public bool IsTemporaryCopy { get; protected set; }

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public void MarkTemporaryCopy()
    {
        AssertMutable();
        IsTemporaryCopy = true;
    }

    protected void ConfigureCosts()
    {
        var secondaryCosts = this.SecondaryCosts();
        foreach (var (resourceId, amount) in _costs)
            secondaryCosts.Set(resourceId, amount);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var potion = await AlchemyBackpack.Brew(
            Owner,
            PotionFamily,
            ProductQuality,
            upgraded: IsUpgraded);
        if (potion is not null)
            Owner.GetRelic<FirstFormulaPrincipleDiscount>()?.ConsumeAfterSuccessfulBrew(this);
    }
}