using HarmonyLib;
using System;
using System.Reflection;

namespace RichPing;

/// <summary>
/// Harmony 补丁：拦截游戏显示 Ping 气泡时获取的 endTurnPing 文本。
/// 游戏在 FlavorSynchronizer.CreateEndTurnPingDialogueIfNecessary 中通过
/// LocString("characters", "{角色}.banter.{alive|dead}.endTurnPing").GetFormattedText()
/// 获取默认角色台词。本补丁在 LocString.GetFormattedText 处拦截，
/// 当检测到 endTurnPing 键时改用 RichPing 提供的自定义文本。
/// </summary>
internal static class HarmonyPatcher
{
    private static Harmony _harmony;

    public static void Apply()
    {
        _harmony = new Harmony("net.richping");
        TryPatchLocStringGetFormattedText();
    }

    /// <summary>对 LocString.GetFormattedText 打 Prefix 补丁</summary>
    private static void TryPatchLocStringGetFormattedText()
    {
        var locStringType = Type.GetType("MegaCrit.Sts2.Core.Localization.LocString, sts2")
            ?? FindTypeInLoadedAssemblies("MegaCrit.Sts2.Core.Localization.LocString");

        var targetMethod = locStringType?.GetMethod(
            "GetFormattedText",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (targetMethod != null)
        {
            var prefix = typeof(EndTurnPingPrefix).GetMethod(nameof(EndTurnPingPrefix.Prefix));
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefix));
        }
    }

    private static Type FindTypeInLoadedAssemblies(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}

/// <summary>
/// Prefix 补丁：当 LocString 为 characters 表的 *.banter.(alive|dead).endTurnPing 时，
/// 返回 RichPing 自定义文本并跳过原方法。
/// </summary>
internal static class EndTurnPingPrefix
{
    public static bool Prefix(object __instance, ref string __result)
    {
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
            var runManagerType = Type.GetType("MegaCrit.Sts2.Core.Runs.RunManager, sts2");
            if (runManagerType == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if ((runManagerType = asm.GetType("MegaCrit.Sts2.Core.Runs.RunManager")) != null)
                        break;

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
