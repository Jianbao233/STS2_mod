using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace DimensionalTraveler.Content.Cards.Potions;

public enum PotionFamily
{
    Shield,
    SelfDefense,
    Attack,
    Corruption,
    VolatileDraw,
}

public enum PotionQuality
{
    Normal,
    Refined,
}

public abstract class AlchemyPotionCard(
    PotionFamily family,
    PotionQuality quality,
    TargetType targetType)
    : ModCardTemplate(0, CardType.Skill, CardRarity.Token, targetType, showInCardLibrary: true)
{
    public PotionFamily Family { get; } = family;

    public PotionQuality Quality { get; } = quality;

    public abstract SecondaryResourceDefinition MainPrinciple { get; }

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override (PileType, CardPilePosition) GetResultPileTypeAndPositionForCardPlay() =>
        (PileType.None, CardPilePosition.Bottom);

    protected static TargetType Anyone => CustomTargetType.Anyone;
}