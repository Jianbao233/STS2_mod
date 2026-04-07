using Godot;
using HarmonyLib;
using MultiplayerTools.Core;
using MultiplayerTools.Steam;
using MultiplayerTools.UI;
using MultiplayerTools.Platform;
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
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] TryScheduleInit failed: " + ex.Message);
            }
        }

        private static void OnFrame1()
        {
            try
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null)
                {
                    tree.ProcessFrame -= OnFrame1;
                    tree.ProcessFrame += OnFrame2;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnFrame1 failed: " + ex.Message);
            }
        }

        private static void OnFrame2()
        {
            try
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                if (tree == null) return;

                tree.ProcessFrame -= OnFrame2;
                Config.Load();
                Loc.Reload();
                PlatformInfo.LogStartupInfo();
                new Harmony("multiplayer.tools").PatchAll(Assembly.GetExecutingAssembly());

                tree.Root?.AddChild(new F1InputNode());
                tree.Root?.AddChild(new MpFloatingButton());

                MpPanel.Toggle();
                MpPanel.Hide();
                GD.Print("[MultiplayerTools] Initialized");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnFrame2 failed: " + ex);
            }
        }
    }

    /// <summary>
    /// F1 hotkey input node. Added to the scene tree root.
    /// In Godot 4 use _ShortcutInput (or _Input) with InputEventFromAction.
    /// </summary>
    internal partial class F1InputNode : Node
    {
        public override void _EnterTree()
        {
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F1)
            {
                if (!MainMenuGuard.IsMainMenuHomeActive())
                    return;

                MpPanel.Toggle();
            }
            base._UnhandledInput(@event);
        }
    }
}
