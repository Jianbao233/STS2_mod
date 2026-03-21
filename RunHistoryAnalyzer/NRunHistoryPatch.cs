using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace RunHistoryAnalyzer;

/// <summary>
/// Hook NRunHistory 界面相关操作：
/// 1. 当玩家在历史记录列表中选中某条记录时，获取其对应的 .run 文件路径。
/// 2. 将分析按钮注入到详情面板底部操作栏（待实现）。
///
/// 当前版本：按钮显示在屏幕右下角，点击后分析当前选中的文件。
/// </summary>
[HarmonyPatch]
internal static class NRunHistoryPatch
{
    /// <summary>
    /// 监听玩家选择历史记录的事件。
    /// 在玩家选择某条历史记录后，调用 SetSelectedFile 记录其 .run 文件路径。
    ///
    /// 具体 Hook 点需在运行时通过反射找到 NRunHistory 的选择方法后补充。
    /// 当前方案：监听所有 Node 的子节点变化，尝试找到 _SelectHistory 或类似方法。
    /// </summary>
    static MethodBase? TargetMethod()
    {
        // MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistory")
            ?? AccessTools.TypeByName("NRunHistory");
        if (t == null)
        {
            GD.Print("[RunHistoryAnalyzer] NRunHistory type not found. UI injection disabled.");
            return null;
        }

        // 尝试找到 OnHistorySelected 或类似方法
        foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (method.Name.Contains("History") && method.Name.Contains("Select"))
            {
                GD.Print($"[RunHistoryAnalyzer] Found target method: {method.Name}");
                return method;
            }
        }

        GD.Print("[RunHistoryAnalyzer] No HistorySelect method found in NRunHistory.");
        return null;
    }

    static void Postfix(object __instance)
    {
        try
        {
            // 从 __instance 中提取 .run 文件路径
            // NRunHistory 在选中某条记录时，会持有一个 RunHistory 对象
            // 通过反射找到 _currentHistory 或类似字段
            var instType = __instance.GetType();

            foreach (var field in instType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType.Name.Contains("RunHistory") || field.FieldType.Name.Contains("HistoryEntry"))
                {
                    var val = field.GetValue(__instance);
                    if (val != null)
                    {
                        // 尝试获取文件路径
                        var pathField = val.GetType().GetField("FilePath")
                            ?? val.GetType().GetField("file_path")
                            ?? val.GetType().GetField("_filePath");

                        if (pathField != null)
                        {
                            var path = pathField.GetValue(val) as string;
                            if (!string.IsNullOrEmpty(path))
                            {
                                RunHistoryAnalyzerMod.SetSelectedFile(path);
                                GD.Print($"[RunHistoryAnalyzer] Selected history: {path}");
                                return;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[RunHistoryAnalyzer] NRunHistory patch error: {ex.Message}");
        }
    }
}
