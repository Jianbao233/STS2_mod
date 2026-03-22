using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace RunHistoryAnalyzer;

/// <summary>
/// 离开历史记录详情子菜单时清空选中 .run 路径，隐藏右下角分析按钮与报告窗口，
/// 避免「分析失败」等状态残留在主菜单等处。
/// </summary>
[HarmonyPatch]
internal static class NRunHistorySubmenuHiddenPatch
{
    static MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory");
        return t?.GetMethod("OnSubmenuHidden", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static bool Prepare()
    {
        return TargetMethod() != null;
    }

    static void Postfix()
    {
        try
        {
            RunHistoryAnalyzerMod.SetSelectedFile("");
            RunHistoryAnalyzerMod.SetCurrentPlayerId(0);
            RunHistoryAnalyzerMod.ResultWindow?.Hide();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RunHistoryAnalyzer] NRunHistorySubmenuHiddenPatch: {ex.Message}");
        }
    }
}
