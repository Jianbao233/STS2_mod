using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HostPriority;

/// <summary>
/// Postfix RelicPickingResult.GenerateRelicFight：当房主在 players 中时，强制 result.player = 房主。
/// </summary>
[HarmonyPatch]
internal static class RelicPickingPostfix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.TreasureRelicPicking.RelicPickingResult")
            ?? AccessTools.TypeByName("RelicPickingResult");
        return t != null ? AccessTools.Method(t, "GenerateRelicFight") : null;
    }

    static void Postfix(object players, object __result)
    {
        if (!HostPriorityMod.Enabled) return;
        if (players == null || __result == null) return;

        var playersList = players as IList;
        if (playersList == null || playersList.Count == 0) return;

        var localContext = AccessTools.TypeByName("MegaCrit.Sts2.Core.Context.LocalContext")
            ?? AccessTools.TypeByName("LocalContext");
        if (localContext == null) return;

        var playerType = playersList.GetType().GetGenericArguments().Length > 0
            ? playersList.GetType().GetGenericArguments()[0]
            : typeof(object);
        var ienumType = typeof(IEnumerable<>).MakeGenericType(playerType);
        var getMe = localContext.GetMethod("GetMe", new[] { ienumType });
        if (getMe == null) return;

        var host = getMe.Invoke(null, new object[] { players });
        if (host == null) return;

        if (playersList.Contains(host))
        {
            var playerField = __result.GetType().GetField("player");
            playerField?.SetValue(__result, host);
        }
    }
}
