using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MultiplayerTools.Core;
using MultiplayerTools.UI;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Add player page — mirrors v2 _page_add_player + _refresh_add_page + _build_copy_panel + _build_fresh_panel + _do_add_player.
    ///
    /// Layout:
    ///   [title]
    ///   → warning if no save selected
    ///   → Radio: Copy Mode | Fresh Mode
    ///   → Copy panel: source player radio list
    ///   → Fresh panel: character grid (simplified, no mods scan)
    ///   → separator
    ///   → new player: Steam ID input + Friends button
    ///   → confirm button
    /// </summary>
    internal static class AddPlayerPage
    {
        // Static state (persists across refreshes within the same page visit)
        private static bool _isCopyMode = true;
        private static int _selectedSourceIdx = 0;
        private static string? _selectedCharId;

        // ── Hardcoded characters (no mods scan — can be extended with TemplateStorage) ──
        private static readonly (string charId, string name, int maxHp, string starterRelic)[] BuiltInChars = new[]
        {
            ("CHARACTER.IRONCLAD",     "Ironclad",     80, "RELIC.STARTER_IRONCLAD"),
            ("CHARACTER.SILENT",       "Silent",        70, "RELIC.STARTER_SILENT"),
            ("CHARACTER.DEFECT",       "Defect",        60, "RELIC.STARTER_DEFECT"),
            ("CHARACTER.WATCHER",      "Watcher",       72, "RELIC.STARTER_WATCHER"),
            ("CHARACTER.NEMESIS",      "Nemesis",       78, "RELIC.NEMESIS_STARTER"),
        };

        private static readonly string[] BuiltInDecks = new[]
        {
            // Ironclad starter
            "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE",
            "CARD.DEFEND", "CARD.DEFEND", "CARD.DEFEND", "CARD.DEFEND",
            "CARD.BASH", "CARD.SEARING_BLOW__1_",
            // Silent starter
            // (same names reused — in real game these are different card IDs)
        };

        internal static void Build(VBoxContainer container)
        {
            _isCopyMode = true;
            _selectedSourceIdx = 0;
            _selectedCharId = BuiltInChars[0].charId;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("add.title", "Add Player")), false, Node.InternalMode.Disabled);

            if (MpSessionState.CurrentSavePath == null || MpSessionState.SaveData.Count == 0)
            {
                AddWarning(container, Loc.Get("add.select_save", "No save selected. Go to Save Select first."));
                return;
            }

            // Mode radio buttons
            AddModeRadios(container);

            // Copy mode panel
            BuildCopyPanel(container);

            // Fresh mode panel (hidden initially)
            BuildFreshPanel(container);

            // Separator
            AddSeparator(container);

            // New player info
            var newTitle = new Label { Text = Loc.Get("add.new_player", "New Player") };
            newTitle.AddThemeFontSizeOverride("font_size", 19);
            newTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            container.AddChild(newTitle, false, Node.InternalMode.Disabled);

            // Steam ID row
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

            // Confirm button
            var confirmBtn = MpPanel.CreateActionButton(Loc.Get("add.confirm", "Add Player"), Panel.Styles.Green);
            confirmBtn.CustomMinimumSize = new Vector2(140, 38);
            confirmBtn.AddThemeFontSizeOverride("font_size", 18);
            confirmBtn.Pressed += () => DoAddPlayer(idEdit.Text);
            container.AddChild(confirmBtn, false, Node.InternalMode.Disabled);
        }

        private static void AddModeRadios(VBoxContainer container)
        {
            var radioRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            radioRow.AddThemeConstantOverride("separation", 16);
            container.AddChild(radioRow, false, Node.InternalMode.Disabled);

            var copyRadio = new CheckBox
            {
                Text = Loc.Get("add.copy_mode", "Copy existing player"),
                ButtonPressed = _isCopyMode
            };
            copyRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _isCopyMode = true;
                RefreshPanels();
            };
            radioRow.AddChild(copyRadio, false, Node.InternalMode.Disabled);

            var freshRadio = new CheckBox
            {
                Text = Loc.Get("add.fresh_mode", "New from template"),
                ButtonPressed = !_isCopyMode
            };
            freshRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _isCopyMode = false;
                RefreshPanels();
            };
            radioRow.AddChild(freshRadio, false, Node.InternalMode.Disabled);
        }

        // Static references for panel switching
        private static PanelContainer? _copyPanel;
        private static PanelContainer? _freshPanel;

        private static void BuildCopyPanel(VBoxContainer container)
        {
            _copyPanel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var copyStyle = new StyleBoxFlat
            {
                BgColor = Godot.Colors.Transparent,
                BorderColor = Godot.Colors.Transparent,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            copyStyle.SetBorderWidthAll(0);
            copyStyle.SetCornerRadiusAll(0);
            _copyPanel.AddThemeStyleboxOverride("panel", copyStyle);
            container.AddChild(_copyPanel, false, Node.InternalMode.Disabled);

            var inner = new VBoxContainer();
            inner.AddThemeConstantOverride("separation", 4);
            _copyPanel.AddChild(inner, false, Node.InternalMode.Disabled);

            var lbl = new Label { Text = Loc.Get("add.source_player", "Source Player") };
            lbl.AddThemeFontSizeOverride("font_size", 17);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            inner.AddChild(lbl, false, Node.InternalMode.Disabled);

            var players = MpSessionState.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var pl = players[i] as Dictionary<string, object>;
                if (pl == null) continue;
                int hp = GetInt(pl, "current_hp");
                int gold = GetInt(pl, "gold");
                string netId = GetStr(pl, "net_id");
                string charId = GetStr(pl, "character_id");

                var radio = new CheckBox
                {
                    Text = $"[{i + 1}] {GetPlayerDisplayName(netId, charId)}  HP={hp}  Gold={gold}",
                    ButtonPressed = (_selectedSourceIdx == i)
                };
                int idx = i;
                radio.Toggled += pressed =>
                {
                    if (!pressed) return;
                    _selectedSourceIdx = idx;
                    RefreshPanels();
                };
                inner.AddChild(radio, false, Node.InternalMode.Disabled);
            }
        }

        private static void BuildFreshPanel(VBoxContainer container)
        {
            _freshPanel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var freshStyle = new StyleBoxFlat
            {
                BgColor = Godot.Colors.Transparent,
                BorderColor = Godot.Colors.Transparent,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            freshStyle.SetBorderWidthAll(0);
            freshStyle.SetCornerRadiusAll(0);
            _freshPanel.AddThemeStyleboxOverride("panel", freshStyle);
            container.AddChild(_freshPanel, false, Node.InternalMode.Disabled);

            var inner = new VBoxContainer();
            inner.AddThemeConstantOverride("separation", 6);
            _freshPanel.AddChild(inner, false, Node.InternalMode.Disabled);

            var lbl = new Label { Text = Loc.Get("add.select_char", "Select Character") };
            lbl.AddThemeFontSizeOverride("font_size", 17);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            inner.AddChild(lbl, false, Node.InternalMode.Disabled);

            // Character grid (simplified — just radio buttons stacked)
            foreach (var (charId, _, maxHp, relic) in BuiltInChars)
            {
                string displayName = CharacterDisplayNames.Resolve(charId);
                var radio = new CheckBox
                {
                    Text = $"{displayName}  (HP {maxHp})",
                    ButtonPressed = (_selectedCharId == charId)
                };
                string cid = charId;
                radio.Toggled += pressed =>
                {
                    if (!pressed) return;
                    _selectedCharId = cid;
                    RefreshPanels();
                };
                inner.AddChild(radio, false, Node.InternalMode.Disabled);
            }
        }

        private static void RefreshPanels()
        {
            // This is a simplified approach — on refresh we rebuild the page
            // For Godot C# without complex state management, full refresh is acceptable
            MpPanel.RefreshCurrentPage();
        }

        private static void DoAddPlayer(string steamIdRaw)
        {
            string steamId = steamIdRaw.Trim();
            if (string.IsNullOrEmpty(steamId))
            {
                ShowMsg(Loc.Get("add.choose_steam", "Please enter a Steam ID."), Panel.Styles.MpGold);
                return;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, @"^\d{15,}$"))
            {
                ShowMsg(Loc.Get("add.id_format", "Steam ID must be 15+ digits."), Panel.Styles.Red);
                return;
            }

            // Check conflict
            foreach (var p in MpSessionState.GetPlayers())
                if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) && nid?.ToString() == steamId)
                {
                    ShowMsg(Loc.Get("add.id_conflict", "Steam ID already in save."), Panel.Styles.Red);
                    return;
                }

            string savePath = MpSessionState.CurrentSavePath!;
            PlayerOpsService.OperationResult result;

            if (_isCopyMode)
            {
                result = PlayerOpsService.AddPlayerCopy(_selectedSourceIdx, steamId, savePath);
            }
            else
            {
                // Fresh mode
                if (string.IsNullOrEmpty(_selectedCharId))
                {
                    ShowMsg(Loc.Get("add.no_char", "Please select a character."), Panel.Styles.MpGold);
                    return;
                }
                var charData = Array.Find(BuiltInChars, c => c.charId == _selectedCharId);
                var starterDeck = GetStarterDeck(_selectedCharId);
                result = PlayerOpsService.AddPlayerFresh(
                    steamId, _selectedCharId, charData.maxHp,
                    starterDeck, charData.starterRelic, savePath, 100
                );
            }

            if (result.Success)
            {
                MpSessionState.ReloadSave();
                ShowMsg(Loc.Get("add.success", "Player added successfully!"), Panel.Styles.Green);
                MpPanel.SwitchPage(MpPanel.PAGE_TAKEOVER);
            }
            else
            {
                ShowMsg(result.Message, Panel.Styles.Red);
            }
        }

        private static List<string> GetStarterDeck(string charId)
        {
            // Hardcoded starter decks (simplified — just enough for new players to work)
            return charId switch
            {
                "CHARACTER.IRONCLAD" => new List<string> {
                    "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE", "CARD.STRIKE",
                    "CARD.DEFEND", "CARD.DEFEND", "CARD.DEFEND", "CARD.DEFEND",
                    "CARD.BASH"
                },
                "CHARACTER.SILENT" => new List<string> {
                    "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT",
                    "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT",
                    "CARD.SLASHER_SILENT"
                },
                "CHARACTER.DEFECT" => new List<string> {
                    "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT",
                    "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT",
                    "CARD.START_ZOOM"
                },
                "CHARACTER.WATCHER" => new List<string> {
                    "CARD.STRIKE_WATCHER", "CARD.STRIKE_WATCHER", "CARD.STRIKE_WATCHER", "CARD.STRIKE_WATCHER", "CARD.STRIKE_WATCHER",
                    "CARD.DEFEND_WATCHER", "CARD.DEFEND_WATCHER", "CARD.DEFEND_WATCHER", "CARD.DEFEND_WATCHER",
                    "CARD.VIGIL"
                },
                _ => new List<string>()
            };
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

        private static int GetInt(Dictionary<string, object> d, string key)
        {
            if (!d.TryGetValue(key, out var v)) return 0;
            return v switch { int i => i, long l => (int)l, double dbl => (int)dbl, _ => 0 };
        }
    }
}
