using Godot;
using HarmonyLib;
using System;
using System.Reflection;
using RunHistoryAnalyzer.UI;

namespace RunHistoryAnalyzer;

/// <summary>
/// 全局状态：记录当前选中的历史记录文件路径，以及分析按钮/结果窗口的引用。
/// </summary>
public static class RunHistoryAnalyzerMod
{
    public const string ModId = "RunHistoryAnalyzer";

    /// <summary>当前在历史记录详情面板中选中的 .run 文件路径。</summary>
    public static string CurrentSelectedFilePath = "";

    /// <summary>分析按钮实例。</summary>
    internal static AnalyzeButton? AnalyzeBtn;

    /// <summary>结果窗口实例。</summary>
    internal static AnalyzeResultWindow? ResultWindow;

    private static bool _initialized;
    private static bool _harmonyPatched;

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // 创建分析按钮和结果窗口，加入根节点
        AnalyzeBtn = new AnalyzeButton();
        ResultWindow = new AnalyzeResultWindow();

        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Root?.AddChild(AnalyzeBtn);
        tree?.Root?.AddChild(ResultWindow);

        GD.Print($"[RunHistoryAnalyzer] Loaded.");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_harmonyPatched) return;
        _harmonyPatched = true;
        try
        {
            var harmony = new Harmony(ModId);
            harmony.PatchAll();
            GD.Print("[RunHistoryAnalyzer] Harmony patches applied.");
        }
        catch (Exception e)
        {
            GD.PushError($"[RunHistoryAnalyzer] Harmony patch failed: {e}");
        }
    }

    /// <summary>设置当前选中的历史记录文件路径，并更新按钮状态。</summary>
    public static void SetSelectedFile(string filePath)
    {
        CurrentSelectedFilePath = filePath;
        AnalyzeBtn?.UpdateFilePath(filePath);
    }
}
