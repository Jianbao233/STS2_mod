using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.Extraction;

public enum AlchemyPrincipleKind
{
    Vitality,
    Volatility,
    Corruption,
    Catalysis,
    Diffusion,
    Echo,
}

public enum ExtractionPlanScope
{
    SharedPool,
    ExplicitSpecial,
}

public enum ExtractionChoiceMode
{
    None,
    AttackPotion,
}

public readonly record struct ExtractionPotionReward(
    PotionFamily Family,
    PotionQuality Quality,
    bool IsUpgraded = false);

public sealed record ExtractionPlan(
    string PotionId,
    ExtractionPlanScope Scope,
    AlchemyPrincipleKind SpecialPrinciple,
    AlchemyPrincipleKind BasicPrinciple,
    int BasicAmount,
    IReadOnlyList<ExtractionPotionReward> PotionRewards,
    ExtractionChoiceMode ChoiceMode = ExtractionChoiceMode.None,
    int Gold = 0,
    int MaxHp = 0);

public sealed record ExtractionCatalogValidation(
    IReadOnlyList<string> MissingPlans,
    IReadOnlyList<string> StaleSharedPlans,
    IReadOnlyList<string> InvalidPlans)
{
    public bool IsValid => MissingPlans.Count == 0
        && StaleSharedPlans.Count == 0
        && InvalidPlans.Count == 0;
}

