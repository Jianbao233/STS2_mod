using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq; // IReadOnlyList.Contains 扩展方法

namespace RichPing;

/// <summary>
/// RichPing Mod 入口。
/// 在战斗回合结束时，玩家可通过 Ping 按钮发送自定义催促/调侃文本，
/// 支持多角色、多阶段（幕）、存活/死亡状态，以及第三方 Mod 角色接口。
/// </summary>
[ModInitializer("ModLoaded")]
public static class RichPingMod
{
    public const string ModId = "RichPing";

    #region 运行时状态

    private static bool _useCustomPing = true;
    private static bool _randomPick = true;
    private static bool _useStages = true;
    private static List<string> _messages = new();
    private static Dictionary<int, List<string>> _stageMessages = new();
    private static List<string> _deadMessages = new();
    private static Dictionary<int, List<string>> _deadStageMessages = new();
    private static Dictionary<string, CharacterPingConfig> _characterConfigs = new();
    private static int _sequentialIndex;
    private static readonly List<IRichPingTextProvider> _externalProviders = new();

    /// <summary>角色 Entry 别名映射，用于兼容游戏可能使用的不同命名（如 SILENT → THE_SILENT）</summary>
    private static readonly Dictionary<string, string> CharacterIdAliases = new()
    {
        ["SILENT"] = "THE_SILENT",
        ["WATCHER"] = "THE_HIEROPHANT",
        ["REGENT"] = "THE_REGENT",
        ["NECROBINDER"] = "THE_NECROBINDER",
    };

    #endregion

    #region Mod 生命周期

    public static void ModLoaded()
    {
        Log.Info("[RichPing] Mod 加载中...");
        LoadConfig();
        ModConfigIntegration.Register();
        HarmonyPatcher.Apply();
        Log.Info("[RichPing] 已加载");
    }

    /// <summary>
    /// 注册第三方 Mod 的 Ping 文本提供者。
    /// 建议在 ModLoaded 之后、第一次 Ping 之前调用；若加载顺序不确定，可延迟一帧。
    /// </summary>
    public static void RegisterExternalProvider(IRichPingTextProvider provider)
    {
        if (provider != null && !_externalProviders.Contains(provider))
            _externalProviders.Add(provider);
    }

    #endregion

    #region 配置加载

    /// <summary>从 ping_messages.json 加载配置</summary>
    public static void LoadConfig()
    {
        _messages.Clear();
        _stageMessages.Clear();
        _deadMessages.Clear();
        _deadStageMessages.Clear();
        _characterConfigs.Clear();

        try
        {
            if (!FileAccess.FileExists("res://ping_messages.json"))
            {
                SetDefaults();
                return;
            }

            using var file = FileAccess.Open("res://ping_messages.json", FileAccess.ModeFlags.Read);
            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok)
            {
                SetDefaults();
                return;
            }

            var data = json.Data;
            if (data.VariantType != Godot.Variant.Type.Dictionary)
            {
                SetDefaults();
                return;
            }

            var dict = data.AsGodotDictionary();

            // 全局开关
            if (dict.TryGetValue("use_custom_ping", out var ucp)) _useCustomPing = ucp.AsBool();
            if (dict.TryGetValue("random_pick", out var rp)) _randomPick = rp.AsBool();
            if (dict.TryGetValue("use_stages", out var us)) _useStages = us.AsBool();

            // 解析各配置块
            ParseMessages(dict);
            ParseStages(dict);
            ParseDeadMessages(dict);
            ParseCharacters(dict);
        }
        catch (Exception e)
        {
            Log.Warn($"[RichPing] 加载配置失败: {e.Message}");
            SetDefaults();
        }

