using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Character editor tab. Two modes:
    ///  - In-game: edits the local Player object's stats via reflection.
    ///  - Save-driven: reads/writes the currently selected save file's player JSON.
    /// </summary>
    internal static class CharacterTab
    {
        // In-game mode fields
        private static LineEdit? _maxHpEdit;
        private static LineEdit? _curHpEdit;
        private static LineEdit? _energyEdit;
        private static LineEdit? _goldEdit;
        private static Label? _statusLabel;
        private static Player? _localPlayer;

        // Save-driven mode fields
        private static string? _currentSavePath;
        private static Dictionary<string, object>? _currentSaveData;
        private static int _selectedPlayerIdx;
        private static VBoxContainer? _saveDetailVBox;

        static CharacterTab()
        {
            SaveTab.SaveContextChanged += OnSaveContextChanged;
        }

        private static void OnSaveContextChanged(string? savePath)
        {
            _currentSavePath = savePath;
            _currentSaveData = null;
            _selectedPlayerIdx = 0;
        }

        internal static void Build(VBoxContainer container)
        {
            _localPlayer = MpPanel.GetLocalPlayer();
            _statusLabel = null;

            if (_localPlayer != null)
            {
                BuildInGame(container);
            }
            else
            {
                BuildSaveDriven(container);
            }
        }

        // === In-game mode (existing behavior) ===

        private static void BuildInGame(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("char.title", "Character Editor")), false, Node.InternalMode.Disabled);

            // Player identity
            var nameRow = new HBoxContainer();
            nameRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(nameRow, false, Node.InternalMode.Disabled);
            AddLabel(nameRow, Loc.Get("char.name", "Name:"), 90);
            AddValue(nameRow, GetPlayerName(_localPlayer!), Panel.Styles.Gold);

            var charRow = new HBoxContainer();
            charRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(charRow, false, Node.InternalMode.Disabled);
            AddLabel(charRow, Loc.Get("char.character", "Character:"), 90);
            AddValue(charRow, GetCharacterName(_localPlayer!), Panel.Styles.Gold);

            // Divider
            var divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Panel.Styles.Divider };
            container.AddChild(divider, false, Node.InternalMode.Disabled);

            // Stats editor
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("char.stats", "Stats")), false, Node.InternalMode.Disabled);

            var editCol = new VBoxContainer();
            editCol.AddThemeConstantOverride("separation", 10);
            container.AddChild(editCol, false, Node.InternalMode.Disabled);

            AddEditRow(editCol, Loc.Get("tmpl.max_hp", "Max HP"), _localPlayer!.Creature?.MaxHp ?? 0, v => _maxHpEdit = v, out _maxHpEdit);
            AddEditRow(editCol, Loc.Get("tmpl.cur_hp", "Current HP"), _localPlayer!.Creature?.CurrentHp ?? 0, v => _curHpEdit = v, out _curHpEdit);
            AddEditRow(editCol, Loc.Get("tmpl.energy", "Energy"), _localPlayer!.MaxEnergy, v => _energyEdit = v, out _energyEdit);
            AddEditRow(editCol, Loc.Get("tmpl.gold", "Gold"), GetGold(_localPlayer!), v => _goldEdit = v, out _goldEdit);

            // Status label
            _statusLabel = new Label { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            container.AddChild(_statusLabel, false, Node.InternalMode.Disabled);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var applyBtn = MpPanel.CreateActionButton(Loc.Get("char.apply", "Apply Changes"), Panel.Styles.Green);
            applyBtn.CustomMinimumSize = new Vector2(140, 36);
            applyBtn.Pressed += OnApplyChangesInGame;
            btnRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

            var resetBtn = MpPanel.CreateActionButton(Loc.Get("char.reset", "Reset"), Panel.Styles.Blue);
            resetBtn.CustomMinimumSize = new Vector2(90, 36);
            resetBtn.Pressed += OnResetFieldsInGame;
            btnRow.AddChild(resetBtn, false, Node.InternalMode.Disabled);
        }

        private static void OnApplyChangesInGame()
        {
            if (_localPlayer == null) return;
            try
            {
                int maxHp = ParseInt(_maxHpEdit?.Text ?? "", _localPlayer.Creature?.MaxHp ?? 0);
                int curHp = ParseInt(_curHpEdit?.Text ?? "", maxHp);
                int energy = ParseInt(_energyEdit?.Text ?? "", _localPlayer.MaxEnergy);
                int gold = ParseInt(_goldEdit?.Text ?? "", 0);

                var creature = _localPlayer.Creature;
                if (creature != null)
                {
                    creature.SetMaxHpInternal(maxHp);
                    creature.SetCurrentHpInternal(Math.Min(curHp, maxHp));
                }

                _localPlayer.MaxEnergy = Math.Max(1, energy);
                SetGold(_localPlayer, gold);

                SetStatusLabel(Loc.Get("char.applied", "Changes applied!"), Panel.Styles.Green);
                GD.Print($"[MultiplayerTools] Applied changes: HP={curHp}/{maxHp} E={energy} G={gold}");
            }
            catch (Exception ex)
            {
                SetStatusLabel(Loc.Get("error", "Error") + ": " + ex.Message, Panel.Styles.Red);
                GD.PrintErr("[MultiplayerTools] OnApplyChanges failed: " + ex.Message);
            }
        }

        private static void OnResetFieldsInGame()
        {
            if (_localPlayer == null) return;
            if (_maxHpEdit != null && GodotObject.IsInstanceValid(_maxHpEdit))
                _maxHpEdit.Text = (_localPlayer.Creature?.MaxHp ?? 0).ToString();
            if (_curHpEdit != null && GodotObject.IsInstanceValid(_curHpEdit))
                _curHpEdit.Text = (_localPlayer.Creature?.CurrentHp ?? 0).ToString();
            if (_energyEdit != null && GodotObject.IsInstanceValid(_energyEdit))
                _energyEdit.Text = _localPlayer.MaxEnergy.ToString();
            if (_goldEdit != null && GodotObject.IsInstanceValid(_goldEdit))
                _goldEdit.Text = GetGold(_localPlayer).ToString();
        }

        // === Save-driven mode (main-menu / game-external) ===

        private static void BuildSaveDriven(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("char.title_from_save", "Save Editor")), false, Node.InternalMode.Disabled);

            // Ensure we have loaded the save
            if (_currentSavePath == null || _currentSaveData == null)
            {
                if (_currentSavePath == null)
                {
                    // Nothing selected yet — show prompt
                    ShowSavePrompt(container);
                    return;
                }
                // Load it now
                _currentSaveData = SaveManagerHelper.ParseSaveFile(_currentSavePath);
            }

            if (_currentSaveData == null)
            {
                ShowSavePrompt(container);
                return;
            }

            // Player selector + stats editor
            BuildSaveEditor(container);
        }

        private static void ShowSavePrompt(VBoxContainer container)
        {
            var hint = new Label
            {
                Text = Loc.Get("char.no_save_selected",
                    "No save selected — go to Save tab and click a save first"),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            hint.AddThemeFontSizeOverride("font_size", 16);
            hint.AddThemeColorOverride("font_color", Panel.Styles.Gray);
            container.AddChild(hint, false, Node.InternalMode.Disabled);

            var jumpBtn = MpPanel.CreateActionButton(Loc.Get("save.title", "Save Management"), Panel.Styles.Blue);
            jumpBtn.CustomMinimumSize = new Vector2(180, 36);
            jumpBtn.Pressed += () => MpPanel.SwitchToTab(MpPanel.TAB_SAVE);
            container.AddChild(jumpBtn, false, Node.InternalMode.Disabled);

            _statusLabel = new Label { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            container.AddChild(_statusLabel, false, Node.InternalMode.Disabled);
        }

        private static void BuildSaveEditor(VBoxContainer container)
        {
            if (_currentSaveData == null || _currentSavePath == null) return;

            // Find players
            var players = Core.VPlayer.FromSaveJson(_currentSaveData, _currentSavePath);
            if (players.Count == 0)
            {
                var noPlayers = new Label
                {
                    Text = Loc.Get("player.no_players", "No players in this save"),
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                };
                noPlayers.AddThemeFontSizeOverride("font_size", 16);
                noPlayers.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(noPlayers, false, Node.InternalMode.Disabled);
                return;
            }

            // Clamp selected index
            if (_selectedPlayerIdx >= players.Count) _selectedPlayerIdx = 0;
            var vp = players[_selectedPlayerIdx];

            // Player selector row
            var selectorRow = new HBoxContainer();
            selectorRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(selectorRow, false, Node.InternalMode.Disabled);

            var selectorLabel = new Label { Text = Loc.Get("player.name", "Name") + ":", CustomMinimumSize = new Vector2(60, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            selectorLabel.AddThemeFontSizeOverride("font_size", 13);
            selectorLabel.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            selectorRow.AddChild(selectorLabel, false, Node.InternalMode.Disabled);

            for (int i = 0; i < players.Count; i++)
            {
                var btn = MpPanel.CreateActionButton(
                    $"P{i + 1}: {players[i].Name}",
                    i == _selectedPlayerIdx ? Panel.Styles.Gold : null);
                btn.CustomMinimumSize = new Vector2(100, 30);
                int idx = i;
                btn.Pressed += () => { _selectedPlayerIdx = idx; _statusLabel = null; MpPanel.RefreshCurrentTab(); };
                selectorRow.AddChild(btn, false, Node.InternalMode.Disabled);
            }

            // Divider
            var divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Panel.Styles.Divider };
            container.AddChild(divider, false, Node.InternalMode.Disabled);

            // Stats
            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("char.stats", "Stats")), false, Node.InternalMode.Disabled);

            _saveDetailVBox = new VBoxContainer();
            _saveDetailVBox.AddThemeConstantOverride("separation", 10);
            container.AddChild(_saveDetailVBox, false, Node.InternalMode.Disabled);

            AddSaveEditRow(_saveDetailVBox, Loc.Get("tmpl.max_hp", "Max HP"), vp.MaxHp, "maxHp");
            AddSaveEditRow(_saveDetailVBox, Loc.Get("tmpl.cur_hp", "Current HP"), vp.CurrentHp, "curHp");
            AddSaveEditRow(_saveDetailVBox, Loc.Get("tmpl.gold", "Gold"), vp.Gold, "gold");

            // Status label
            _statusLabel = new Label { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            container.AddChild(_statusLabel, false, Node.InternalMode.Disabled);

            // Buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var applyBtn = MpPanel.CreateActionButton(Loc.Get("char.apply", "Apply Changes"), Panel.Styles.Green);
            applyBtn.CustomMinimumSize = new Vector2(140, 36);
            applyBtn.Pressed += OnApplyChangesSaveDriven;
            btnRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

            var resetBtn = MpPanel.CreateActionButton(Loc.Get("char.reset", "Reset"), Panel.Styles.Blue);
            resetBtn.CustomMinimumSize = new Vector2(90, 36);
            resetBtn.Pressed += () => { _statusLabel = null; MpPanel.RefreshCurrentTab(); };
            btnRow.AddChild(resetBtn, false, Node.InternalMode.Disabled);
        }

        private static Dictionary<string, LineEdit> _saveFieldEdits = new();

        private static void AddSaveEditRow(VBoxContainer parent, string label, int currentValue, string fieldKey)
        {
            _saveFieldEdits[fieldKey] = null!;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
            var edit = new LineEdit { Text = currentValue.ToString(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 36) };
            edit.AddThemeFontSizeOverride("font_size", 16);
            edit.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(edit, false, Node.InternalMode.Disabled);
            _saveFieldEdits[fieldKey] = edit;
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }

        private static void OnApplyChangesSaveDriven()
        {
            if (_currentSaveData == null || _currentSavePath == null) return;
            try
            {
                int maxHp = ParseInt(_saveFieldEdits.TryGetValue("maxHp", out var e1) ? e1?.Text ?? "" : "", 0);
                int curHp = ParseInt(_saveFieldEdits.TryGetValue("curHp", out var e2) ? e2?.Text ?? "" : "", 0);
                int gold = ParseInt(_saveFieldEdits.TryGetValue("gold", out var e3) ? e3?.Text ?? "" : "", 0);

                // Clamp current HP to max HP
                if (maxHp > 0 && curHp > maxHp) curHp = maxHp;

                // Edit the JSON: find players array, update the selected player
                if (_currentSaveData.TryGetValue("players", out var raw) && raw is List<object> players)
                {
                    if (_selectedPlayerIdx < players.Count && players[_selectedPlayerIdx] is Dictionary<string, object> pd)
                    {
                        // Try both snake_case and camelCase keys
                        TrySetInt(pd, "max_hp", maxHp);
                        TrySetInt(pd, "maxHp", maxHp);
                        TrySetInt(pd, "current_hp", curHp);
                        TrySetInt(pd, "currentHp", curHp);
                        TrySetInt(pd, "gold", gold);
                    }
                }

                // Write back
                bool ok = SaveManagerHelper.WriteSaveFile(_currentSavePath, _currentSaveData);
                if (ok)
                {
                    // Reload
                    _currentSaveData = SaveManagerHelper.ParseSaveFile(_currentSavePath);
                    SetStatusLabel(Loc.Get("char.applied", "Changes applied!"), Panel.Styles.Green);
                    GD.Print($"[MultiplayerTools] Save-driven apply: HP={curHp}/{maxHp} G={gold} → {_currentSavePath}");
                }
                else
                {
                    SetStatusLabel(Loc.Get("error", "Error saving file"), Panel.Styles.Red);
                }
            }
            catch (Exception ex)
            {
                SetStatusLabel(Loc.Get("error", "Error") + ": " + ex.Message, Panel.Styles.Red);
                GD.PrintErr("[MultiplayerTools] OnApplyChangesSaveDriven failed: " + ex.Message);
            }
        }

        private static void TrySetInt(Dictionary<string, object> d, string key, int value)
        {
            if (d.ContainsKey(key)) d[key] = value;
        }

        // === Shared helpers ===

        private static void SetStatusLabel(string text, Color color)
        {
            if (_statusLabel != null && GodotObject.IsInstanceValid(_statusLabel))
            {
                _statusLabel.Text = text;
                _statusLabel.AddThemeColorOverride("font_color", color);
            }
        }

        private static void AddLabel(HBoxContainer row, string text, float width)
        {
            var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void AddValue(HBoxContainer row, string text, Color color)
        {
            var lbl = new Label { Text = text };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", color);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void AddEditRow(VBoxContainer parent, string label, int currentValue, Action<LineEdit> setter, out LineEdit? outEdit)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
            var edit = new LineEdit { Text = currentValue.ToString(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 36) };
            edit.AddThemeFontSizeOverride("font_size", 16);
            edit.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(edit, false, Node.InternalMode.Disabled);
            setter(edit);
            outEdit = edit;
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, out var v) ? v : fallback;

        private static string GetPlayerName(Player p)
        {
            try
            {
                var prop = p.GetType().GetProperty("Name");
                if (prop != null && prop.GetValue(p) is string s && !string.IsNullOrEmpty(s)) return s;
                var charName = p.GetType().GetProperty("CharacterName");
                if (charName != null && charName.GetValue(p) is string cs && !string.IsNullOrEmpty(cs)) return cs;
            }
            catch { }
            return "Player";
        }

        private static string GetCharacterName(Player p)
        {
            try
            {
                var charName = p.GetType().GetProperty("CharacterName");
                if (charName != null && charName.GetValue(p) is string s && !string.IsNullOrEmpty(s)) return s;
            }
            catch { }
            return "Unknown";
        }

        private static int GetGold(Player p)
        {
            try
            {
                var prop = p.GetType().GetProperty("Gold");
                if (prop != null && prop.GetValue(p) is int g) return g;
            }
            catch { }
            return 0;
        }

        private static void SetGold(Player p, int gold)
        {
            try
            {
                var cmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.Cmd.PlayerCmd");
                if (cmdType != null)
                {
                    var method = AccessTools.Method(cmdType, "SetGold", new[] { typeof(int), typeof(Player) });
                    method?.Invoke(null, new object[] { gold, p });
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] SetGold failed: " + ex.Message);
            }
        }

        private static class AccessTools
        {
            private static readonly Assembly? Sts2Assembly = AppDomain.CurrentDomain
                .GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            internal static Type? TypeByName(string name) =>
                Sts2Assembly?.GetType(name);

            internal static MethodInfo? Method(Type type, string name, Type[] args)
            {
                if (type == null) return null;
                return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, args, null);
            }
        }
    }
}
