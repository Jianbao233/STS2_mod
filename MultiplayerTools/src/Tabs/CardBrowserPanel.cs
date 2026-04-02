using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Simple card browser popup. Shows a scrollable grid of all cards.
    /// </summary>
    internal static class CardBrowserPanel
    {
        /// <summary>Open a card browser. Calls onSelected with the chosen CardModel or null.</summary>
        internal static void Open(List<CardModel> allCards, Action<CardModel?> onSelected)
        {
            var overlay = new CanvasLayer { Layer = 200 };
            (Engine.GetMainLoop() as SceneTree)?.Root?.AddChild(overlay, false, Node.InternalMode.Disabled);

            var backdrop = new ColorRect
            {
                Color = new Color(0, 0, 0, 0.7f),
                AnchorRight = 1, AnchorBottom = 1,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backdrop.GuiInput += _ => { };
            overlay.AddChild(backdrop, false, Node.InternalMode.Disabled);

            var panel = new PanelContainer
            {
                AnchorLeft = 0.1f, AnchorRight = 0.9f,
                AnchorTop = 0.08f, AnchorBottom = 0.92f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both
            };
            panel.AddThemeStyleboxOverride("panel", Panel.Styles.CreateFlat(Panel.Styles.PanelBg, Panel.Styles.PanelBorder));
            overlay.AddChild(panel, false, Node.InternalMode.Disabled);

            var vbox = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1, CustomMinimumSize = new Vector2I(0, 12) };
            vbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vbox, false, Node.InternalMode.Disabled);

            // Header
            var header = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            header.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);
            var titleLbl = new Label { Text = Loc.Get("cb.title", "Select Card"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            titleLbl.AddThemeFontSizeOverride("font_size", 20);
            titleLbl.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            header.AddChild(titleLbl, false, Node.InternalMode.Disabled);
            var closeBtn = MpPanel.CreateActionButton("X", Panel.Styles.Red);
            closeBtn.CustomMinimumSize = new Vector2(36, 32);
            closeBtn.Pressed += () => { overlay.QueueFree(); onSelected(null); };
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // Search
            var searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("cb.search_hint", "Search cards..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 36)
            };
            searchEdit.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(searchEdit, false, Node.InternalMode.Disabled);

            // Card grid
            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(scroll, false, Node.InternalMode.Disabled);
            var grid = new GridContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            grid.Columns = 8;
            scroll.AddChild(grid, false, Node.InternalMode.Disabled);

            Action rebuildGrid = () =>
            {
                grid.GetParent()?.RemoveChild(grid);
                scroll.AddChild(grid, false, Node.InternalMode.Disabled);
                MpPanel.ClearChildren(grid);
                var q = string.IsNullOrWhiteSpace(searchEdit.Text)
                    ? allCards
                    : allCards.Where(c => (c.Id?.Entry ?? "").Contains(searchEdit.Text, StringComparison.OrdinalIgnoreCase)).ToList();
                if (q.Count == 0)
                {
                    var lbl = new Label { Text = Loc.Get("cb.no_match", "No matching cards") };
                    lbl.AddThemeFontSizeOverride("font_size", 16);
                    lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                    grid.AddChild(lbl, false, Node.InternalMode.Disabled);
                    return;
                }
                foreach (var card in q.Take(200))
                {
                    var cardPanel = new PanelContainer
                    {
                        CustomMinimumSize = new Vector2(0, 90),
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };
                    cardPanel.AddThemeStyleboxOverride("panel", Panel.Styles.CreateFlat(new Color(0.10f, 0.08f, 0.14f, 0.9f), Panel.Styles.PanelBorder));
                    var lbl2 = new Label { Text = card.Id?.Entry ?? "??" };
                    lbl2.AddThemeFontSizeOverride("font_size", 9);
                    lbl2.AddThemeColorOverride("font_color", Panel.Styles.Cream);
                    lbl2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    cardPanel.AddChild(lbl2, false, Node.InternalMode.Disabled);
                    CardModel captured = card;
                    cardPanel.GuiInput += ev =>
                    {
                        if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        { overlay.QueueFree(); onSelected(captured); }
                    };
                    grid.AddChild(cardPanel, false, Node.InternalMode.Disabled);
                }
            };

            searchEdit.TextChanged += _ => rebuildGrid();
            rebuildGrid();
        }
    }
}
