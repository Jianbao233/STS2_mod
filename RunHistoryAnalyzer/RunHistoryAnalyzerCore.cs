using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RunHistoryAnalyzer.Models;

namespace RunHistoryAnalyzer;

/// <summary>
/// 核心分析引擎：负责加载 .run JSON 文件、执行所有检测规则、缓存结果。
/// </summary>
public static class RunHistoryAnalyzerCore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false
    };

    private static readonly List<IAnomalyRule> _rules = new()
    {
        // P0：数学等式，零模糊
        new Detection.GoldConservationRule(),
        new Detection.ShopGoldSpikeRule(),
        new Detection.NonShopLargeGoldGainRule(),
        new Detection.HpConservationRule(),
        new Detection.HpBoundaryRule(),
        // P1：规则明确，低误报
        new Detection.CardSourceTraceRule(),
        new Detection.CharacterCardAffinityRule(),
        new Detection.RelicSourceTraceRule(),
        new Detection.RelicMultiPickRule(),
        // P2：参考级
        new Detection.PotionSourceTraceRule(),
    };

    /// <summary>分析结果缓存：key = 文件路径 + 玩家ID（联机切换角色时不得共用同一份结果）。</summary>
    private static readonly Dictionary<string, CachedResult> _cache = new();

    private static string MakeCacheKey(string filePath, ulong playerId) => $"{filePath}\u001f{playerId}";

    /// <summary>
    /// 分析指定 .run 文件，返回所有检测到的异常。
    /// 若文件未变化则使用缓存。
    /// </summary>
    public static AnalyzeResult Analyze(string filePath)
    {
        return Analyze(filePath, currentPlayerId: 0);
    }

    /// <summary>
    /// 分析指定 .run 文件，只针对指定玩家 ID（如 0 表示分析所有玩家）。
    /// 若文件未变化则使用缓存。
    /// </summary>
    public static AnalyzeResult Analyze(string filePath, ulong currentPlayerId)
    {
        if (string.IsNullOrEmpty(filePath))
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), "文件不存在或路径无效", currentPlayerId);

        if (!File.Exists(filePath))
        {
            try
            {
                var norm = filePath.Replace("\\", "/");
                if (norm.StartsWith("user://", StringComparison.Ordinal))
                {
                    var abs = ProjectSettings.GlobalizePath(norm);
                    if (File.Exists(abs))
                        filePath = abs;
                }
            }
            catch { /* keep filePath */ }
        }

        if (!File.Exists(filePath))
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), "文件不存在或路径无效", currentPlayerId);

        try
        {
            var lastWriteTime = File.GetLastWriteTimeUtc(filePath);

            var cacheKey = MakeCacheKey(filePath, currentPlayerId);
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.FileLastWriteTime == lastWriteTime)
                    return cached.Result;
            }

            // 加载 JSON
            string json = File.ReadAllText(filePath);
            var history = JsonSerializer.Deserialize<RunHistoryData>(json, _jsonOptions);

            if (history == null)
                return new AnalyzeResult(filePath, null, new List<Anomaly>(), "JSON 解析失败", currentPlayerId);

            // 执行所有检测规则（传入 currentPlayerId，过滤到对应玩家）
            history.AnalysisPlayerId = currentPlayerId;
            var anomalies = new List<Anomaly>();
            foreach (var rule in _rules)
            {
                try
                {
                    foreach (var anomaly in rule.Check(history))
                        anomalies.Add(anomaly);
                }
                catch (Exception ex)
                {
                    Godot.GD.Print($"[RunHistoryAnalyzer] Rule {rule.Name} threw: {ex.Message}");
                }
            }

            // 按等级排序：High > Medium > Low
            anomalies.Sort((a, b) => b.Level.CompareTo(a.Level));

            var result = new AnalyzeResult(filePath, history, anomalies, null, currentPlayerId);

            _cache[cacheKey] = new CachedResult(lastWriteTime, result);

            Godot.GD.Print($"[RunHistoryAnalyzer] 分析完成: {anomalies.Count} 条异常 | {System.IO.Path.GetFileName(filePath)}");

            return result;
        }
        catch (JsonException ex)
        {
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), $"JSON 格式错误：{ex.Message}", currentPlayerId);
        }
        catch (Exception ex)
        {
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), $"分析失败：{ex.Message}", currentPlayerId);
        }
    }

    /// <summary>清除指定文件的缓存。</summary>
    public static void InvalidateCache(string filePath)
    {
        var prefix = filePath + "\u001f";
        List<string>? toRemove = null;
        foreach (var k in _cache.Keys)
        {
            if (k == filePath || k.StartsWith(prefix, StringComparison.Ordinal))
                (toRemove ??= new List<string>()).Add(k);
        }
        if (toRemove != null)
            foreach (var k in toRemove)
                _cache.Remove(k);
    }

    /// <summary>清除所有缓存。</summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    private sealed class CachedResult
    {
        public DateTime FileLastWriteTime { get; }
        public AnalyzeResult Result { get; }

        public CachedResult(DateTime fileLastWriteTime, AnalyzeResult result)
        {
            FileLastWriteTime = fileLastWriteTime;
            Result = result;
        }
    }
}

