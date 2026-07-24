using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Formulas;

public abstract class MasterpieceFormulaCard(
    PotionFamily family,
    params (string ResourceId, int Amount)[] costs)
    : AlchemyFormulaCard(family, PotionQuality.Masterpiece, 3, CardRarity.Rare, costs)
{
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_SHIELD_POTION_FORMULA")]
public sealed class MasterpieceShieldPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceShieldPotionFormula()
        : base(
            PotionFamily.Shield,
            (AlchemyPrinciples.Vitality.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_SELF_DEFENSE_POTION_FORMULA")]
public sealed class MasterpieceSelfDefensePotionFormula : MasterpieceFormulaCard
{
    public MasterpieceSelfDefensePotionFormula()
        : base(PotionFamily.SelfDefense, (AlchemyPrinciples.Vitality.Id, 6))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_ATTACK_POTION_FORMULA")]
public sealed class MasterpieceAttackPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceAttackPotionFormula()
        : base(PotionFamily.Attack, (AlchemyPrinciples.Corruption.Id, 6))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_CORRUPTION_POTION_FORMULA")]
public sealed class MasterpieceCorruptionPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceCorruptionPotionFormula()
        : base(
            PotionFamily.Corruption,
            (AlchemyPrinciples.Corruption.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_VOLATILE_DRAW_POTION_FORMULA")]
public sealed class MasterpieceVolatileDrawPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceVolatileDrawPotionFormula()
        : base(PotionFamily.VolatileDraw, (AlchemyPrinciples.Volatility.Id, 6))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_TEMPORARY_STRENGTH_POTION_FORMULA")]
public sealed class MasterpieceTemporaryStrengthPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceTemporaryStrengthPotionFormula()
        : base(
            PotionFamily.TemporaryStrength,
            (AlchemyPrinciples.Vitality.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_TEMPORARY_DEXTERITY_POTION_FORMULA")]
public sealed class MasterpieceTemporaryDexterityPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceTemporaryDexterityPotionFormula()
        : base(
            PotionFamily.TemporaryDexterity,
            (AlchemyPrinciples.Vitality.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_WEAKNESS_POTION_FORMULA")]
public sealed class MasterpieceWeaknessPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceWeaknessPotionFormula()
        : base(
            PotionFamily.Weakness,
            (AlchemyPrinciples.Corruption.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "MASTERPIECE_STRENGTH_REDUCTION_POTION_FORMULA")]
public sealed class MasterpieceStrengthReductionPotionFormula : MasterpieceFormulaCard
{
    public MasterpieceStrengthReductionPotionFormula()
        : base(
            PotionFamily.StrengthReduction,
            (AlchemyPrinciples.Corruption.Id, 3),
            (AlchemyPrinciples.Volatility.Id, 3))
    {
        ConfigureCosts();
    }
}