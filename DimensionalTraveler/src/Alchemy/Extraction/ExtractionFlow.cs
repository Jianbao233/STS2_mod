using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace DimensionalTraveler.Alchemy.Extraction;

public static class ExtractionFlow
{
    public static void Register() => ExtractPotionGameAction.Register();

    public static bool CanEnqueue(Player player, int potionSlotIndex, out string failureCode)
    {
        if (!TryGetPlan(player, potionSlotIndex, out _, out failureCode))
            return false;

        failureCode = string.Empty;
        return true;
    }

    public static bool TryGetPlan(
        Player player,
        int potionSlotIndex,
        out ExtractionPlan plan,
        out string failureCode)
    {
        plan = null!;
        failureCode = "unknown";
        if (!State.AlchemyCombatState.IsTraveler(player))
        {
            failureCode = "wrong_character";
            return false;
        }
        if (player.Creature.CombatState is null
            || player.PlayerCombatState?.Phase != MegaCrit.Sts2.Core.Combat.PlayerTurnPhase.Play)
        {
            failureCode = "not_player_play_phase";
            return false;
        }
        if (!player.CanUseOrRemovePotions)
        {
            failureCode = "potion_interaction_disabled";
            return false;
        }
        if (potionSlotIndex < 0 || potionSlotIndex >= player.PotionSlots.Count)
        {
            failureCode = "invalid_potion_slot";
            return false;
        }

        var potion = player.PotionSlots[potionSlotIndex];
        if (potion is null || potion.Owner != player || potion.IsQueued)
        {
            failureCode = "potion_unavailable";
            return false;
        }
        if (!PotionExtractionCatalog.TryGet(potion.Id.Entry, out plan))
        {
            failureCode = "recipe_unregistered";
            return false;
        }

        failureCode = string.Empty;
        return true;
    }

    public static bool Enqueue(Player player, int potionSlotIndex, out string failureCode)
    {
        if (!TryGetPlan(player, potionSlotIndex, out var plan, out failureCode))
            return false;

        if (!ExtractPotionGameAction.Request(player, potionSlotIndex, plan.PotionId))
        {
            failureCode = "managed_action_request_rejected";
            return false;
        }

        failureCode = string.Empty;
        return true;
    }
}