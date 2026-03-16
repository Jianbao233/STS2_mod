using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// ModManager.Initialize 完成后，创建控制面板并挂载到场景树。F7 由 F7InputLayer 处理。
/// 静态构造器作为备用：PatchAll 加载本类时也会调度初始化。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    private static bool _initScheduled;

    /// <summary>PatchAll 加载本类时运行，确保即使 Postfix 未触发也能初始化</summary>
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
            ControlPanelMod.EnsureInitialized();
            ControlPanelMod.CreateAndAttachPanel();
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
            ControlPanelMod.EnsureInitialized();
            ControlPanelMod.CreateAndAttachPanel();
        }
    }
}
