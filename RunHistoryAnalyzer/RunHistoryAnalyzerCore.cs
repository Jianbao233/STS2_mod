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
        new Detection.HpConservationRule(),
        new Detection.HpBoundaryRule(),
        // P1：规则明确，低误报
        new Detection.CardSourceTraceRule(),
        new Detection.RelicSourceTraceRule(),
        // P2：参考级
        new Detection.PotionSourceTraceRule(),
    };

    /// <summary>分析结果缓存：key = .run 文件路径，value = 分析结果（含文件修改时间）。</summary>
    private static readonly Dictionary<string, CachedResult> _cache = new();

    /// <summary>
    /// 分析指定 .run 文件，返回所有检测到的异常。
    /// 若文件未变化则使用缓存。
    /// </summary>
    public static AnalyzeResult Analyze(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), "文件不存在或路径无效");

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
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), "文件不存在或路径无效");

        try
        {
            var lastWriteTime = File.GetLastWriteTimeUtc(filePath);

            // 检查缓存：文件未修改则返回缓存结果
            if (_cache.TryGetValue(filePath, out var cached))
            {
                if (cached.FileLastWriteTime == lastWriteTime)
                    return cached.Result;
            }

            // 加载 JSON
            string json = File.ReadAllText(filePath);
            var history = JsonSerializer.Deserialize<RunHistoryData>(json, _jsonOptions);

            if (history == null)
                return new AnalyzeResult(filePath, null, new List<Anomaly>(), "JSON 解析失败");

            // 执行所有检测规则
            var anomalies = new List<Anomaly>();
            foreach (var rule in _rules)
            {
                try
                {
                    var anomaly = rule.Check(history);
                    if (anomaly != null)
                        anomalies.Add(anomaly);
                }
                catch (Exception ex)
                {
                    Godot.GD.Print($"[RunHistoryAnalyzer] Rule {rule.Name} threw: {ex.Message}");
                }
            }

            // 按等级排序：High > Medium > Low
            anomalies.Sort((a, b) => b.Level.CompareTo(a.Level));

            var result = new AnalyzeResult(filePath, history, anomalies, null);

            // 更新缓存
            _cache[filePath] = new CachedResult(lastWriteTime, result);

            Godot.GD.Print($"[RunHistoryAnalyzer] 分析完成: {anomalies.Count} 条异常 | {System.IO.Path.GetFileName(filePath)}");

            return result;
        }
        catch (JsonException ex)
        {
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), $"JSON 格式错误：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new AnalyzeResult(filePath, null, new List<Anomaly>(), $"分析失败：{ex.Message}");
        }
    }

    /// <summary>清除指定文件的缓存。</summary>
    public static void InvalidateCache(string filePath)
    {
        _cache.Remove(filePath);
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

    public AnalyzeResult(string filePath, RunHistoryData? history, List<Anomaly> anomalies, string? errorMessage)
    {
        FilePath = filePath;
        History = history;
        Anomalies = anomalies;
        ErrorMessage = errorMessage;
    }

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

        if (History != null)
        {
            sb.AppendLine($"对局时间：{History.GetStartDateTime():yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"角色：{GetCharacterDisplay(History)} | 难度：{History.GetDifficulty()} | 结果：{(History.Win ? "胜利" : "失败")}");
            sb.AppendLine($"种子：{History.Seed}");
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

    private static string GetCharacterDisplay(RunHistoryData history)
    {
        if (history.Players.Count == 0) return "未知";
        var ch = history.Players[0].Character;
        return ch switch
        {
            "CHARACTER.IRONCLAD" => "铁甲战士",
            "CHARACTER.SILENT" => "静默猎手",
            "CHARACTER.DEFECT" => "故障机器人",
            "CHARACTER.NECROMANCER" => "亡灵契约师",
            "CHARACTER.HEXAGUARD" => "储君",
            "MOD.WATCHER" => "观者",
            _ => ch
        };
    }
}
