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
    /// 模态卡牌选择弹窗：搜索 + 网格展示。
    /// 支持两种模式：
    /// - 普通模式：点击卡牌后回调 onSelected(card) 并关闭弹窗
    /// - 批量模式：点击卡牌切换选中状态，全部选完后按"完成"才回调 onBatchSelected
    /// </summary>
    internal static class CardBrowserPanel
    {
        /// <summary>普通模式：选中一张卡后立即回调并关闭。</summary>
        internal static void Open(List<CardModel> allCards, Action<CardModel> onSelected)
        {
            Close();
            _allCards = allCards;
            _onSelected = onSelected;
            _batchMode = false;
            _searchText = "";
            Build();
        }

        /// <summary>批量模式：可选择多张卡，点击"完成"后一次性回调所有选中卡。</summary>
        internal static void OpenForBatch(List<CardModel> allCards, Action<List<CardModel>> onBatchSelected)
        {
            Close();
            _allCards = allCards;
            _onBatchSelected = onBatchSelected;
            _batchMode = true;
            _selectedCards = new HashSet<string>();
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
            _onBatchSelected = null;
            _batchMode = false;
            _selectedCards = null;
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

            // ── 标题栏 ─────────────────────────────────────────────────────
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(header, false, Node.InternalMode.Disabled);

            _titleLabel = new Label
            {
                Text = _batchMode
                    ? Loc.Get("cb.title_batch", "Batch Add Cards")
                    : Loc.Get("cb.title", "Select Card"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _titleLabel.AddThemeFontSizeOverride("font_size", 20);
            _titleLabel.AddThemeColorOverride("font_color", SC.Gold);
            header.AddChild(_titleLabel, false, Node.InternalMode.Disabled);

            // 批量模式：显示已选计数
            if (_batchMode)
            {
                _selectedCountLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                _selectedCountLabel.AddThemeFontSizeOverride("font_size", 14);
                _selectedCountLabel.AddThemeColorOverride("font_color", SC.Blue);
                RefreshSelectedCountLabel();
                header.AddChild(_selectedCountLabel, false, Node.InternalMode.Disabled);
            }

            var closeBtn = LoadoutPanel.CreateActionButton("×", new Color(0.7f, 0.2f, 0.15f));
            closeBtn.CustomMinimumSize = new Vector2(36, 36);
            closeBtn.Pressed += Close;
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // 批量模式：完成按钮
            if (_batchMode)
            {
                _doneBtn = LoadoutPanel.CreateActionButton(
                    Loc.Get("cb.confirm_batch", "Done (0)"), new Color(0.3f, 0.6f, 0.3f));
                _doneBtn.CustomMinimumSize = new Vector2(100, 36);
                _doneBtn.Pressed += OnBatchConfirm;
                header.AddChild(_doneBtn, false, Node.InternalMode.Disabled);
            }

            // ── 搜索栏 ───────────────────────────────────────────────────
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

            _countLabel = new Label();
            _countLabel.AddThemeFontSizeOverride("font_size", 13);
            _countLabel.AddThemeColorOverride("font_color", SC.Gray);
            searchRow.AddChild(_countLabel, false, Node.InternalMode.Disabled);

            // ── 网格区域 ───────────────────────────────────────────────────
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
                string cardId = c.Id?.Entry ?? Guid.NewGuid().ToString();
                bool isSelected = _batchMode && _selectedCards?.Contains(cardId) == true;

                Action onClick = _batchMode
                    ? () => OnCardToggle(c, cardId)
                    : (Action)(() =>
                    {
                        _onSelected?.Invoke(c);
                        Close();
                    });

                var clip = CardsTab.CreateNCardWrapperPublic(c, PileType.Deck, listCopy, onClick);
                clip.CustomMinimumSize = new Vector2(172, 300);

                // 批量模式下：被选中的卡牌加边框高亮
                if (isSelected)
                    CardsTab.ApplyCardSelectedHighlight(clip);

                grid.AddChild(clip, false, Node.InternalMode.Disabled);
            }
        }

        private static void OnCardToggle(CardModel card, string cardId)
        {
            if (_selectedCards == null) return;
            if (_selectedCards.Contains(cardId))
                _selectedCards.Remove(cardId);
            else
                _selectedCards.Add(cardId);

            RefreshSelectedCountLabel();
            RebuildGrid(); // 刷新边框高亮
        }

        private static void RefreshSelectedCountLabel()
        {
            int count = _selectedCards?.Count ?? 0;
            if (_selectedCountLabel != null && GodotObject.IsInstanceValid(_selectedCountLabel))
                _selectedCountLabel.Text = string.Format(Loc.Get("cb.selected_count", "Selected {0}"), count);
            if (_doneBtn != null && GodotObject.IsInstanceValid(_doneBtn))
                _doneBtn.Text = string.Format(Loc.Get("cb.confirm_batch", "Done ({0})"), count);
        }

        private static void OnBatchConfirm()
        {
            if (_selectedCards == null || _selectedCards.Count == 0)
            {
                Close();
                return;
            }
            var ids = _selectedCards;
            var selected = _allCards?.Where(c => ids.Contains(c.Id?.Entry ?? "")).ToList()
                ?? new List<CardModel>();
            Close();
            _onBatchSelected?.Invoke(selected);
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
            internal static readonly Color Blue  = new Color("2980B9");
        }

        private static CanvasLayer? _layer;
        private static List<CardModel>? _allCards;
        private static Action<CardModel>? _onSelected;
        private static Action<List<CardModel>>? _onBatchSelected;
        private static bool _batchMode;
        private static HashSet<string>? _selectedCards;
        private static string _searchText = "";
        private static LineEdit? _searchEdit;
        private static Label? _countLabel;
        private static Label? _titleLabel;
        private static Label? _selectedCountLabel;
        private static Button? _doneBtn;
        private static VBoxContainer? _gridVBox;
        private static ScrollContainer? _gridScroll;
    }
}
