using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.CardPiles;
using DimensionalTraveler.Bootstrap;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Alchemy.Backpack;

public interface IAlchemyBackpackCapacityModifier
{
    int CapacityModifier { get; }
}

public interface IAlchemyBackpackCapacityProvider
{
    int Capacity { get; }

    PotionQuality MaximumQuality { get; }
}

public static class AlchemyBackpack
{
    public const string LocalId = "backpack";
    public const int BaseCapacity = 3;

    public static ModCardPileDefinition Definition { get; private set; } = null!;

    public static PileType PileType => Definition.PileType;

    public static void Register()
    {
        if (Definition is not null)
            return;

        Definition = ModCardPileRegistry.For(Entry.ModId).RegisterOwned(LocalId, new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
        });
    }

    public static CardPile GetPile(Player player) =>
        PileType.GetPile(player)
        ?? throw new InvalidOperationException("药剂背包尚未附加到当前玩家的战斗状态。");

    public static IReadOnlyList<AlchemyPotionCard> GetPotions(Player player) =>
        GetPile(player).Cards.OfType<AlchemyPotionCard>().ToArray();

    public static int GetCapacity(Player player)
    {
        var baseCapacity = player.Piles
            .SelectMany(static pile => pile.Cards)
            .OfType<IAlchemyBackpackCapacityProvider>()
            .Select(static provider => provider.Capacity)
            .DefaultIfEmpty(BaseCapacity)
            .Max();
        var relicCapacity = player.Relics
            .OfType<IAlchemyBackpackCapacityModifier>()
            .Sum(static modifier => modifier.CapacityModifier);
        return baseCapacity + relicCapacity;
    }

    public static PotionQuality GetMaximumQuality(Player player) =>
        player.Piles
            .SelectMany(static pile => pile.Cards)
            .OfType<IAlchemyBackpackCapacityProvider>()
            .Select(static provider => provider.MaximumQuality)
            .DefaultIfEmpty(PotionQuality.Refined)
            .Max();

    public static bool CanStore(Player player, PotionQuality quality) =>
        quality <= GetMaximumQuality(player);

    public static bool HasSpace(Player player) =>
        GetPile(player).Cards.Count < GetCapacity(player);

    public static bool CanStoreNew(Player player, PotionQuality quality) =>
        HasSpace(player) && CanStore(player, quality);

    public static IReadOnlyList<AlchemyPotionCard> GetPurificationCandidates(Player player) =>
        GetPotions(player)
            .Where(potion => !potion.IsUpgraded && AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 1))
            .ToArray();

    public static IReadOnlyList<AlchemyPotionCard> GetSublimationCandidates(Player player) =>
        GetPotions(player)
            .Where(potion =>
                potion.Quality == PotionQuality.Normal
                && AlchemyPrinciples.CanPay(player, potion.MainPrinciple, 2))
            .ToArray();

    public static Task<bool> CommitPurification(AlchemyPotionCard potion)
    {
        if (potion.IsUpgraded || potion.Pile?.Type != PileType)
            return Task.FromResult(false);

        CardCmd.Upgrade(potion, CardPreviewStyle.None);
        AlchemyCombatState.Require(potion.Owner).Update(
            static turn => turn.Record(ExperimentRecord.UpgradedExistingPotion));
        return Task.FromResult(true);
    }

    public static async Task<bool> CommitSublimation(AlchemyPotionCard potion)
    {
        if (potion.Quality != PotionQuality.Normal || potion.Pile?.Type != PileType)
            return false;

        return await TransformQuality(potion, PotionQuality.Refined) is not null;
    }

    public static async Task<bool> CommitMasterpiece(AlchemyPotionCard potion)
    {
        if (potion.Quality != PotionQuality.Refined || potion.Pile?.Type != PileType)
            return false;

        return await TransformQuality(potion, PotionQuality.Masterpiece) is not null;
    }

    public static async Task<AlchemyPotionCard?> Brew(
        Player player,
        PotionFamily family,
        PotionQuality quality = PotionQuality.Normal,
        bool upgraded = false,
        PotionOrigin origin = PotionOrigin.Original,
        bool recordAsBrewed = true)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return null;

        var state = AlchemyCombatState.Require(player);
        var turn = state.Snapshot;
        var consumePrePurification = origin == PotionOrigin.Original
            && recordAsBrewed
            && !upgraded
            && turn.PrePurificationCharges > 0;
        var canonical = PotionCatalog.GetCanonical(family, quality);
        var potion = (AlchemyPotionCard)combatState.CreateCard(canonical, player);
        potion.SetOrigin(origin);
        if (upgraded || consumePrePurification)
            CardCmd.Upgrade(potion, CardPreviewStyle.None);

        var destination = origin == PotionOrigin.EchoDerived
            ? MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand
            : CanStoreNew(player, quality)
                ? PileType
                : MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand;
        var result = await CardPileCmd.AddGeneratedCardToCombat(potion, destination, player);
        if (!result.success)
            return null;

        if (origin == PotionOrigin.Original && (recordAsBrewed || consumePrePurification))
        {
            state.Update(turnState =>
            {
                if (recordAsBrewed)
                    turnState.Record(ExperimentRecord.BrewedOriginalPotion);
                if (consumePrePurification)
                    turnState.PrePurificationCharges -= 1;
            });
        }

        return potion;
    }

    public static async Task<AlchemyPotionCard?> TransformQuality(
        AlchemyPotionCard potion,
        PotionQuality quality)
    {
        if (potion.Quality == quality)
            return potion;

        var scope = potion.CardScope
            ?? throw new InvalidOperationException("药剂品质转换只能在跑局或战斗状态中执行。");
        var replacement = (AlchemyPotionCard)scope.CreateCard(
            PotionCatalog.GetCanonical(potion.Family, quality),
            potion.Owner);
        replacement.SetOrigin(potion.Origin);
        if (potion.IsUpgraded)
            CardCmd.Upgrade(replacement, CardPreviewStyle.None);

        var originalWasInBackpack = potion.Pile?.Type == PileType;
        var result = await CardCmd.Transform(potion, replacement, CardPreviewStyle.None);
        var transformed = result?.cardAdded as AlchemyPotionCard;
        if (transformed is not null)
        {
            if (originalWasInBackpack && !CanStore(transformed.Owner, transformed.Quality))
                await CardPileCmd.Add(transformed, MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand);

            AlchemyCombatState.Require(potion.Owner).Update(
                static turn => turn.Record(ExperimentRecord.UpgradedExistingPotion));
        }

        return transformed;
    }
}