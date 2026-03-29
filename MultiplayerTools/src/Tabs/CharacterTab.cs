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
    /// Player attribute editor tab. Direct HP / Gold / Energy editing of the local player.
    /// </summary>
    internal static class CharacterTab
    {
        private static LineEdit? _maxHpEdit;
        private static LineEdit? _curHpEdit;
        private static LineEdit? _energyEdit;
        private static LineEdit? _goldEdit;
        private static Label? _statusLabel;
        private static Player? _localPlayer;

        internal static void Build(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("char.title", "Character Editor")), false, Node.InternalMode.Disabled);

            _localPlayer = MpPanel.GetLocalPlayer();
            if (_localPlayer == null)
            {
                var hint = new Label
                {
                    Text = Loc.Get("not_in_game", "Not in a run"),
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                };
                hint.AddThemeFontSizeOverride("font_size", 20);
                hint.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(hint, false, Node.InternalMode.Disabled);
                return;
            }

            // Player identity
            var nameRow = new HBoxContainer();
            nameRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(nameRow, false, Node.InternalMode.Disabled);
            AddLabel(nameRow, Loc.Get("char.name", "Name:"), 90);
            AddValue(nameRow, GetPlayerName(_localPlayer), Panel.Styles.Gold);

            var charRow = new HBoxContainer();
            charRow.AddThemeConstantOverride("separation", 10);
            container.AddChild(charRow, false, Node.InternalMode.Disabled);
            AddLabel(charRow, Loc.Get("char.character", "Character:"), 90);
            AddValue(charRow, GetCharacterName(_localPlayer), Panel.Styles.Gold);

            // Divider
            var divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Panel.Styles.Divider };
            container.AddChild(divider, false, Node.InternalMode.Disabled);

            // Stats editor
            container.AddChild(MpPanel.CreateSectionHeader(
                Loc.Get("char.stats", "Stats")), false, Node.InternalMode.Disabled);

            var editCol = new VBoxContainer();
            editCol.AddThemeConstantOverride("separation", 10);
            container.AddChild(editCol, false, Node.InternalMode.Disabled);

            AddEditRow(editCol, Loc.Get("tmpl.max_hp", "Max HP"), _localPlayer.Creature?.MaxHp ?? 0, v => _maxHpEdit = v, out _maxHpEdit);
            AddEditRow(editCol, Loc.Get("tmpl.cur_hp", "Current HP"), _localPlayer.Creature?.CurrentHp ?? 0, v => _curHpEdit = v, out _curHpEdit);
            AddEditRow(editCol, Loc.Get("tmpl.energy", "Energy"), _localPlayer.MaxEnergy, v => _energyEdit = v, out _energyEdit);
            AddEditRow(editCol, Loc.Get("tmpl.gold", "Gold"), GetGold(_localPlayer), v => _goldEdit = v, out _goldEdit);

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
            applyBtn.Pressed += OnApplyChanges;
            btnRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

            var resetBtn = MpPanel.CreateActionButton(Loc.Get("char.reset", "Reset"), Panel.Styles.Blue);
            resetBtn.CustomMinimumSize = new Vector2(90, 36);
            resetBtn.Pressed += OnResetFields;
            btnRow.AddChild(resetBtn, false, Node.InternalMode.Disabled);
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

        private static void OnApplyChanges()
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

                // Gold via reflection
                SetGold(_localPlayer, gold);

                if (_statusLabel != null && GodotObject.IsInstanceValid(_statusLabel))
                {
                    _statusLabel.Text = Loc.Get("char.applied", "Changes applied!");
                    _statusLabel.AddThemeColorOverride("font_color", Panel.Styles.Green);
                }
                GD.Print($"[MultiplayerTools] Applied changes: HP={curHp}/{maxHp} E={energy} G={gold}");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnApplyChanges failed: " + ex.Message);
                if (_statusLabel != null && GodotObject.IsInstanceValid(_statusLabel))
                {
                    _statusLabel.Text = Loc.Get("error", "Error") + ": " + ex.Message;
                    _statusLabel.AddThemeColorOverride("font_color", Panel.Styles.Red);
                }
            }
        }

        private static void OnResetFields()
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
