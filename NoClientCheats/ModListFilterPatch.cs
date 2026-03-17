using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// 屏蔽 Mod 检测：从 GetModNameList 返回值中移除 NoClientCheats，使客机无法在联机 Mod 列表中看到本 Mod。
/// 参考 sts2-heybox-support (小黑盒官方支持) 的 ModDetectionBypass 实现。
/// </summary>
[HarmonyPatch]
internal static class ModListFilterPatch
{
    private const string ModIdPrefix = "NoClientCheats";

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("GetModNameList", BindingFlags.Public | BindingFlags.Static);
    }

    /// <summary>Postfix：从 Mod 列表中移除 NoClientCheats，使联机时客机无法检测到本 Mod。</summary>
    static void Postfix(ref List<string> __result)
    {
        if (!NoClientCheatsMod.HideFromModList) return;
        if (__result == null) return;
        __result.RemoveAll(s => s != null && s.StartsWith(ModIdPrefix));
    }
}
