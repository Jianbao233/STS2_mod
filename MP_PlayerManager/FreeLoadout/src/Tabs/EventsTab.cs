using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 事件管理 Tab：
    /// 允许玩家重新进入已触发的事件，或跳过事件直接进入下一房间。
    /// 参考 MODIFICATION_SCHEME.md 中 FreeLoadout 的事件重入逻辑。
    /// </summary>
    internal static class EventsTab
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
                Loc.Get("events.title", "Events")), false, Node.InternalMode.Disabled);

            // 操作说明
            var hintLbl = new Label
            {
                Text = Loc.Get("events.hint", "Use the map to navigate events. These controls help re-enter events."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            hintLbl.AddThemeFontSizeOverride("font_size", 13);
            hintLbl.AddThemeColorOverride("font_color", SC.Gray);
            container.AddChild(hintLbl, false, Node.InternalMode.Disabled);

            // 事件重入按钮组
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("events.reenter_section", "Re-enter Events")), false, Node.InternalMode.Disabled);

            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var reenterBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("events.reenter", "Re-enter Event"), SC.Green);
            reenterBtn.CustomMinimumSize = new Vector2(140, 36);
            reenterBtn.Pressed += () => TryReenterCurrentEvent();
            btnRow.AddChild(reenterBtn, false, Node.InternalMode.Disabled);

            var skipBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("events.skip", "Skip Event"), SC.Red);
            skipBtn.CustomMinimumSize = new Vector2(110, 36);
            skipBtn.Pressed += () => TrySkipCurrentEvent();
            btnRow.AddChild(skipBtn, false, Node.InternalMode.Disabled);

            var mapBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("events.open_map", "Open Map"), SC.Blue);
            mapBtn.CustomMinimumSize = new Vector2(110, 36);
            mapBtn.Pressed += () => OpenMapScreen();
            btnRow.AddChild(mapBtn, false, Node.InternalMode.Disabled);

            // 事件历史
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("events.history", "Event History")), false, Node.InternalMode.Disabled);

            var historyScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            historyScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(historyScroll, false, Node.InternalMode.Disabled);

            var historyVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            historyVBox.AddThemeConstantOverride("separation", 4);
            historyScroll.AddChild(historyVBox, false, Node.InternalMode.Disabled);

            var noHistoryLbl = new Label { Text = Loc.Get("events.no_history", "No events encountered yet") };
            noHistoryLbl.AddThemeFontSizeOverride("font_size", 13);
            noHistoryLbl.AddThemeColorOverride("font_color", SC.Gray);
            historyVBox.AddChild(noHistoryLbl, false, Node.InternalMode.Disabled);
        }

        private static void TryReenterCurrentEvent()
        {
            try
            {
                var rm = RunManager.Instance;
                var state = rm?.DebugOnlyGetState();
                if (state == null) { GD.Print("[MP_PlayerManager] Not in a run state."); return; }

                var currentRoom = state.CurrentMapPoint;
                if (currentRoom == null) { GD.Print("[MP_PlayerManager] No current map point."); return; }

                GD.Print($"[MP_PlayerManager] Re-enter event requested at map point: {currentRoom}");
                // 事件重入逻辑：调用 NEventRoom.Proceed 或直接 EnterRoomDebug
                // TODO: 实现具体的事件重入 API（需要验证游戏 API）
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] TryReenterCurrentEvent failed: " + ex.Message);
            }
        }

        private static void TrySkipCurrentEvent()
        {
            try
            {
                var rm = RunManager.Instance;
                var state = rm?.DebugOnlyGetState();
                if (state == null) { GD.Print("[MP_PlayerManager] Not in a run state."); return; }

                GD.Print("[MP_PlayerManager] Skip event requested.");
                // TODO: 实现事件跳过逻辑（前进到下一房间）
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] TrySkipCurrentEvent failed: " + ex.Message);
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