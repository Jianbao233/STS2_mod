using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace HostPriority;

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
            GD.PushWarning($"[HostPriority] ModConfig 注册失败: {e.Message}");
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
            try
            {
                DoRegister();
                SyncFromConfig();
            }
            catch (Exception e)
            {
                GD.PushWarning($"[HostPriority] ModConfig 注册失败: {e.Message}");
            }
        }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        list.Add(MakeHeader("Host Priority", "房主优先"));
        list.Add(MakeToggle("enabled", "Host Priority", "房主优先",
            defaultValue: true,
            descEn: "When enabled, host wins in relic rock-paper-scissors, map path selection, and event option splits. Host-only mod.",
            descZhs: "开启时，房主在遗物猜拳、地图路径、事件选项分歧中胜出。仅房主需安装。",
            onChanged: v => { try { HostPriorityMod.Enabled = Convert.ToBoolean(v); } catch { } }));
        list.Add(MakeSeparator());
        list.Add(MakeHeader("Host-only mod. Clients don't need to install.", "仅房主需安装，客机无需安装。"));

        var arr = Array.CreateInstance(_entryType, list.Count);
        for (int i = 0; i < list.Count; i++)
            arr.SetValue(list[i], i);

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { HostPriorityMod.ModId, "房主优先 / Host Priority", arr });
            GD.Print("[HostPriority] ModConfig 注册完成");
        }
    }

    private static void SyncFromConfig()
    {
        try
        {
            HostPriorityMod.Enabled = GetValue("enabled", true);
        }
        catch { }
    }

    private static bool GetValue(string key, bool fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType.GetMethod("GetValue").MakeGenericMethod(typeof(bool));
            return (bool)method.Invoke(null, new object[] { HostPriorityMod.ModId, key });
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

    private static object MakeToggle(string key, string labelEn, string labelZhs,
        bool defaultValue = true, string descEn = null, string descZhs = null, Action<object> onChanged = null)
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

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
        => new Dictionary<string, string> { [k1] = v1, [k2] = v2 };

    private static void SetProp(object obj, string name, object value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);
}
