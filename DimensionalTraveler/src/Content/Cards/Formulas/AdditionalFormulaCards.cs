using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Formulas;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "TEMPORARY_STRENGTH_POTION_FORMULA")]
public sealed class TemporaryStrengthPotionFormula : AlchemyFormulaCard
{
    public TemporaryStrengthPotionFormula()
        : base(
            PotionFamily.TemporaryStrength,
            PotionQuality.Normal,
            1,
            CardRarity.Common,
            (AlchemyPrinciples.Vitality.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "TEMPORARY_DEXTERITY_POTION_FORMULA")]
public sealed class TemporaryDexterityPotionFormula : AlchemyFormulaCard
{
    public TemporaryDexterityPotionFormula()
        : base(
            PotionFamily.TemporaryDexterity,
            PotionQuality.Normal,
            1,
            CardRarity.Common,
            (AlchemyPrinciples.Vitality.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "WEAKNESS_POTION_FORMULA")]
public sealed class WeaknessPotionFormula : AlchemyFormulaCard
{
    public WeaknessPotionFormula()
        : base(
            PotionFamily.Weakness,
            PotionQuality.Normal,
            1,
            CardRarity.Uncommon,
            (AlchemyPrinciples.Corruption.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "STRENGTH_REDUCTION_POTION_FORMULA")]
public sealed class StrengthReductionPotionFormula : AlchemyFormulaCard
{
    public StrengthReductionPotionFormula()
        : base(
            PotionFamily.StrengthReduction,
            PotionQuality.Normal,
            1,
            CardRarity.Uncommon,
            (AlchemyPrinciples.Corruption.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}