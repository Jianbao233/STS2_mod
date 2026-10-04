using Godot;
using SpireInstrument.Core;

namespace SpireInstrument.Ui;

/// <summary>
/// 本地化的 Godot 侧：注入系统语言 + 建好 UI 后遍历控件树翻译静态文案。
/// （纯逻辑在 <see cref="Strings"/>，本文件只做"取语言"和"走树"。）
/// </summary>
public static class Localizer
{
    private static bool _localeHooked;

    /// <summary>把 Godot 的语言探测接进纯逻辑层（只接一次）。</summary>
    public static void HookLocale()
    {
        if (_localeHooked) return;
        _localeHooked = true;
        Strings.SystemLocale = () =>
        {
            // 真相源是**游戏自己的语言设置**（user://default/1/settings.save 里的 "language": "zhs"）。
            // TranslationServer.GetLocale() 与 OS.GetLocale() 都不可靠：
            // 前者游戏可能没同步，后者是 Windows 系统语言 —— 用错了就会出现"游戏中文、面板英文"。
            try
            {
                using var f = Godot.FileAccess.Open("user://default/1/settings.save", Godot.FileAccess.ModeFlags.Read);
                if (f != null)
                {
                    string text = f.GetAsText();
                    var m = System.Text.RegularExpressions.Regex.Match(text, "\"language\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { /* 落到下面的回退 */ }

            try
            {
                string loc = TranslationServer.GetLocale();
                if (!string.IsNullOrEmpty(loc)) return loc;
            }
            catch { /* 落到系统语言 */ }

            try { return OS.GetLocale(); }
            catch { return "en"; }
        };
    }

    /// <summary>
    /// 整棵树走一遍，把 Label / Button / OptionButton 的静态文案翻掉。
    /// 只处理能精确命中表的文案；动态拼接的由各自生成函数显式调用 Strings.Pick。
    /// </summary>
    public static void LocalizeTree(Node root)
    {
        HookLocale();
        if (!Strings.UseEnglish) return;
        Walk(root);
    }

    private static void Walk(Node node)
    {
        switch (node)
        {
            case Label label:
                label.Text = Strings.Tr(label.Text);
                break;
            case Button button:
                button.Text = Strings.Tr(button.Text);
                if (!string.IsNullOrEmpty(button.TooltipText)) button.TooltipText = Strings.Tr(button.TooltipText);
                break;
        }

        foreach (var child in node.GetChildren()) Walk(child);
    }
}
