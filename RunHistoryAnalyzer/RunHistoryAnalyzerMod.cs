using Godot;
using HarmonyLib;
using System;
using System.Reflection;
using RunHistoryAnalyzer.UI;

namespace RunHistoryAnalyzer;

/// <summary>
/// 全局状态：记录当前选中的历史记录文件路径、当前查看的玩家ID，以及分析按钮/结果窗口的引用。
/// </summary>
public static class RunHistoryAnalyzerMod
{
    public const string ModId = "RunHistoryAnalyzer";

    /// <summary>当前在历史记录详情面板中选中的 .run 文件路径。</summary>
    public static string CurrentSelectedFilePath = "";

    /// <summary>
    /// 当前在 NRunHistory 中选中的玩家 ID（ulong）。
    /// 用于分析时只处理当前查看的那名角色。
    /// </summary>
    public static ulong CurrentPlayerId = 0;

    /// <summary>ModConfig：显示/隐藏分析工具栏（默认 F6）。</summary>
    public static Key ToggleToolbarKey = Key.F6;

    /// <summary>
    /// 为 false 时强制隐藏右下角分析按钮（仍保留选中路径，按快捷键可再显示）。
    /// </summary>
    public static bool AnalyzerToolbarVisible = true;

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
        tree?.Root?.AddChild(new RunHistoryAnalyzerHotkey());

        ModConfigIntegration.Register();

        GD.Print($"[RunHistoryAnalyzer] Loaded.");
    }

    /// <summary>从 ModConfig 下拉项更新快捷键（如 "F6"）。</summary>
    public static void SetToggleToolbarKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return;
        try
        {
            ToggleToolbarKey = (Key)Enum.Parse(typeof(Key), keyName.Trim(), ignoreCase: true);
        }
        catch
        {
            ToggleToolbarKey = Key.F6;
        }
    }

    /// <summary>切换分析工具栏显示；隐藏时同时关闭报告窗口。</summary>
    public static void ToggleAnalyzerToolbar()
    {
        AnalyzerToolbarVisible = !AnalyzerToolbarVisible;
        AnalyzeBtn?.ApplyToolbarVisibility();
        if (!AnalyzerToolbarVisible)
            ResultWindow?.Hide();
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

    /// <summary>
    /// 设置当前选中的历史记录文件路径，同时重置当前玩家ID。
    /// </summary>
    public static void SetSelectedFile(string filePath)
    {
        CurrentSelectedFilePath = filePath;
        CurrentPlayerId = 0; // 切换文件时重置，等 SelectPlayer patch 重新设置
        AnalyzeBtn?.UpdateFilePath(filePath);
    }

    /// <summary>
    /// 设置当前选中的玩家 ID（由 NRunHistorySelectPlayerPatch 调用）。
    /// </summary>
    public static void SetCurrentPlayerId(ulong playerId)
    {
        CurrentPlayerId = playerId;
    }
}
