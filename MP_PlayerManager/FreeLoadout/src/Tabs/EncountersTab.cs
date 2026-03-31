using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 遭遇（Encounters）管理 Tab：
    /// 允许玩家编辑/重置当前战斗的遭遇，或跳到特定遭遇。
    /// </summary>
    internal static class EncountersTab
    {
        private static class SC
        {
            internal static readonly Color Gold   = new Color("E3A83D");
            internal static readonly Color Cream  = new Color("E3D5C1");
            internal static readonly Color Gray   = new Color("7F8C8D");
            internal static readonly Color Green   = new Color("27AE60");
            internal static readonly Color Red    = new Color("C0392B");
            internal static readonly Color Blue    = new Color("2980B9");
        }

        internal static void Build(VBoxContainer container, Player player)
        {
            if (player == null)
            {
                var hint = new Label { Text = Loc.Get("not_in_game", "Not in a run") };
                hint.AddThemeFontSizeOverride("font_size", 20);
                hint.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(hint, false, Node.InternalMode.Disabled);
                return;
            }

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("encounters.title", "Encounters")), false, Node.InternalMode.Disabled);

            // 操作说明
            var hintLbl = new Label
            {
                Text = Loc.Get("encounters.hint", "Edit or reset encounters in the current run. Use map to navigate to specific encounters."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            hintLbl.AddThemeFontSizeOverride("font_size", 13);
            hintLbl.AddThemeColorOverride("font_color", SC.Gray);
            container.AddChild(hintLbl, false, Node.InternalMode.Disabled);

            // 操作按钮组
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("encounters.actions", "Actions")), false, Node.InternalMode.Disabled);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var resetBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("encounters.reset", "Reset Encounter"), SC.Red);
            resetBtn.CustomMinimumSize = new Vector2(140, 36);
            resetBtn.Pressed += () => TryResetCurrentEncounter();
            actionRow.AddChild(resetBtn, false, Node.InternalMode.Disabled);

            var startBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("encounters.start_fake", "Start Fake Battle"), SC.Green);
            startBtn.CustomMinimumSize = new Vector2(150, 36);
            startBtn.Pressed += () => TryStartFakeBattle();
            actionRow.AddChild(startBtn, false, Node.InternalMode.Disabled);

            var mapBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("encounters.open_map", "Open Map"), SC.Blue);
            mapBtn.CustomMinimumSize = new Vector2(110, 36);
            mapBtn.Pressed += () => OpenMapScreen();
            actionRow.AddChild(mapBtn, false, Node.InternalMode.Disabled);

            // 当前房间信息
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("encounters.current", "Current Room")), false, Node.InternalMode.Disabled);

            ShowCurrentRoomInfo(container);
        }

        private static void ShowCurrentRoomInfo(VBoxContainer container)
        {
            try
            {
                var rm = RunManager.Instance;
                var state = rm?.DebugOnlyGetState();
                if (state == null)
                {
                    var lbl = new Label { Text = Loc.Get("not_in_game", "Not in a run") };
                    lbl.AddThemeFontSizeOverride("font_size", 14);
                    lbl.AddThemeColorOverride("font_color", SC.Gray);
                    container.AddChild(lbl, false, Node.InternalMode.Disabled);
                    return;
                }

                var currentPoint = state.CurrentMapPoint;
                if (currentPoint == null)
                {
                    var lbl = new Label { Text = Loc.Get("encounters.no_room", "No current room") };
                    lbl.AddThemeFontSizeOverride("font_size", 14);
                    lbl.AddThemeColorOverride("font_color", SC.Gray);
                    container.AddChild(lbl, false, Node.InternalMode.Disabled);
                    return;
                }

                string roomType = "Unknown";
                try { roomType = currentPoint.RoomType?.ToString() ?? "Unknown"; } catch { }

                var roomLbl = new Label
                {
                    Text = $"  {Loc.Get("encounters.room_type", "Room Type")}: {roomType}",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                roomLbl.AddThemeFontSizeOverride("font_size", 15);
                roomLbl.AddThemeColorOverride("font_color", SC.Cream);
                container.AddChild(roomLbl, false, Node.InternalMode.Disabled);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] ShowCurrentRoomInfo failed: " + ex.Message);
            }
        }

        private static void TryResetCurrentEncounter()
        {
            try
            {
                var rm = RunManager.Instance;
                var state = rm?.DebugOnlyGetState();
                if (state == null) { GD.Print("[MP_PlayerManager] Not in a run state."); return; }

                var currentPoint = state.CurrentMapPoint;
                if (currentPoint == null) { GD.Print("[MP_PlayerManager] No current map point."); return; }

                GD.Print($"[MP_PlayerManager] Reset encounter requested at: {currentPoint}");
                // TODO: 实现遭遇重置逻辑
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] TryResetCurrentEncounter failed: " + ex.Message);
            }
        }

        private static void TryStartFakeBattle()
        {
            try
            {
                GD.Print("[MP_PlayerManager] Start fake battle requested.");
                // TODO: 实现假战斗逻辑（调试用）
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] TryStartFakeBattle failed: " + ex.Message);
            }
        }

        private static void OpenMapScreen()
        {
            try
            {
                var mapScreen = NGame.Instance?.GetMapScreen();
                if (mapScreen != null)
                {
                    mapScreen.Visible = true;
                    GD.Print("[MP_PlayerManager] Map screen opened.");
                }
                else
                {
                    GD.Print("[MP_PlayerManager] Map screen not available.");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] OpenMapScreen failed: " + ex.Message);
            }
        }
    }
}