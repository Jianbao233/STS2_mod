using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace RunHistoryAnalyzer;

/// <summary>
/// 可选依赖 ModConfig：注册「显示/隐藏分析工具栏」快捷键（默认 F6）。
/// 快捷键配置使用 Keybind 模式：用户点击按钮后弹出按键捕获遮罩，等待下一次按键按下。
/// </summary>
internal static class ModConfigIntegration
{
    private static bool _detected;
    private static bool _available;
    private static Type? _apiType;
    private static Type? _entryType;
    private static Type? _configType;

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
                if (_apiType != null && _entryType != null && _configType != null) break;
            }
            catch { }
        }
    }

    public static void Register()
    {
        if (!IsAvailable) return;
        try { DeferredRegister(); }
        catch (Exception e) { GD.PushWarning($"[RunHistoryAnalyzer] ModConfig 注册失败: {e.Message}"); }
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
            catch (Exception e) { GD.PushWarning($"[RunHistoryAnalyzer] ModConfig DoRegister 失败: {e.Message}"); }
        }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        list.Add(MakeHeader("UI", "界面"));

        // Keybind：按键绑定——用户点击后弹出遮罩，监听下一次按键输入来设置快捷键
        list.Add(MakeKeybind(
            "toggle_toolbar_key",
            "Toolbar hotkey",
            "工具栏快捷键",
            "在历史记录中选中存档后，按此键显示/隐藏右下角「分析」按钮与相关浮动 UI。",
            RunHistoryAnalyzerMod.ToggleToolbarKey.ToString(),
            OnHotkeyChanged));

        var arr = Array.CreateInstance(_entryType!, list.Count);
        for (var i = 0; i < list.Count; i++) arr.SetValue(list[i], i);

        var register = _apiType!.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType!.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { RunHistoryAnalyzerMod.ModId, "历史记录异常检测 / Run History Analyzer", arr });
            GD.Print("[RunHistoryAnalyzer] ModConfig 注册完成");
        }

        SyncFromConfig();
    }

    private static void OnHotkeyChanged(object? value)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
        {
            try { RunHistoryAnalyzerMod.ToggleToolbarKey = (Key)Enum.Parse(typeof(Key), s.Trim(), ignoreCase: true); }
            catch { RunHistoryAnalyzerMod.ToggleToolbarKey = Key.F6; }
        }
    }

    private static void SyncFromConfig()
    {
        try
        {
            var s = GetValue("toggle_toolbar_key", "F6");
            if (!string.IsNullOrEmpty(s))
            {
                try { RunHistoryAnalyzerMod.ToggleToolbarKey = (Key)Enum.Parse(typeof(Key), s.Trim(), ignoreCase: true); }
                catch { RunHistoryAnalyzerMod.ToggleToolbarKey = Key.F6; }
            }
        }
        catch { }
    }

    private static T GetValue<T>(string key, T fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType!.GetMethod("GetValue")!.MakeGenericMethod(typeof(T));
            return (T)method.Invoke(null, new object[] { RunHistoryAnalyzerMod.ModId, key })!;
        }
        catch { return fallback; }
    }

    private static object ConfigTypeValue(string name) => Enum.Parse(_configType!, name);

    private static object MakeHeader(string labelEn, string labelZhs)
    {
        var e = Activator.CreateInstance(_entryType!) ?? throw new InvalidOperationException("ConfigEntry");
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("Header"));
        return e;
    }

    /// <summary>
    /// 创建 Keybind 类型的配置项。
    /// ModConfig 若不支持 Keybind 类型则 fallback 为普通字符串输入框，
    /// 行为上用户仍可直接输入按键名。
    /// </summary>
    private static object MakeKeybind(string key, string labelEn, string labelZhs,
        string? desc, string defaultValue, Action<object?> onChanged)
    {
        var e = Activator.CreateInstance(_entryType!) ?? throw new InvalidOperationException("ConfigEntry");
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));

        // 优先尝试 Keybind 类型；若无此类型则降级为字符串输入
        try { SetProp(e, "Type", ConfigTypeValue("Keybind")); }
        catch
        {
            try { SetProp(e, "Type", ConfigTypeValue("Text")); }
            catch { SetProp(e, "Type", ConfigTypeValue("String")); }
        }

        SetProp(e, "DefaultValue", defaultValue);
        if (desc != null)
        {
            SetProp(e, "Description", desc);
            SetProp(e, "Descriptions", Dict("en", desc, "zhs", desc));
        }
        SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
        => new Dictionary<string, string> { [k1] = v1, [k2] = v2 };

    private static void SetProp(object obj, string name, object? value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);
}
