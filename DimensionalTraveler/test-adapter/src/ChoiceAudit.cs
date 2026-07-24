using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace DimensionalTraveler.TestAdapter;

internal static class ChoiceAudit
{
    private const int Capacity = 64;
    private static readonly object Gate = new();
    private static readonly List<ChoiceRecord> Records = [];
    private static long _sequence;
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;

        var harmony = new Harmony("DimensionalTraveler.TestAdapter.ChoiceAudit");
        harmony.Patch(
            AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.ReserveChoiceId)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(ChoiceAudit), nameof(AfterReserve))));
        harmony.Patch(
            AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(ChoiceAudit), nameof(BeforeWaitForRemote))));
        harmony.Patch(
            AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.ReceiveReplayChoice)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(ChoiceAudit), nameof(BeforeReceiveReplay))));
        harmony.Patch(
            AccessTools.Method(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.SyncLocalChoice)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(ChoiceAudit), nameof(BeforeSyncLocal))));
        _installed = true;
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Records.Clear();
            _sequence = 0;
        }
    }

    public static JsonArray Capture()
    {
        lock (Gate)
        {
            return new JsonArray(Records.Select(record => (JsonNode?)new JsonObject
            {
                ["sequence"] = record.Sequence,
                ["operation"] = record.Operation,
                ["playerNetId"] = record.PlayerNetId.ToString(),
                ["choiceId"] = record.ChoiceId,
            }).ToArray());
        }
    }

    private static void AfterReserve(Player player, uint __result) =>
        Add("reserve", player, __result);

    private static void BeforeWaitForRemote(Player player, uint choiceId) =>
        Add("wait_remote", player, choiceId);

    private static void BeforeReceiveReplay(Player player, uint choiceId) =>
        Add("receive_replay", player, choiceId);

    private static void BeforeSyncLocal(Player player, uint choiceId) =>
        Add("sync_local", player, choiceId);

    private static void Add(string operation, Player player, uint choiceId)
    {
        lock (Gate)
        {
            Records.Add(new ChoiceRecord(
                Interlocked.Increment(ref _sequence),
                operation,
                player.NetId,
                choiceId));
            if (Records.Count > Capacity)
                Records.RemoveRange(0, Records.Count - Capacity);
        }
    }

    private readonly record struct ChoiceRecord(
        long Sequence,
        string Operation,
        ulong PlayerNetId,
        uint ChoiceId);
}