using MegaCrit.Sts2.Core.Models;

namespace DimensionalTraveler.Content.Cards.Potions;

public static class PotionCatalog
{
    private static readonly IReadOnlyDictionary<(PotionFamily Family, PotionQuality Quality), Func<CardModel>> CanonicalFactories =
        new Dictionary<(PotionFamily, PotionQuality), Func<CardModel>>
        {
            [(PotionFamily.Shield, PotionQuality.Normal)] = static () => ModelDb.Card<ShieldPotion>(),
            [(PotionFamily.Shield, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedShieldPotion>(),
            [(PotionFamily.Shield, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceShieldPotion>(),
            [(PotionFamily.SelfDefense, PotionQuality.Normal)] = static () => ModelDb.Card<SelfDefensePotion>(),
            [(PotionFamily.SelfDefense, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedSelfDefensePotion>(),
            [(PotionFamily.SelfDefense, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceSelfDefensePotion>(),
            [(PotionFamily.Attack, PotionQuality.Normal)] = static () => ModelDb.Card<AttackPotion>(),
            [(PotionFamily.Attack, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedAttackPotion>(),
            [(PotionFamily.Attack, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceAttackPotion>(),
            [(PotionFamily.Corruption, PotionQuality.Normal)] = static () => ModelDb.Card<CorruptionPotion>(),
            [(PotionFamily.Corruption, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedCorruptionPotion>(),
            [(PotionFamily.Corruption, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceCorruptionPotion>(),
            [(PotionFamily.VolatileDraw, PotionQuality.Normal)] = static () => ModelDb.Card<VolatileDrawPotion>(),
            [(PotionFamily.VolatileDraw, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedVolatileDrawPotion>(),
            [(PotionFamily.VolatileDraw, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceVolatileDrawPotion>(),
            [(PotionFamily.TemporaryStrength, PotionQuality.Normal)] = static () => ModelDb.Card<TemporaryStrengthPotion>(),
            [(PotionFamily.TemporaryStrength, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedTemporaryStrengthPotion>(),
            [(PotionFamily.TemporaryStrength, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceTemporaryStrengthPotion>(),
            [(PotionFamily.TemporaryDexterity, PotionQuality.Normal)] = static () => ModelDb.Card<TemporaryDexterityPotion>(),
            [(PotionFamily.TemporaryDexterity, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedTemporaryDexterityPotion>(),
            [(PotionFamily.TemporaryDexterity, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceTemporaryDexterityPotion>(),
            [(PotionFamily.Weakness, PotionQuality.Normal)] = static () => ModelDb.Card<WeaknessPotion>(),
            [(PotionFamily.Weakness, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedWeaknessPotion>(),
            [(PotionFamily.Weakness, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceWeaknessPotion>(),
            [(PotionFamily.StrengthReduction, PotionQuality.Normal)] = static () => ModelDb.Card<StrengthReductionPotion>(),
            [(PotionFamily.StrengthReduction, PotionQuality.Refined)] = static () => ModelDb.Card<RefinedStrengthReductionPotion>(),
            [(PotionFamily.StrengthReduction, PotionQuality.Masterpiece)] = static () => ModelDb.Card<MasterpieceStrengthReductionPotion>(),
        };

    public static void ValidateCompleteness()
    {
        var expected = Enum.GetValues<PotionFamily>()
            .SelectMany(
                static family => Enum.GetValues<PotionQuality>(),
                static (family, quality) => (family, quality))
            .ToArray();
        var missing = expected
            .Where(key => !CanonicalFactories.ContainsKey(key))
            .Select(static key => $"{key.family}/{key.quality}")
            .ToArray();
        if (missing.Length > 0 || CanonicalFactories.Count != expected.Length)
        {
            throw new InvalidOperationException(
                $"药剂目录不完整：期望 {expected.Length} 项，实际 {CanonicalFactories.Count} 项，缺失 [{string.Join(", ", missing)}]。");
        }
    }

    public static CardModel GetCanonical(PotionFamily family, PotionQuality quality) =>
        CanonicalFactories.TryGetValue((family, quality), out var factory)
            ? factory()
            : throw new ArgumentOutOfRangeException(nameof(family), $"未注册药剂模型：{family}/{quality}");
}