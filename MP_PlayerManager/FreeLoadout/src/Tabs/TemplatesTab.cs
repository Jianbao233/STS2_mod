using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MP_PlayerManager.Tabs;

namespace MP_PlayerManager
{
    /// <summary>
    /// 模板标签页：管理角色模板的创建、编辑和加载。
    /// 左栏：模板列表（搜索 / 新建 / 复制 / 删除）。
    /// 右栏：模板编辑器（角色 / 基础信息 / 卡牌列表 / 添加卡牌）。
    /// </summary>
    internal static class TemplatesTab
    {
        /// <summary>左栏最小宽度（px），避免 ScrollContainer / ShrinkBegin 把列表压成细条。</summary>
        private const float LeftColumnMinWidth = 320f;

        /// <summary>防止连点或输入系统重复触发导致一次新建多条模板。</summary>
        private static ulong _lastNewTemplateClickTicks;

        private static bool _suppressNameChanged;
        private static Label? _cardDeckSectionLabel;

        // ── Build ──────────────────────────────────────────────────────────────

        internal static void Build(VBoxContainer container, Player? player)
        {
            if (_templates == null) LoadTemplates();

            var hbox = new HBoxContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            hbox.AddThemeConstantOverride("separation", 12);
            container.AddChild(hbox, false, Node.InternalMode.Disabled);

            // 左栏：不用外层 ScrollContainer（会导致最小宽度塌成细条）；固定最小宽度 + 与右侧按比例分栏
            var leftColumn = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(LeftColumnMinWidth, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.38f
            };
            leftColumn.AddThemeConstantOverride("separation", 8);
            hbox.AddChild(leftColumn, false, Node.InternalMode.Disabled);

            var listHeader = new Label
            {
                Text = Loc.Get("tmpl.title", "Character Templates"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            listHeader.AddThemeFontSizeOverride("font_size", 16);
            listHeader.AddThemeColorOverride("font_color", SC.Gold);
            leftColumn.AddChild(listHeader, false, Node.InternalMode.Disabled);

            // 搜索栏
            var searchRow = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            searchRow.AddThemeConstantOverride("separation", 4);
            leftColumn.AddChild(searchRow, false, Node.InternalMode.Disabled);
            _searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("tmpl.filter_hint", "Search..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 32)
            };
            _searchEdit.AddThemeFontSizeOverride("font_size", 14);
            _searchEdit.TextChanged += OnSearchChanged;
            searchRow.AddChild(_searchEdit, false, Node.InternalMode.Disabled);

            // 操作按钮栏
            var btnRow = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            btnRow.AddThemeConstantOverride("separation", 6);
            leftColumn.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var newBtn = LoadoutPanel.CreateActionButton(Loc.Get("tmpl.new_template", "New"), null);
            newBtn.ToggleMode = false;
            newBtn.CustomMinimumSize = new Vector2(60, 30);
            newBtn.Pressed += OnNewTemplate;
            btnRow.AddChild(newBtn, false, Node.InternalMode.Disabled);

            var copyBtn = LoadoutPanel.CreateActionButton(Loc.Get("tmpl.copy", "Copy"), null);
            copyBtn.CustomMinimumSize = new Vector2(50, 30);
            copyBtn.Pressed += OnCopyTemplate;
            btnRow.AddChild(copyBtn, false, Node.InternalMode.Disabled);

            var delBtn = LoadoutPanel.CreateActionButton(Loc.Get("tmpl.delete", "Del"), new Color(0.7f, 0.2f, 0.15f));
            delBtn.CustomMinimumSize = new Vector2(40, 30);
            delBtn.Pressed += OnDeleteTemplate;
            btnRow.AddChild(delBtn, false, Node.InternalMode.Disabled);

            // 仅模板名单用纵向滚动（宽与左栏一致，不再出现窄竖条）
            var listScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftColumn.AddChild(listScroll, false, Node.InternalMode.Disabled);
            _templateListVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            _templateListVBox.AddThemeConstantOverride("separation", 4);
            listScroll.AddChild(_templateListVBox, false, Node.InternalMode.Disabled);

            // 右：编辑器面板
            _editorVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.62f
            };
            _editorVBox.AddThemeConstantOverride("separation", 8);
            hbox.AddChild(_editorVBox, false, Node.InternalMode.Disabled);

            RebuildTemplateList();
            ShowEditorHint();
        }

        // ── 模板列表 ─────────────────────────────────────────────────────────

        private static void RebuildTemplateList()
        {
            if (_templateListVBox == null) return;
            LoadoutPanel.ClearChildren(_templateListVBox);

            var q = string.IsNullOrWhiteSpace(_searchText)
                ? _templates
                : _templates.Where(t => t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            if (q.Count == 0)
            {
                var lbl = new Label
                {
                    Text = Loc.Get("tmpl.no_templates", "No templates"),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                lbl.AddThemeFontSizeOverride("font_size", 14);
                lbl.AddThemeColorOverride("font_color", SC.Gray);
                _templateListVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var t in q)
            {
                var btn = new Button
                {
                    Text = $"  {t.Name}",
                    CustomMinimumSize = new Vector2(LeftColumnMinWidth - 24f, 36),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                btn.AddThemeConstantOverride("h_separation", 4);
                btn.AddThemeFontSizeOverride("font_size", 14);
                btn.AddThemeColorOverride("font_color", _selected == t ? SC.Gold : SC.Cream);
                btn.AddThemeColorOverride("font_hover_color", SC.Gold);
                LoadoutPanel.ApplyFlatStyle(btn);

                TemplateData captured = t;
                btn.Pressed += () => SelectTemplate(captured);
                _templateListVBox.AddChild(btn, false, Node.InternalMode.Disabled);
            }
        }

        private static void OnSearchChanged(string text)
        {
            _searchText = text;
            RebuildTemplateList();
        }

        // ── 选择 / 新建 / 复制 / 删除 ─────────────────────────────────────────

        private static void SelectTemplate(TemplateData t)
        {
            _selected = t;
            RebuildTemplateList();
            ShowTemplateEditor(t);
        }

        private static void OnNewTemplate()
        {
            ulong now = Time.GetTicksMsec();
            if (_lastNewTemplateClickTicks > 0 && now - _lastNewTemplateClickTicks < 450)
                return;
            _lastNewTemplateClickTicks = now;

            var t = new TemplateData { Name = Loc.Get("tmpl.new_template", "New Template") };
            _templates.Insert(0, t);
            SaveTemplates();
            RebuildTemplateList();
            SelectTemplate(t);
        }

        private static void OnCopyTemplate()
        {
            if (_selected == null) return;
            var copy = new TemplateData
            {
                Name = _selected.Name + Loc.Get("tmpl.copy_suffix", " (Copy)"),
                Description = _selected.Description,
                CardIds = new List<string>(_selected.CardIds),
                RelicIds = new List<string>(_selected.RelicIds),
                PotionIds = new List<string>(_selected.PotionIds),
                Gold = _selected.Gold,
                MaxHp = _selected.MaxHp,
                Energy = _selected.Energy
            };
            var idx = _templates.IndexOf(_selected);
            _templates.Insert(Math.Max(0, idx), copy);
            SaveTemplates();
            RebuildTemplateList();
            SelectTemplate(copy);
        }

        private static void OnDeleteTemplate()
        {
            if (_selected == null) return;
            string id = _selected.Id;
            _selected = null;
            _templates.RemoveAll(x => x.Id == id);
            SaveTemplates();
            RebuildTemplateList();
            ShowEditorHint();
        }

        // ── 编辑器 ────────────────────────────────────────────────────────────

        private static void ShowEditorHint()
        {
            if (_editorVBox == null) return;
            _cardDeckSectionLabel = null;
            LoadoutPanel.ClearChildren(_editorVBox);
            var lbl = new Label
            {
                Text = Loc.Get("tmpl.select_hint", "Select or create a template"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            lbl.AddThemeFontSizeOverride("font_size", 18);
            lbl.AddThemeColorOverride("font_color", SC.Gray);
            _editorVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void ShowTemplateEditor(TemplateData t)
        {
            if (_editorVBox == null) return;
            LoadoutPanel.ClearChildren(_editorVBox);

            // 标题行
            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(titleRow, false, Node.InternalMode.Disabled);

            _nameEdit = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 36)
            };
            _nameEdit.AddThemeFontSizeOverride("font_size", 18);
            _nameEdit.AddThemeColorOverride("font_color", SC.Gold);
            _nameEdit.TextChanged += name =>
            {
                if (_suppressNameChanged) return;
                t.Name = name;
                SaveTemplates();
                RebuildTemplateList();
            };
            titleRow.AddChild(_nameEdit, false, Node.InternalMode.Disabled);
            _suppressNameChanged = true;
            _nameEdit.Text = t.Name;
            _suppressNameChanged = false;

            // 基础信息
            _editorVBox.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("tmpl.basic_info", "Basic Info")), false, Node.InternalMode.Disabled);

            var infoGrid = new GridContainer { Columns = 3 };
            infoGrid.AddThemeConstantOverride("h_separation", 12);
            infoGrid.AddThemeConstantOverride("v_separation", 6);
            _editorVBox.AddChild(infoGrid, false, Node.InternalMode.Disabled);

            AddInfoField(infoGrid, Loc.Get("tmpl.max_hp", "Max HP"), t.MaxHp.ToString(), v => { t.MaxHp = ParseInt(v, 0); SaveTemplates(); });
            AddInfoField(infoGrid, Loc.Get("tmpl.gold", "Gold"), t.Gold.ToString(), v => { t.Gold = ParseInt(v, 0); SaveTemplates(); });
            AddInfoField(infoGrid, Loc.Get("char.current_energy", "Energy"), t.Energy.ToString(), v => { t.Energy = ParseInt(v, 0); SaveTemplates(); });

            // 卡牌列表
            _cardDeckSectionLabel = LoadoutPanel.CreateSectionHeader(Loc.Fmt("tmpl.card_deck", t.CardIds.Count));
            _editorVBox.AddChild(_cardDeckSectionLabel, false, Node.InternalMode.Disabled);

            var cardsScroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            cardsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _editorVBox.AddChild(cardsScroll, false, Node.InternalMode.Disabled);

            var cardsVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            cardsVBox.AddThemeConstantOverride("separation", 4);
            cardsScroll.AddChild(cardsVBox, false, Node.InternalMode.Disabled);

            TemplateData captured = t;
            RebuildCardList(cardsVBox, captured);

            // 添加卡牌按钮
            var addCardRow = new HBoxContainer();
            addCardRow.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(addCardRow, false, Node.InternalMode.Disabled);
            var addBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("tmpl.add_card", "+ Add Card"), new Color(0.3f, 0.6f, 0.3f));
            addBtn.CustomMinimumSize = new Vector2(120, 32);
            TemplateData tmpl = t;
            addBtn.Pressed += () =>
            {
                var allCards = ModelDb.AllCards.OrderBy(c => c.Id.Entry).ToList();
                CardBrowserPanel.Open(allCards, card =>
                {
                    tmpl.CardIds.Add(card.Id.Entry);
                    SaveTemplates();
                    RebuildCardList(cardsVBox, tmpl);
                    RefreshEditorHeader(tmpl);
                });
            };
            addCardRow.AddChild(addBtn, false, Node.InternalMode.Disabled);
        }

        private static void RebuildCardList(VBoxContainer parent, TemplateData t)
        {
            LoadoutPanel.ClearChildren(parent);
            if (t.CardIds.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("tmpl.no_cards", "No cards") };
                lbl.AddThemeFontSizeOverride("font_size", 13);
                lbl.AddThemeColorOverride("font_color", SC.Gray);
                parent.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            var allCards = ModelDb.AllCards.ToDictionary(c => c.Id.Entry, c => c);
            var grid = new GridContainer { Columns = 8 };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            parent.AddChild(grid, false, Node.InternalMode.Disabled);

            foreach (var cardId in t.CardIds.ToList())
            {
                if (!allCards.TryGetValue(cardId, out var card)) continue;
                var clip = CardsTab.CreateNCardWrapperPublic(card, PileType.Deck, new List<CardModel> { card });
                clip.CustomMinimumSize = new Vector2(0, 120);
                grid.AddChild(clip, false, Node.InternalMode.Disabled);

                clip.GuiInput += ev =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                    {
                        t.CardIds.Remove(cardId);
                        SaveTemplates();
                        RebuildCardList(parent, t);
                        RefreshEditorHeader(t);
                    }
                };
            }
        }

        private static void RefreshEditorHeader(TemplateData t)
        {
            if (_cardDeckSectionLabel != null && GodotObject.IsInstanceValid(_cardDeckSectionLabel))
                _cardDeckSectionLabel.Text = Loc.Fmt("tmpl.card_deck", t.CardIds.Count);
        }

        private static void AddInfoField(GridContainer grid, string label, string value, Action<string> onChanged)
        {
            var lbl = new Label { Text = label };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", SC.Cream);
            lbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            grid.AddChild(lbl, false, Node.InternalMode.Disabled);

            var edit = new LineEdit { Text = value };
            edit.AddThemeFontSizeOverride("font_size", 14);
            Action<string> capturedCallback = onChanged;
            edit.TextChanged += (newText) => { capturedCallback(newText); };
            grid.AddChild(edit, false, Node.InternalMode.Disabled);

            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddChild(spacer, false, Node.InternalMode.Disabled);
        }

        // ── 数据存取 ─────────────────────────────────────────────────────────

        private static void LoadTemplates()
        {
            _templates = TemplateStorage.LoadAll();
        }

        private static void SaveTemplates()
        {
            TemplateStorage.SaveAll(_templates);
        }

        private static int ParseInt(string s, int fallback)
        {
            return int.TryParse(s, out var v) ? v : fallback;
        }

        // ── 样式颜色 ─────────────────────────────────────────────────────────

        private static class SC
        {
            internal static readonly Color Gold    = new Color("E3A83D");
            internal static readonly Color Cream  = new Color("E3D5C1");
            internal static readonly Color Gray   = new Color("7F8C8D");
            internal static readonly Color Red    = new Color("C0392B");
            internal static readonly Color Green  = new Color("27AE60");
            internal static readonly Color Blue   = new Color("2980B9");
        }

        // ── 私有字段 ─────────────────────────────────────────────────────────

        private static List<TemplateData> _templates;
        private static TemplateData _selected;
        private static string _searchText = "";

        private static VBoxContainer _templateListVBox;
        private static VBoxContainer _editorVBox;
        private static LineEdit _searchEdit;
        private static LineEdit _nameEdit;
    }
}
