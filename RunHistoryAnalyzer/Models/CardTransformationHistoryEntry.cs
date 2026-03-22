using System.Text.Json.Serialization;

namespace RunHistoryAnalyzer.Models;

/// <summary>
/// 与游戏 <c>MegaCrit.Sts2.Core.Runs.History.CardTransformationHistoryEntry</c> 对齐。
/// </summary>
public sealed class CardTransformationHistoryEntry
{
    [JsonPropertyName("original_card")]
    public SerializableCard OriginalCard { get; set; } = new();

    [JsonPropertyName("final_card")]
    public SerializableCard FinalCard { get; set; } = new();
}
