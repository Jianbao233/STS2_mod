using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace RichPing;

/// <summary>
/// ModConfig 集成，严格按 https://github.com/xhyrzldf/ModConfig-STS2 官方 README 实现。
/// 零依赖：通过 Type.GetType 反射，未安装 ModConfig 时模组照常运行。
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
                {
                    FindTypesInAssemblies();
                }
                _available = _apiType != null && _entryType != null && _configType != null;
            }
            return _available;
        }
    }

    /// <summary>Type.GetType 可能因 ALC 找不到，回退：遍历已加载程序集</summary>
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

    /// <summary>
    /// 注册配置项。README：必须在 Initialize 之后调用，使用两帧延迟确保 ModConfig 已加载。
    /// </summary>
    public static void Register()
    {
        if (!IsAvailable) return;
        try
        {
            DeferredRegister();
        }
        catch (Exception e)
        {
            Log.Warn($"[RichPing] ModConfig 注册失败: {e.Message}");
        }
    }

    private static void DeferredRegister()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (tree == null) return;
        // README: "Use deferred registration (2-frame delay)"
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
        if (!IsAvailable) return;
        try { DoRegister(); }
        catch (Exception e) { Log.Warn($"[RichPing] ModConfig 注册失败: {e.Message}"); }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        // ========== 一、全局开关 ==========
        list.Add(MakeHeader("Global Switch", "全局开关"));
        list.Add(MakeToggle("use_custom_ping", "Enable Custom Ping", "启用自定义 Ping",
            descEn: "Master switch. Off = use game default Ping text. On = use RichPing (JSON + ModConfig).",
            descZhs: "调控目标：总开关。关=游戏原版文本；开=使用 RichPing 配置（JSON + 本页）。",
            onChanged: v => RichPingMod.UpdateFromModConfig("use_custom_ping", v)));

        list.Add(MakeSeparator());

        // ========== 二、全局文本类别（存活/死亡） ==========
        list.Add(MakeHeader("Global Text Categories", "全局文本类别"));
        list.Add(MakeToggle("use_alive_ping", "Alive Text", "存活时催促文本",
            descEn: "Controls: alive Ping. On = use custom urging text. Off = game default for alive.",
            descZhs: "调控目标：存活状态下发送的催促队友文本。关=游戏默认；开=自定义。",
            onChanged: v => RichPingMod.UpdateFromModConfig("use_alive_ping", v)));
        list.Add(MakeToggle("use_dead_ping", "Dead Text", "死亡后调侃文本",
            descEn: "Controls: dead Ping. On = use custom调侃. Off = game default for dead.",
            descZhs: "调控目标：死亡后发送的调侃/梗向文本。关=游戏默认；开=自定义。",
            onChanged: v => RichPingMod.UpdateFromModConfig("use_dead_ping", v)));

        list.Add(MakeSeparator());

        // ========== 三、选取行为 ==========
        list.Add(MakeHeader("Pick Behavior", "选取行为"));
        list.Add(MakeToggle("random_pick", "Random Pick", "随机选取",
            descEn: "Controls: how to pick. On = random from pool. Off = sequential rotation.",
            descZhs: "调控目标：从候选池选哪条。开=随机；关=按顺序轮转。",
            onChanged: v => RichPingMod.UpdateFromModConfig("random_pick", v)));
        list.Add(MakeToggle("use_stages", "Stage-based", "按楼层切换",
            descEn: "Controls: act-based text. On = different text per act 0/1/2. Off = default only.",
            descZhs: "调控目标：是否按幕切换。开=第一/二/三幕不同文本；关=仅用 default。",
            onChanged: v => RichPingMod.UpdateFromModConfig("use_stages", v)));
        list.Add(MakeToggle("use_character_specific", "Character-specific", "角色专属优先",
            descEn: "Controls: source priority. On = prefer character config. Off = global only.",
            descZhs: "调控目标：文本来源优先。开=优先角色专属；关=仅用全局（messages/stages）。",
            onChanged: v => RichPingMod.UpdateFromModConfig("use_character_specific", v)));

        list.Add(MakeSeparator());

        // ========== 四、过滤 ==========
        list.Add(MakeHeader("Filter - Excluded Phrases", "过滤 - 排除文本"));
        if (TryConfigType("Input", out var inputType) || TryConfigType("Text", out inputType))
        {
            list.Add(MakeEntry("excluded_messages", "Excluded Messages", inputType,
                defaultValue: "",
                labels: Dict("en", "Excluded (comma-separated)", "zhs", "排除文本"),
                descEn: "Controls: blacklist. Any phrase here = never send texts containing it.",
                descZhs: "调控目标：黑名单。逗号/分号分隔。文本包含任一短语即永不发送。",
                onChanged: v => RichPingMod.UpdateFromModConfig("excluded_messages", v)));
        }

        list.Add(MakeSeparator());

        // ========== 五、角色个性化（存活/死亡分别开关） ==========
        list.Add(MakeHeader("Character Settings - Alive", "角色设置 - 存活时文本"));
        AddCharToggles(list, "alive",
            "Controls: whether to use this char's alive text. Off = global for this char when alive.",
            "调控目标：该角色存活时是否用其专属催促文本。关=对该角色存活时用全局。");
        list.Add(MakeSeparator());
        list.Add(MakeHeader("Character Settings - Dead", "角色设置 - 死亡后文本"));
        AddCharToggles(list, "dead",
            "Controls: whether to use this char's dead text. Off = global for this char when dead.",
            "调控目标：该角色死亡后是否用其专属调侃文本。关=对该角色死亡后用全局。");

        // 构造 ConfigEntry[]（反射创建的元素需放进正确的数组类型）
        var arr = Array.CreateInstance(_entryType, list.Count);
        for (int i = 0; i < list.Count; i++)
            arr.SetValue(list[i], i);

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { RichPingMod.ModId, "丰富 Ping 文本", arr });
            Log.Info("[RichPing] ModConfig 注册完成");
        }

        SyncAllToRichPingMod();
    }

    private static void AddCharToggles(List<object> list, string suffix, string descEn, string descZhs)
    {
        var chars = new[] {
            ("char_ironclad", "Ironclad", "铁甲战士"),
            ("char_silent", "The Silent", "静默猎手"),
            ("char_defect", "Defect", "故障机器人"),
            ("char_watcher", "Watcher", "观者"),
            ("char_regent", "The Regent", "储君"),
            ("char_necrobinder", "Necrobinder", "亡灵契约师"),
        };
        foreach (var (key, en, zhs) in chars)
        {
            var k = key + "_" + suffix;
            list.Add(MakeToggle(k, en, zhs, descEn, descZhs, true, v => RichPingMod.UpdateFromModConfig(k, v)));
        }
    }

    private static bool TryConfigType(string name, out object value)
    {
        try
        {
            value = Enum.Parse(_configType, name);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static void SyncAllToRichPingMod()
    {
        RichPingMod.UpdateFromModConfig("use_custom_ping", GetValue("use_custom_ping", true));
        RichPingMod.UpdateFromModConfig("use_alive_ping", GetValue("use_alive_ping", true));
        RichPingMod.UpdateFromModConfig("use_dead_ping", GetValue("use_dead_ping", true));
        RichPingMod.UpdateFromModConfig("random_pick", GetValue("random_pick", true));
        RichPingMod.UpdateFromModConfig("use_stages", GetValue("use_stages", true));
        RichPingMod.UpdateFromModConfig("use_character_specific", GetValue("use_character_specific", true));
        if (IsAvailable)
        {
            try { RichPingMod.UpdateFromModConfig("excluded_messages", GetValue("excluded_messages", "")); } catch { }
        }
        foreach (var prefix in new[] { "char_ironclad", "char_silent", "char_defect", "char_watcher", "char_regent", "char_necrobinder" })
        {
            try { RichPingMod.UpdateFromModConfig(prefix + "_alive", GetValue(prefix + "_alive", true)); } catch { }
            try { RichPingMod.UpdateFromModConfig(prefix + "_dead", GetValue(prefix + "_dead", true)); } catch { }
        }
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

    private static object MakeEntry(string key, string label, object type,
        object defaultValue = null,
        Action<object> onChanged = null,
        Dictionary<string, string> labels = null,
        string descEn = null, string descZhs = null)
    {
        var entry = Activator.CreateInstance(_entryType);
        SetProp(entry, "Key", key);
        SetProp(entry, "Label", label);
        SetProp(entry, "Type", type);
        if (defaultValue != null) SetProp(entry, "DefaultValue", defaultValue);
        if (onChanged != null) SetProp(entry, "OnChanged", onChanged);
        if (labels != null) SetProp(entry, "Labels", labels);
        if (descEn != null || descZhs != null)
        {
            SetProp(entry, "Description", descEn ?? descZhs);
            SetProp(entry, "Descriptions", Dict("en", descEn ?? "", "zhs", descZhs ?? ""));
        }
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
