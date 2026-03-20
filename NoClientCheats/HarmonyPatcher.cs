using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// ModManager.Initialize 完成后调度 NoClientCheats 初始化与 ModConfig 注册。
///
/// 三重保险：
/// 1. static ctor：PatchAll 时尝试注册（此时 Engine 可能为 null，失败则静默）
/// 2. Postfix：ModManager.Initialize 完成后注册（Engine 应该已就绪）
/// 3. CheatBlockPrefix.TryScheduleInit：作弊拦截首次触发时兜底尝试（确保终局调用）
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    private static bool _initScheduled;

    /// <summary>
    /// static ctor 在 PatchAll 加载本类时执行。
    /// 早期 Engine.GetMainLoop() 可能为 null，失败则静默忽略，依赖 Postfix。
    /// </summary>
    static ModManagerInitPostfix()
    {
        TryScheduleInit();
    }

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix()
    {
        TryScheduleInit();
    }

    /// <summary>
    /// 尝试注册两帧延迟初始化回调。无论哪条路径先调用，初始化只执行一次。
    /// </summary>
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
            NoClientCheatsMod.EnsureInitialized();
            NoClientCheatsMod.ApplyHarmonyPatches();
        }
    }
}
