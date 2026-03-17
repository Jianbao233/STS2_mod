using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// ModManager.Initialize 完成后调度 NoClientCheats 初始化与 ModConfig 注册。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    private static bool _initScheduled;

    static ModManagerInitPostfix()
    {
        try
        {
            if (_initScheduled) return;
            _initScheduled = true;
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null) tree.ProcessFrame += OnBackupFrame1;
        }
        catch { }
    }

    private static void OnBackupFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnBackupFrame1; tree.ProcessFrame += OnBackupFrame2; }
    }

    private static void OnBackupFrame2()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null)
        {
            tree.ProcessFrame -= OnBackupFrame2;
            NoClientCheatsMod.EnsureInitialized();
            NoClientCheatsMod.ApplyHarmonyPatches();
        }
    }

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager")
            ?? AccessTools.TypeByName("ModManager");
        return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
    }

    static void Postfix()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
                tree.ProcessFrame += OnInitFrame1;
        }
        catch { }
    }

    private static void OnInitFrame1()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) { tree.ProcessFrame -= OnInitFrame1; tree.ProcessFrame += OnInitFrame2; }
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