        if (_messages.Count == 0)
            _messages.Add("快点！");
    }

    private static void SetDefaults()
    {
        _messages = new List<string> { "快点！", "到你了。" };
        _stageMessages = new Dictionary<int, List<string>>
        {
            [0] = new List<string> { "快点！" },
            [1] = new List<string> { "到你了。" },
            [2] = new List<string> { "我等着呢。" }
        };
    }

    private static void ParseMessages(Godot.Collections.Dictionary dict)
    {
        if (!dict.TryGetValue("messages", out var v) || v.VariantType != Godot.Variant.Type.Array)
            return;
        foreach (var item in v.AsGodotArray())
            if (item.VariantType == Godot.Variant.Type.String)
                _messages.Add((string)item);
    }

    private static void ParseStages(Godot.Collections.Dictionary dict)
    {
        if (!dict.TryGetValue("stages", out var v) || v.VariantType != Godot.Variant.Type.Dictionary)
            return;
        ParseStageDict(v.AsGodotDictionary(), _stageMessages);
    }

    private static void ParseDeadMessages(Godot.Collections.Dictionary dict)
    {
        if (dict.TryGetValue("dead_messages", out var v) && v.VariantType == Godot.Variant.Type.Array)
            foreach (var item in v.AsGodotArray())
                if (item.VariantType == Godot.Variant.Type.String)
                    _deadMessages.Add((string)item);

        if (!dict.TryGetValue("dead_stages", out var ds) || ds.VariantType != Godot.Variant.Type.Dictionary)
            return;
        ParseStageDict(ds.AsGodotDictionary(), _deadStageMessages);
    }

    /// <summary>解析 stages / dead_stages 格式的字典，act 0/1/2 → 字符串列表</summary>
    private static void ParseStageDict(Godot.Collections.Dictionary stages, Dictionary<int, List<string>> target)
    {
        foreach (var kv in stages)
        {
            if (kv.Key.VariantType != Godot.Variant.Type.String) continue;
            var keyStr = (string)kv.Key;
            if (keyStr.StartsWith("_")) continue; // 跳过 _comment 等
            if (!int.TryParse(keyStr, out var actIndex) || actIndex < 0 || actIndex > 2) continue;

            var list = new List<string>();
            if (kv.Value.VariantType == Godot.Variant.Type.Array)
                foreach (var item in kv.Value.AsGodotArray())
                    if (item.VariantType == Godot.Variant.Type.String)
                        list.Add((string)item);
            if (list.Count > 0)
                target[actIndex] = list;
        }
    }

    private static void ParseCharacters(Godot.Collections.Dictionary dict)
    {
        if (!dict.TryGetValue("characters", out var v) || v.VariantType != Godot.Variant.Type.Dictionary)
            return;

        var chars = v.AsGodotDictionary();
        foreach (var kv in chars)
        {
            if (kv.Key.VariantType != Godot.Variant.Type.String) continue;
            var charId = (string)kv.Key;
            if (charId.StartsWith("_")) continue;
            if (kv.Value.VariantType != Godot.Variant.Type.Dictionary) continue;

            var cfgDict = kv.Value.AsGodotDictionary();
            var cfg = new CharacterPingConfig();

            // default（存活默认）
            if (cfgDict.TryGetValue("default", out var def) && def.VariantType == Godot.Variant.Type.Array)
                foreach (var item in def.AsGodotArray())
                    if (item.VariantType == Godot.Variant.Type.String)
                        cfg.Default.Add((string)item);

            // stages（存活分幕）
            if (cfgDict.TryGetValue("stages", out var st) && st.VariantType == Godot.Variant.Type.Dictionary)
                ParseStageDict(st.AsGodotDictionary(), cfg.Stages);

            // dead（死亡默认）
            if (cfgDict.TryGetValue("dead", out var dead) && dead.VariantType == Godot.Variant.Type.Array)
                foreach (var item in dead.AsGodotArray())
                    if (item.VariantType == Godot.Variant.Type.String)
                        cfg.Dead.Add((string)item);

            // dead_stages（死亡分幕）
            if (cfgDict.TryGetValue("dead_stages", out var dst) && dst.VariantType == Godot.Variant.Type.Dictionary)
                ParseStageDict(dst.AsGodotDictionary(), cfg.DeadStages);

            if (cfg.HasAnyContent())
                _characterConfigs[charId] = cfg;
        }
    }

    #endregion

    #region 文本选取（供 Harmony 调用）

    /// <summary>
    /// 获取自定义 Ping 文本。优先级：外部提供者 → 角色专属 → 全局。
    /// </summary>
    /// <param name="characterId">角色 Entry，如 IRONCLAD、THE_SILENT</param>
    /// <param name="actIndex">当前幕，0/1/2</param>
    /// <param name="isDead">是否已死亡</param>
    public static string GetCustomPingText(string characterId, int actIndex, bool isDead)
    {
        if (!_useCustomPing) return null;

        // 1. 外部提供者（第三方 Mod 角色）
        foreach (var p in _externalProviders)
        {
            if (p.SupportedCharacterIds.Count == 0 || p.SupportedCharacterIds.Contains(characterId))
            {
                var text = p.GetPingText(characterId, actIndex, isDead);
                if (!string.IsNullOrEmpty(text)) return text;
            }
        }

        // 2. 解析别名，兼容不同命名
        var resolvedId = ResolveCharacterId(characterId);

        // 3. 角色专属配置
        if (_characterConfigs.TryGetValue(resolvedId, out var charCfg))
        {
            if (isDead)
            {
                if (_useStages && charCfg.DeadStages.TryGetValue(actIndex, out var deadStageList) && deadStageList.Count > 0)
                    return Pick(deadStageList);
                if (charCfg.Dead.Count > 0)
                    return Pick(charCfg.Dead);
            }
            else
            {
                if (_useStages && charCfg.Stages.TryGetValue(actIndex, out var stageList) && stageList.Count > 0)
                    return Pick(stageList);
                if (charCfg.Default.Count > 0)
                    return Pick(charCfg.Default);
            }
        }

        // 4. 全局兜底
        if (isDead)
        {
            if (_useStages && _deadStageMessages.TryGetValue(actIndex, out var deadGlob) && deadGlob.Count > 0)
                return Pick(deadGlob);
            if (_deadMessages.Count > 0)
                return Pick(_deadMessages);
        }

        if (_useStages && _stageMessages.TryGetValue(actIndex, out var globStage) && globStage.Count > 0)
            return Pick(globStage);
        if (_messages.Count > 0)
            return Pick(_messages);

        return null;
    }

    /// <summary>将游戏可能返回的别名映射为配置中使用的 key</summary>
    private static string ResolveCharacterId(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return characterId;
        return CharacterIdAliases.TryGetValue(characterId, out var canonical) ? canonical : characterId;
    }

    /// <summary>从列表中选取一条：随机 或 顺序轮转</summary>
    private static string Pick(List<string> list)
    {
        if (list == null || list.Count == 0) return null;
        if (_randomPick)
            return list[(int)(GD.Randi() % list.Count)];
        var idx = _sequentialIndex % list.Count;
        _sequentialIndex++;
        return list[idx];
    }

    #endregion

    #region ModConfig 回调

    public static void ReloadConfig() => LoadConfig();

    /// <summary>由 ModConfig 开关变更时调用，更新运行时选项</summary>
    public static void UpdateFromModConfig(bool? useCustom, bool? randomPick, bool? useStages)
    {
        if (useCustom.HasValue) _useCustomPing = useCustom.Value;
        if (randomPick.HasValue) _randomPick = randomPick.Value;
        if (useStages.HasValue) _useStages = useStages.Value;
    }

    #endregion

    #region 内部类型

    private class CharacterPingConfig
    {
        public List<string> Default { get; } = new();
        public Dictionary<int, List<string>> Stages { get; } = new();
        public List<string> Dead { get; } = new();
        public Dictionary<int, List<string>> DeadStages { get; } = new();

        public bool HasAnyContent() =>
            Default.Count > 0 || Stages.Count > 0 || Dead.Count > 0 || DeadStages.Count > 0;
    }

    #endregion
}
