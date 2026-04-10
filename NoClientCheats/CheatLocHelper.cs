using System;
using System.Globalization;
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
        return _FallbackDisplayName(key, _CharacterFallback(key), characterId);
    }

    /// <summary>遗物 ID（如 ICE_CREAM）→ 显示名（如 冰淇淋）。</summary>
    public static string GetRelicDisplayName(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return "";
        var loc = _GetLocString("relics", relicId.Trim() + ".title");
        if (!string.IsNullOrEmpty(loc)) return loc;
        return _FallbackDisplayName(relicId.Trim(), _RelicFallback(relicId.Trim()), relicId);
    }

    /// <summary>卡牌 ID（如 BASH）→ 显示名。</summary>
    public static string GetCardDisplayName(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return "";
        var key = cardId.Trim();
        if (key.StartsWith("CARD.", StringComparison.OrdinalIgnoreCase))
            key = key.Substring("CARD.".Length);
        var loc = _GetLocString("cards", key + ".title");
        if (!string.IsNullOrEmpty(loc)) return loc;
        return _FallbackDisplayName(key, _CardFallback(key), key);
    }

    /// <summary>药水 ID → 显示名。</summary>
    public static string GetPotionDisplayName(string potionId)
    {
        if (string.IsNullOrWhiteSpace(potionId)) return "";
        var key = potionId.Trim();
        var loc = _GetLocString("potions", key + ".title");
        if (!string.IsNullOrEmpty(loc)) return loc;
        return _FallbackDisplayName(key, null, key);
    }

    /// <summary>是否为 NCC 生成的派生作弊记录，而非原始控制台指令。</summary>
    public static bool IsDerivedDetectionCommand(string cheatCommand)
    {
        if (string.IsNullOrWhiteSpace(cheatCommand)) return false;
        return cheatCommand.StartsWith("deck:", StringComparison.OrdinalIgnoreCase)
            || cheatCommand.StartsWith("game_action:", StringComparison.OrdinalIgnoreCase)
            || cheatCommand.StartsWith("ui_exploit:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>将作弊指令字符串转为更可读的显示（含汉化）。</summary>
    public static string LocalizeCheatCommand(string cheatCommand, bool compact = false)
    {
        if (string.IsNullOrWhiteSpace(cheatCommand)) return cheatCommand;

        var text = cheatCommand.Trim();
        if (text.StartsWith("deck:", StringComparison.OrdinalIgnoreCase))
            return _LocalizeDeckCommand(text["deck:".Length..], compact);
        if (text.StartsWith("game_action:", StringComparison.OrdinalIgnoreCase))
            return _LocalizeActionCommand(text["game_action:".Length..], compact);
        if (text.StartsWith("ui_exploit:", StringComparison.OrdinalIgnoreCase))
            return _LocalizeUiCommand(text["ui_exploit:".Length..], compact);
        if (_TryLocalizeDetectorExpression(text, compact, out var localizedDetector))
            return localizedDetector;

        return _LocalizeConsoleCommand(text);
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
        bool zh = string.Equals(Localization.Lang, "zhs", StringComparison.OrdinalIgnoreCase);
        return id?.ToUpperInvariant() switch
        {
            "IRONCLAD" => zh ? "铁甲战士" : "Ironclad",
            "DEFECT" => zh ? "故障机器人" : "Defect",
            "SILENT" => zh ? "静默猎手" : "Silent",
            "WATCHER" => zh ? "观者" : "Watcher",
            "HERMIT" => zh ? "隐者" : "Hermit",
            "NECROBINDER" => zh ? "亡灵契约师" : "Necrobinder",
            "DEPRIVED" => zh ? "被剥夺者" : "Deprived",
            "REGENT" => zh ? "储君" : "Regent",
            "LOCKED" => zh ? "？？？" : "Locked",
            _ => null
        };
    }

    static string _RelicFallback(string id)
    {
        bool zh = string.Equals(Localization.Lang, "zhs", StringComparison.OrdinalIgnoreCase);
        return id?.ToUpperInvariant() switch
        {
            "ICE_CREAM" => zh ? "冰淇淋" : "Ice Cream",
            _ => null
        };
    }

    static string _CardFallback(string id)
    {
        bool zh = string.Equals(Localization.Lang, "zhs", StringComparison.OrdinalIgnoreCase);
        return id?.ToUpperInvariant() switch
        {
            "ASCENDERS_BANE" => zh ? "登升者克星" : "Ascender's Bane",
            _ => null
        };
    }

    static string _LocalizeConsoleCommand(string cheatCommand)
    {
        var parts = cheatCommand.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return cheatCommand;

        var cmd = parts[0].Trim();
        var arg = parts.Length > 1 ? parts[1].Trim() : "";
        if (string.Equals(cmd, "relic", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_relic", string.IsNullOrEmpty(arg) ? "?" : GetRelicDisplayName(arg));
        if (string.Equals(cmd, "card", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_card", string.IsNullOrEmpty(arg) ? "?" : GetCardDisplayName(arg));
        if (string.Equals(cmd, "potion", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_potion", string.IsNullOrEmpty(arg) ? "?" : GetPotionDisplayName(arg));
        if (string.Equals(cmd, "gold", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_gold", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "power", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_power", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "upgrade", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_upgrade", string.IsNullOrEmpty(arg) ? "?" : GetCardDisplayName(arg));
        if (string.Equals(cmd, "remove_card", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_remove_card", string.IsNullOrEmpty(arg) ? "?" : GetCardDisplayName(arg));
        if (string.Equals(cmd, "damage", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_damage", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "block", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_block", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "heal", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_heal", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "draw", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_draw", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "energy", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_energy", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "stars", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_stars", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "room", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_room", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "event", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_event", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "fight", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_fight", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "act", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_act", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "travel", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_travel", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "ancient", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_ancient", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "afflict", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_afflict", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "enchant", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("cmd_enchant", string.IsNullOrEmpty(arg) ? "?" : arg);
        if (string.Equals(cmd, "kill", StringComparison.OrdinalIgnoreCase))
            return Localization.Tr("cmd_kill");
        if (string.Equals(cmd, "win", StringComparison.OrdinalIgnoreCase))
            return Localization.Tr("cmd_win");
        if (string.Equals(cmd, "godmode", StringComparison.OrdinalIgnoreCase))
            return Localization.Tr("cmd_godmode");

        return Localization.Trf("cmd_unknown", cheatCommand);
    }

    static string _LocalizeDeckCommand(string payload, bool compact)
    {
        _SplitPayload(payload, out var mainExpr, out var suffixes);
        var summary = _LocalizeDeckMain(mainExpr);
        if (compact) return $"{Localization.Tr("deck_exploit")}：{summary}";

        var outcome = _LocalizeOutcome(suffixes);
        return string.IsNullOrEmpty(outcome)
            ? $"{Localization.Tr("deck_exploit")}：{summary}"
            : $"{Localization.Tr("deck_exploit")}：{summary} | {outcome}";
    }

    static string _LocalizeActionCommand(string payload, bool compact)
    {
        _SplitPayload(payload, out var mainExpr, out var suffixes);
        var summary = _LocalizeDeltaExpr(mainExpr);
        if (compact) return $"{Localization.Tr("game_action_exploit")}：{summary}";

        var outcome = _LocalizeOutcome(suffixes);
        return string.IsNullOrEmpty(outcome)
            ? $"{Localization.Tr("game_action_exploit")}：{summary}"
            : $"{Localization.Tr("game_action_exploit")}：{summary} | {outcome}";
    }

    static string _LocalizeUiCommand(string payload, bool compact)
    {
        _SplitPayload(payload, out var mainExpr, out var suffixes);
        var summary = _LocalizeDeltaExpr(mainExpr);
        if (compact) return $"{Localization.Tr("ui_exploit")}：{summary}";

        var outcome = _LocalizeOutcome(suffixes);
        return string.IsNullOrEmpty(outcome)
            ? $"{Localization.Tr("ui_exploit")}：{summary}"
            : $"{Localization.Tr("ui_exploit")}：{summary} | {outcome}";
    }

    static string _LocalizeDeckMain(string expr)
    {
        _SplitExpr(expr, out var name, out var args);
        if (string.Equals(name, "transform_multi_select", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_transform_multi_select", _GetNamedInt(args, "calls", 2));
        if (string.Equals(name, "reward_multi_select", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_reward_multi_select",
                _GetNamedInt(args, "calls", 2),
                _GetNamedInt(args, "gained", _GetNamedInt(args, "delta", 1)));
        if (string.Equals(name, "remove_multi_select", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_remove_multi_select",
                _GetNamedInt(args, "calls", 2),
                _GetNamedInt(args, "removed", Math.Abs(_GetNamedInt(args, "delta", 1))));
        if (string.Equals(name, "multi_select_excess", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_multi_select_excess",
                _GetNamedInt(args, "calls", 2),
                _GetNamedInt(args, "delta", 0));
        if (string.Equals(name, "reward_excess", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_reward_excess", _GetNamedInt(args, "gained", _GetPositionalInt(args, 1)));
        if (string.Equals(name, "transform_delta0", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_transform_delta0", _GetNamedInt(args, "changed", 0));
        if (string.Equals(name, "transform_cheat", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_transform_cheat",
                _GetNamedInt(args, "extra", 0),
                _GetNamedInt(args, "missing", 0));
        if (string.Equals(name, "reward_multi_cheat", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_reward_multi_cheat", _GetNamedInt(args, "extra", 0));
        if (string.Equals(name, "remove_multi_cheat", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_remove_multi_cheat", _GetNamedInt(args, "missing", 0));
        if (string.Equals(name, "upgrade_undo", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("deck_upgrade_undo", _GetNamedInt(args, "count", _GetPositionalInt(args, 1)));

        return _LocalizeDeltaExpr(expr);
    }

    static string _LocalizeDeltaExpr(string expr)
    {
        _SplitExpr(expr, out var name, out var args);
        if (string.Equals(name, "remove_excess", StringComparison.OrdinalIgnoreCase))
        {
            var deleted = _GetNamedInt(args, "deleted", _GetPositionalInt(args, 1));
            return _HasNamedValue(args, "allowed")
                ? Localization.Trf("deck_remove_excess_allowed", deleted, _GetNamedInt(args, "allowed", 0))
                : Localization.Trf("remove_excess", deleted);
        }
        if (string.Equals(name, "upgrade_excess", StringComparison.OrdinalIgnoreCase))
        {
            var upgraded = _GetNamedInt(args, "upgraded", _GetPositionalInt(args, 1));
            return _HasNamedValue(args, "allowed")
                ? Localization.Trf("deck_upgrade_excess_allowed", upgraded, _GetNamedInt(args, "allowed", 0))
                : Localization.Trf("upgrade_excess", upgraded);
        }
        if (string.Equals(name, "add_cards", StringComparison.OrdinalIgnoreCase))
        {
            var count = _GetNamedInt(args, "count", _GetPositionalInt(args, 1));
            return Localization.Trf("deck_add_cards_allowed", count, _GetNamedInt(args, "allowed", 0));
        }
        if (string.Equals(name, "add_excess", StringComparison.OrdinalIgnoreCase))
            return Localization.Trf("add_excess", _GetPositionalInt(args, 1));
        if (string.Equals(name, "card_mismatch", StringComparison.OrdinalIgnoreCase))
            return Localization.Tr("card_mismatch");
        if (string.Equals(name, "deck_unknown", StringComparison.OrdinalIgnoreCase))
            return Localization.Tr("deck_unknown");

        return expr;
    }

    static bool _TryLocalizeDetectorExpression(string expr, bool compact, out string localized)
    {
        _SplitExpr(expr, out var name, out _);
        switch (name)
        {
            case "transform_multi_select":
            case "reward_multi_select":
            case "remove_multi_select":
            case "multi_select_excess":
            case "reward_excess":
            case "transform_delta0":
            case "transform_cheat":
            case "reward_multi_cheat":
            case "remove_multi_cheat":
                localized = _LocalizeDeckCommand(expr, compact);
                return true;
            case "remove_excess":
            case "upgrade_excess":
            case "add_excess":
            case "add_cards":
            case "upgrade_undo":
            case "card_mismatch":
            case "deck_unknown":
                localized = _LocalizeDeltaExpr(expr);
                return true;
            default:
                localized = null;
                return false;
        }
    }

    static string _LocalizeOutcome(string[] suffixes)
    {
        bool observeOnly = false;
        bool hostStateCorrected = false;
        bool usedPreSnapshot = false;
        bool usedLastSnapshot = false;
        bool missingPrev = false;
        bool missingSnapshot = false;
        bool syncUnready = false;
        bool deferredRefresh = false;

        foreach (var raw in suffixes)
        {
            var part = raw?.Trim();
            if (string.IsNullOrEmpty(part)) continue;

            if (part.StartsWith("observe:", StringComparison.OrdinalIgnoreCase))
            {
                observeOnly = true;
                continue;
            }

            if (!part.StartsWith("rollback:", StringComparison.OrdinalIgnoreCase))
                continue;

            var rollback = part["rollback:".Length..];
            if (rollback.Contains("host_only_no_wire", StringComparison.OrdinalIgnoreCase)
                || rollback.Contains("host_only_no_client_capability", StringComparison.OrdinalIgnoreCase))
                observeOnly = true;
            if (rollback.Contains("_syncData", StringComparison.OrdinalIgnoreCase))
                hostStateCorrected = true;
            if (rollback.Contains("_preCheatSnapshot", StringComparison.OrdinalIgnoreCase))
                usedPreSnapshot = true;
            if (rollback.Contains("_lastSerializablePlayer", StringComparison.OrdinalIgnoreCase))
                usedLastSnapshot = true;
            if (rollback.Contains("aborted_no_prev", StringComparison.OrdinalIgnoreCase))
                missingPrev = true;
            if (rollback.Contains("no_snapshot", StringComparison.OrdinalIgnoreCase))
                missingSnapshot = true;
            if (rollback.Contains("synchronizer_null", StringComparison.OrdinalIgnoreCase))
                syncUnready = true;
            if (rollback.Contains("deferred_refresh_queued", StringComparison.OrdinalIgnoreCase))
                deferredRefresh = true;
        }

        string outcome = null;
        if (observeOnly) outcome = _AppendOutcome(outcome, Localization.Tr("deck_detect_only"));
        if (hostStateCorrected) outcome = _AppendOutcome(outcome, Localization.Tr("deck_host_state_corrected"));
        if (usedPreSnapshot) outcome = _AppendOutcome(outcome, Localization.Tr("deck_prev_snapshot"));
        if (usedLastSnapshot) outcome = _AppendOutcome(outcome, Localization.Tr("deck_last_snapshot"));
        if (missingPrev) outcome = _AppendOutcome(outcome, Localization.Tr("deck_missing_prev"));
        if (missingSnapshot) outcome = _AppendOutcome(outcome, Localization.Tr("deck_missing_snapshot"));
        if (syncUnready) outcome = _AppendOutcome(outcome, Localization.Tr("deck_sync_unready"));
        if (deferredRefresh) outcome = _AppendOutcome(outcome, Localization.Tr("deck_deferred_refresh"));
        return outcome;
    }

    static string _AppendOutcome(string current, string next)
    {
        if (string.IsNullOrEmpty(next)) return current;
        return string.IsNullOrEmpty(current) ? next : current + " / " + next;
    }

    static void _SplitPayload(string payload, out string mainExpr, out string[] suffixes)
    {
        var parts = (payload ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
        mainExpr = parts.Length > 0 ? parts[0].Trim() : payload?.Trim() ?? "";
        if (parts.Length <= 1)
        {
            suffixes = Array.Empty<string>();
            return;
        }

        suffixes = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
            suffixes[i - 1] = parts[i].Trim();
    }

    static void _SplitExpr(string expr, out string name, out string args)
    {
        var text = expr?.Trim() ?? "";
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            name = text;
            args = "";
            return;
        }

        name = text[..open].Trim();
        args = text.Substring(open + 1, close - open - 1).Trim();
    }

    static int _GetNamedInt(string args, string key, int fallback)
    {
        if (string.IsNullOrWhiteSpace(args)) return fallback;
        var parts = args.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = trimmed[(key.Length + 1)..];
            return int.TryParse(raw, out var value) ? value : fallback;
        }
        return fallback;
    }

    static bool _HasNamedValue(string args, string key)
    {
        if (string.IsNullOrWhiteSpace(args)) return false;
        var parts = args.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Trim().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static int _GetPositionalInt(string args, int fallback)
    {
        if (string.IsNullOrWhiteSpace(args)) return fallback;
        return int.TryParse(args.Trim(), out var value) ? value : fallback;
    }

    static string _FallbackDisplayName(string rawId, string localizedFallback, string finalFallback)
    {
        if (!string.IsNullOrWhiteSpace(localizedFallback))
            return localizedFallback;
        if (!string.IsNullOrWhiteSpace(rawId) && !string.Equals(Localization.Lang, "zhs", StringComparison.OrdinalIgnoreCase))
            return _HumanizeId(rawId);
        return finalFallback ?? rawId ?? "";
    }

    static string _HumanizeId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return "";
        var text = rawId.Trim();
        int dot = text.LastIndexOf('.');
        if (dot >= 0 && dot < text.Length - 1)
            text = text[(dot + 1)..];
        text = text.Replace('_', ' ').ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
    }
}
