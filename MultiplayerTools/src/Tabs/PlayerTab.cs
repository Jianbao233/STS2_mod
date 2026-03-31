using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Player list tab. Two modes:
    ///  - In-game: reads from RunManager / Player objects (if a run is active).
    ///  - Save-driven: reads from the currently selected save file JSON (game external).
    /// The mode is decided at Build() time based on RunManager availability.
    /// </summary>
    internal static class PlayerTab
    {
        // "Context" mode: set by SaveTab.SaveContextChanged
        private static List<Core.VPlayer>? _vPlayers;

        static PlayerTab()
        {
            // Subscribe to save selection changes so we can refresh when a save is picked
            SaveTab.SaveContextChanged += OnSaveContextChanged;
        }

        private static void OnSaveContextChanged(string? savePath)
        {
            _vPlayers = null;
        }

        internal static void Build(VBoxContainer container)
        {
            // Try in-game mode first
            var player = MpPanel.GetLocalPlayer();
            var runState = RunManager.Instance?.DebugOnlyGetState();

            if (player != null && runState != null)
            {
                BuildInGame(container, runState, player);
            }
            else
            {
                BuildSaveDriven(container);
            }
        }

        // === In-game mode (existing behavior, preserved for backward compatibility) ===

        private static void BuildInGame(VBoxContainer container, RunState runState, Player localPlayer)
        {
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("player.title", "Players in Run")), false, Node.InternalMode.Disabled);

            var players = runState.Players;
            if (players == null || players.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("player.no_players", "No players found"), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
                lbl.AddThemeFontSizeOverride("font_size", 16);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            BuildPlayerTable(container, players, localPlayer);
        }

        private static void BuildPlayerTable(VBoxContainer container, IReadOnlyList<Player> players, Player localPlayer)
        {
            // Table header
            var tableHeader = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tableHeader.AddThemeConstantOverride("separation", 4);
            container.AddChild(tableHeader, false, Node.InternalMode.Disabled);
            AddHeaderCell(tableHeader, Loc.Get("player.index", "#"), 40);
            AddHeaderCell(tableHeader, Loc.Get("player.name", "Name"), 130);
            AddHeaderCell(tableHeader, Loc.Get("player.steam_id", "Steam ID"), 160);
            AddHeaderCell(tableHeader, Loc.Get("player.hp", "HP"), 90);
            AddHeaderCell(tableHeader, Loc.Get("player.gold", "Gold"), 80);

            var listScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(listScroll, false, Node.InternalMode.Disabled);
            var listVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            listVBox.AddThemeConstantOverride("separation", 3);
            listScroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            var templates = TemplateStorage.LoadAll();

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i] as Player;
                if (p == null) continue;
                bool isLocal = p == localPlayer;

                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 4);
                row.CustomMinimumSize = new Vector2(0, 34);
                var rowColor = isLocal ? Panel.Styles.Gold : Panel.Styles.Cream;

                AddCell(row, (i + 1).ToString(), 40, rowColor);
                AddCell(row, GetPlayerDisplayName(p), 130, rowColor);
                AddCell(row, (i + 1).ToString(), 160, Panel.Styles.Gray);

                int curHp = p.Creature?.CurrentHp ?? 0;
                int maxHp = p.Creature?.MaxHp ?? 0;
                AddCell(row, $"{curHp}/{maxHp}", 90, curHp < maxHp / 3 && maxHp > 0 ? Panel.Styles.Red : rowColor);
                AddCell(row, GetPlayerGold(p).ToString(), 80, rowColor);

                listVBox.AddChild(row, false, Node.InternalMode.Disabled);

                // Apply template buttons
                if (templates.Count > 0)
                {
                    var tmplRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    tmplRow.AddThemeConstantOverride("separation", 6);
                    tmplRow.CustomMinimumSize = new Vector2(0, 30);

                    var applyBtn = MpPanel.CreateActionButton(Loc.Get("player.apply_tmpl", "Apply Template"), Panel.Styles.Green);
                    applyBtn.CustomMinimumSize = new Vector2(110, 26);
                    Player capP = p;
                    applyBtn.Pressed += async () => await TemplateApplier.ApplyToPlayerAsync(capP, templates[0]);
                    tmplRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

                    foreach (var tmpl in templates.Take(4))
                    {
                        var tmplBtn = MpPanel.CreateActionButton(tmpl.Name, null);
                        tmplBtn.CustomMinimumSize = new Vector2(90, 26);
                        TemplateData capTmpl = tmpl;
                        Player capPlayer = p;
                        tmplBtn.Pressed += async () => await TemplateApplier.ApplyToPlayerAsync(capPlayer, capTmpl);
                        tmplRow.AddChild(tmplBtn, false, Node.InternalMode.Disabled);
                    }
                    listVBox.AddChild(tmplRow, false, Node.InternalMode.Disabled);
                }
            }
        }

        // === Save-driven mode (main-menu / game-external) ===

        private static void BuildSaveDriven(VBoxContainer container)
        {
            string title = Loc.Get("player.title_from_save", "Players in Save");
            container.AddChild(MpPanel.CreateSectionHeader(title), false, Node.InternalMode.Disabled);

            // Try to load from the currently selected save (set by SaveTab)
            if (_vPlayers == null)
            {
                // Check if SaveTab has a selected save path
                string? savePath = SaveTab.SelectedSavePath;
                if (!string.IsNullOrEmpty(savePath))
                {
                    var saveData = SaveManagerHelper.ParseSaveFile(savePath);
                    _vPlayers = Core.VPlayer.FromSaveJson(saveData, savePath);
                }
            }

            if (_vPlayers == null || _vPlayers.Count == 0)
            {
                var hintLbl = new Label
                {
                    Text = Loc.Get("player.no_save_selected",
                        "No save selected — go to Save tab and click a save first"),
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                hintLbl.AddThemeFontSizeOverride("font_size", 16);
                hintLbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(hintLbl, false, Node.InternalMode.Disabled);

                // Also show a button to jump to Save tab
                var jumpBtn = MpPanel.CreateActionButton(Loc.Get("save.title", "Save Management"), Panel.Styles.Blue);
                jumpBtn.CustomMinimumSize = new Vector2(180, 36);
                jumpBtn.Pressed += () => MpPanel.SwitchToTab(MpPanel.TAB_SAVE);
                container.AddChild(jumpBtn, false, Node.InternalMode.Disabled);
                return;
            }

            // Table header
            var tableHeader = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tableHeader.AddThemeConstantOverride("separation", 4);
            container.AddChild(tableHeader, false, Node.InternalMode.Disabled);
            AddHeaderCell(tableHeader, Loc.Get("player.index", "#"), 40);
            AddHeaderCell(tableHeader, Loc.Get("player.name", "Name"), 130);
            AddHeaderCell(tableHeader, Loc.Get("player.character", "Character"), 130);
            AddHeaderCell(tableHeader, Loc.Get("player.hp", "HP"), 90);
            AddHeaderCell(tableHeader, Loc.Get("player.gold", "Gold"), 80);
            AddHeaderCell(tableHeader, Loc.Get("tmpl.deck_size", "Deck"), 60);

            var listScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(listScroll, false, Node.InternalMode.Disabled);
            var listVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            listVBox.AddThemeConstantOverride("separation", 3);
            listScroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            var templates = TemplateStorage.LoadAll();

            for (int i = 0; i < _vPlayers.Count; i++)
            {
                var vp = _vPlayers[i];
                var rowColor = i == 0 ? Panel.Styles.Gold : Panel.Styles.Cream;

                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 4);
                row.CustomMinimumSize = new Vector2(0, 34);

                AddCell(row, (i + 1).ToString(), 40, rowColor);
                AddCell(row, vp.Name, 130, rowColor);
                AddCell(row, vp.CharacterId, 130, rowColor);
                AddCell(row, $"{vp.CurrentHp}/{vp.MaxHp}", 90,
                    vp.CurrentHp < vp.MaxHp / 3 && vp.MaxHp > 0 ? Panel.Styles.Red : rowColor);
                AddCell(row, vp.Gold.ToString(), 80, rowColor);
                AddCell(row, vp.DeckCount.ToString(), 60, rowColor);

                listVBox.AddChild(row, false, Node.InternalMode.Disabled);

                // Template apply buttons (read-only in save-driven mode)
                if (templates.Count > 0)
                {
                    var tmplRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    tmplRow.AddThemeConstantOverride("separation", 6);
                    tmplRow.CustomMinimumSize = new Vector2(0, 30);

                    var applyBtn = MpPanel.CreateActionButton(Loc.Get("player.apply_tmpl", "Apply Template"), Panel.Styles.Green);
                    applyBtn.CustomMinimumSize = new Vector2(110, 26);
                    Core.VPlayer capVp = vp;
                    applyBtn.Pressed += () => ApplyTemplateToSavePlayer(capVp, templates[0]);
                    tmplRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

                    foreach (var tmpl in templates.Take(4))
                    {
                        var tmplBtn = MpPanel.CreateActionButton(tmpl.Name, null);
                        tmplBtn.CustomMinimumSize = new Vector2(90, 26);
                        TemplateData capTmpl = tmpl;
                        Core.VPlayer capVp2 = vp;
                        tmplBtn.Pressed += () => ApplyTemplateToSavePlayer(capVp2, capTmpl);
                        tmplRow.AddChild(tmplBtn, false, Node.InternalMode.Disabled);
                    }
                    listVBox.AddChild(tmplRow, false, Node.InternalMode.Disabled);
                }
            }
        }

        private static void ApplyTemplateToSavePlayer(Core.VPlayer vp, TemplateData tmpl)
        {
            // TODO (future): implement ApplyTemplateToSave(vp, tmpl)
            // For now just log — full JSON editing of the save file can be wired through PlayerOpsService
            GD.Print($"[MultiplayerTools] ApplyTemplateToSave: player={vp.Name}, template={tmpl.Name} (save-driven mode — not yet wired)");
            MpPanel.ShowStatusMessage(Loc.Get("tmpl.apply_hint", "Select a player first"), Panel.Styles.Gray);
        }

        private static string GetPlayerDisplayName(Player p)
        {
            try
            {
                var nameProp = p.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var val = nameProp.GetValue(p);
                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                }
                var charNameProp = p.GetType().GetProperty("CharacterName");
                if (charNameProp != null)
                {
                    var val = charNameProp.GetValue(p);
                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            return "Player";
        }

        private static int GetPlayerGold(Player p)
        {
            try
            {
                var goldProp = p.GetType().GetProperty("Gold");
                if (goldProp != null)
                {
                    var val = goldProp.GetValue(p);
                    if (val is int i) return i;
                }
            }
            catch { }
            return 0;
        }

        private static void AddHeaderCell(HBoxContainer row, string text, float width)
        {
            var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void AddCell(HBoxContainer row, string text, float width, Color color)
        {
            var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", color);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }
    }
}
