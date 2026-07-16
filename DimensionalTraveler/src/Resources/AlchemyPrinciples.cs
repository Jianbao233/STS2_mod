using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using DimensionalTraveler.Bootstrap;
using DimensionalTraveler.Characters;

namespace DimensionalTraveler.Resources;

public static class AlchemyPrinciples
{
    public const string VitalityLocalId = "vitality";
    public const string VolatilityLocalId = "volatility";
    public const string CorruptionLocalId = "corruption";

    public static SecondaryResourceDefinition Vitality { get; private set; } = null!;

    public static SecondaryResourceDefinition Volatility { get; private set; } = null!;

    public static SecondaryResourceDefinition Corruption { get; private set; } = null!;

    public static IReadOnlyList<SecondaryResourceDefinition> All { get; private set; } = [];

    public static void Register()
    {
        if (All.Count > 0)
            return;

        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);
        Vitality = RegisterPrinciple(registry, VitalityLocalId);
        Volatility = RegisterPrinciple(registry, VolatilityLocalId);
        Corruption = RegisterPrinciple(registry, CorruptionLocalId);
        All = [Vitality, Volatility, Corruption];

        registry.AlwaysShowInCombatUiForCharacter<Traveler>(VitalityLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(VolatilityLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(CorruptionLocalId);
    }

    public static int Get(Player player, SecondaryResourceDefinition principle) =>
        SecondaryResourceCmd.Get(player, principle.Id);

    public static bool CanPay(Player player, SecondaryResourceDefinition principle, int amount) =>
        amount <= 0 || Get(player, principle) >= amount;

    public static Task<int> Gain(
        Player player,
        SecondaryResourceDefinition principle,
        int amount,
        AbstractModel? source = null) =>
        SecondaryResourceCmd.Gain(player, principle.Id, amount, source);

    public static Task<bool> Spend(
        Player player,
        SecondaryResourceDefinition principle,
        int amount,
        CardModel? card = null,
        AbstractModel? source = null) =>
        SecondaryResourceCmd.Spend(player, principle.Id, amount, card, source);

    private static SecondaryResourceDefinition RegisterPrinciple(
        ModSecondaryResourceRegistry registry,
        string localId) =>
        registry.Register(localId, new SecondaryResourceDefinition(
            defaultAmount: 0,
            baseMaxAmount: null,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat));
}