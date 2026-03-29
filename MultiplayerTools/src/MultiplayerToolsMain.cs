using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace MultiplayerTools
{
    /// <summary>
    /// Mod entry point. Initializes Harmony, loads config, registers hotkeys,
    /// and sets up the main panel. Pattern from MP_PlayerManager TrainerBootstrap.
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
                new Harmony("multiplayer.tools").PatchAll(Assembly.GetExecutingAssembly());

                var tree2 = Engine.GetMainLoop() as SceneTree;
                tree2?.Root?.AddChild(new F1InputNode());

                MpPanel.Toggle();
                MpPanel.Hide();
                GD.Print("[MultiplayerTools] Initialized");
            }
        }
    }

    /// <summary>
    /// F1 hotkey input node. Added to the scene tree root.
    /// </summary>
    internal partial class F1InputNode : Node
    {
        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F1)
            {
                MpPanel.Toggle();
            }
        }
    }
}
