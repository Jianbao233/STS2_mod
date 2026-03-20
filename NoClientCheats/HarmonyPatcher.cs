using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// ModManager.Initialize 完成后调度 NoClientCheats 初始化与 ModConfig 注册。
/// 
/// 注意：static ctor 中 Engine.GetMainLoop() 在游戏加载早期为 null，
/// 所以只在 Postfix（Initialize 已完成）中注册两帧延迟回调来执行初始化。
/// </summary>
[HarmonyPatch]
internal static class ModManagerInitPostfix
{
    /// <summary>
    /// static ctor 不可靠（GetMainLoop 此时为 null），不用于注册初始化。
    /// 仅用于确保 PatchAll 能发现本类。
    /// </summary>
    static ModManagerInitPostfix()
    {
        // 占位：仅让 HarmonyPatchAll 发现本类。实际初始化在 Postfix 中。
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
        catch (Exception e)
        {
            GD.PushError($"[NoClientCheats] ModManagerInitPostfix.Postfix failed: {e.Message}");
        }
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
