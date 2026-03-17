using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace NoClientCheats;

/// <summary>
/// ModConfig 集成，零依赖反射。未安装 ModConfig 时模组照常运行（使用默认开启）。
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
            GD.PushWarning($"[NoClientCheats] ModConfig 注册失败: {e.Message}");
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
                GD.PushWarning($"[NoClientCheats] ModConfig 注册失败: {e.Message}");
            }
        }
    }

    private static void DoRegister()
    {
        var list = new List<object>();

        list.Add(MakeHeader("No Client Cheats", "禁止客机作弊"));
        list.Add(MakeToggle("block_enabled", "Block Client Cheats", "禁止客机作弊",
            defaultValue: true,
            descEn: "When enabled (host only), client cheat commands (gold, relic, card, etc.) are silently dropped. Host can still use cheats.",
            descZhs: "开启时（仅房主），客机发出的作弊指令（gold、relic、card 等）将被静默丢弃。房主仍可使用作弊。",
            onChanged: v => { try { NoClientCheatsMod.BlockEnabled = Convert.ToBoolean(v); } catch { } }));
        list.Add(MakeToggle("hide_from_mod_list", "Hide from Mod List", "屏蔽 Mod 检测",
            defaultValue: true,
            descEn: "When enabled, this mod is removed from the mod list sent to clients, so they cannot detect it. Ref: sts2-heybox-support.",
            descZhs: "开启时，从联机 Mod 列表中移除本 Mod，客机无法检测到。参考 sts2-heybox-support。",
            onChanged: v => { try { NoClientCheatsMod.HideFromModList = Convert.ToBoolean(v); } catch { } }));
        list.Add(MakeSeparator());
        list.Add(MakeHeader("Host-only mod. Clients don't need to install.", "仅房主需安装，客机无需安装。"));
        list.Add(MakeHeader("Thanks: sts2-heybox-support, 皮一下就很凡@B站", "致谢：sts2-heybox-support（小黑盒），皮一下就很凡@B站"));

        var arr = Array.CreateInstance(_entryType, list.Count);
        for (int i = 0; i < list.Count; i++)
            arr.SetValue(list[i], i);

        var register = _apiType.GetMethod("Register", new[] { typeof(string), typeof(string), _entryType.MakeArrayType() });
        if (register != null)
        {
            register.Invoke(null, new object[] { NoClientCheatsMod.ModId, "禁止客机作弊 / No Client Cheats", arr });
            GD.Print("[NoClientCheats] ModConfig 注册完成");
        }
    }

    private static void SyncFromConfig()
    {
        try
        {
            NoClientCheatsMod.BlockEnabled = GetValue("block_enabled", true);
            NoClientCheatsMod.HideFromModList = GetValue("hide_from_mod_list", true);
        }
        catch { }
    }

    private static bool GetValue(string key, bool fallback)
    {
        if (!IsAvailable) return fallback;
        try
        {
            var method = _apiType.GetMethod("GetValue").MakeGenericMethod(typeof(bool));
            return (bool)method.Invoke(null, new object[] { NoClientCheatsMod.ModId, key });
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
