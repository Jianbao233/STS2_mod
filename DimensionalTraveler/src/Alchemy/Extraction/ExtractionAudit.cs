using MegaCrit.Sts2.Core.Entities.Players;

namespace DimensionalTraveler.Alchemy.Extraction;

public enum ExtractionAuditStage
{
    Rejected,
    AwaitingChoice,
    Cancelled,
    Committed,
}

public sealed record ExtractionAuditRecord(
    long Sequence,
    ulong PlayerNetId,
    int PotionSlotIndex,
    string PotionId,
    ExtractionAuditStage Stage,
    string Detail);

public static class ExtractionAudit
{
    private const int Capacity = 64;
    private static readonly object Gate = new();
    private static readonly List<ExtractionAuditRecord> Records = [];
    private static long _sequence;

    public static void Record(
        Player player,
        int potionSlotIndex,
        string potionId,
        ExtractionAuditStage stage,
        string detail = "")
    {
        lock (Gate)
        {
            Records.Add(new ExtractionAuditRecord(
                Interlocked.Increment(ref _sequence),
                player.NetId,
                potionSlotIndex,
                potionId,
                stage,
                detail));
            if (Records.Count > Capacity)
                Records.RemoveRange(0, Records.Count - Capacity);
        }
    }

    public static IReadOnlyList<ExtractionAuditRecord> Capture(Player player)
    {
        lock (Gate)
        {
            return Records
                .Where(record => record.PlayerNetId == player.NetId)
                .ToArray();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Records.Clear();
            _sequence = 0;
        }
    }
}