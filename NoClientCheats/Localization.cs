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
            ["notify_blocked"] = ("[CHEAT BLOCKED]", "[作弊已拦截]"),
            ["notify_detected"] = ("[CHEAT DETECTED]", "[检测到作弊]"),
            ["label_role"] = ("Role", "角色"),
            ["btn_history"] = ("View History  ->", "查看历史  ->"),
            ["tooltip_history"] = ("Open history panel", "呼出拦截历史面板"),

            // ── LanConnectBridge ─────────────────────────────────────────
            ["lobby_blocked"] = ("[CHEAT BLOCKED]", "[作弊拦截]"),
            ["lobby_detected"] = ("[CHEAT DETECTED]", "[检测到作弊]"),
            ["lobby_logged"] = ("[CHEAT LOGGED]", "[作弊记录]"),
            ["tried_use"] = ("tried", "尝试使用"),
            ["executed"] = ("executed", "执行了"),

            // ── DeckSync ────────────────────────────────────────────────────
            ["deck_exploit"] = ("Deck Exploit", "卡组作弊"),
            ["deck_rollback"] = ("Deck Rollback", "卡组回滚"),
            ["deck_detect_only"] = ("Observe only (no safe client rollback path)", "仅提醒（当前无安全的客机回滚通道）"),
            ["deck_host_state_corrected"] = ("Host state corrected", "主机权威状态已修正"),
            ["deck_prev_snapshot"] = ("Used pre-cheat snapshot", "使用作弊前快照"),
            ["deck_last_snapshot"] = ("Used previous legal snapshot", "使用上一份合法快照"),
            ["deck_missing_prev"] = ("Missing previous snapshot", "缺少前置快照"),
            ["deck_missing_snapshot"] = ("Missing rollback snapshot", "缺少回滚快照"),
            ["deck_sync_unready"] = ("Synchronizer not ready", "同步器未就绪"),
            ["deck_deferred_refresh"] = ("Deferred host refresh queued", "已排队延迟刷新主机状态"),
            ["deck_transform_multi_select"] = ("Transform multi-select ({0} picks)", "转化多选（{0} 次选择）"),
            ["deck_reward_multi_select"] = ("Reward multi-select ({0} picks, +{1} card(s))", "奖励多选（{0} 次选择，+{1} 张卡）"),
            ["deck_remove_multi_select"] = ("Remove multi-select ({0} picks, -{1} card(s))", "删牌多选（{0} 次选择，-{1} 张卡）"),
            ["deck_multi_select_excess"] = ("Illegal multi-select ({0} picks, delta {1})", "非法多选（{0} 次选择，变化 {1}）"),
            ["deck_reward_excess"] = ("Rewarded {0} extra card(s)", "超额获得奖励卡 {0} 张"),
            ["deck_transform_delta0"] = ("Transform mismatch ({0} card(s) changed)", "转化结果异常（变动了 {0} 张卡）"),
            ["deck_transform_cheat"] = ("Transform mismatch (+{0} / -{1})", "转化异常（多出 {0} 张 / 少了 {1} 张）"),
            ["deck_reward_multi_cheat"] = ("Reward mismatch (+{0})", "奖励异常（多出 {0} 张）"),
            ["deck_remove_multi_cheat"] = ("Removal mismatch (-{0})", "删牌异常（少了 {0} 张）"),
            ["deck_add_cards_allowed"] = ("Added {0} extra card(s) (allowed {1})", "超额获得 {0} 张卡（允许 {1} 张）"),
            ["deck_remove_excess_allowed"] = ("Removed {0} extra card(s) (allowed {1})", "超额删除 {0} 张卡（允许 {1} 张）"),
            ["deck_upgrade_excess_allowed"] = ("Upgraded {0} extra card(s) (allowed {1})", "超额升级 {0} 张卡（允许 {1} 张）"),
            ["deck_upgrade_undo"] = ("Undid {0} upgrade(s)", "回退了 {0} 次升级"),
            ["deck_upgrade_excess"] = ("Excess upgrade", "超额升级"),
            ["deck_remove_excess"] = ("Excess removal", "超额删除"),
            ["deck_transform_excess"] = ("Excess transform", "超额转化"),
            ["deck_unknown"] = ("Unknown deck mismatch", "未知卡组异常"),

            // ── UI 即时对比检测 ─────────────────────────────────────────────
            ["ui_exploit"] = ("UI Exploit", "UI 操作作弊"),
            ["game_action_exploit"] = ("Game Action Exploit", "游戏动作作弊"),
            ["immediate_rollback"] = ("Immediate Rollback", "立即回滚"),
            ["remove_excess"] = ("Removed {0} extra card(s)", "超额删除了 {0} 张卡"),
            ["upgrade_excess"] = ("Upgraded {0} extra card(s)", "超额升级了 {0} 张卡"),
            ["add_excess"] = ("Added {0} extra card(s)", "超额添加了 {0} 张卡"),
            ["card_mismatch"] = ("Card identity mismatch", "卡牌身份不匹配"),
            ["no_pre_snapshot"] = ("No pre-snapshot (skipped)", "无预快照（已跳过）"),

            // ── Console / generic command localization ────────────────────
            ["cmd_gold"] = ("Gold {0}", "金币 {0}"),
            ["cmd_relic"] = ("Relic: {0}", "遗物：{0}"),
            ["cmd_card"] = ("Card: {0}", "卡牌：{0}"),
            ["cmd_potion"] = ("Potion: {0}", "药水：{0}"),
            ["cmd_power"] = ("Power: {0}", "能力：{0}"),
            ["cmd_upgrade"] = ("Upgrade: {0}", "升级：{0}"),
            ["cmd_remove_card"] = ("Remove Card: {0}", "删牌：{0}"),
            ["cmd_damage"] = ("Damage {0}", "伤害 {0}"),
            ["cmd_block"] = ("Block {0}", "格挡 {0}"),
            ["cmd_heal"] = ("Heal {0}", "治疗 {0}"),
            ["cmd_draw"] = ("Draw {0}", "抽牌 {0}"),
            ["cmd_energy"] = ("Energy {0}", "能量 {0}"),
            ["cmd_stars"] = ("Stars {0}", "星能 {0}"),
            ["cmd_room"] = ("Room {0}", "房间 {0}"),
            ["cmd_event"] = ("Event {0}", "事件 {0}"),
            ["cmd_fight"] = ("Fight {0}", "战斗 {0}"),
            ["cmd_act"] = ("Act {0}", "章节 {0}"),
            ["cmd_travel"] = ("Travel {0}", "传送 {0}"),
            ["cmd_ancient"] = ("Ancient {0}", "远古词条 {0}"),
            ["cmd_afflict"] = ("Afflict {0}", "诅咒 {0}"),
            ["cmd_enchant"] = ("Enchant {0}", "附魔 {0}"),
            ["cmd_kill"] = ("Kill", "秒杀"),
            ["cmd_win"] = ("Win", "直接胜利"),
            ["cmd_godmode"] = ("Godmode", "无敌"),
            ["cmd_unknown"] = ("Command: {0}", "指令：{0}"),
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

    public static string Trf(string key, params object[] args)
    {
        return string.Format(Tr(key), args);
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