public static class PotionExtractionCatalog
{
    private static readonly IReadOnlyDictionary<string, ExtractionPlan> Plans =
        new Dictionary<string, ExtractionPlan>(StringComparer.Ordinal)
        {
            ["ATTACK_POTION"] = AttackChoice("ATTACK_POTION"),
            ["BEETLE_JUICE"] = Plan("BEETLE_JUICE", Special.Echo, Basic.Vitality, 4, RefinedPlus(PotionFamily.Weakness)),
            ["BLESSING_OF_THE_FORGE"] = Plan("BLESSING_OF_THE_FORGE", Special.Catalysis, Basic.Volatility, 4),
            ["BLOCK_POTION"] = Plan("BLOCK_POTION", Special.Diffusion, Basic.Vitality, 2, Normal(PotionFamily.Shield)),
            ["BOTTLED_POTENTIAL"] = Plan("BOTTLED_POTENTIAL", Special.Catalysis, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["CLARITY"] = Plan("CLARITY", Special.Echo, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["COLORLESS_POTION"] = Plan("COLORLESS_POTION", Special.Diffusion, Basic.Volatility, 2, Normal(PotionFamily.VolatileDraw)),
            ["CURE_ALL"] = Plan("CURE_ALL", Special.Catalysis, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["DEXTERITY_POTION"] = Plan("DEXTERITY_POTION", Special.Catalysis, Basic.Vitality, 2, Normal(PotionFamily.TemporaryDexterity)),
            ["DISTILLED_CHAOS"] = Plan("DISTILLED_CHAOS", Special.Echo, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["DROPLET_OF_PRECOGNITION"] = Plan("DROPLET_OF_PRECOGNITION", Special.Catalysis, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["DUPLICATOR"] = Plan("DUPLICATOR", Special.Echo, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["ENERGY_POTION"] = Plan("ENERGY_POTION", Special.Catalysis, Basic.Volatility, 2, Normal(PotionFamily.VolatileDraw)),
            ["ENTROPIC_BREW"] = Plan("ENTROPIC_BREW", Special.Diffusion, Basic.Volatility, 5),
            ["EXPLOSIVE_AMPOULE"] = Plan("EXPLOSIVE_AMPOULE", Special.Diffusion, Basic.Corruption, 2, Normal(PotionFamily.Attack)),
            ["FAIRY_IN_A_BOTTLE"] = Plan("FAIRY_IN_A_BOTTLE", Special.Echo, Basic.Vitality, 4, RefinedPlus(PotionFamily.SelfDefense)),
            ["FIRE_POTION"] = Plan("FIRE_POTION", Special.Catalysis, Basic.Corruption, 2, Normal(PotionFamily.Attack)),
            ["FLEX_POTION"] = Plan("FLEX_POTION", Special.Catalysis, Basic.Vitality, 2, Normal(PotionFamily.TemporaryStrength)),
            ["FORTIFIER"] = Plan("FORTIFIER", Special.Diffusion, Basic.Vitality, 3, Refined(PotionFamily.Shield)),
            ["FRUIT_JUICE"] = Plan("FRUIT_JUICE", Special.Catalysis, Basic.Vitality, 4, RefinedPlus(PotionFamily.SelfDefense)),
            ["FYSH_OIL"] = Plan("FYSH_OIL", Special.Catalysis, Basic.Vitality, 3, Normal(PotionFamily.TemporaryStrength), Normal(PotionFamily.TemporaryDexterity)),
            ["GAMBLERS_BREW"] = Plan("GAMBLERS_BREW", Special.Catalysis, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["GIGANTIFICATION_POTION"] = Plan("GIGANTIFICATION_POTION", Special.Catalysis, Basic.Vitality, 4, RefinedPlus(PotionFamily.TemporaryStrength)),
            ["HEART_OF_IRON"] = Plan("HEART_OF_IRON", Special.Echo, Basic.Vitality, 3, Refined(PotionFamily.SelfDefense)),
            ["LIQUID_BRONZE"] = Plan("LIQUID_BRONZE", Special.Echo, Basic.Corruption, 4),
            ["LIQUID_MEMORIES"] = Plan("LIQUID_MEMORIES", Special.Echo, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["LUCKY_TONIC"] = Plan("LUCKY_TONIC", Special.Catalysis, Basic.Vitality, 4, RefinedPlus(PotionFamily.Shield)),
            ["MAZALETHS_GIFT"] = Plan("MAZALETHS_GIFT", Special.Echo, Basic.Vitality, 4, RefinedPlus(PotionFamily.TemporaryStrength)),
            ["OROBIC_ACID"] = Plan("OROBIC_ACID", Special.Diffusion, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["POTION_OF_BINDING"] = Plan("POTION_OF_BINDING", Special.Diffusion, Basic.Corruption, 3, Normal(PotionFamily.Weakness), Normal(PotionFamily.Corruption)),
            ["POWDERED_DEMISE"] = Plan("POWDERED_DEMISE", Special.Echo, Basic.Corruption, 3, Refined(PotionFamily.Corruption)),
            ["POWER_POTION"] = Plan("POWER_POTION", Special.Diffusion, Basic.Volatility, 2, Normal(PotionFamily.VolatileDraw)),
            ["RADIANT_TINCTURE"] = Plan("RADIANT_TINCTURE", Special.Catalysis, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["REGEN_POTION"] = Plan("REGEN_POTION", Special.Echo, Basic.Vitality, 4),
            ["SHACKLING_POTION"] = Plan("SHACKLING_POTION", Special.Diffusion, Basic.Corruption, 4, RefinedPlus(PotionFamily.StrengthReduction)),
            ["SHIP_IN_A_BOTTLE"] = Plan("SHIP_IN_A_BOTTLE", Special.Diffusion, Basic.Vitality, 4, RefinedPlus(PotionFamily.Shield)),
            ["SKILL_POTION"] = Plan("SKILL_POTION", Special.Diffusion, Basic.Volatility, 2, Normal(PotionFamily.VolatileDraw)),
            ["SNECKO_OIL"] = Plan("SNECKO_OIL", Special.Catalysis, Basic.Volatility, 4, RefinedPlus(PotionFamily.VolatileDraw)),
            ["SPEED_POTION"] = Plan("SPEED_POTION", Special.Catalysis, Basic.Vitality, 2, Normal(PotionFamily.TemporaryDexterity)),
            ["STABLE_SERUM"] = Plan("STABLE_SERUM", Special.Echo, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["STRENGTH_POTION"] = Plan("STRENGTH_POTION", Special.Catalysis, Basic.Vitality, 2, Normal(PotionFamily.TemporaryStrength)),
            ["SWIFT_POTION"] = Plan("SWIFT_POTION", Special.Catalysis, Basic.Volatility, 2, Normal(PotionFamily.VolatileDraw)),
            ["TOUCH_OF_INSANITY"] = Plan("TOUCH_OF_INSANITY", Special.Catalysis, Basic.Volatility, 3, Refined(PotionFamily.VolatileDraw)),
            ["VULNERABLE_POTION"] = Plan("VULNERABLE_POTION", Special.Diffusion, Basic.Corruption, 2, Normal(PotionFamily.Corruption)),
            ["WEAK_POTION"] = Plan("WEAK_POTION", Special.Diffusion, Basic.Corruption, 2, Normal(PotionFamily.Weakness)),

            ["GLOWWATER_POTION"] = Plan("GLOWWATER_POTION", Special.Catalysis, Basic.Corruption, 5, RefinedPlus(PotionFamily.VolatileDraw), scope: ExtractionPlanScope.ExplicitSpecial),
            ["FOUL_POTION"] = Plan("FOUL_POTION", Special.Diffusion, Basic.Corruption, 3, scope: ExtractionPlanScope.ExplicitSpecial, gold: 200, maxHp: 3),
            ["POTION_SHAPED_ROCK"] = Plan("POTION_SHAPED_ROCK", Special.Catalysis, Basic.Corruption, 1, Normal(PotionFamily.Attack), scope: ExtractionPlanScope.ExplicitSpecial),
        };

    public static IReadOnlyList<ExtractionPlan> All => Plans.Values
        .OrderBy(static plan => plan.PotionId, StringComparer.Ordinal)
        .ToArray();

    public static bool TryGet(string potionId, out ExtractionPlan plan) =>
        Plans.TryGetValue(potionId, out plan!);

    public static ExtractionCatalogValidation ValidateSharedPool(IEnumerable<string> runtimePotionIds)
    {
        var actualIds = runtimePotionIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var expectedIds = Plans.Values
            .Where(static plan => plan.Scope == ExtractionPlanScope.SharedPool)
            .Select(static plan => plan.PotionId)
            .ToHashSet(StringComparer.Ordinal);
        var invalidPlans = Plans.Values
            .Where(static plan => !IsValid(plan))
            .Select(static plan => plan.PotionId)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        return new ExtractionCatalogValidation(
            MissingPlans: actualIds.Except(expectedIds).OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            StaleSharedPlans: expectedIds.Except(actualIds).OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            InvalidPlans: invalidPlans);
    }

    private static ExtractionPlan AttackChoice(string id) => new(
        id,
        ExtractionPlanScope.SharedPool,
        Special.Diffusion,
        Basic.Volatility,
        BasicAmount: 2,
        PotionRewards: [],
        ChoiceMode: ExtractionChoiceMode.AttackPotion);

    private static ExtractionPlan Plan(
        string id,
        AlchemyPrincipleKind special,
        AlchemyPrincipleKind basic,
        int basicAmount,
        ExtractionPotionReward? first = null,
        ExtractionPotionReward? second = null,
        ExtractionPlanScope scope = ExtractionPlanScope.SharedPool,
        int gold = 0,
        int maxHp = 0) => new(
        id,
        scope,
        special,
        basic,
        basicAmount,
        [.. new[] { first, second }.OfType<ExtractionPotionReward>()],
        Gold: gold,
        MaxHp: maxHp);

    private static ExtractionPotionReward Normal(PotionFamily family) =>
        new(family, PotionQuality.Normal);

    private static ExtractionPotionReward Refined(PotionFamily family) =>
        new(family, PotionQuality.Refined);

    private static ExtractionPotionReward RefinedPlus(PotionFamily family) =>
        new(family, PotionQuality.Refined, IsUpgraded: true);

    private static bool IsValid(ExtractionPlan plan) =>
        !string.IsNullOrWhiteSpace(plan.PotionId)
        && plan.BasicAmount > 0
        && plan.Gold >= 0
        && plan.MaxHp >= 0
        && (plan.ChoiceMode != ExtractionChoiceMode.AttackPotion || plan.PotionRewards.Count == 0)
        && (plan.ChoiceMode != ExtractionChoiceMode.None || plan.PotionRewards.Count <= 2);

    private static class Basic
    {
        public const AlchemyPrincipleKind Vitality = AlchemyPrincipleKind.Vitality;
        public const AlchemyPrincipleKind Volatility = AlchemyPrincipleKind.Volatility;
        public const AlchemyPrincipleKind Corruption = AlchemyPrincipleKind.Corruption;
    }

    private static class Special
    {
        public const AlchemyPrincipleKind Catalysis = AlchemyPrincipleKind.Catalysis;
        public const AlchemyPrincipleKind Diffusion = AlchemyPrincipleKind.Diffusion;
        public const AlchemyPrincipleKind Echo = AlchemyPrincipleKind.Echo;
    }
}