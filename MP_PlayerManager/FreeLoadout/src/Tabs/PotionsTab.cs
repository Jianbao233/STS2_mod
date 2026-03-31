using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 药水管理 Tab：
    /// 显示/添加/移除当前玩家的药水。
    /// </summary>
    internal static class PotionsTab
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
                var hint = new Label { Text = Loc.Get("not_in_game", "Not in a run") };
                hint.AddThemeFontSizeOverride("font_size", 20);
                hint.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(hint, false, Node.InternalMode.Disabled);
                return;
            }

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("potions.title", "Potions")), false, Node.InternalMode.Disabled);

            // 操作按钮行
            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var addBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("potions.add", "+ Add Potion"), SC.Blue);
            addBtn.CustomMinimumSize = new Vector2(120, 32);
            addBtn.Pressed += () => OpenPotionBrowser(container, player);
            actionRow.AddChild(addBtn, false, Node.InternalMode.Disabled);

            var clearBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("potions.clear_all", "Clear All"), SC.Red);
            clearBtn.CustomMinimumSize = new Vector2(90, 32);
            Player captured = player;
            clearBtn.Pressed += () =>
            {
                ClearAllPotions(captured);
            };
            actionRow.AddChild(clearBtn, false, Node.InternalMode.Disabled);

            // 当前药水列表（占位，显示 WIP）
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("potions.current", "Current Potions")), false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            var hintLbl = new Label { Text = Loc.Get("potions.wip", "Potion management - coming soon") };
            hintLbl.AddThemeFontSizeOverride("font_size", 14);
            hintLbl.AddThemeColorOverride("font_color", SC.Gray);
            listVBox.AddChild(hintLbl, false, Node.InternalMode.Disabled);
        }

        private static void ClearAllPotions(Player player)
        {
            try
            {
                // PotionCmd API 验证中
                GD.Print("[MP_PlayerManager] ClearAllPotions — Potion API not yet verified.");
                LoadoutPanel.RequestRefresh();
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] ClearAllPotions failed: " + ex.Message);
            }
        }

        private static void OpenPotionBrowser(VBoxContainer container, Player player)
        {
            var allPotions = ModelDb.AllPotions?.OrderBy(p => p.Id?.Entry ?? "").ToList();
            if (allPotions == null || allPotions.Count == 0)
            {
                GD.Print("[MP_PlayerManager] No potions found in ModelDb.");
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

            // 标题
            var header = new HBoxContainer();
            var title = new Label { Text = Loc.Get("potions.select", "Select Potion"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", SC.Blue);
            header.AddChild(title, false, Node.InternalMode.Disabled);
            var closeBtn = LoadoutPanel.CreateActionButton("×", SC.Red);
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += () => layer.QueueFree();
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);

            // 搜索
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
                    ? allPotions
                    : allPotions.Where(p => (p.Id?.Entry ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var potion in filtered)
                {
                    string pid = potion.Id?.Entry ?? "";
                    var btn = LoadoutPanel.CreateItemButton(pid, new Vector2(0, 26));
                    btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    PotionModel capPotion = potion;
                    btn.Pressed += () =>
                    {
                        // PotionCmd.Obtain 验证中
                        GD.Print($"[MP_PlayerManager] Potion '{pid}' selected — PotionCmd API not yet implemented.");
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