/// <summary>
/// 单次分析的结果容器。
/// </summary>
public sealed class AnalyzeResult
{
    public string FilePath { get; }
    public RunHistoryData? History { get; }
    public List<Anomaly> Anomalies { get; }
    public string? ErrorMessage { get; }
    /// <summary>分析时指定的目标玩家 ID（0 = 分析所有玩家）。</summary>
    public ulong CurrentPlayerId { get; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasAnomalies => Anomalies.Count > 0;
    public AnomalyLevel MaxLevel
    {
        get
        {
            if (Anomalies.Count == 0) return AnomalyLevel.None;
            return Anomalies[0].Level; // 已按等级排序
        }
    }

    public AnalyzeResult(string filePath, RunHistoryData? history, List<Anomaly> anomalies, string? errorMessage, ulong currentPlayerId = 0)
    {
        FilePath = filePath;
        History = history;
        Anomalies = anomalies;
        ErrorMessage = errorMessage;
        CurrentPlayerId = currentPlayerId;
    }

    /// <summary>获取分析目标角色的中文名（用于报告标题）。</summary>
    private string GetTargetCharacterName()
    {
        if (History == null) return "未知";
        var player = History.GetTargetPlayer();
        return GetCharacterDisplayInternal(player?.Character ?? "");
    }

    private static string GetCharacterDisplayInternal(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => "铁甲战士",
        "CHARACTER.SILENT" => "静默猎手",
        "CHARACTER.DEFECT" => "故障机器人",
        "CHARACTER.NECROMANCER" => "亡灵契约师",
        "CHARACTER.NECROBINDER" => "亡灵契约师",
        "CHARACTER.HEXAGUARD" => "储君",
        "CHARACTER.REGENT" => "储君",
        "MOD.WATCHER" => "观者",
        _ => characterId
    };

    /// <summary>生成导出报告文本。</summary>
    public string ToExportText()
    {
        if (HasError)
        {
            return $"[RunHistoryAnalyzer] 分析失败：{ErrorMessage}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== RunHistoryAnalyzer 检测报告 ===");
        sb.AppendLine($"分析时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"源文件：{FilePath}");
        if (CurrentPlayerId != 0)
            sb.AppendLine($"分析目标玩家 ID：{CurrentPlayerId}");
        else
            sb.AppendLine("分析目标玩家：全部（单玩家存档等效于首位）");

        if (History != null)
        {
            var targetChar = History.GetTargetPlayer();
            var charName = GetCharacterDisplayInternal(targetChar?.Character ?? "");
            var isMultiplayer = History.Players.Count > 1;
            var diff = History.GetDifficulty();
            var result = History.Win ? "胜利" : "失败";
            sb.AppendLine($"对局时间：{History.GetStartDateTime():yyyy-MM-dd HH:mm:ss}");
            if (isMultiplayer)
                sb.AppendLine($"目标角色：{charName}（联机模式，共 {History.Players.Count} 名玩家）");
            else
                sb.AppendLine($"角色：{charName}");
            sb.AppendLine($"难度：{diff} | 结果：{result}");
            sb.AppendLine($"种子：{History.Seed}");
            if (!string.IsNullOrEmpty(History.GameMode))
                sb.AppendLine($"游戏模式：{History.GameMode}");
            if (!string.IsNullOrEmpty(History.BuildId))
                sb.AppendLine($"构建/版本标识：{History.BuildId}");
        }

        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"检测结果：{Anomalies.Count} 项异常");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        if (Anomalies.Count == 0)
        {
            sb.AppendLine("未检测到明显异常。");
        }
        else
        {
            foreach (var a in Anomalies)
            {
                string icon = a.Level switch
                {
                    AnomalyLevel.High => "【高】",
                    AnomalyLevel.Medium => "【中】",
                    AnomalyLevel.Low => "【低】",
                    _ => "【?】"
                };
                sb.AppendLine($"{icon}{a.Title}");
                sb.AppendLine($"  {a.Description}");
                if (!string.IsNullOrEmpty(a.Detail))
                {
                    foreach (var line in a.Detail.Split('\n'))
                        sb.AppendLine($"  {line.Trim()}");
                }
                if (!string.IsNullOrEmpty(a.PossibleCause))
                    sb.AppendLine($"  {a.PossibleCause}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("本报告由 RunHistoryAnalyzer 自动生成");
        sb.AppendLine("仅供辅助参考，无法作为严格仲裁依据");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        return sb.ToString();
    }
}
