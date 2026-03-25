using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace MP_PlayerManager
{
    /// <summary>
    /// 与 NoClientCheats / RunHistoryAnalyzer 完全一致的初始化架构：
    /// 1. static ctor：PatchAll 时尝试（Engine 可能为 null，静默跳过）
    /// 2. Postfix：ModManager.Initialize 完成后调度两帧延迟初始化
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
            return t?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
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
                tree.ProcessFrame += OnFrame1;
            }
            catch { }
        }

        private static void OnFrame1()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                tree.ProcessFrame -= OnFrame1;
                tree.ProcessFrame += OnFrame2;
            }
        }

        private static void OnFrame2()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                tree.ProcessFrame -= OnFrame2;
                Config.Load();
                Loc.Reload();
                new Harmony("bon.mp_playermanager").PatchAll(Assembly.GetExecutingAssembly());

                var tree2 = Engine.GetMainLoop() as SceneTree;
                tree2?.Root?.AddChild(new F1InputNode());

                LoadoutPanel.Toggle(); // Build + 首次显示
                LoadoutPanel.Hide();   // 立即隐藏，用户按 F1 呼出
                GD.Print("[MP_PlayerManager] Initialized");
            }
        }
    }
}
