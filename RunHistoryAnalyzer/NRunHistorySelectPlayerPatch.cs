using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace RunHistoryAnalyzer;

/// <summary>
/// 监听 NRunHistory.SelectPlayer，当玩家切换查看的角色时，更新 CurrentPlayerId。
/// 联机模式中同一 .run 文件含有多名角色，Patch 会确保分析时只针对当前选中的那名。
/// </summary>
[HarmonyPatch]
internal static class NRunHistorySelectPlayerPatch
{
    static MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory");
        return t?.GetMethod("SelectPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static bool Prepare()
    {
        return TargetMethod() != null;
    }

    static void Postfix(object __instance)
    {
        try
        {
            var instType = __instance.GetType();
            var playerIconField = AccessTools.Field(instType, "_selectedPlayerIcon");
            if (playerIconField == null) return;

            var playerIcon = playerIconField.GetValue(__instance);
            if (playerIcon == null) return;

            // NRunHistoryPlayerIcon.Player → RunHistoryPlayer → Id
            var playerProp = AccessTools.Property(playerIcon.GetType(), "Player");
            if (playerProp == null) return;

            var player = playerProp.GetValue(playerIcon);
            if (player == null) return;

            var idProp = AccessTools.Property(player.GetType(), "Id");
            if (idProp == null) return;

            var playerId = Convert.ToUInt64(idProp.GetValue(player)!);
            RunHistoryAnalyzerMod.SetCurrentPlayerId(playerId);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RunHistoryAnalyzer] NRunHistorySelectPlayerPatch: {ex.Message}");
        }
    }
}
