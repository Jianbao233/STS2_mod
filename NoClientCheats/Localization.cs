using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// 静态本地化模块。语言跟随游戏内设置（与设置里 Language 一致），而非 OS / Godot TranslationServer 默认值。
/// 优先读 <c>MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language</c>（反射，零编译期依赖），失败再回退 <c>TranslationServer.GetLocale()</c>。
/// </summary>
internal static class Localization
{
    private static readonly Dictionary<string, (string En, string Zhs)> _tr;

    private static Type _locMgrType;
    private static PropertyInfo _locMgrInstanceProp;
    private static PropertyInfo _locMgrLanguageProp;

    static Localization()
    {
        _tr = new Dictionary<string, (string, string)>
        {
            // ── CheatHistoryPanel ──────────────────────────────────────────
            ["panel_title"] = ("Cheat Block Records ({0})", "作弊拦截记录 ({0} 条)"),
            ["hint_row"] = ("{0} toggle | Session only | Total {1}", "{0} 呼出/隐藏 | 记录保存本局 | 总计 {1} 条"),
            ["empty"] = ("No records yet", "暂无拦截记录"),
            ["tooltip_move"] = ("Drag to move | Edge to resize", "拖拽移动 | 边缘拖拽调整大小"),
            ["tooltip_center"] = ("Center window", "居中"),
            ["btn_clear"] = ("Clear", "清空"),
            ["tooltip_clear"] = ("Clear history", "清空历史记录"),
            ["btn_close"] = ("Close", "关闭"),
            ["tooltip_close"] = ("Close ({0} to reopen)", "关闭（{0} 重新呼出）"),
            ["btn_show"] = ("Show", "呼出"),
            ["tooltip_show"] = ("Show history panel", "呼出历史面板"),

            // ── CheatNotification ─────────────────────────────────────────
            ["notify_blocked"] = ("[CHEAT BLOCKED]", "[禁止作弊]"),
            ["label_role"] = ("Role", "角色"),
            ["btn_history"] = ("View History  ->", "查看历史  ->"),
            ["tooltip_history"] = ("Open history panel", "呼出拦截历史面板"),

            // ── LanConnectBridge ─────────────────────────────────────────
            ["lobby_blocked"] = ("[CHEAT BLOCKED]", "[作弊拦截]"),
            ["lobby_logged"] = ("[CHEAT LOGGED]", "[作弊记录]"),
            ["tried_use"] = ("tried", "尝试使用"),
            ["executed"] = ("executed", "执行了"),

            // ── DeckSync ────────────────────────────────────────────────────
            ["deck_exploit"] = ("Deck Exploit", "卡组作弊"),
            ["deck_rollback"] = ("Deck Rollback", "卡组回滚"),
            ["deck_upgrade_excess"] = ("Excess upgrade", "超额升级"),
            ["deck_remove_excess"] = ("Excess removal", "超额删除"),
            ["deck_transform_excess"] = ("Excess transform", "超额转化"),
            ["deck_unknown"] = ("Unknown deck mismatch", "未知卡组异常"),

            // ── UI 即时对比检测 ─────────────────────────────────────────────
            ["ui_exploit"] = ("UI Exploit", "UI 操作作弊"),
            ["immediate_rollback"] = ("Immediate Rollback", "立即回滚"),
            ["remove_excess"] = ("Removed {0} extra card(s)", "超额删除了 {0} 张卡"),
            ["upgrade_excess"] = ("Upgraded {0} extra card(s)", "超额升级了 {0} 张卡"),
            ["add_excess"] = ("Added {0} extra card(s)", "超额添加了 {0} 张卡"),
            ["card_mismatch"] = ("Card identity mismatch", "卡牌身份不匹配"),
            ["no_pre_snapshot"] = ("No pre-snapshot (skipped)", "无预快照（已跳过）"),
        };
    }

    /// <summary>当前语言：<c>zhs</c> 或 <c>en</c>（每次读取，随设置变化）。</summary>
    public static string Lang => IsChineseLocale() ? "zhs" : "en";

    /// <summary>返回指定 key 的当前语言字符串。</summary>
    public static string Tr(string key)
    {
        if (_tr.TryGetValue(key, out var pair))
            return IsChineseLocale() ? pair.Zhs : pair.En;
        return key;
    }

    public static string Trf(string key, int value)
    {
        return string.Format(Tr(key), value);
    }

    public static string Trf(string key, string arg0, int arg1)
    {
        return string.Format(Tr(key), arg0, arg1);
    }

    static void EnsureLocManagerRefs()
    {
        if (_locMgrType != null) return;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Localization.LocManager")
                ?? asm.GetType("LocManager");
            if (t == null) continue;
            _locMgrType = t;
            const BindingFlags stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _locMgrInstanceProp = t.GetProperty("Instance", stat);
            const BindingFlags inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _locMgrLanguageProp = t.GetProperty("Language", inst);
            break;
        }
    }

    /// <summary>与游戏内「语言」设置一致：zho/chs/zh* → 中文，其余（eng 等）→ 英文。</summary>
    static bool IsChineseLocale()
    {
        try
        {
            EnsureLocManagerRefs();
            if (_locMgrInstanceProp != null && _locMgrLanguageProp != null)
            {
                var inst = _locMgrInstanceProp.GetValue(null);
                if (inst != null)
                {
                    var raw = _locMgrLanguageProp.GetValue(inst) as string;
                    if (!string.IsNullOrEmpty(raw))
                        return IsChineseLanguageCode(raw);
                }
            }
        }
        catch { /* 回退 */ }

        try
        {
            var loc = TranslationServer.GetLocale();
            if (!string.IsNullOrEmpty(loc))
                return loc.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
        catch { }

        return false;
    }

    static bool IsChineseLanguageCode(string raw)
    {
        if (raw.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(raw, "zho", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(raw, "chs", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(raw, "zhs", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
