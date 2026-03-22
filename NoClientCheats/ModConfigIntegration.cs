using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace NoClientCheats;

/// <summary>
/// ModConfig 集成，与 RichPing 完全一致写法，确保模组配置中可见且开关可用。
/// </summary>
internal static class ModConfigIntegration
{
    private static bool _detected;
    private static bool _available;
    private static Type _apiType;
    private static Type _entryType;
    private static Type _configType;
    private static Type _managerType;
    private static MethodInfo _managerSetValue;

    internal static bool IsAvailable
    {
        get
        {
            if (!_detected)
            {
                _detected = true;
                _apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig");
                _entryType = Type.GetType("ModConfig.ConfigEntry, ModConfig");
                _configType = Type.GetType("ModConfig.ConfigType, ModConfig");
                if (_apiType == null || _entryType == null || _configType == null)
                    FindTypesInAssemblies();
                _available = _apiType != null && _entryType != null && _configType != null;
            }
            return _available;
        }
    }

    private static void FindTypesInAssemblies()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (_apiType == null) _apiType = asm.GetType("ModConfig.ModConfigApi");
                if (_entryType == null) _entryType = asm.GetType("ModConfig.ConfigEntry");
                if (_configType == null) _configType = asm.GetType("ModConfig.ConfigType");
                if (_managerType == null) _managerType = asm.GetType("ModConfig.ModConfigManager");
                if (_apiType != null && _entryType != null && _configType != null && _managerType != null) break;
            }
            catch { }
        }
    }

    public static void Register()
    {
        if (!IsAvailable) return;
        try { DeferredRegister(); }
        catch (Exception e) { GD.PushWarning($"[NoClientCheats] ModConfig 注册失败: {e.Message}"); }
    }

    private static void DeferredRegister()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        tree.ProcessFrame += OnFrame1;
    }

    private static void OnFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnFrame1; tree.ProcessFrame += OnFrame2; }
    }

    private static void OnFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null)
        {
            tree.ProcessFrame -= OnFrame2;
            if (!IsAvailable) return;
            try { DoRegister(); }
            catch (Exception e) { GD.PushWarning($"[NoClientCheats] ModConfig DoRegister 失败: {e.Message}"); }
            // 提前获取 ModConfigManager.SetValue 以便绕过 OnChanged
            if (_managerType != null && _managerSetValue == null)
                _managerSetValue = _managerType.GetMethod(
                    "SetValue",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(object) }, null);
        }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        list.Add(MakeHeader("Core", "核心功能"));
        list.Add(MakeToggle("block_enabled", "Block Client Cheats", "禁止客机作弊",
            "When enabled (host only), client cheat commands are silently dropped.",
            "开启时（仅房主），客机作弊指令将被静默丢弃。",
            true, v => { try { NoClientCheatsMod.BlockEnabled = Convert.ToBoolean(v); } catch { } }));

        list.Add(MakeHeader("Notification Popup", "拦截通知弹窗"));
        list.Add(MakeToggle("show_notification", "Show Popup", "显示弹窗",
            "When blocked, show a red popup at the top of the screen.",
            "作弊被拦截时，在屏幕顶部显示红色弹窗。",
            true, v => { try { NoClientCheatsMod.ShowNotification = Convert.ToBoolean(v); } catch { } }));

        list.Add(MakeSlider("notification_duration", "Popup Duration (sec)", "弹窗停留时间（秒）",
            1f, 15f, 0.5f, "0.0", 5f,
            "How long the red popup stays (seconds).", "红色弹窗停留时间（秒）。",
            v => { try { NoClientCheatsMod.NotificationDuration = Convert.ToSingle(v); } catch { } }));

        list.Add(MakeHeader("History Panel (F9)", "历史面板（F9）"));
        list.Add(MakeToggle("show_history_panel", "Enable History Panel", "启用历史面板",
            "Enable F9 to toggle cheat history panel.", "启用 F9 呼出历史面板。",
            true, v => {
                try { NoClientCheatsMod.ShowHistoryPanel = Convert.ToBoolean(v); }
                catch { }
                if (!NoClientCheatsMod.ShowHistoryPanel)
                    NoClientCheatsMod.DestroyHistoryPanel();
            }));
        list.Add(MakeToggle("show_history_on_cheat", "Show Panel on Cheat", "作弊时唤起历史面板",
            "When a client cheat is blocked, automatically open the history panel.", "客机作弊被拦截时自动打开历史记录面板。",
            false, v => { try { NoClientCheatsMod.ShowHistoryOnCheat = Convert.ToBoolean(v); } catch { } }));

        var historyOptions = new[] { "10", "15", "20", "25", "30", "35", "40", "45", "50" };
        list.Add(MakeDropdown("history_max", "Max History Records", "最大历史条数",
            historyOptions, "25",
            "Max cheat history records to keep.", "最多保存的历史记录条数。",
            v => { try { if (v != null) NoClientCheatsMod.HistoryMaxRecords = Convert.ToInt32(v.ToString()); } catch { } }));

        // ── 快捷键绑定 ────────────────────────────────────────────────────
        list.Add(MakeKeyBind("history_key", "History Toggle Key", "历史面板快捷键",
            (long)Key.F9,
            v => { try { if (v != null) NoClientCheatsMod.SetHistoryKeyFromLong(Convert.ToInt64(v)); } catch { } }));

        // ── 操作按钮（KeyBind 类型模拟按钮，点击触发动作后立即重置为 Unbound）──
        list.Add(MakeHeader("Actions", "操作"));
        list.Add(MakeActionButton("btn_open_history", "Open History Panel", "打开历史面板",
            "Open the cheat interception history panel.", "呼出历史记录面板。",
            () => NoClientCheatsMod.ShowHistoryPanelUI()));
        list.Add(MakeActionButton("btn_center_window", "Center Window", "窗口居中",
            "Move the history panel back to the center of the screen.", "将历史面板窗口移回屏幕中央。",
            () => NoClientCheatsMod.CenterHistoryWindow()));

        list.Add(MakeSeparator());
        list.Add(MakeHeader("Mod Detection", "Mod 检测"));
        list.Add(MakeToggle("hide_from_mod_list", "Hide from Mod List", "屏蔽 Mod 检测",
            "When enabled, this mod is removed from the mod list sent to clients.",
            "开启时，从联机 Mod 列表中移除本 Mod。",
            true, v => { try { NoClientCheatsMod.HideFromModList = Convert.ToBoolean(v); } catch { } }));

        list.Add(MakeSeparator());
        list.Add(MakeHeader("Host-only mod.", "仅房主需安装，客机无需安装。"));

        var arr = Array.CreateInstance(_entryType, list.Count);
        for (int i = 0; i < list.Count; i++) arr.SetValue(list[i], i);

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { NoClientCheatsMod.ModId, "禁止客机作弊 / No Client Cheats", arr });
            GD.Print("[NoClientCheats] ModConfig 注册完成");
        }

        SyncFromConfig();
    }

    private static void SyncFromConfig()
    {
        try { NoClientCheatsMod.BlockEnabled = GetValue("block_enabled", true); } catch { }
        try { NoClientCheatsMod.ShowNotification = GetValue("show_notification", true); } catch { }
        try { NoClientCheatsMod.ShowHistoryPanel = GetValue("show_history_panel", true); } catch { }
        try { NoClientCheatsMod.ShowHistoryOnCheat = GetValue("show_history_on_cheat", false); } catch { }
        try { NoClientCheatsMod.HideFromModList = GetValue("hide_from_mod_list", true); } catch { }
        try { NoClientCheatsMod.NotificationDuration = GetValue("notification_duration", 5.0f); } catch { }
        try { var s = GetValue("history_max", "25"); if (!string.IsNullOrEmpty(s) && int.TryParse(s, out var n)) NoClientCheatsMod.HistoryMaxRecords = n; } catch { }
        try { NoClientCheatsMod.SetHistoryKeyFromLong(GetValue("history_key", (long)Key.F9)); } catch { }
    }

    private static T GetValue<T>(string key, T fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType.GetMethod("GetValue").MakeGenericMethod(typeof(T));
            return (T)method.Invoke(null, new object[] { NoClientCheatsMod.ModId, key });
        }
        catch { return fallback; }
    }

    private static void SetValue(string key, object value)
    {
        if (!IsAvailable) return;
        try
        {
            var method = _apiType.GetMethod("SetValue");
            method?.Invoke(null, new[] { NoClientCheatsMod.ModId, key, value });
        }
        catch { }
    }

    private static object ConfigTypeValue(string name) => Enum.Parse(_configType, name);

    // ── 操作按钮（KeyBind 类型模拟按钮）────────────────────────────────
    // 原理：
    // 1. DefaultValue=0（Unbound），OnChanged 只在用户按键时触发（ESC/鼠标也触发，返回0）
    // 2. OnChanged 里执行动作，然后直接调用 ModConfigManager.SetValue(0L) 重置
    //    ModConfigManager.SetValue 是 internal static，绕过 ModConfigApi.SetValue，
    //    因而不会再次触发 OnChanged，彻底避免递归
    // 3. 按钮文字始终显示 "Unbound"（等于默认值），始终可点
    private static readonly long _buttonResetValue = 0L;

    private static object MakeActionButton(string key, string labelEn, string labelZhs,
        string descEn, string descZhs, Action action)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn); // 显示在按钮上（英）
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs)); // 显示在按钮上（汉）
        SetProp(e, "Type", ConfigTypeValue("KeyBind"));
        SetProp(e, "DefaultValue", _buttonResetValue); // 0 = Unbound，始终与默认值一致
        SetProp(e, "Description", descEn);
        SetProp(e, "Descriptions", Dict("en", descEn, "zhs", descZhs));
        SetProp(e, "OnChanged", new Action<object>(v => {
            try { action(); }
            catch (Exception ex)
            {
                GD.PushWarning($"[NoClientCheats] Action button '{key}' error: {ex.Message}");
            }
            // 直接调用 ModConfigManager.SetValue 绕过 OnChanged 重置为 0
            try
            {
                _managerSetValue?.Invoke(null, new object[] { NoClientCheatsMod.ModId, key, _buttonResetValue });
            }
            catch { }
        }));
        return e;
    }

    private static object MakeHeader(string labelEn, string labelZhs)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("Header"));
        return e;
    }

    private static object MakeSeparator()
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Type", ConfigTypeValue("Separator"));
        return e;
    }

    private static object MakeToggle(string key, string labelEn, string labelZhs,
        string descEn = null, string descZhs = null,
        bool defaultValue = true, Action<object> onChanged = null)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("Toggle"));
        SetProp(e, "DefaultValue", defaultValue);
        if (descEn != null || descZhs != null)
        {
            SetProp(e, "Description", descEn ?? descZhs);
            SetProp(e, "Descriptions", Dict("en", descEn ?? "", "zhs", descZhs ?? ""));
        }
        if (onChanged != null) SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static object MakeSlider(string key, string labelEn, string labelZhs,
        float min, float max, float step, string format, float defaultValue,
        string descEn = null, string descZhs = null, Action<object> onChanged = null)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("Slider"));
        SetProp(e, "Min", min);
        SetProp(e, "Max", max);
        SetProp(e, "Step", step);
        SetProp(e, "Format", format);
        SetProp(e, "DefaultValue", defaultValue);
        if (descEn != null || descZhs != null)
        {
            SetProp(e, "Description", descEn ?? descZhs);
            SetProp(e, "Descriptions", Dict("en", descEn ?? "", "zhs", descZhs ?? ""));
        }
        if (onChanged != null) SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static object MakeDropdown(string key, string labelEn, string labelZhs,
        string[] options, string defaultValue,
        string descEn = null, string descZhs = null, Action<object> onChanged = null)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("Dropdown"));
        SetProp(e, "Options", options);
        SetProp(e, "DefaultValue", defaultValue);
        if (descEn != null || descZhs != null)
        {
            SetProp(e, "Description", descEn ?? descZhs);
            SetProp(e, "Descriptions", Dict("en", descEn ?? "", "zhs", descZhs ?? ""));
        }
        if (onChanged != null) SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static object MakeKeyBind(string key, string labelEn, string labelZhs,
        long defaultValue, Action<object> onChanged = null)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("KeyBind"));
        SetProp(e, "DefaultValue", defaultValue);
        if (onChanged != null) SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
        => new Dictionary<string, string> { [k1] = v1, [k2] = v2 };

    private static void SetProp(object obj, string name, object value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);
}
