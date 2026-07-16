using MegaCrit.Sts2.Core.Models;

namespace DimensionalTraveler.Content.Cards.Potions;

public static class PotionCatalog
{
    private static readonly IReadOnlyDictionary<(PotionFamily Family, PotionQuality Quality), Func<CardModel>> CanonicalFactories =
        new Dictionary<(PotionFamily, PotionQuality), Func<CardModel>>
        {
            [(PotionFamily.Shield, PotionQuality.Normal)] = static () => ModelDb.Card<ShieldPotion>(),
            [(PotionFamily.Shield, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedShieldPotion>(),
            [(PotionFamily.SelfDefense, PotionQuality.Normal)] = static () => ModelDb.Card<SelfDefensePotion>(),
            [(PotionFamily.SelfDefense, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedSelfDefensePotion>(),
            [(PotionFamily.Attack, PotionQuality.Normal)] = static () => ModelDb.Card<AttackPotion>(),
            [(PotionFamily.Attack, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedAttackPotion>(),
            [(PotionFamily.Corruption, PotionQuality.Normal)] = static () => ModelDb.Card<CorruptionPotion>(),
            [(PotionFamily.Corruption, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedCorruptionPotion>(),
            [(PotionFamily.VolatileDraw, PotionQuality.Normal)] = static () => ModelDb.Card<VolatileDrawPotion>(),
            [(PotionFamily.VolatileDraw, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedVolatileDrawPotion>(),
        };

    public static CardModel GetCanonical(PotionFamily family, PotionQuality quality) =>
        CanonicalFactories.TryGetValue((family, quality), out var factory)
            ? factory()
            : throw new ArgumentOutOfRangeException(nameof(family), $"未注册药剂模型：{family}/{quality}");
}