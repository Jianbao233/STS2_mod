using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 能力（Power）管理 Tab：
    /// 显示/添加/移除玩家身上的 Power，支持预设批量应用。
    /// </summary>
    internal static class PowersTab
    {
        private static class SC
        {
            internal static readonly Color Gold   = new Color("E3A83D");
            internal static readonly Color Cream  = new Color("E3D5C1");
            internal static readonly Color Gray   = new Color("7F8C8D");
            internal static readonly Color Purple = new Color("CC77FF");
            internal static readonly Color Green   = new Color("27AE60");
            internal static readonly Color Red    = new Color("C0392B");
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
                Loc.Get("powers.title", "Powers")), false, Node.InternalMode.Disabled);

            // 操作按钮行
            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var addBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("powers.add", "+ Add Power"), SC.Purple);
            addBtn.CustomMinimumSize = new Vector2(120, 32);
            addBtn.Pressed += () => OpenPowerBrowser(container, player);
            actionRow.AddChild(addBtn, false, Node.InternalMode.Disabled);

            var removeAllBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("powers.remove_all", "Remove All"), SC.Red);
            removeAllBtn.CustomMinimumSize = new Vector2(110, 32);
            Player captured = player;
            removeAllBtn.Pressed += () => RemoveAllPlayerPowers(captured);
            actionRow.AddChild(removeAllBtn, false, Node.InternalMode.Disabled);

            // 预设区
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("powers.presets_section", "Presets")), false, Node.InternalMode.Disabled);

            BuildPresetButtons(container, player);

            // 当前 Power 列表
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("powers.current", "Current Powers")), false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            BuildPowerList(listVBox, player);
        }

        private static void BuildPowerList(VBoxContainer container, Player player)
        {
            try
            {
                var powers = player.ActivePowers;
                if (powers == null || powers.Count == 0)
                {
                    var empty = new Label { Text = Loc.Get("empty", "Empty") };
                    empty.AddThemeFontSizeOverride("font_size", 14);
                    empty.AddThemeColorOverride("font_color", SC.Gray);
                    container.AddChild(empty, false, Node.InternalMode.Disabled);
                    return;
                }

                foreach (var power in powers)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);
                    container.AddChild(row, false, Node.InternalMode.Disabled);

                    string powerId = power?.Id?.Entry ?? "unknown";
                    int amount = 0;
                    try { amount = power.Amount; } catch { }

                    var lbl = new Label
                    {
                        Text = $"  {powerId} x{amount}",
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };
                    lbl.AddThemeFontSizeOverride("font_size", 13);
                    lbl.AddThemeColorOverride("font_color", SC.Purple);
                    row.AddChild(lbl, false, Node.InternalMode.Disabled);

                    var removeBtn = LoadoutPanel.CreateActionButton("×", SC.Red);
                    removeBtn.CustomMinimumSize = new Vector2(28, 28);
                    PowerModel capturedPower = power;
                    removeBtn.Pressed += () =>
                    {
                        try { PowerCmd.Remove(capturedPower, player.Creature); LoadoutPanel.RequestRefresh(); }
                        catch { }
                    };
                    row.AddChild(removeBtn, false, Node.InternalMode.Disabled);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] BuildPowerList failed: " + ex.Message);
            }
        }

        private static void BuildPresetButtons(VBoxContainer container, Player player)
        {
            var presetsRow = new HBoxContainer();
            presetsRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(presetsRow, false, Node.InternalMode.Disabled);

            var presets = new[]
            {
                ("Strength+6", (Action)(() => ApplyPowerPreset(player, "Strength", 6))),
                ("Dex+6", (Action)(() => ApplyPowerPreset(player, "Dexterity", 6))),
                ("Thorns6", (Action)(() => ApplyPowerPreset(player, "Thorns", 6))),
                ("Regen10", (Action)(() => ApplyPowerPreset(player, "Regen", 10))),
            };

            foreach (var (label, action) in presets)
            {
                var btn = LoadoutPanel.CreateActionButton(label, SC.Purple);
                btn.CustomMinimumSize = new Vector2(80, 30);
                btn.Pressed += action;
                presetsRow.AddChild(btn, false, Node.InternalMode.Disabled);
            }
        }

        private static void ApplyPowerPreset(Player player, string powerId, int amount)
        {
            try
            {
                var powerModel = ModelDb.AllPowers?.FirstOrDefault(p =>
                    string.Equals(p.Id?.Entry, powerId, StringComparison.OrdinalIgnoreCase));
                if (powerModel != null && player.Creature != null)
                {
                    PowerCmd.Apply(powerModel, player.Creature, amount);
                    LoadoutPanel.RequestRefresh();
                    GD.Print($"[MP_PlayerManager] Applied {powerId} x{amount}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ApplyPowerPreset '{powerId}' failed: {ex.Message}");
            }
        }

        private static void RemoveAllPlayerPowers(Player player)
        {
            try
            {
                var powers = player.ActivePowers?.ToList();
                if (powers == null) return;
                foreach (var p in powers)
                {
                    try { PowerCmd.Remove(p, player.Creature); } catch { }
                }
                LoadoutPanel.RequestRefresh();
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] RemoveAllPowers failed: " + ex.Message);
            }
        }

        private static void OpenPowerBrowser(VBoxContainer container, Player player)
        {
            var allPowers = ModelDb.AllPowers?.OrderBy(p => p.Id?.Entry ?? "").ToList();
            if (allPowers == null || allPowers.Count == 0)
            {
                GD.Print("[MP_PlayerManager] No powers found in ModelDb.");
                return;
            }

            var layer = new CanvasLayer { Layer = 110 };
            var backstop = new ColorRect { Color = new Color(0, 0, 0, 0.6f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Stop };
            backstop.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    layer.QueueFree();
                    backstop.GetViewport()?.SetInputAsHandled();
                }
            };
            layer.AddChild(backstop, false, Node.InternalMode.Disabled);

            var panel = new PanelContainer
            {
                AnchorLeft = 0.1f, AnchorRight = 0.9f,
                AnchorTop = 0.08f, AnchorBottom = 0.92f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            var style = new StyleBoxFlat { BgColor = new Color(0.07f, 0.06f, 0.1f, 0.97f), BorderColor = new Color(0.4f, 0.35f, 0.25f, 0.7f) };
            style.SetBorderWidthAll(2); style.SetCornerRadiusAll(8); style.SetContentMarginAll(12);
            panel.AddThemeStyleboxOverride("panel", style);
            layer.AddChild(panel, false, Node.InternalMode.Disabled);

            var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            vbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vbox, false, Node.InternalMode.Disabled);

            // 标题行
            var header = new HBoxContainer();
            var title = new Label { Text = Loc.Get("powers.select", "Select Power"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", SC.Purple);
            header.AddChild(title, false, Node.InternalMode.Disabled);
            var closeBtn = LoadoutPanel.CreateActionButton("×", SC.Red);
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += () => layer.QueueFree();
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);

            // 数量输入
            var amountRow = new HBoxContainer();
            amountRow.AddThemeConstantOverride("separation", 8);
            var amountLbl = new Label { Text = "Amount:" };
            amountLbl.AddThemeFontSizeOverride("font_size", 13);
            amountLbl.AddThemeColorOverride("font_color", SC.Cream);
            amountLbl.CustomMinimumSize = new Vector2(60, 0);
            amountRow.AddChild(amountLbl, false, Node.InternalMode.Disabled);

            var amountEdit = new LineEdit { Text = "1", CustomMinimumSize = new Vector2(60, 32) };
            amountEdit.AddThemeFontSizeOverride("font_size", 14);
            amountRow.AddChild(amountEdit, false, Node.InternalMode.Disabled);
            vbox.AddChild(amountRow, false, Node.InternalMode.Disabled);

            // 搜索栏
            var searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("cb.search_hint", "Search..."),
                CustomMinimumSize = new Vector2(0, 32)
            };
            searchEdit.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(searchEdit, false, Node.InternalMode.Disabled);

            // 列表
            var listScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(listScroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 4);
            listScroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            Player capturedPlayer = player;
            Action rebuild = () =>
            {
                LoadoutPanel.ClearChildren(listVBox);
                string search = searchEdit.Text;
                var filtered = string.IsNullOrWhiteSpace(search)
                    ? allPowers
                    : allPowers.Where(p => (p.Id?.Entry ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var power in filtered)
                {
                    string pid = power.Id?.Entry ?? "";
                    var btn = LoadoutPanel.CreateItemButton(pid, new Vector2(0, 26));
                    btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    PowerModel capPower = power;
                    btn.Pressed += () =>
                    {
                        int amount = int.TryParse(amountEdit.Text, out var a) ? a : 1;
                        try
                        {
                            if (capturedPlayer.Creature != null)
                                PowerCmd.Apply(capPower, capturedPlayer.Creature, amount);
                            LoadoutPanel.RequestRefresh();
                        }
                        catch (Exception ex) { GD.PrintErr("[MP_PlayerManager] Apply power failed: " + ex.Message); }
                        layer.QueueFree();
                    };
                    listVBox.AddChild(btn, false, Node.InternalMode.Disabled);
                }
            };

            searchEdit.TextChanged += _ => rebuild();
            rebuild();

            NGame.Instance?.AddChild(layer, false, Node.InternalMode.Disabled);
        }
    }
}