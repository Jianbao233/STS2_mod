using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace HostPriority;

internal static class ModListFilterPatchCommon
{
    internal const string ModIdPrefix = "HostPriority";

    internal static void FilterModList(List<string> list)
    {
        if (list == null) return;
        var before = list.Count;
        list.RemoveAll(s => s != null && s.StartsWith(ModIdPrefix));
        if (before != list.Count)
            GD.Print($"[HostPriority] Filtered mod list for client join: removed HostPriority (was {before} mods, now {list.Count})");
    }
}

/// <summary>
/// 屏蔽 Mod 检测：从 GetGameplayRelevantModNameList 返回值中移除 HostPriority。
/// </summary>
[HarmonyPatch]
internal static class ModListFilterPatch
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        if (t == null) return null;
        return t.GetMethod("GetGameplayRelevantModNameList", BindingFlags.Public | BindingFlags.Static)
            ?? t.GetMethod("GetModNameList", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix(ref List<string> __result)
    {
        ModListFilterPatchCommon.FilterModList(__result);
    }
}

/// <summary>
/// 备用：在 InitialGameInfoMessage.Basic 的返回结果中过滤 mods，确保发送给客机的列表不包含 HostPriority。
/// </summary>
[HarmonyPatch]
internal static class InitialGameInfoFilterPatch
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.InitialGameInfoMessage");
        if (t == null) return null;
        return t.GetMethod("Basic", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix(ref object __result)
    {
        if (__result == null) return;
        var t = __result.GetType();
        var modsField = t.GetField("mods", BindingFlags.Public | BindingFlags.Instance);
        if (modsField?.GetValue(__result) is List<string> list)
        {
            ModListFilterPatchCommon.FilterModList(list);
        }
    }
}
