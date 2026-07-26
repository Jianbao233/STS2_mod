using STS2RitsuLib.Combat.SecondaryResources;

namespace DimensionalTraveler.Content.Cards.Potions;

public static class PotionMainPrinciples
{
    public static SecondaryResourceDefinition For(PotionFamily family) =>
        ((AlchemyPotionCard)PotionCatalog.GetCanonical(family, PotionQuality.Normal)).MainPrinciple;
}