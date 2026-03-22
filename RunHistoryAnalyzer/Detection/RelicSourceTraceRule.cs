using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1 - 遗物来源追溯】
/// 最终遗物列表中每件遗物必须来自以下合法来源之一：
/// - 初始遗物（角色天生携带）
/// - 遗物选择（RelicChoices 中 wasPicked=true）
/// - 古遗物祭坛（AncientChoices 中 wasChosen=true）
/// - 商店购买（BoughtRelics）
/// - 事件奖励（EventChoices 中 wasChosen=true）
///
/// 无法追溯来源的遗物 → 标记为 [高] 异常（控制台作弊 / 直接改存档）。
/// </summary>
public class RelicSourceTraceRule : Models.IAnomalyRule
{
    public string Name => "RelicSourceTrace";
    public string DisplayName => "遗物来源追溯";

    /// <summary>各角色的初始遗物ID集合（不带 RELIC. 前缀）。</summary>
    private static readonly Dictionary<string, HashSet<string>> _starterRelics = new()
    {
        ["CHARACTER.IRONCLAD"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "BURNING_BLOOD" },
        ["CHARACTER.SILENT"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "RING_OF_THE_SNAKE" },
        ["CHARACTER.DEFECT"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CRACKED_CORE" },
        // 亡灵契约师：存档为 NECROBINDER，初始遗物 Bound Phylactery
        ["CHARACTER.NECROBINDER"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "BOUND_PHYLACTERY" },
        ["CHARACTER.NECROMANCER"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "BOUND_PHYLACTERY", "CURSED_SIGIL" },
        // 储君 Regent：游戏内类 Regent，初始遗物 Divine Right（旧表误写为 AESCULAPIUS_BOOK）
        ["CHARACTER.HEXAGUARD"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "DIVINE_RIGHT" },
        ["CHARACTER.REGENT"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "DIVINE_RIGHT" },
        ["MOD.WATCHER"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "PURE" },
    };

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            if (history.AnalysisPlayerId != 0 && player.Id != history.AnalysisPlayerId) continue;

            string character = player.Character;
            var starters = _starterRelics.TryGetValue(character, out var s) ? s : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 收集所有可追溯的来源（使用规范化 ID，不带 RELIC. 前缀）
            var traceableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 初始遗物
            foreach (var id in starters)
                traceableIds.Add(id);

            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.PlayerId != player.Id) continue;

                // 遗物选择（如 boss swap）：choice 格式为 "RELIC.XXX"
                foreach (var choice in stat.RelicChoices)
                {
                    if (choice.WasPicked && !string.IsNullOrEmpty(choice.Choice))
                        traceableIds.Add(StripPrefix(choice.Choice));
                }

                // 古遗物祭坛：title.key 格式为 "XXX.title"（无 RELIC. 前缀）
                foreach (var ancient in stat.AncientChoices)
                {
                    if (ancient.WasChosen && !string.IsNullOrEmpty(ancient.TextKey))
                        traceableIds.Add(StripPrefix(ancient.TextKey));
                }

                // 事件奖励遗物：TextKey 或 title（table=relics）的 key，如 PRECISE_SCISSORS.title
                foreach (var evt in stat.EventChoices)
                {
                    if (!evt.WasChosen)
                        continue;
                    if (!string.IsNullOrEmpty(evt.TextKey))
                        traceableIds.Add(StripPrefix(evt.TextKey));
                    if (!string.IsNullOrEmpty(evt.Title?.Key)
                        && string.Equals(evt.Title.Table, "relics", StringComparison.OrdinalIgnoreCase))
                        traceableIds.Add(StripPrefix(evt.Title.Key));
                }

                // 商店购买：bought_relics 格式为 ModelId 字符串（可能带 RELIC. 前缀）
                foreach (var bought in stat.BoughtRelics)
                    traceableIds.Add(StripPrefix(bought));
            }

            // 最终遗物追溯
            var untracedRelics = new List<string>();
            foreach (var relic in player.Relics)
            {
                string id = relic.Id;
                string normalized = StripPrefix(id);

                bool isStarter = starters.Contains(normalized) || starters.Contains(id);
                bool isTraceable = traceableIds.Contains(normalized) || traceableIds.Contains(id);

                if (!isStarter && !isTraceable)
                    untracedRelics.Add(id);
            }

            if (untracedRelics.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("无法追溯来源的遗物：");
                foreach (var r in untracedRelics)
                    sb.Append($"{r}  ");
                sb.AppendLine();
                sb.Append("(遗物必须来自：初始遗物 / 遗物选择 / 古遗物祭坛 / 商店购买 / 事件奖励)");

                return new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "遗物来源追溯",
                    $"发现 {untracedRelics.Count} 件无法追溯来源的遗物",
                    sb.ToString(),
                    "可能原因：relic 控制台作弊 / 存档直接添加"
                );
            }
        }

        return null;
    }

    /// <summary>
    /// 移除 ID 前缀，返回规范化的短名。
    /// 输入示例："RELIC.SCROLL_BOXES" → "SCROLL_BOXES"
    ///           "SCROLL_BOXES"       → "SCROLL_BOXES"
    ///           "NEW_LEAF"           → "NEW_LEAF"
    ///           "LOST_COFFER.title"  → "LOST_COFFER"
    /// </summary>
    private static string StripPrefix(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (id.StartsWith("RELIC.", StringComparison.OrdinalIgnoreCase))
            return id.Substring("RELIC.".Length);
        int dot = id.LastIndexOf('.');
        if (dot > 0) // "NEW_LEAF.title" → "NEW_LEAF"
            return id.Substring(0, dot);
        return id;
    }
}
