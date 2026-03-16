using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace RichPing;

/// <summary>
/// ModManager.Initialize 完成后立即调度 RichPing 初始化（LoadConfig + ModConfig 注册）。
/// 保证打开游戏主菜单后即可在模组配置中看到 RichPing，无需进入对局或使用 Ping。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                tree.ProcessFrame += OnInitFrame1;
            }
        }
        catch { }
    }

    private static void OnInitFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnInitFrame1; tree.ProcessFrame += OnInitFrame2; }
    }

    private static void OnInitFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnInitFrame2; RichPingMod.EnsureInitialized(); }
    }
}

/// <summary>
/// Harmony 补丁：拦截 LocString.GetFormattedText。
/// 游戏在 FlavorSynchronizer.CreateEndTurnPingDialogueIfNecessary 中通过
/// LocString("characters", "{角色}.banter.{alive|dead}.endTurnPing").GetFormattedText() 获取 Ping 文本。
/// 使用 [HarmonyPatch] 让 ModManager.PatchAll 自动发现，因 Sts2Stubs 的 ModInitializer 不被游戏识别。
/// </summary>
[HarmonyPatch]
internal static class EndTurnPingPrefix
{
    private static bool _initScheduled;

    /// <summary>PatchAll 加载本类时运行，调度延迟初始化（LoadConfig + ModConfig 注册）</summary>
    static EndTurnPingPrefix()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;
            _initScheduled = true;
            tree.ProcessFrame += OnInitFrame1;
        }
        catch { /* 静默忽略，避免影响 PatchAll */ }
    }

    /// <summary>首次 Patch 调用时若尚未调度，则尝试调度（应对静态构造时 tree 为 null 的情况）</summary>
    private static void TryScheduleInit()
    {
        if (_initScheduled) return;
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;
            _initScheduled = true;
            tree.ProcessFrame += OnInitFrame1;
        }
        catch { }
    }

    private static void OnInitFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnInitFrame1; tree.ProcessFrame += OnInitFrame2; }
    }

    private static void OnInitFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnInitFrame2; RichPingMod.EnsureInitialized(); }
    }

    static MethodBase TargetMethod()
    {
        // AccessTools.TypeByName 跨程序集查找，比 Type.GetType(..., assembly) 更稳健
        var locStringType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Localization.LocString")
            ?? FindTypeByFullName("MegaCrit.Sts2.Core.Localization.LocString");
        return locStringType?.GetMethod("GetFormattedText", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    }

    private static Type FindTypeByFullName(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            catch { /* 忽略无法加载的程序集 */ }
        }
        return null;
    }

    public static bool Prefix(object __instance, ref string __result)
    {
        TryScheduleInit();
        if (__instance == null) return true;

        var locTable = __instance.GetType()
            .GetProperty("LocTable", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as string;
        var locKey = __instance.GetType()
            .GetProperty("LocEntryKey", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(__instance) as string;

        if (locTable != "characters" || string.IsNullOrEmpty(locKey))
            return true;

        // 仅拦截 endTurnPing 相关键
        bool isDead = locKey.EndsWith(".banter.dead.endTurnPing", StringComparison.Ordinal);
        bool isAlive = locKey.EndsWith(".banter.alive.endTurnPing", StringComparison.Ordinal);
        if (!isAlive && !isDead)
            return true;

        var characterId = ExtractCharacterId(locKey);
        var actIndex = GetCurrentActIndex();
        var custom = RichPingMod.GetCustomPingText(characterId, actIndex, isDead);

        if (custom != null)
        {
            __result = custom;
            return false; // 跳过原方法，使用自定义文本
        }

        return true; // 执行原方法，使用游戏默认
    }

    /// <summary>从 locKey 中提取角色 Entry，如 "IRONCLAD.banter.alive.endTurnPing" → "IRONCLAD"</summary>
    private static string ExtractCharacterId(string locKey)
    {
        if (string.IsNullOrEmpty(locKey)) return "";
        var idx = locKey.IndexOf(".banter.", StringComparison.Ordinal);
        return idx > 0 ? locKey.Substring(0, idx) : locKey;
    }

    /// <summary>获取当前幕索引（0/1/2），战斗外或异常时返回 0</summary>
    private static int GetCurrentActIndex()
    {
        try
        {
            var runManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager")
                ?? FindTypeByFullName("MegaCrit.Sts2.Core.Runs.RunManager");

            var instance = runManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null) return 0;

            var state = instance.GetType().GetProperty("State")?.GetValue(instance);
            if (state == null) return 0;

            var actIndex = state.GetType().GetProperty("CurrentActIndex")?.GetValue(state);
            return actIndex is int i ? i : 0;
        }
        catch
        {
            return 0;
        }
    }
}
