using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace DimensionalTraveler.TestAdapter;

internal enum TestPotionGrantStage
{
    Requested,
    Committed,
    Rejected,
}

internal sealed record TestPotionGrantRecord(
    long Sequence,
    ulong PlayerNetId,
    string PotionId,
    TestPotionGrantStage Stage,
    int? SlotIndex,
    string? Detail);

internal static class TestPotionGrantAudit
{
    private static readonly Lock Gate = new();
    private static readonly List<TestPotionGrantRecord> Records = [];
    private static long _nextSequence;

    public static void Record(
        ulong playerNetId,
        string potionId,
        TestPotionGrantStage stage,
        int? slotIndex = null,
        string? detail = null)
    {
        lock (Gate)
        {
            Records.Add(new(
                Interlocked.Increment(ref _nextSequence),
                playerNetId,
                potionId,
                stage,
                slotIndex,
                detail));
        }
    }

    public static IReadOnlyList<TestPotionGrantRecord> Capture(Player player)
    {
        lock (Gate)
        {
            return Records
                .Where(record => record.PlayerNetId == player.NetId)
                .ToArray();
        }
    }

    public static bool TryGetCommitted(ulong playerNetId, string potionId, out TestPotionGrantRecord record)
    {
        lock (Gate)
        {
            record = Records.LastOrDefault(candidate =>
                candidate.PlayerNetId == playerNetId &&
                string.Equals(candidate.PotionId, potionId, StringComparison.Ordinal) &&
                candidate.Stage == TestPotionGrantStage.Committed)!;
            return record is not null;
        }
    }
}

internal static class TestPotionGrantAction
{
    private const string ActionKey = "test_potion_grant_v1";

    private static readonly RitsuLibManagedNetActionDescriptor<TestPotionGrantRequest> Descriptor = new(
        Entry.ModId,
        ActionKey,
        Serialize,
        Deserialize,
        Execute,
        GameActionType.CombatPlayPhaseOnly);

    public static void Register() => RitsuLibManagedNetActions.Register(Descriptor);

    public static bool Request(Player player, string potionId)
    {
        if (!CanRequest(player, potionId, out _))
            return false;

        var requested = RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            Descriptor,
            new TestPotionGrantRequest(potionId),
            player.NetId);
        if (requested)
            TestPotionGrantAudit.Record(player.NetId, potionId, TestPotionGrantStage.Requested);
        return requested;
    }

    public static bool CanRequest(Player player, string potionId, out string failureCode)
    {
        if (player.Creature.CombatState is null || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
        {
            failureCode = "not_player_play_phase";
            return false;
        }
        if (!player.CanUseOrRemovePotions)
        {
            failureCode = "potion_interaction_disabled";
            return false;
        }
        if (player.PotionSlots.All(static potion => potion is not null))
        {
            failureCode = "potion_slots_full";
            return false;
        }
        if (ResolvePotion(potionId) is null)
        {
            failureCode = "potion_not_found";
            return false;
        }

        failureCode = string.Empty;
        return true;
    }

    private static byte[] Serialize(TestPotionGrantRequest request) => JsonSerializer.SerializeToUtf8Bytes(request);

    private static TestPotionGrantRequest Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<TestPotionGrantRequest>(payload)
        ?? throw new InvalidOperationException("测试药水注入动作载荷为空或格式无效。");

    private static async Task Execute(RitsuLibManagedNetActionContext<TestPotionGrantRequest> context)
    {
        var player = context.Player;
        var potionId = context.Message.PotionId;
        if (!CanRequest(player, potionId, out var failureCode))
        {
            TestPotionGrantAudit.Record(player.NetId, potionId, TestPotionGrantStage.Rejected, detail: failureCode);
            return;
        }

        var potion = ResolvePotion(potionId);
        if (potion is null)
        {
            TestPotionGrantAudit.Record(player.NetId, potionId, TestPotionGrantStage.Rejected, detail: "potion_not_found");
            return;
        }

        var result = await PotionCmd.TryToProcure(potion.ToMutable(), player);
        if (!result.success)
        {
            TestPotionGrantAudit.Record(
                player.NetId,
                potionId,
                TestPotionGrantStage.Rejected,
                detail: result.failureReason.ToString());
            return;
        }

        TestPotionGrantAudit.Record(
            player.NetId,
            potionId,
            TestPotionGrantStage.Committed,
            player.GetPotionSlotIndex(result.potion));
    }

    private static PotionModel? ResolvePotion(string potionId) =>
        ModelDb.AllPotions.FirstOrDefault(potion =>
            string.Equals(potion.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));

    private sealed record TestPotionGrantRequest(string PotionId);
}