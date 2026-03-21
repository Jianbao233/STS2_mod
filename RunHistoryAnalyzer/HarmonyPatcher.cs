using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace RunHistoryAnalyzer;

/// <summary>
/// Hook ModManager.Initialize 完成后初始化本 Mod。
/// 参考 NoClientCheats 的三重保险模式。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    private static bool _initScheduled;

    static ModManagerInitPostfix()
    {
        TryScheduleInit();
    }

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!;
    }

    static void Postfix()
    {
        TryScheduleInit();
    }

    internal static void TryScheduleInit()
    {
        if (_initScheduled) return;
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;
            _initScheduled = true;
            tree.ProcessFrame += OnInitFrame1;
        }
        catch { }
    }

    private static void OnInitFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null)
        {
            tree.ProcessFrame -= OnInitFrame1;
            tree.ProcessFrame += OnInitFrame2;
        }
    }

    private static void OnInitFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null)
        {
            tree.ProcessFrame -= OnInitFrame2;
            RunHistoryAnalyzerMod.EnsureInitialized();
            RunHistoryAnalyzerMod.ApplyHarmonyPatches();
        }
    }
}
