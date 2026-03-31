using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 角色属性编辑 Tab：
    /// 当前玩家属性面板：HP/Gold/Energy/Stars，快捷操作（满血/+100金币/+3能量等）。
    /// </summary>
    internal static class CharacterTab
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
                var hint = new Label
                {
                    Text = Loc.Get("not_in_game", "Not in a run"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                hint.AddThemeFontSizeOverride("font_size", 20);
                hint.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(hint, false, Node.InternalMode.Disabled);
                return;
            }

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("char.title", "Character Status")), false, Node.InternalMode.Disabled);

            string charName = "?";
            try { charName = player.NetId ?? "Unknown"; } catch { }
            var charLbl = new Label { Text = $"  {charName}" };
            charLbl.AddThemeFontSizeOverride("font_size", 16);
            charLbl.AddThemeColorOverride("font_color", SC.Gold);
            container.AddChild(charLbl, false, Node.InternalMode.Disabled);

            int currentHp = 0, maxHp = 0;
            try { currentHp = player.Creature?.CurrentHp ?? 0; } catch { }
            try { maxHp = player.Creature?.MaxHp ?? 0; } catch { }

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("char.hp_section", "Health")), false, Node.InternalMode.Disabled);

            var hpRow = new HBoxContainer();
            hpRow.AddThemeConstantOverride("separation", 12);
            container.AddChild(hpRow, false, Node.InternalMode.Disabled);

            var hpLabel = new Label { Text = Loc.Get("tmpl.cur_hp", "HP") + ":" };
            hpLabel.CustomMinimumSize = new Vector2(80, 0);
            hpLabel.AddThemeFontSizeOverride("font_size", 14);
            hpLabel.AddThemeColorOverride("font_color", SC.Cream);
            hpLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            hpRow.AddChild(hpLabel, false, Node.InternalMode.Disabled);

            var hpEdit = new LineEdit { Text = currentHp.ToString(), CustomMinimumSize = new Vector2(60, 32) };
            hpEdit.AddThemeFontSizeOverride("font_size", 14);
            hpEdit.AddThemeColorOverride("font_color", SC.Gold);
            hpRow.AddChild(hpEdit, false, Node.InternalMode.Disabled);
            hpRow.AddChild(new Label { Text = " / " }, false, Node.InternalMode.Disabled);

            var maxHpEdit = new LineEdit { Text = maxHp.ToString(), CustomMinimumSize = new Vector2(60, 32) };
            maxHpEdit.AddThemeFontSizeOverride("font_size", 14);
            maxHpEdit.AddThemeColorOverride("font_color", SC.Gold);
            hpRow.AddChild(maxHpEdit, false, Node.InternalMode.Disabled);

            Player capturedPlayer = player;
            var fullHpBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("char.full_hp", "Full HP"), SC.Green);
            fullHpBtn.CustomMinimumSize = new Vector2(80, 32);
            fullHpBtn.Pressed += () =>
            {
                try
                {
                    int mhp = int.TryParse(maxHpEdit.Text, out var v) ? v : maxHp;
                    mhp = Math.Max(1, mhp);
                    capturedPlayer.Creature?.SetMaxHpInternal(mhp);
                    capturedPlayer.Creature?.SetCurrentHpInternal(mhp);
                    hpEdit.Text = mhp.ToString();
                    maxHpEdit.Text = mhp.ToString();
                }
                catch (Exception ex) { GD.PrintErr("[MP_PlayerManager] FullHP failed: " + ex.Message); }
            };
            hpRow.AddChild(fullHpBtn, false, Node.InternalMode.Disabled);

            var hpSlider = new HSlider
            {
                MinValue = 0,
                MaxValue = Math.Max(1, maxHp),
                Value = currentHp,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(200, 20)
            };
            hpSlider.Step = 1;
            hpSlider.ValueChanged += v =>
            {
                hpEdit.Text = ((int)v).ToString();
                try { capturedPlayer.Creature?.SetCurrentHpInternal((int)v); } catch { }
            };
            var sliderRow = new HBoxContainer();
            sliderRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(sliderRow, false, Node.InternalMode.Disabled);
            sliderRow.AddChild(new Label { Text = "0" }, false, Node.InternalMode.Disabled);
            sliderRow.AddChild(hpSlider, false, Node.InternalMode.Disabled);
            sliderRow.AddChild(new Label { Text = maxHp.ToString() }, false, Node.InternalMode.Disabled);

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("char.resources_section", "Resources")), false, Node.InternalMode.Disabled);

            int gold = 0;
            try { gold = player.Gold; } catch { }
            AddInfoRow(container, Loc.Get("tmpl.gold", "Gold") + ":", gold.ToString(),
                v => { try { capturedPlayer.Gold = Math.Max(0, int.TryParse(v, out var g) ? g : 0); } catch { } },
                out var goldEdit);

            int energy = 0;
            try { energy = player.PlayerCombatState?.Energy ?? 0; } catch { }
            AddInfoRow(container, Loc.Get("tmpl.energy", "Energy") + ":", energy.ToString(),
                v => {
                    try
                    {
                        int e = Math.Max(0, int.TryParse(v, out var en) ? en : 0);
                        capturedPlayer.MaxEnergy = e;
                        if (capturedPlayer.PlayerCombatState != null)
                            capturedPlayer.PlayerCombatState.Energy = e;
                    }
                    catch { }
                },
                out var energyEdit);

            int stars = 0;
            try { stars = player.PlayerCombatState?.Stars ?? 0; } catch { }
            AddInfoRow(container, Loc.Get("char.stars", "Stars") + ":", stars.ToString(),
                v => {
                    try
                    {
                        if (capturedPlayer.PlayerCombatState != null)
                            capturedPlayer.PlayerCombatState.Stars = Math.Max(0, int.TryParse(v, out var s) ? s : 0);
                    }
                    catch { }
                },
                out _);

            container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) }, false, Node.InternalMode.Disabled);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 12);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            if (TemplatesTab.GetSelectedTemplate() is { } t)
            {
                var applyTemplateBtn = LoadoutPanel.CreateActionButton(
                    Loc.Get("tmpl.apply", "Apply Template"), new Color(0.2f, 0.5f, 0.2f));
                applyTemplateBtn.CustomMinimumSize = new Vector2(140, 36);
                applyTemplateBtn.Pressed += async () =>
                    await TemplateApplier.ApplyToPlayerAsync(capturedPlayer, t);
                actionRow.AddChild(applyTemplateBtn, false, Node.InternalMode.Disabled);
            }

            var healFullBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("char.heal_full", "Heal Full"), SC.Green);
            healFullBtn.CustomMinimumSize = new Vector2(90, 36);
            healFullBtn.Pressed += () =>
            {
                try
                {
                    var creature = capturedPlayer.Creature;
                    if (creature != null) { creature.SetCurrentHpInternal(creature.MaxHp); hpEdit.Text = creature.MaxHp.ToString(); }
                }
                catch { }
            };
            actionRow.AddChild(healFullBtn, false, Node.InternalMode.Disabled);

            var addGoldBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("char.add_gold", "+100 Gold"), SC.Gold);
            addGoldBtn.CustomMinimumSize = new Vector2(100, 36);
            addGoldBtn.Pressed += () => { try { capturedPlayer.Gold += 100; goldEdit.Text = capturedPlayer.Gold.ToString(); } catch { } };
            actionRow.AddChild(addGoldBtn, false, Node.InternalMode.Disabled);

            var addEnergyBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("char.add_energy", "+3 Energy"), SC.Blue);
            addEnergyBtn.CustomMinimumSize = new Vector2(110, 36);
            addEnergyBtn.Pressed += () =>
            {
                try
                {
                    if (capturedPlayer.PlayerCombatState != null)
                    {
                        int newEnergy = capturedPlayer.PlayerCombatState.Energy + 3;
                        capturedPlayer.MaxEnergy = Math.Max(capturedPlayer.MaxEnergy, newEnergy);
                        capturedPlayer.PlayerCombatState.Energy = newEnergy;
                        energyEdit.Text = newEnergy.ToString();
                    }
                }
                catch { }
            };
            actionRow.AddChild(addEnergyBtn, false, Node.InternalMode.Disabled);

            var refreshBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("char.refresh", "Refresh"), SC.Gray);
            refreshBtn.CustomMinimumSize = new Vector2(90, 32);
            refreshBtn.Pressed += () => { LoadoutPanel.ClearChildren(container); Build(container, capturedPlayer); };
            container.AddChild(refreshBtn, false, Node.InternalMode.Disabled);
        }

        private static void AddInfoRow(VBoxContainer parent, string label, string value, Action<string> onChanged, out LineEdit? outEdit)
        {
            outEdit = null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var lbl = new Label { Text = label };
            lbl.CustomMinimumSize = new Vector2(100, 0);
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", SC.Cream);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);

            var edit = new LineEdit
            {
                Text = value,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(80, 32)
            };
            edit.AddThemeFontSizeOverride("font_size", 14);
            edit.AddThemeColorOverride("font_color", SC.Gold);
            Action<string> cb = onChanged;
            edit.TextChanged += t => { cb(t); };
            row.AddChild(edit, false, Node.InternalMode.Disabled);
            outEdit = edit;
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }
    }
}