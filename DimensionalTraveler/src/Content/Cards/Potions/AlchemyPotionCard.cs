using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Resolution;
using DimensionalTraveler.Alchemy.State;

namespace DimensionalTraveler.Content.Cards.Potions;

public enum PotionFamily
{
    Shield,
    SelfDefense,
    Attack,
    Corruption,
    VolatileDraw,
    TemporaryStrength,
    TemporaryDexterity,
    Weakness,
    StrengthReduction,
}

public enum PotionQuality
{
    Normal,
    Refined,
    Masterpiece,
}

public abstract class AlchemyPotionCard(
    PotionFamily family,
    PotionQuality quality,
    TargetType targetType,
    int energyCost = 0)
    : ModCardTemplate(energyCost, CardType.Skill, CardRarity.Token, targetType, showInCardLibrary: true)
{
    private PotionOrigin _origin = PotionOrigin.Original;

    public PotionFamily Family { get; } = family;

    public PotionQuality Quality { get; } = quality;

    [SavedProperty]
    public PotionOrigin Origin
    {
        get => _origin;
        protected set
        {
            AssertMutable();
            _origin = value;
            if (value == PotionOrigin.EchoDerived)
                EnergyCost.SetThisCombat(EchoEnergyCost);
        }
    }

    public abstract SecondaryResourceDefinition MainPrinciple { get; }

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected sealed override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        PotionResolution.ResolvePlayedPotion(choiceContext, this, cardPlay);

    internal abstract Task ResolveSingleTarget(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardPlay cardPlay);

    internal PotionDescriptor Describe() =>
        new(Family, Quality, IsUpgraded, Origin);

    internal void SetOrigin(PotionOrigin origin)
    {
        Origin = origin;
    }

    private int EchoEnergyCost => Quality switch
    {
        PotionQuality.Normal => 1,
        PotionQuality.Refined => 2,
        PotionQuality.Masterpiece => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(Quality), Quality, null),
    };

    protected override CardLocation GetResultLocationForCardPlay() =>
        new(Owner, PileType.None, CardPilePosition.Bottom);

    protected static TargetType Anyone => CustomTargetType.Anyone;
}