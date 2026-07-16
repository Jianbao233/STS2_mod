using STS2RitsuLib.Interop.AutoRegistration;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Formulas;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SHIELD_POTION_FORMULA")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class ShieldPotionFormula : AlchemyFormulaCard
{
    public ShieldPotionFormula()
        : base(
            PotionFamily.Shield,
            (AlchemyPrinciples.Vitality.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "SELF_DEFENSE_POTION_FORMULA")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class SelfDefensePotionFormula : AlchemyFormulaCard
{
    public SelfDefensePotionFormula()
        : base(PotionFamily.SelfDefense, (AlchemyPrinciples.Vitality.Id, 2))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "ATTACK_POTION_FORMULA")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class AttackPotionFormula : AlchemyFormulaCard
{
    public AttackPotionFormula()
        : base(PotionFamily.Attack, (AlchemyPrinciples.Corruption.Id, 2))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "CORRUPTION_POTION_FORMULA")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class CorruptionPotionFormula : AlchemyFormulaCard
{
    public CorruptionPotionFormula()
        : base(
            PotionFamily.Corruption,
            (AlchemyPrinciples.Corruption.Id, 1),
            (AlchemyPrinciples.Volatility.Id, 1))
    {
        ConfigureCosts();
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "VOLATILE_DRAW_POTION_FORMULA")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class VolatileDrawPotionFormula : AlchemyFormulaCard
{
    public VolatileDrawPotionFormula()
        : base(PotionFamily.VolatileDraw, (AlchemyPrinciples.Volatility.Id, 2))
    {
        ConfigureCosts();
    }
}