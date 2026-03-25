using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MP_PlayerManager.Tabs;

namespace MP_PlayerManager
{
    /// <summary>
    /// 模态卡牌选择弹窗：搜索 + 网格展示，选中后回调 onSelected(card)。
    /// </summary>
    internal static class CardBrowserPanel
    {
        /// <summary>打开弹窗，allCards 为候选卡牌列表，onSelected 回调选中结果。</summary>
        internal static void Open(List<CardModel> allCards, Action<CardModel> onSelected)
        {
            Close();
            _allCards = allCards;
            _onSelected = onSelected;
            _searchText = "";
            Build();
        }

        internal static void Close()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer))
                _layer.QueueFree();
            _layer = null;
            _allCards = null;
            _onSelected = null;
        }

        private static void Build()
        {
            _layer = new CanvasLayer
            {
                Layer = 110,
                Name = "CardBrowserPanel"
            };

            // 遮罩
            var backstop = new ColorRect
            {
                Color = new Color(0, 0, 0, 0.6f),
                AnchorRight = 1, AnchorBottom = 1,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backstop.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    Close();
                    backstop.GetViewport()?.SetInputAsHandled();
                }
            };
            _layer.AddChild(backstop, false, Node.InternalMode.Disabled);

            // 主面板
            var panel = new PanelContainer
            {
                AnchorLeft = 0.1f, AnchorRight = 0.9f,
                AnchorTop = 0.08f, AnchorBottom = 0.92f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.07f, 0.06f, 0.1f, 0.97f),
                BorderColor = new Color(0.4f, 0.35f, 0.25f, 0.7f)
            };
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(8);
            style.SetContentMarginAll(12);
            panel.AddThemeStyleboxOverride("panel", style);
            _layer.AddChild(panel, false, Node.InternalMode.Disabled);

            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            vbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vbox, false, Node.InternalMode.Disabled);

            // 标题栏
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);

            var title = new Label { Text = Loc.Get("cb.title", "Select Card"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", SC.Gold);
            header.AddChild(title, false, Node.InternalMode.Disabled);

            var closeBtn = LoadoutPanel.CreateActionButton("×", new Color(0.7f, 0.2f, 0.15f));
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += Close;
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // 搜索栏
            var searchRow = new HBoxContainer();
            searchRow.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(searchRow, false, Node.InternalMode.Disabled);

            var searchIcon = new Label { Text = "🔍" };
            searchIcon.AddThemeFontSizeOverride("font_size", 18);
            searchRow.AddChild(searchIcon, false, Node.InternalMode.Disabled);

            _searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("cb.search_hint", "Search cards..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 36)
            };
            _searchEdit.AddThemeFontSizeOverride("font_size", 16);
            _searchEdit.TextChanged += OnSearchChanged;
            searchRow.AddChild(_searchEdit, false, Node.InternalMode.Disabled);

            // 计数标签
            _countLabel = new Label();
            _countLabel.AddThemeFontSizeOverride("font_size", 13);
            _countLabel.AddThemeColorOverride("font_color", SC.Gray);
            searchRow.AddChild(_countLabel, false, Node.InternalMode.Disabled);

            // 网格区域：单 Scroll + 单 Grid，全宽铺开（避免多段 Grid 挤成左缘细条）
            _gridScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _gridScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(_gridScroll, false, Node.InternalMode.Disabled);

            _gridVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            _gridVBox.AddThemeConstantOverride("separation", 8);
            _gridScroll.AddChild(_gridVBox, false, Node.InternalMode.Disabled);

            NGame.Instance?.AddChild(_layer, false, Node.InternalMode.Disabled);
            RebuildGrid();
        }

        private static void OnSearchChanged(string text)
        {
            _searchText = text;
            RebuildGrid();
        }

        private static void RebuildGrid()
        {
            if (_gridVBox == null) return;
            LoadoutPanel.ClearChildren(_gridVBox);

            var q = string.IsNullOrWhiteSpace(_searchText)
                ? _allCards
                : _allCards?.Where(c => MatchesSearch(c, _searchText)).ToList();

            if (_countLabel != null)
                _countLabel.Text = $"{q?.Count ?? 0} / {_allCards?.Count ?? 0}";

            if (q == null || q.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("cb.no_match", "No matching cards") };
                lbl.AddThemeFontSizeOverride("font_size", 16);
                lbl.AddThemeColorOverride("font_color", SC.Gray);
                _gridVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            const int cols = 5;
            var grid = new GridContainer
            {
                Columns = cols,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            _gridVBox.AddChild(grid, false, Node.InternalMode.Disabled);

            List<CardModel> listCopy = q;
            foreach (var card in listCopy)
            {
                CardModel c = card;
                var clip = CardsTab.CreateNCardWrapperPublic(
                    c,
                    PileType.Deck,
                    listCopy,
                    () =>
                    {
                        _onSelected?.Invoke(c);
                        Close();
                    });
                clip.CustomMinimumSize = new Vector2(172, 300);
                grid.AddChild(clip, false, Node.InternalMode.Disabled);
            }
        }

        private static bool MatchesSearch(CardModel card, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            try
            {
                var title = card.Title?.ToString() ?? "";
                var desc = card.Description?.ToString() ?? "";
                var id = card.Id?.Entry ?? "";
                if (title.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
                if (id.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
                if (desc.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        private static class SC
        {
            internal static readonly Color Gold  = new Color("E3A83D");
            internal static readonly Color Cream = new Color("E3D5C1");
            internal static readonly Color Gray  = new Color("7F8C8D");
        }

        private static CanvasLayer? _layer;
        private static List<CardModel>? _allCards;
        private static Action<CardModel>? _onSelected;
        private static string _searchText = "";
        private static LineEdit? _searchEdit;
        private static Label? _countLabel;
        private static VBoxContainer? _gridVBox;
        private static ScrollContainer? _gridScroll;
    }
}
