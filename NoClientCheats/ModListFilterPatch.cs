using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// 屏蔽 Mod 检测：从 Mod 列表返回值中移除 NoClientCheats，使客机无法在联机 Mod 列表中看到本 Mod。
/// v0.99+ 使用 GetGameplayRelevantModNameList；旧版使用 GetModNameList。
/// </summary>
[HarmonyPatch]
internal static class ModListFilterPatch
{
    private const string ModIdPrefix = "NoClientCheats";

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        if (t == null) return null;
        // v0.99+ 已重命名为 GetGameplayRelevantModNameList
        var m = t.GetMethod("GetGameplayRelevantModNameList", BindingFlags.Public | BindingFlags.Static)
            ?? t.GetMethod("GetModNameList", BindingFlags.Public | BindingFlags.Static);
        return m;
    }

    /// <summary>Postfix：从 Mod 列表中移除 NoClientCheats，使联机时客机无法检测到本 Mod。</summary>
    static void Postfix(ref List<string> __result)
    {
        if (!NoClientCheatsMod.HideFromModList) return;
        if (__result == null) return;
        __result.RemoveAll(s => s != null && s.StartsWith(ModIdPrefix));
    }
}
