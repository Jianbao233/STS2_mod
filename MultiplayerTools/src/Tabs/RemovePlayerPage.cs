using System;
using System.Collections.Generic;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Remove player page — mirrors v2 _page_remove_player + _do_remove_player.
    ///
    /// Layout:
    ///   [title + subtitle]
    ///   → warning if no save selected
    ///   → scrollable player list (red tint, radio selection)
    ///   → warning (irreversible)
    ///   → confirm button
    /// </summary>
    internal static class RemovePlayerPage
    {
        private static int _selectedIndex = -1;

        internal static void Build(VBoxContainer container)
        {
            _selectedIndex = -1;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("remove.title", "Remove Player")), false, Node.InternalMode.Disabled);
            var subtitle = new Label { Text = Loc.Get("remove.subtitle", "Remove a player from the save") };
            subtitle.AddThemeFontSizeOverride("font_size", 17);
            subtitle.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            container.AddChild(subtitle, false, Node.InternalMode.Disabled);

            if (MpSessionState.CurrentSavePath == null || MpSessionState.SaveData.Count == 0)
            {
                AddWarning(container, Loc.Get("remove.select_save", "No save selected. Go to Save Select first."));
                return;
            }

            var players = MpSessionState.GetPlayers();
            if (players.Count == 0)
            {
                AddWarning(container, Loc.Get("player.no_players", "No players found."));
                return;
            }

            // Player list (scrollable)
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 260)
            };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var scrollContent = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            scrollContent.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(scrollContent, false, Node.InternalMode.Disabled);

            for (int i = 0; i < players.Count; i++)
            {
                var pl = players[i] as Dictionary<string, object>;
                if (pl == null) continue;
                RenderRemoveCard(scrollContent, pl, i, i == 0);
            }

            // Warning
            AddWarning(container, "⚠ " + Loc.Get("remove.irreversible", "This action cannot be undone."));

            // Confirm button
            var confirmBtn = MpPanel.CreateActionButton(Loc.Get("remove.confirm_btn", "Remove Player"), Panel.Styles.Red);
            confirmBtn.CustomMinimumSize = new Vector2(160, 38);
            confirmBtn.AddThemeFontSizeOverride("font_size", 18);
            confirmBtn.Pressed += () => DoRemovePlayer();
            container.AddChild(confirmBtn, false, Node.InternalMode.Disabled);
        }

        private static void RenderRemoveCard(VBoxContainer parent, Dictionary<string, object> pl, int index, bool isHost)
        {
            string netId = GetStr(pl, "net_id");
            string charId = GetStr(pl, "character_id");
            int deckN = GetDeckCount(pl);
            int relicsN = GetRelicsCount(pl);
            int potionsN = GetPotionsCount(pl);

            // Red-tinted card (removal danger)
            var card = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 80)
            };
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Godot.Color("3D1A1A"),
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            cardStyle.SetBorderWidthAll(0);
            cardStyle.SetCornerRadiusAll(8);
            card.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(card, false, Node.InternalMode.Disabled);

            var margin = new MarginContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            card.AddChild(margin, false, Node.InternalMode.Disabled);

            var inner = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            inner.AddThemeConstantOverride("separation", 10);
            margin.AddChild(inner, false, Node.InternalMode.Disabled);

            // Radio (disabled for host)
            if (!isHost)
            {
                var radio = new CheckBox
                {
                    CustomMinimumSize = new Vector2(24, 0),
                    ButtonPressed = _selectedIndex == index
                };
                int idx = index;
                radio.Toggled += pressed =>
                {
                    if (!pressed) { if (_selectedIndex == idx) _selectedIndex = -1; }
                    else _selectedIndex = idx;
                };
                inner.AddChild(radio, false, Node.InternalMode.Disabled);
            }
            else
            {
                var hostBadge = new Label { Text = "[HOST]" };
                hostBadge.AddThemeFontSizeOverride("font_size", 16);
                hostBadge.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
                inner.AddChild(hostBadge, false, Node.InternalMode.Disabled);
            }

            var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            textCol.AddThemeConstantOverride("separation", 4);
            inner.AddChild(textCol, false, Node.InternalMode.Disabled);

            var headLbl = new Label
            {
                Text = $"[{index + 1}] {GetPlayerDisplayName(netId, charId)}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            headLbl.AddThemeFontSizeOverride("font_size", 18);
            headLbl.AddThemeFontSizeOverride("font_size", isHost ? 18 : 17);
            headLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
            textCol.AddChild(headLbl, false, Node.InternalMode.Disabled);

            string detail = $"{deckN} cards  ·  {relicsN} relics  ·  {potionsN} potions";
            var detailLbl = new Label { Text = detail, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            detailLbl.AddThemeFontSizeOverride("font_size", 16);
            detailLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            textCol.AddChild(detailLbl, false, Node.InternalMode.Disabled);
        }

        private static void DoRemovePlayer()
        {
            if (_selectedIndex < 0)
            {
                ShowMsg(Loc.Get("remove.not_selected", "Please select a player to remove."), Panel.Styles.MpGold);
                return;
            }
            if (_selectedIndex == 0)
            {
                ShowMsg(Loc.Get("remove.host_protected", "Cannot remove the host player."), Panel.Styles.Red);
                return;
            }

            // Show confirmation dialog
            ShowConfirmDialog(() =>
            {
                var result = PlayerOpsService.RemovePlayerFull(_selectedIndex, MpSessionState.CurrentSavePath!);
                if (result.Success)
                {
                    MpSessionState.ReloadSave();
                    ShowMsg(Loc.Get("remove.success", "Player removed successfully!"), Panel.Styles.Green);
                    MpPanel.SwitchPage(MpPanel.PAGE_TAKEOVER);
                }
                else
                {
                    ShowMsg(result.Message, Panel.Styles.Red);
                }
            });
        }

        private static void ShowConfirmDialog(Action onConfirm)
        {
            var dlg = new Popup
            {
                Title = Loc.Get("remove.confirm_title", "Confirm Remove"),
                Exclusive = true
            };
            dlg.SetPosition(new Vector2I(200, 200));
            dlg.SetSize(new Vector2I(360, 160));
            (Engine.GetMainLoop() as SceneTree)?.Root.AddChild(dlg);
            dlg.PopupCentered(new Vector2I(360, 160));

            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 12);
            dlg.AddChild(content);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 16);
            margin.AddThemeConstantOverride("margin_bottom", 16);
            content.AddChild(margin);

            var msg = new Label
            {
                Text = Loc.Get("remove.confirm_msg", "Are you sure you want to remove this player? This cannot be undone."),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            msg.AddThemeFontSizeOverride("font_size", 17);
            msg.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
            margin.AddChild(msg, false, Node.InternalMode.Disabled);

            var btnRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            btnRow.AddThemeConstantOverride("separation", 10);
            margin.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var okBtn = MpPanel.CreateActionButton(Loc.Get("confirm", "Confirm"), Panel.Styles.Red);
            okBtn.CustomMinimumSize = new Vector2(100, 34);
            okBtn.Pressed += () => { dlg.Hide(); dlg.QueueFree(); onConfirm(); };
            btnRow.AddChild(okBtn, false, Node.InternalMode.Disabled);

            var cancelBtn = MpPanel.CreateActionButton(Loc.Get("cancel", "Cancel"), Panel.Styles.MpPrimaryBtn);
            cancelBtn.CustomMinimumSize = new Vector2(100, 34);
            cancelBtn.Pressed += () => { dlg.Hide(); dlg.QueueFree(); };
            btnRow.AddChild(cancelBtn, false, Node.InternalMode.Disabled);
        }

        private static void AddWarning(VBoxContainer container, string text)
        {
            var warn = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 40)
            };
            var warnStyle = new StyleBoxFlat
            {
                BgColor = new Godot.Color("3D1A1A"),
                BorderColor = Godot.Colors.Transparent,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            warnStyle.SetBorderWidthAll(0);
            warnStyle.SetCornerRadiusAll(6);
            warn.AddThemeStyleboxOverride("panel", warnStyle);
            container.AddChild(warn, false, Node.InternalMode.Disabled);

            var warnMargin = new MarginContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            warnMargin.AddThemeConstantOverride("margin_left", 12);
            warnMargin.AddThemeConstantOverride("margin_right", 12);
            warnMargin.AddThemeConstantOverride("margin_top", 8);
            warnMargin.AddThemeConstantOverride("margin_bottom", 8);
            warn.AddChild(warnMargin, false, Node.InternalMode.Disabled);

            var warnLbl = new Label { Text = text, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            warnLbl.AddThemeFontSizeOverride("font_size", 17);
            warnLbl.AddThemeColorOverride("font_color", Panel.Styles.Red);
            warnMargin.AddChild(warnLbl, false, Node.InternalMode.Disabled);
        }

        private static void ShowMsg(string text, Color color)
        {
            MpPanel.ShowStatusMessage(text, color);
        }

        private static string GetPlayerDisplayName(string netId, string charId)
        {
            var steamName = Steam.SteamIntegration.GetPersonaName(netId);
            if (!string.IsNullOrEmpty(steamName) && steamName != netId)
                return steamName;
            return CharacterDisplayNames.Resolve(charId);
        }

        private static string GetStr(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
        }

        private static int GetDeckCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("deck", out var raw) && raw is List<object> deck) return deck.Count;
            return 0;
        }

        private static int GetRelicsCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("relics", out var raw) && raw is List<object> relics) return relics.Count;
            return 0;
        }

        private static int GetPotionsCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("potions", out var raw) && raw is List<object> potions) return potions.Count;
            return 0;
        }
    }
}
