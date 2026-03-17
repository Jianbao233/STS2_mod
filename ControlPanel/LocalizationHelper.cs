using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;

namespace ControlPanel;

/// <summary>
/// 通过反射调用游戏 LocString 获取本地化文本；失败时回退到本地 events.json 文件。
/// </summary>
public static class LocalizationHelper
{
    /// <summary>获取药水描述，key 如 "ATTACK_POTION.description"</summary>
    public static string GetPotionDescription(string potionId)
    {
        return GetLocString("potions", potionId + ".description");
    }

    /// <summary>获取事件完整文本：description + 各选项。优先 LocString，失败时从 events.json 文件读取</summary>
    public static string GetEventText(string eventId)
    {
        var sb = new System.Text.StringBuilder();
        var desc = GetLocString("events", eventId + ".pages.INITIAL.description");
        var title = GetLocString("events", eventId + ".title");
        if (!string.IsNullOrEmpty(desc)) sb.AppendLine(desc);
        if (!string.IsNullOrEmpty(title) && sb.Length == 0) sb.AppendLine(title);
        TryAppendOptions("events", eventId, sb);
        var result = sb.ToString().TrimEnd();
        if (string.IsNullOrEmpty(result)) result = GetEventTextFromFile(eventId);
        return result;
    }

    private static string GetEventTextFromFile(string eventId)
    {
        try
        {
            var exe = OS.GetExecutablePath();
            var gameDir = Path.GetDirectoryName(exe) ?? "";
            var candidates = new[]
            {
                Path.Combine(gameDir, "extracted", "localization", "zhs", "events.json"),
                Path.Combine(gameDir, "extracted", "localization", "eng", "events.json"),
                Path.Combine(gameDir, "..", "extracted", "localization", "zhs", "events.json"),
            };
            foreach (var p in candidates)
            {
                var path = Path.GetFullPath(p);
                if (!File.Exists(path)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                var sb = new System.Text.StringBuilder();
                var descKey = eventId + ".pages.INITIAL.description";
                if (root.TryGetProperty(descKey, out var descEl)) sb.AppendLine(StripBbCode(descEl.GetString() ?? ""));
                var titleKey = eventId + ".title";
                if (sb.Length == 0 && root.TryGetProperty(titleKey, out var titleEl)) sb.AppendLine(StripBbCode(titleEl.GetString() ?? ""));
                foreach (var prop in root.EnumerateObject())
                {
                    var k = prop.Name;
                    if (!k.StartsWith(eventId + ".pages.INITIAL.options.")) continue;
                    var rem = k.Substring((eventId + ".pages.INITIAL.options.").Length);
                    var optKey = rem.Contains(".") ? rem.Substring(0, rem.IndexOf('.')) : rem;
                    if (k.EndsWith(".title")) { sb.AppendLine(); sb.Append("• " + StripBbCode(prop.Value.GetString() ?? "")); }
                    else if (k.EndsWith(".description")) sb.AppendLine("  " + StripBbCode(prop.Value.GetString() ?? ""));
                }
                return sb.ToString().TrimEnd();
            }
        }
        catch { }
        return "";
    }

    private static string StripBbCode(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Text.RegularExpressions.Regex.Replace(s, @"\[/?[a-z0-9_]+\]", "");
    }

    private static void TryAppendOptions(string table, string eventId, System.Text.StringBuilder sb)
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var locMgr = asm.GetType("MegaCrit.Sts2.Core.Localization.LocManager") ?? asm.GetType("LocManager");
                if (locMgr == null) continue;
                var inst = locMgr.GetProperty("Instance")?.GetValue(null);
                if (inst == null) continue;
                var getTable = locMgr.GetMethod("GetTable");
                if (getTable == null) continue;
                var tbl = getTable.Invoke(inst, new object[] { table });
                if (tbl == null) continue;
                var keys = tbl.GetType().GetProperty("Keys")?.GetValue(tbl) as System.Collections.IEnumerable;
                if (keys == null) continue;
                var prefix = eventId + ".pages.INITIAL.options.";
                var added = new HashSet<string>();
                foreach (var k in keys)
                {
                    var key = k?.ToString();
                    if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix)) continue;
                    var remain = key.Substring(prefix.Length);
                    var optKey = remain.Contains(".") ? remain.Substring(0, remain.IndexOf('.')) : remain;
                    if (added.Contains(optKey)) continue;
                    added.Add(optKey);
                    var optTitle = GetLocString(table, prefix + optKey + ".title");
                    var optDesc = GetLocString(table, prefix + optKey + ".description");
                    if (!string.IsNullOrEmpty(optTitle) || !string.IsNullOrEmpty(optDesc))
                    {
                        sb.AppendLine();
                        sb.AppendLine($"• {optTitle}");
                        if (!string.IsNullOrEmpty(optDesc)) sb.AppendLine("  " + optDesc);
                    }
                }
                break;
            }
        }
        catch { /* 可选功能，失败则忽略 */ }
    }

    private static string GetLocString(string table, string key)
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var locType = asm.GetType("MegaCrit.Sts2.Core.Localization.LocString")
                    ?? asm.GetType("Sts2.Core.Localization.LocString");
                if (locType == null) continue;
                var ctor = locType.GetConstructor(new[] { typeof(string), typeof(string) });
                if (ctor == null) continue;
                var loc = ctor.Invoke(new object[] { table, key });
                var method = locType.GetMethod("GetFormattedText");
                if (method == null) continue;
                return method.Invoke(loc, null) as string ?? "";
            }
        }
        catch { }
        return "";
    }
}
