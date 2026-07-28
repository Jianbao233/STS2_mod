using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Pools;

namespace DimensionalTraveler.Content.Relics;

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "POTION_RESERVE")]
public sealed class PotionReserve : ModRelicTemplate, IAlchemyBackpackCapacityModifier
{
    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("POTION_BELT");

    public override RelicRarity Rarity => RelicRarity.Shop;

    public int CapacityModifier => 1;

    public override async Task AfterObtained()
    {
        await PlayerCmd.GainMaxPotionCount(2, Owner);
    }

    public override decimal ModifyMerchantPrice(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        MerchantEntry entry,
        decimal cost) =>
        player == Owner && entry is MerchantPotionEntry
            ? cost * 0.25m
            : cost;

    public override async Task BeforeCombatStart()
    {
        if (!PotionReserveCombatEntrySnapshot.IsFullAtEntry(Owner) || !Owner.HasOpenPotionSlots)
            return;

        var sharedPool = ModelDb.PotionPool<SharedPotionPool>()
            .AllPotions
            .OrderBy(static potion => potion.Id.Entry, StringComparer.Ordinal)
            .ToArray();
        if (sharedPool.Length == 0)
            return;

        var canonical = Owner.RunState.Rng.CombatPotionGeneration.NextItem(sharedPool);
        if (canonical is null)
            return;

        var potion = canonical.ToMutable();
        var result = await PotionCmd.TryToProcure(potion, Owner);
        if (result.success)
            Flash();
    }
}