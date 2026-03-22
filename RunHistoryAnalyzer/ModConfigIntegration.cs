using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace RunHistoryAnalyzer;

/// <summary>
/// 可选依赖 ModConfig：注册「显示/隐藏分析工具栏」快捷键（默认 F6）。
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
        list.Add(MakeDropdown(
            "toggle_toolbar_key",
            "Toolbar show/hide key",
            "显示/隐藏分析工具栏快捷键",
            new[] { "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            "F6",
            "Press to show or hide the analyze button overlay (when a run file is selected).",
            "在历史记录中选中存档后，按此键显示/隐藏右下角「分析」按钮与相关浮动 UI。",
            v =>
            {
                try
                {
                    if (v != null) RunHistoryAnalyzerMod.SetToggleToolbarKey(v.ToString()!);
                }
                catch { }
            }));

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

    private static void SyncFromConfig()
    {
        try
        {
            var s = GetValue("toggle_toolbar_key", "F6");
            if (!string.IsNullOrEmpty(s))
                RunHistoryAnalyzerMod.SetToggleToolbarKey(s);
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

    private static object MakeDropdown(string key, string labelEn, string labelZhs,
        string[] options, string defaultValue,
        string? descEn, string? descZhs,
        Action<object> onChanged)
    {
        var e = Activator.CreateInstance(_entryType!) ?? throw new InvalidOperationException("ConfigEntry");
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
        SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
        => new Dictionary<string, string> { [k1] = v1, [k2] = v2 };

    private static void SetProp(object obj, string name, object? value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);
}
