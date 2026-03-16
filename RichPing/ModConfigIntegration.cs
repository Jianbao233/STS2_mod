using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace RichPing;

/// <summary>
/// ModConfig 集成：通过反射零依赖接入。
/// 若玩家未安装 ModConfig，IsAvailable 为 false，Mod 照常运行，仅无游戏内配置 UI。
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
            if (_detected) return _available;
            _detected = true;
            _apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig");
            _entryType = Type.GetType("ModConfig.ConfigEntry, ModConfig");
            _configType = Type.GetType("ModConfig.ConfigType, ModConfig");
            _available = _apiType != null && _entryType != null && _configType != null;
            return _available;
        }
    }

    /// <summary>
    /// 注册配置项。需延迟两帧以确保 ModConfig 已加载（Mod 加载顺序不定）。
    /// </summary>
    public static void Register()
    {
        if (!IsAvailable) return;

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += OnFrame1;
    }

    private static void OnFrame1()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame -= OnFrame1;
        tree.ProcessFrame += OnFrame2;
    }

    private static void OnFrame2()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame -= OnFrame2;
        try { DoRegister(); }
        catch (Exception e) { Log.Warn($"[RichPing] ModConfig 注册失败: {e.Message}"); }
    }

    private static void DoRegister()
    {
        var entries = new[]
        {
            MakeEntry("use_custom_ping", "使用自定义 Ping 文本", ConfigType("Toggle"),
                defaultValue: true,
                labels: Dict("en", "Use Custom Ping", "zhs", "使用自定义 Ping 文本"),
                onChanged: v => RichPingMod.UpdateFromModConfig(useCustom: (bool)v, randomPick: null, useStages: null)),

            MakeEntry("random_pick", "随机选取消息", ConfigType("Toggle"),
                defaultValue: true,
                labels: Dict("en", "Random Pick", "zhs", "随机选取消息"),
                onChanged: v => RichPingMod.UpdateFromModConfig(useCustom: null, randomPick: (bool)v, useStages: null)),

            MakeEntry("use_stages", "按楼层阶段切换文本", ConfigType("Toggle"),
                defaultValue: true,
                labels: Dict("en", "Stage-based Messages", "zhs", "按楼层阶段切换文本"),
                onChanged: v => RichPingMod.UpdateFromModConfig(useCustom: null, randomPick: null, useStages: (bool)v)),
        };

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), entries.GetType() });
        register?.Invoke(null, new object[] { RichPingMod.ModId, "丰富 Ping 文本", entries });

        // 读取已保存值并同步到 RichPingMod
        var useCustom = GetValue("use_custom_ping", true);
        var randomPick = GetValue("random_pick", true);
        var useStages = GetValue("use_stages", true);
        RichPingMod.UpdateFromModConfig(useCustom, randomPick, useStages);
    }

    private static object ConfigType(string name) => Enum.Parse(_configType, name);

    private static object MakeEntry(string key, string label, object type,
        object defaultValue = null,
        Action<object> onChanged = null,
        Dictionary<string, string> labels = null)
    {
        var entry = Activator.CreateInstance(_entryType);
        SetProp(entry, "Key", key);
        SetProp(entry, "Label", label);
        SetProp(entry, "Type", type);
        if (defaultValue != null) SetProp(entry, "DefaultValue", defaultValue);
        if (onChanged != null) SetProp(entry, "OnChanged", onChanged);
        if (labels != null) SetProp(entry, "Labels", labels);
        return entry;
    }

    private static Dictionary<string, string> Dict(string k1, string v1, string k2, string v2)
    {
        return new Dictionary<string, string> { [k1] = v1, [k2] = v2 };
    }

    private static void SetProp(object obj, string name, object value)
    {
        obj.GetType().GetProperty(name)?.SetValue(obj, value);
    }

    private static T GetValue<T>(string key, T fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType.GetMethod("GetValue").MakeGenericMethod(typeof(T));
            return (T)method.Invoke(null, new object[] { RichPingMod.ModId, key });
        }
        catch { return fallback; }
    }
}
