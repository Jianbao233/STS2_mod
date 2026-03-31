using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MultiplayerTools.Core;
using MultiplayerTools.UI;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Takeover player page — mirrors v2 _page_takeover + _do_takeover.
    ///
    /// Layout:
    ///   [title + subtitle]
    ///   → warning if no save selected
    ///   → scrollable player list (host: label; others: radio)
    ///   → separator
    ///   → successor Steam ID input row (LineEdit + Friends button)
    ///   → warning (irreversible)
    ///   → confirm + refresh buttons
    /// </summary>
    internal static class TakeoverPage
    {
        private static int _selectedPlayerIndex = -1;

        internal static void Build(VBoxContainer container)
        {
            _selectedPlayerIndex = -1;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("takeover.title", "Take Over Player")), false, Node.InternalMode.Disabled);
            var subtitle = new Label { Text = Loc.Get("takeover.subtitle", "Replace another player's slot with your Steam ID") };
            subtitle.AddThemeFontSizeOverride("font_size", 17);
            subtitle.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            container.AddChild(subtitle, false, Node.InternalMode.Disabled);

            if (MpSessionState.CurrentSavePath == null || MpSessionState.SaveData.Count == 0)
            {
                AddWarning(container, Loc.Get("takeover.select_save", "No save selected. Go to Save Select and click a save card first."));
                return;
            }

            var players = MpSessionState.GetPlayers();
            if (players.Count == 0)
            {
                AddWarning(container, Loc.Get("player.no_players", "No players found in this save."));
                return;
            }

            // Player list (scrollable)
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 220)
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
                RenderPlayerCard(scrollContent, pl, i, i == 0);
            }

            // Separator
            AddSeparator(container);

            // Successor section
            var succTitle = new Label { Text = Loc.Get("takeover.successor", "Successor") };
            succTitle.AddThemeFontSizeOverride("font_size", 19);
            succTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            container.AddChild(succTitle, false, Node.InternalMode.Disabled);

            // Steam ID input row
            var idRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            idRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(idRow, false, Node.InternalMode.Disabled);

            var idEdit = new LineEdit
            {
                PlaceholderText = "Steam ID (15+ digits)",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(300, 32)
            };
            idEdit.AddThemeFontSizeOverride("font_size", 18);
            idRow.AddChild(idEdit, false, Node.InternalMode.Disabled);

            var friendsBtn = MpPanel.CreateActionButton(Loc.Get("friend.friends_btn", "Friends"), Panel.Styles.MpPrimaryBtn);
            friendsBtn.CustomMinimumSize = new Vector2(80, 32);
            friendsBtn.Pressed += () => ShowFriendPicker(idEdit);
            idRow.AddChild(friendsBtn, false, Node.InternalMode.Disabled);

            // Warning
            AddWarning(container, "⚠ " + Loc.Get("takeover.irreversible", "This action cannot be undone."));

            // Button row
            var btnRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            btnRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var confirmBtn = MpPanel.CreateActionButton(Loc.Get("takeover.confirm", "Confirm Takeover"), Panel.Styles.MpGold);
            confirmBtn.CustomMinimumSize = new Vector2(140, 38);
            confirmBtn.AddThemeFontSizeOverride("font_size", 18);
            confirmBtn.Pressed += () => DoTakeover(idEdit.Text);
            btnRow.AddChild(confirmBtn, false, Node.InternalMode.Disabled);

            var refreshBtn = MpPanel.CreateActionButton(Loc.Get("takeover.refresh", "Refresh"), Panel.Styles.MpPrimaryBtn);
            refreshBtn.CustomMinimumSize = new Vector2(100, 38);
            refreshBtn.Pressed += () =>
            {
                MpSessionState.ReloadSave();
                MpPanel.SwitchPage(MpPanel.PAGE_TAKEOVER);
            };
            btnRow.AddChild(refreshBtn, false, Node.InternalMode.Disabled);
        }

        private static void RenderPlayerCard(VBoxContainer parent, Dictionary<string, object> pl, int index, bool isHost)
        {
            string netId = GetStr(pl, "net_id");
            string charId = GetStr(pl, "character_id");
            int hp = GetInt(pl, "current_hp");
            int maxHp = GetInt(pl, "max_hp");
            int gold = GetInt(pl, "gold");
            int deckN = GetDeckCount(pl);
            int relicsN = GetRelicsCount(pl);

            var card = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, isHost ? 72 : 68)
            };
            var cardStyle = new StyleBoxFlat
            {
                BgColor = isHost ? new Godot.Color("1A2A50") : Panel.Styles.MpNavSelected,
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

            if (!isHost)
            {
                // Radio button placeholder (visual — actual selection stored in _selectedPlayerIndex)
                var radio = new CheckBox
                {
                    CustomMinimumSize = new Vector2(24, 0),
                    ButtonPressed = _selectedPlayerIndex == index
                };
                radio.Toggled += pressed =>
                {
                    if (pressed) _selectedPlayerIndex = index;
                    else if (_selectedPlayerIndex == index) _selectedPlayerIndex = -1;
                };
                inner.AddChild(radio, false, Node.InternalMode.Disabled);
            }

            var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            textCol.AddThemeConstantOverride("separation", 4);
            inner.AddChild(textCol, false, Node.InternalMode.Disabled);

            // Heading line
            string heading = isHost
                ? $"[{index + 1}] {GetPlayerDisplayName(netId, charId)}  {Loc.Get("takeover.host", "Host")}"
                : $"[{index + 1}] {GetPlayerDisplayName(netId, charId)}";
            var headLbl = new Label { Text = heading, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            headLbl.AddThemeFontSizeOverride("font_size", isHost ? 18 : 17);
            headLbl.AddThemeColorOverride("font_color", isHost ? Panel.Styles.MpGold : Panel.Styles.MpTextNav);
            if (isHost) headLbl.AddThemeFontSizeOverride("font_size", 18);
            textCol.AddChild(headLbl, false, Node.InternalMode.Disabled);

            // Detail line
            string detail = $"{netId}  HP {hp}/{maxHp}  Gold {gold}  {deckN} cards  {relicsN} relics";
            var detailLbl = new Label { Text = detail, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            detailLbl.AddThemeFontSizeOverride("font_size", 16);
            detailLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            textCol.AddChild(detailLbl, false, Node.InternalMode.Disabled);
        }

        private static void DoTakeover(string steamIdRaw)
        {
            if (_selectedPlayerIndex < 0)
            {
                ShowMsg(Loc.Get("takeover.not_selected", "Please select a player to take over."), Panel.Styles.MpGold);
                return;
            }
            if (_selectedPlayerIndex == 0)
            {
                ShowMsg(Loc.Get("takeover.host_protected", "Cannot take over the host player."), Panel.Styles.Red);
                return;
            }
            string steamId = steamIdRaw.Trim();
            if (string.IsNullOrEmpty(steamId))
            {
                ShowMsg(Loc.Get("takeover.choose_steam", "Please enter a Steam ID."), Panel.Styles.MpGold);
                return;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, @"^\d{15,}$"))
            {
                ShowMsg(Loc.Get("takeover.id_format", "Steam ID must be 15+ digits."), Panel.Styles.Red);
                return;
            }

            var result = PlayerOpsService.TakeOverPlayer(_selectedPlayerIndex, steamId, MpSessionState.CurrentSavePath!);
            if (result.Success)
            {
                bool ok = MpSessionState.FlushSave();
                if (ok)
                {
                    MpSessionState.ReloadSave();
                    ShowMsg(Loc.Get("takeover.success", "Player taken over successfully!"), Panel.Styles.Green);
                    MpPanel.SwitchPage(MpPanel.PAGE_TAKEOVER);
                }
                else
                {
                    ShowMsg(Loc.Get("takeover.save_failed", "Takeover succeeded but save failed."), Panel.Styles.Red);
                }
            }
            else
            {
                ShowMsg(result.Message, Panel.Styles.Red);
            }
        }

        private static void ShowFriendPicker(LineEdit targetEdit)
        {
            var me = Steam.SteamIntegration.GetCurrentSteamId() ?? "";
            var room = BuildRoomMemberOptions(me);
            var friends = Steam.SteamIntegration.GetLocalFriends()
                .Select(c => new SteamIdPickerPopup.SteamIdOption(
                    c.SteamId,
                    c.PersonaName,
                    string.IsNullOrEmpty(c.PersonaName) || c.PersonaName == c.SteamId ? null : c.SteamId))
                .ToList();

            SteamIdPickerPopup.Show(targetEdit, room, friends);
        }

        private static List<SteamIdPickerPopup.SteamIdOption> BuildRoomMemberOptions(string localSteamId)
        {
            var opts = new List<SteamIdPickerPopup.SteamIdOption>();
            var players = MpSessionState.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is not Dictionary<string, object> pl) continue;
                string netId = GetStr(pl, "net_id").Trim();
                if (string.IsNullOrEmpty(netId)) continue;
                if (!string.IsNullOrEmpty(localSteamId) && netId == localSteamId) continue;

                string charId = GetStr(pl, "character_id");
                string display = GetPlayerDisplayName(netId, charId);
                string sub = $"[{i + 1}] {Loc.Get("friend.player", "Player")} · {CharacterDisplayNames.Resolve(charId)}";
                opts.Add(new SteamIdPickerPopup.SteamIdOption(netId, display, sub));
            }
            return opts;
        }

        private static void AddSeparator(VBoxContainer container)
        {
            var sep = new ColorRect
            {
                CustomMinimumSize = new Vector2(0, 1),
                Color = Panel.Styles.Divider,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            sep.AddThemeConstantOverride("custom_minimum_size_y", 1);
            container.AddChild(sep, false, Node.InternalMode.Disabled);
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

        // ── Helpers (same as v2) ───────────────────────────────────────────────

        private static string GetPlayerDisplayName(string netId, string charId)
        {
            // Try Steam nickname first
            var steamName = Steam.SteamIntegration.GetPersonaName(netId);
            if (!string.IsNullOrEmpty(steamName) && steamName != netId)
                return steamName;
            return CharacterDisplayNames.Resolve(charId);
        }

        private static string GetStr(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
        }

        private static int GetInt(Dictionary<string, object> d, string key)
        {
            if (!d.TryGetValue(key, out var v)) return 0;
            return v switch { int i => i, long l => (int)l, double dbl => (int)dbl, _ => 0 };
        }

        private static int GetDeckCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("deck", out var raw) && raw is List<object> deck)
                return deck.Count;
            return 0;
        }

        private static int GetRelicsCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("relics", out var raw) && raw is List<object> relics)
                return relics.Count;
            return 0;
        }
    }
}
