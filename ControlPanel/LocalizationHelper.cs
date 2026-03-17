using System;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 通过反射调用游戏 LocString 获取本地化文本。
/// </summary>
public static class LocalizationHelper
{
    /// <summary>获取药水描述，key 如 "ATTACK_POTION.description"</summary>
    public static string GetPotionDescription(string potionId)
    {
        return GetLocString("potions", potionId + ".description");
    }

    /// <summary>获取事件完整文本：body/intro + 各选项文本。用于事件面板右侧展示。</summary>
    public static string GetEventText(string eventId)
    {
        var sb = new System.Text.StringBuilder();
        var body = GetLocString("events", eventId + ".body");
        var intro = GetLocString("events", eventId + ".intro");
        var title = GetLocString("events", eventId + ".title");
        if (!string.IsNullOrEmpty(body)) sb.AppendLine(body);
        else if (!string.IsNullOrEmpty(intro)) sb.AppendLine(intro);
        if (!string.IsNullOrEmpty(title) && sb.Length == 0) sb.AppendLine(title);
        for (int i = 0; i < 10; i++)
        {
            var opt = GetLocString("events", eventId + ".options." + i + ".text");
            if (string.IsNullOrEmpty(opt)) break;
            sb.AppendLine();
            sb.AppendLine($"[选项 {i + 1}] {opt}");
        }
        return sb.ToString().TrimEnd();
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
