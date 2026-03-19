using System;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// 将游戏内 ID（CHARACTER.XXX、遗物 ID 等）转为当前语言的显示名。
/// 优先使用游戏 LocString，失败时回退到硬编码中文。
/// </summary>
internal static class CheatLocHelper
{
    /// <summary>角色 ID（如 CHARACTER.IRONCLAD）→ 显示名（如 铁甲战士）。</summary>
    public static string GetCharacterDisplayName(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return "";
        var key = characterId.Trim();
        if (key.StartsWith("CHARACTER.", StringComparison.OrdinalIgnoreCase))
            key = key.Substring("CHARACTER.".Length);
        var loc = _GetLocString("characters", key + ".title");
        if (!string.IsNullOrEmpty(loc)) return loc;
        return _CharacterFallback(key) ?? characterId;
    }

    /// <summary>遗物 ID（如 ICE_CREAM）→ 显示名（如 冰淇淋）。</summary>
    public static string GetRelicDisplayName(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return "";
        var loc = _GetLocString("relics", relicId.Trim() + ".title");
        if (!string.IsNullOrEmpty(loc)) return loc;
        return _RelicFallback(relicId.Trim()) ?? relicId;
    }

    /// <summary>将作弊指令字符串中的 relic/gold 等转为更可读的显示（含汉化）。</summary>
    public static string LocalizeCheatCommand(string cheatCommand)
    {
        if (string.IsNullOrWhiteSpace(cheatCommand)) return cheatCommand;
        var parts = cheatCommand.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return cheatCommand;
        var cmd = parts[0];
        var arg = parts.Length > 1 ? parts[1] : "";
        if (string.Equals(cmd, "relic", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(arg))
            return "遗物：" + GetRelicDisplayName(arg);
        if (string.Equals(cmd, "gold", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrEmpty(arg) ? "金币" : "金币 " + arg;
        return cheatCommand;
    }

    static string _GetLocString(string table, string key)
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

    static string _CharacterFallback(string id)
    {
        return id?.ToUpperInvariant() switch
        {
            "IRONCLAD" => "铁甲战士",
            "DEFECT" => "故障机器人",
            "SILENT" => "静默猎手",
            "WATCHER" => "观者",
            "HERMIT" => "隐者",
            "NECROBINDER" => "亡灵契约师",
            "DEPRIVED" => "被剥夺者",
            "REGENT" => "储君",
            "LOCKED" => "？？？",
            _ => null
        };
    }

    static string _RelicFallback(string id)
    {
        return id?.ToUpperInvariant() switch
        {
            "ICE_CREAM" => "冰淇淋",
            _ => null
        };
    }
}
