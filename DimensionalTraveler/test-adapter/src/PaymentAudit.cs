using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace DimensionalTraveler.TestAdapter;

internal static class PaymentAudit
{
    private const int Capacity = 64;
    private static readonly object Gate = new();
    private static readonly List<PaymentRecord> Records = [];
    private static long _sequence;
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;

        var harmony = new Harmony("DimensionalTraveler.TestAdapter.PaymentAudit");
        var prefix = AccessTools.Method(typeof(PaymentAudit), nameof(BeforeSpend));
        var postfix = AccessTools.Method(typeof(PaymentAudit), nameof(AfterSpend));
        var publicSpend = AccessTools.Method(
            typeof(SecondaryResourceCmd),
            nameof(SecondaryResourceCmd.Spend),
            [typeof(Player), typeof(string), typeof(int), typeof(CardModel), typeof(AbstractModel)]);
        var resolvedCardSpend = AccessTools.Method(
            typeof(SecondaryResourceCmd),
            "SpendResolvedCardPayment",
            [typeof(Player), typeof(string), typeof(int), typeof(CardModel), typeof(AbstractModel)]);

        if (publicSpend is null || resolvedCardSpend is null)
            throw new MissingMethodException("无法定位 RitsuLib 次级资源支付入口。");

        harmony.Patch(publicSpend, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        harmony.Patch(resolvedCardSpend, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        _installed = true;
    }

    public static void Clear()
    {
        lock (Gate)
            Records.Clear();
    }

    public static JsonArray Capture()
    {
        lock (Gate)
        {
            var result = new JsonArray();
            foreach (var record in Records)
            {
                result.Add(new JsonObject
                {
                    ["sequence"] = record.Sequence,
                    ["playerNetId"] = record.PlayerNetId.ToString(),
                    ["cardId"] = record.CardId,
                    ["resourceId"] = record.ResourceId,
                    ["requestedAmount"] = record.RequestedAmount,
                    ["before"] = record.Before,
                    ["after"] = record.After,
                    ["succeeded"] = record.Succeeded,
                    ["error"] = record.Error,
                });
            }
            return result;
        }
    }

    private static void BeforeSpend(
        Player player,
        string resourceId,
        int amount,
        CardModel? card,
        out PaymentAttempt __state)
    {
        __state = new(
            player,
            resourceId,
            amount,
            card?.Id.Entry,
            SecondaryResourceCmd.Get(player, resourceId));
    }

    private static void AfterSpend(ref Task<bool> __result, PaymentAttempt __state) =>
        __result = Observe(__result, __state);

    private static async Task<bool> Observe(Task<bool> operation, PaymentAttempt attempt)
    {
        try
        {
            var succeeded = await operation;
            Add(attempt, succeeded, error: null);
            return succeeded;
        }
        catch (Exception exception)
        {
            Add(attempt, succeeded: false, exception.Message);
            throw;
        }
    }

    private static void Add(PaymentAttempt attempt, bool succeeded, string? error)
    {
        var record = new PaymentRecord(
            Interlocked.Increment(ref _sequence),
            attempt.Player.NetId,
            attempt.CardId,
            attempt.ResourceId,
            attempt.RequestedAmount,
            attempt.Before,
            SecondaryResourceCmd.Get(attempt.Player, attempt.ResourceId),
            succeeded,
            error);

        lock (Gate)
        {
            Records.Add(record);
            if (Records.Count > Capacity)
                Records.RemoveRange(0, Records.Count - Capacity);
        }
    }

    private readonly record struct PaymentAttempt(
        Player Player,
        string ResourceId,
        int RequestedAmount,
        string? CardId,
        int Before);

    private readonly record struct PaymentRecord(
        long Sequence,
        ulong PlayerNetId,
        string? CardId,
        string ResourceId,
        int RequestedAmount,
        int Before,
        int After,
        bool Succeeded,
        string? Error);
}