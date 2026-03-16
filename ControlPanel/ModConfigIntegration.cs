using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace ControlPanel;

/// <summary>
/// ModConfig 集成，零依赖反射。未安装 ModConfig 时模组照常运行。
/// </summary>
internal static class ModConfigIntegration
{
    private static bool _detected;
    private static bool _available;
    private static Type _apiType;
    private static Type _entryType;
    private static Type _configType;

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
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;
            tree.ProcessFrame += OnFrame1;
        }
        catch (Exception e)
        {
            Log.Warn($"[ControlPanel] ModConfig 注册失败: {e.Message}");
        }
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
            catch (Exception e) { Log.Warn($"[ControlPanel] ModConfig 注册失败: {e.Message}"); }
        }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        list.Add(MakeHeader("Control Panel", "控制面板"));
        list.Add(MakeKeyBind("toggle_hotkey", "Toggle Hotkey", "切换快捷键",
            (long)Godot.Key.F7,
            "Hotkey to open/close the control panel.",
            "打开/关闭控制面板的快捷键。",
            v => { try { ControlPanelMod.ToggleKey = (Godot.Key)Convert.ToInt64(v); } catch { } }));
        list.Add(MakeSeparator());
        list.Add(MakeHeader("Card / Potion / Fight tabs", "卡牌 / 药水 / 战斗 标签页"));
        list.Add(MakeHeader("Requires in-run. F7 default.", "需局内进行中，默认 F7"));

        var arr = Array.CreateInstance(_entryType, list.Count);
        for (int i = 0; i < list.Count; i++)
            arr.SetValue(list[i], i);

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { ControlPanelMod.ModId, "控制面板 / Control Panel", arr });
            SyncHotkeyFromConfig();
            Log.Info("[ControlPanel] ModConfig 注册完成");
        }
    }

    private static void SyncHotkeyFromConfig()
    {
        try
        {
            var v = GetValue("toggle_hotkey", (long)Godot.Key.F7);
            ControlPanelMod.ToggleKey = (Godot.Key)v;
        }
        catch { }
    }

    private static long GetValue(string key, long fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType.GetMethod("GetValue").MakeGenericMethod(typeof(long));
            return (long)method.Invoke(null, new object[] { ControlPanelMod.ModId, key });
        }
        catch { return fallback; }
    }

    private static object ConfigTypeValue(string name) => Enum.Parse(_configType, name);

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

    private static object MakeKeyBind(string key, string labelEn, string labelZhs,
        long defaultValue, string descEn, string descZhs, Action<object> onChanged)
    {
        var e = Activator.CreateInstance(_entryType);
        SetProp(e, "Key", key);
        SetProp(e, "Label", labelEn);
        SetProp(e, "Labels", Dict("en", labelEn, "zhs", labelZhs));
        SetProp(e, "Type", ConfigTypeValue("KeyBind"));
        SetProp(e, "DefaultValue", defaultValue);
        SetProp(e, "Description", descEn);
        SetProp(e, "Descriptions", Dict("en", descEn, "zhs", descZhs));
        if (onChanged != null) SetProp(e, "OnChanged", onChanged);
        return e;
    }

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
        => new Dictionary<string, string> { [k1] = v1, [k2] = v2 };

    private static void SetProp(object obj, string name, object value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);
}
