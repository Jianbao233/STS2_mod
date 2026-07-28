using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Players;

namespace DimensionalTraveler.Content.Relics;

internal static class PotionReserveCombatEntrySnapshot
{
    private static readonly Dictionary<ulong, bool> WasFullAtEntry = [];

    public static bool IsFullAtEntry(Player player) =>
        WasFullAtEntry.TryGetValue(player.NetId, out var wasFull) && wasFull;

    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
    private static class CaptureBeforeCombatStart
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static void Prefix(IRunState runState)
        {
            WasFullAtEntry.Clear();
            foreach (var player in runState.Players)
                WasFullAtEntry[player.NetId] = player.Creature.CurrentHp == player.Creature.MaxHp;
        }
    }
}