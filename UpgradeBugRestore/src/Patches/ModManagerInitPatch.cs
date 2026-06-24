using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace UpgradeBugRestore;

/// <summary>
/// 在 ModManager 初始化后（如果存在）或游戏主循环第二帧调度 mod 入口与 Harmony 注入。
/// 入口策略复制自项目内其他 mod（RefreshShop / NoClientCheats），保持兼容。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    private static bool _initScheduled;

    static ModManagerInitPostfix()
    {
        ScheduleInit();
    }

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix()
    {
        ScheduleInit();
    }

    private static void ScheduleInit()
    {
        if (_initScheduled) return;

        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;

            _initScheduled = true;
            tree.ProcessFrame += OnInitFrame1;
        }
        catch
        {
            // 启动兜底，不影响游戏。
        }
    }

    private static void OnInitFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame -= OnInitFrame1;
        tree.ProcessFrame += OnInitFrame2;
    }

    private static void OnInitFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;

        tree.ProcessFrame -= OnInitFrame2;
        UpgradeBugRestoreMod.EnsureInitialized();
        UpgradeBugRestoreMod.ApplyHarmonyPatches();
    }
}