using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace RunHistoryAnalyzer;

/// <summary>
/// 在 NRunHistory.RefreshAndSelectRun 完成后，根据当前选中的 .run 文件名拼出完整路径并更新分析按钮。
/// （旧版用方法名猜 Hook，找不到方法时 TargetMethod 返回 null 会导致 Harmony 抛错并触发「模组加载错误」。）
/// </summary>
[HarmonyPatch]
internal static class NRunHistoryRefreshPatch
{
    static MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory");
        return t?.GetMethod("RefreshAndSelectRun", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>目标方法不存在时跳过本补丁类，避免 PatchAll 因 null 失败。</summary>
    static bool Prepare()
    {
        return TargetMethod() != null;
    }

    static void Postfix(object __instance, int index)
    {
        try
        {
            var instType = __instance.GetType();
            var namesField = AccessTools.Field(instType, "_runNames");
            var names = namesField?.GetValue(__instance) as List<string>;
            if (names == null || index < 0 || index >= names.Count)
                return;

            var fileName = names[index];
            if (string.IsNullOrEmpty(fileName))
                return;

            var saveManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.SaveManager");
            if (saveManagerType == null)
                return;

            var instProp = saveManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var saveManager = instProp?.GetValue(null);
            if (saveManager == null)
                return;

            var pidProp = saveManagerType.GetProperty("CurrentProfileId");
            if (pidProp == null)
                return;
            var profileId = Convert.ToInt32(pidProp.GetValue(saveManager)!);

            var resolved = RunHistoryPathResolver.TryResolveExistingFile(fileName, profileId);
            if (string.IsNullOrEmpty(resolved))
            {
                GD.PrintErr($"[RunHistoryAnalyzer] 无法解析历史文件路径: {fileName} (profile {profileId})");
                return;
            }

            RunHistoryAnalyzerMod.SetSelectedFile(resolved);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RunHistoryAnalyzer] NRunHistoryRefreshPatch: {ex.Message}");
        }
    }
}
