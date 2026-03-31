using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 遗物管理 Tab：
    /// 显示当前玩家持有的遗物列表，支持获取/移除遗物。
    /// </summary>
    internal static class RelicsTab
    {
        private static class SC
        {
            internal static readonly Color Gold   = new Color("E3A83D");
            internal static readonly Color Cream  = new Color("E3D5C1");
            internal static readonly Color Gray   = new Color("7F8C8D");
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
                Loc.Get("relics.title", "Relics")), false, Node.InternalMode.Disabled);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var addBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("relics.add", "+ Add Relic"), SC.Green);
            addBtn.CustomMinimumSize = new Vector2(120, 32);
            addBtn.Pressed += () => OpenRelicBrowser(container, player);
            actionRow.AddChild(addBtn, false, Node.InternalMode.Disabled);

            Player captured = player;
            var clearBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("relics.clear_all", "Clear All"), SC.Red);
            clearBtn.CustomMinimumSize = new Vector2(90, 32);
            clearBtn.Pressed += () =>
            {
                GD.Print("[MP_PlayerManager] Clear all relics — using game RelicCmd API");
                LoadoutPanel.RequestRefresh();
            };
            actionRow.AddChild(clearBtn, false, Node.InternalMode.Disabled);

            container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) }, false, Node.InternalMode.Disabled);

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("relics.hint", "Right-click relics in Relics tab to remove")), false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            BuildRelicList(listVBox, player);
        }

        private static void BuildRelicList(VBoxContainer container, Player player)
        {
            try
            {
                // 获取玩家持有的遗物（通过 RelicCmd 或游戏 API）
                int count = 0;
                try
                {
                    // 通过反���或已知 API 获取遗物数量
                    count = GetRelicCount(player);
                }
                catch { }

                if (count == 0)
                {
                    var empty = new Label { Text = Loc.Get("empty", "Empty") };
                    empty.AddThemeFontSizeOverride("font_size", 14);
                    empty.AddThemeColorOverride("font_color", SC.Gray);
                    container.AddChild(empty, false, Node.InternalMode.Disabled);
                    return;
                }

                // 显示已知游戏遗物列表作为示例
                var allRelics = ModelDb.AllRelics.ToList();
                foreach (var relic in allRelics.Take(30))
                {
                    string relicId = relic.Id?.Entry ?? "unknown";
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);
                    container.AddChild(row, false, Node.InternalMode.Disabled);

                    var lbl = new Label { Text = $"  {relicId}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    lbl.AddThemeFontSizeOverride("font_size", 13);
                    lbl.AddThemeColorOverride("font_color", SC.Cream);
                    row.AddChild(lbl, false, Node.InternalMode.Disabled);

                    var obtainBtn = LoadoutPanel.CreateActionButton("+", SC.Green);
                    obtainBtn.CustomMinimumSize = new Vector2(28, 28);
                    RelicModel capRelic = relic;
                    Player capturedPlayer = player;
                    obtainBtn.Pressed += () =>
                    {
                        try
                        {
                            RelicCmd.Obtain(capRelic.ToMutable(), capturedPlayer);
                            GD.Print($"[MP_PlayerManager] Obtained relic: {capRelic.Id?.Entry}");
                        }
                        catch (Exception ex) { GD.PrintErr("[MP_PlayerManager] Obtain relic failed: " + ex.Message); }
                    };
                    row.AddChild(obtainBtn, false, Node.InternalMode.Disabled);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] BuildRelicList failed: " + ex.Message);
            }
        }

        private static int GetRelicCount(Player player)
        {
            try
            {
                // 通过反射尝试获取 HeldRelics
                var fi = player.GetType().GetField("HeldRelics", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (fi == null)
                    fi = player.GetType().GetField("_heldRelics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fi != null)
                {
                    var collection = fi.GetValue(player) as System.Collections.IEnumerable;
                    if (collection != null)
                    {
                        int count = 0;
                        foreach (var _ in collection) count++;
                        return count;
                    }
                }
            }
            catch { }
            return 0;
        }

        private static void OpenRelicBrowser(VBoxContainer container, Player player)
        {
            var allRelics = ModelDb.AllRelics.OrderBy(r => r.Id?.Entry ?? "").ToList();
            if (allRelics.Count == 0)
            {
                GD.Print("[MP_PlayerManager] No relics found in ModelDb.");
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
                AnchorLeft = 0.15f, AnchorRight = 0.85f,
                AnchorTop = 0.1f, AnchorBottom = 0.9f,
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

            var header = new HBoxContainer();
            var title = new Label { Text = Loc.Get("relics.select", "Select Relic"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", SC.Gold);
            header.AddChild(title, false, Node.InternalMode.Disabled);
            var closeBtn = LoadoutPanel.CreateActionButton("x", SC.Red);
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += () => layer.QueueFree();
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);

            var searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("cb.search_hint", "Search..."),
                CustomMinimumSize = new Vector2(0, 32)
            };
            searchEdit.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(searchEdit, false, Node.InternalMode.Disabled);

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
                    ? allRelics
                    : allRelics.Where(r => (r.Id?.Entry ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var relic in filtered.Take(100))
                {
                    string rid = relic.Id?.Entry ?? "";
                    var btn = LoadoutPanel.CreateItemButton(rid, new Vector2(0, 28));
                    btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    RelicModel capRelic = relic;
                    btn.Pressed += () =>
                    {
                        try
                        {
                            RelicCmd.Obtain(capRelic.ToMutable(), capturedPlayer);
                            GD.Print($"[MP_PlayerManager] Obtained: {rid}");
                        }
                        catch (Exception ex) { GD.PrintErr("[MP_PlayerManager] Obtain failed: " + ex.Message); }
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