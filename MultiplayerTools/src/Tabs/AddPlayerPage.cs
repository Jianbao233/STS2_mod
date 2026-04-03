using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        // Guards against Build being called again during RefreshPanels
        private static bool _pageInitComplete = false;

        // ── Built-in characters (aligned with game wiki / IGN guide) ──
        // Fields: (charId, maxHp, starterRelic, starterDeck)
        // Wiki source: https://ie.ign.com/wikis/slay-the-spire-2/Characters
        //   Ironclad 80/BURNING_BLOOD  → Strike×5, Defend×4, Bash
        //   Silent   70/RING_OF_THE_SNAKE → Strike×5, Defend×5, Neutralize, Survivor
        //   Defect   75/CRACKED_CORE   → Strike×4, Defend×4, Zap, Dualcast
        //   Necrobinder 66/BOUND_PHYLACTERY → Strike×4, Defend×4, Bodyguard, Unleash
        //   Regent   75/DIVINE_RIGHT   → Strike×4, Defend×4, Falling Star, Venerate
        private static readonly (string charId, int maxHp, string starterRelic, string[] starterDeck)[] BuiltInChars = new[]
        {
            ("CHARACTER.IRONCLAD",     80, "RELIC.BURNING_BLOOD",      new[] {
                "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
                "CARD.STRIKE_IRONCLAD", "CARD.STRIKE_IRONCLAD",
                "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
                "CARD.DEFEND_IRONCLAD", "CARD.DEFEND_IRONCLAD",
                "CARD.BASH"
            }),
            ("CHARACTER.SILENT",       70, "RELIC.RING_OF_THE_SNAKE", new[] {
                "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT",
                "CARD.STRIKE_SILENT", "CARD.STRIKE_SILENT",
                "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT",
                "CARD.DEFEND_SILENT", "CARD.DEFEND_SILENT",
                "CARD.NEUTRALIZE", "CARD.SURVIVOR"
            }),
            ("CHARACTER.DEFECT",       75, "RELIC.CRACKED_CORE",       new[] {
                "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT", "CARD.STRIKE_DEFECT",
                "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT", "CARD.DEFEND_DEFECT",
                "CARD.ZAP", "CARD.DUALCAST"
            }),
            ("CHARACTER.NECROBINDER",  66, "RELIC.BOUND_PHYLACTERY",  new[] {
                "CARD.STRIKE_NECROBINDER", "CARD.STRIKE_NECROBINDER",
                "CARD.STRIKE_NECROBINDER", "CARD.STRIKE_NECROBINDER",
                "CARD.DEFEND_NECROBINDER", "CARD.DEFEND_NECROBINDER",
                "CARD.DEFEND_NECROBINDER", "CARD.DEFEND_NECROBINDER",
                "CARD.BODYGUARD", "CARD.UNLEASH"
            }),
            ("CHARACTER.REGENT",       75, "RELIC.DIVINE_RIGHT",       new[] {
                "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT",
                "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT",
                "CARD.FALLING_STAR", "CARD.VENERATE"
            }),
        };

        internal static void Build(VBoxContainer container)
        {
            // Initialise static state only on first build (not on RefreshPanels calls)
            if (!_pageInitComplete)
            {
                _isCopyMode = true;
                _selectedSourceIdx = 0;
                _selectedCharId = BuiltInChars[0].charId;
                _pageInitComplete = true;
            }

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("add.title", "Add Player")), false, Node.InternalMode.Disabled);

            // Clear radio lists on each build
            _copyRadios.Clear();
            _freshRadios.Clear();

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
            radioRow.AddChild(copyRadio, false, Node.InternalMode.Disabled);

            var freshRadio = new CheckBox
            {
                Text = Loc.Get("add.fresh_mode", "New from template"),
                ButtonPressed = !_isCopyMode
            };
            radioRow.AddChild(freshRadio, false, Node.InternalMode.Disabled);

            copyRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _isCopyMode = true;
                freshRadio.SetPressedNoSignal(false);
                RefreshPanels();
            };
            freshRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _isCopyMode = false;
                copyRadio.SetPressedNoSignal(false);
                RefreshPanels();
            };
        }

        // Static references for panel switching
        private static PanelContainer? _copyPanel;
        private static PanelContainer? _freshPanel;
        private static readonly List<CheckBox> _copyRadios = new();
        private static readonly List<CheckBox> _freshRadios = new();

        private static void BuildCopyPanel(VBoxContainer container)
        {
            _copyPanel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Visible = _isCopyMode
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
                string netId = Steam.SteamIntegration.NormalizeSteamIdForApi(GetStr(pl, "net_id"));
                string charId = GetStr(pl, "character_id");

                var radio = new CheckBox
                {
                    Text = $"[{i + 1}] {GetPlayerDisplayName(netId, charId)}  HP={hp}  Gold={gold}",
                    ButtonPressed = (_selectedSourceIdx == i)
                };
                int idx = i;
                var capturedRadio = radio;
                radio.Toggled += pressed =>
                {
                    if (pressed)
                    {
                        _selectedSourceIdx = idx;
                        foreach (var other in _copyRadios)
                            if (other != capturedRadio) other.SetPressedNoSignal(false);
                        RefreshPanels();
                    }
                };
                _copyRadios.Add(radio);
                inner.AddChild(radio, false, Node.InternalMode.Disabled);
            }
        }

        private static void BuildFreshPanel(VBoxContainer container)
        {
            _freshPanel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Visible = !_isCopyMode
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

            // 3-column grid via scroll + manual row wrapping
            const int COLS = 3;
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 240)
            };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            inner.AddChild(scroll, false, Node.InternalMode.Disabled);

            var grid = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            grid.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(grid, false, Node.InternalMode.Disabled);

            // Collect all characters: built-in first, then mods (sorted by mod flag)
            var allChars = GetAllCharacters();
            var charCardMap = new System.Collections.Generic.Dictionary<string, PanelContainer>();

            for (int i = 0; i < allChars.Count; i++)
            {
                if (i % COLS == 0)
                {
                    var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    row.AddThemeConstantOverride("separation", 6);
                    grid.AddChild(row, false, Node.InternalMode.Disabled);
                }

                var entry = allChars[i];
                var rowHost = grid.GetChild(grid.GetChildCount() - 1) as HBoxContainer;
                if (rowHost == null) continue;

                bool isSelected = _selectedCharId == entry.CharId;
                var card = new PanelContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 72)
                };
                var cStyle = new StyleBoxFlat
                {
                    BgColor = isSelected ? Panel.Styles.MpNavSelected : Panel.Styles.MpCard,
                    BorderColor = Panel.Styles.PanelBorder,
                    ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
                };
                cStyle.SetBorderWidthAll(1);
                cStyle.SetCornerRadiusAll(6);
                card.AddThemeStyleboxOverride("panel", cStyle);

                // Register card for direct style lookup (avoids lambda-capturing grid.GetChildren)
                charCardMap[entry.CharId] = card;

                var margin = new MarginContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                };
                margin.AddThemeConstantOverride("margin_left", 8);
                margin.AddThemeConstantOverride("margin_right", 8);
                margin.AddThemeConstantOverride("margin_top", 6);
                margin.AddThemeConstantOverride("margin_bottom", 6);
                card.AddChild(margin, false, Node.InternalMode.Disabled);

                var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                content.AddThemeConstantOverride("separation", 2);
                margin.AddChild(content, false, Node.InternalMode.Disabled);

                var nameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameRow.AddThemeConstantOverride("separation", 4);
                content.AddChild(nameRow, false, Node.InternalMode.Disabled);

                var radio = new CheckBox
                {
                    CustomMinimumSize = new Vector2(20, 0),
                    ButtonPressed = isSelected
                };
                var capturedRadio = radio;
                string cid2 = entry.CharId;
                radio.Toggled += pressed =>
                {
                    if (pressed)
                    {
                        _selectedCharId = cid2;
                        foreach (var other in _freshRadios)
                            if (other != capturedRadio) other.SetPressedNoSignal(false);
                        foreach (var pair in charCardMap)
                            RefreshCharCardStyle(pair.Value, pair.Key == cid2);
                        RefreshPanels();
                    }
                };
                _freshRadios.Add(radio);
                nameRow.AddChild(radio, false, Node.InternalMode.Disabled);

                // Click anywhere on card → forward to the radio's Toggled event.
                // All selection logic lives in radio.Toggled; we just trigger it here.
                card.GuiInput += ev =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    {
                        capturedRadio.EmitSignal(CheckBox.SignalName.Toggled, true);
                    }
                };

                var nameLbl = new Label { Text = entry.DisplayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameLbl.AddThemeFontSizeOverride("font_size", 17);
                nameLbl.AddThemeColorOverride("font_color", isSelected ? Panel.Styles.MpGold : Panel.Styles.MpTextNav);
                nameRow.AddChild(nameLbl, false, Node.InternalMode.Disabled);

                if (entry.IsMod)
                {
                    var modBadge = new Label { Text = "[MOD]" };
                    modBadge.AddThemeFontSizeOverride("font_size", 13);
                    modBadge.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                    nameRow.AddChild(modBadge, false, Node.InternalMode.Disabled);
                }

                var statsLbl = new Label { Text = $"HP {entry.MaxHp}" };
                statsLbl.AddThemeFontSizeOverride("font_size", 15);
                statsLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
                content.AddChild(statsLbl, false, Node.InternalMode.Disabled);

                var relicLbl = new Label { Text = entry.StarterRelic.Replace("RELIC.", "") };
                relicLbl.AddThemeFontSizeOverride("font_size", 14);
                relicLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                content.AddChild(relicLbl, false, Node.InternalMode.Disabled);

                rowHost.AddChild(card, false, Node.InternalMode.Disabled);
            }
        }

        private static void RefreshCharCardStyle(PanelContainer card, bool selected)
        {
            if (card.GetThemeStylebox("panel") is StyleBoxFlat s)
                s.BgColor = selected ? Panel.Styles.MpNavSelected : Panel.Styles.MpCard;
        }

        /// <summary>Single character entry combining built-in and mod data.</summary>
        private class CharEntry
        {
            internal string CharId = "";
            internal int MaxHp;
            internal string StarterRelic = "";
            internal string DisplayName = "";
            internal bool IsMod;
            internal string[] StarterDeck = Array.Empty<string>();
        }

        /// <summary>Get all characters: built-in first, then mods sorted alphabetically.</summary>
        private static List<CharEntry> GetAllCharacters()
        {
            var result = new List<CharEntry>();

            // Built-in
            foreach (var (charId, maxHp, relic, deck) in BuiltInChars)
            {
                result.Add(new CharEntry
                {
                    CharId = charId,
                    MaxHp = maxHp,
                    StarterRelic = relic,
                    DisplayName = CharacterDisplayNames.Resolve(charId),
                    IsMod = false,
                    StarterDeck = deck
                });
            }

            // Mod characters
            foreach (var mod in GetModCharacters())
            {
                result.Add(new CharEntry
                {
                    CharId = mod.CharId,
                    MaxHp = mod.MaxHp,
                    StarterRelic = mod.StarterRelic,
                    DisplayName = mod.DisplayName,
                    IsMod = true,
                    StarterDeck = mod.StarterDeck
                });
            }

            return result;
        }

        /// <summary>Scan mods folder for player_template.json files.</summary>
        private static List<CharEntry> GetModCharacters()
        {
            var mods = new List<CharEntry>();
            try
            {
                string? modsRoot = TryGetModsDirectory();
                if (string.IsNullOrEmpty(modsRoot) || !Directory.Exists(modsRoot)) return mods;

                foreach (var dir in Directory.GetDirectories(modsRoot))
                {
                    var tplPath = Path.Combine(dir, "player_template.json");
                    if (!File.Exists(tplPath)) continue;

                    try
                    {
                        string json = File.ReadAllText(tplPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string charId = "", name = "", relic = "";
                        int maxHp = 75;
                        var deck = new List<string>();

                        if (root.TryGetProperty("character_id", out var cidEl) && cidEl.ValueKind == JsonValueKind.String)
                            charId = cidEl.GetString() ?? "";
                        if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                            name = nameEl.GetString() ?? "";
                        if (root.TryGetProperty("max_hp", out var hpEl) && hpEl.ValueKind == JsonValueKind.Number)
                            maxHp = hpEl.GetInt32();
                        if (root.TryGetProperty("starter_relic", out var relEl) && relEl.ValueKind == JsonValueKind.String)
                            relic = relEl.GetString() ?? "";
                        if (root.TryGetProperty("starter_deck", out var deckEl) && deckEl.ValueKind == JsonValueKind.Array)
                            foreach (var card in deckEl.EnumerateArray())
                                if (card.ValueKind == JsonValueKind.String)
                                    deck.Add(card.GetString() ?? "");

                        if (string.IsNullOrEmpty(charId)) continue;

                        // Skip if already in built-in list
                        if (Array.Exists(BuiltInChars, b => b.charId == charId)) continue;

                        mods.Add(new CharEntry
                        {
                            CharId = charId,
                            MaxHp = maxHp,
                            StarterRelic = relic,
                            DisplayName = name,
                            IsMod = true,
                            StarterDeck = deck.ToArray()
                        });

                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[MultiplayerTools] GetModCharacters: failed to parse {tplPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetModCharacters failed: " + ex.Message);
            }
            return mods;
        }

        private static string? TryGetModsDirectory()
        {
            try
            {
                string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
                if (string.IsNullOrEmpty(exeDir)) return null;
                string mods = Path.Combine(exeDir, "mods");
                return Directory.Exists(mods) ? mods : null;
            }
            catch { return null; }
        }

        private static void RefreshPanels()
        {
            if (_copyPanel == null || !GodotObject.IsInstanceValid(_copyPanel)) return;
            if (_freshPanel == null || !GodotObject.IsInstanceValid(_freshPanel)) return;
            _copyPanel.Visible = _isCopyMode;
            _freshPanel.Visible = !_isCopyMode;
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
                if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) &&
                    Steam.SteamIntegration.NormalizeSteamIdForApi(nid?.ToString()) == Steam.SteamIntegration.NormalizeSteamIdForApi(steamId))
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
                var allChars = GetAllCharacters();
                var charData = allChars.Find(c => c.CharId == _selectedCharId);
                if (charData == null || string.IsNullOrEmpty(charData.CharId))
                {
                    ShowMsg(Loc.Get("add.no_char", "Please select a character."), Panel.Styles.MpGold);
                    return;
                }
                result = PlayerOpsService.AddPlayerFresh(
                    steamId, _selectedCharId, charData.MaxHp,
                    charData.StarterDeck.ToList(), charData.StarterRelic, savePath, 100
                );
            }

            if (result.Success)
            {
                if (!MpSessionState.ReloadSave())
                {
                    ShowMsg(Loc.Get("error.load_save_failed", "Failed to load save"), Panel.Styles.Red);
                    return;
                }
                ShowMsg(Loc.Get("add.success", "Player added successfully!"), Panel.Styles.Green);
                MpPanel.RefreshCurrentPage();
            }
            else
            {
                ShowMsg(result.Message, Panel.Styles.Red);
            }
        }

        private static void ShowFriendPicker(LineEdit targetEdit)
        {
            Steam.SteamIntegration.ClearFriendsListCache();
            var me = Steam.SteamIntegration.GetCurrentSteamId() ?? "";
            var room = BuildRoomMemberOptions(me);
            var rawFriends = Steam.SteamIntegration.GetLocalFriends();
            var friends = rawFriends
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
                string netId = Steam.SteamIntegration.NormalizeSteamIdForApi(GetStr(pl, "net_id"));
                if (string.IsNullOrEmpty(netId)) continue;
                if (!string.IsNullOrEmpty(localSteamId) && netId == localSteamId) continue;

                string charId = GetStr(pl, "character_id");
                string display = GetPlayerDisplayName(netId, charId);
                string sub = $"[{i + 1}] {Loc.Get("friend.player", "Player")} · {CharacterDisplayNames.Resolve(charId)}  ·  {MpSessionState.ShortenSteamId(netId)}";
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
