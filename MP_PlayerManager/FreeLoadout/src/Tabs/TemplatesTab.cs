using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
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
        /// <summary>程序化改搜索框文本时不触发 OnSearchChanged。</summary>
        private static bool _suppressSearchChanged;
        private static Label? _cardDeckSectionLabel;

        // ── Build ──────────────────────────────────────────────────────────────

        internal static void Build(VBoxContainer container, Player? player)
        {
            if (_templates == null) LoadTemplates();
            BuildCharacterRegistry(); // 从 ModelDb 构建角色列表

            // RefreshCurrentTab 会先 ClearChildren，旧节点已 QueueFree，但静态引用仍可能指向已释放对象；
            // 不清理则 RebuildTemplateList 会对无效节点操作，导致列表“消失”。
            if (_templateListVBox != null && !GodotObject.IsInstanceValid(_templateListVBox)) _templateListVBox = null!;
            if (_editorVBox != null && !GodotObject.IsInstanceValid(_editorVBox)) _editorVBox = null!;
            if (_searchEdit != null && !GodotObject.IsInstanceValid(_searchEdit)) _searchEdit = null!;

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
            _searchEdit.Text = _searchText;
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

            // 导出 / 导入按钮（收集操作）
            var exportBtn = LoadoutPanel.CreateActionButton(Loc.Get("tmpl.export", "Export"), new Color(0.3f, 0.5f, 0.8f));
            exportBtn.CustomMinimumSize = new Vector2(58, 30);
            exportBtn.Pressed += OnExportAll;
            btnRow.AddChild(exportBtn, false, Node.InternalMode.Disabled);

            var importBtn = LoadoutPanel.CreateActionButton(Loc.Get("tmpl.import", "Import"), new Color(0.4f, 0.6f, 0.4f));
            importBtn.CustomMinimumSize = new Vector2(58, 30);
            importBtn.Pressed += OnImportAll;
            btnRow.AddChild(importBtn, false, Node.InternalMode.Disabled);

            // 仅模板名单用纵向滚动（宽与左栏一致，不再出现窄竖条）
            var listScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                // 避免子节点暂时为 0 高时 Scroll 被压成不可见细条
                CustomMinimumSize = new Vector2(0, 200f)
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
            if (_templateListVBox == null || !GodotObject.IsInstanceValid(_templateListVBox)) return;
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
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(LeftColumnMinWidth - 32f, 40f)
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
            if (_suppressSearchChanged) return;
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

            var t = new TemplateData
            {
                Name = Loc.Get("tmpl.new_template", "New Template"),
                CharacterId = "CHARACTER.IRONCLAD"
            };
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
                CharacterId = _selected.CharacterId,
                CharacterName = _selected.CharacterName,
                CardIds = new List<string>(_selected.CardIds),
                RelicIds = new List<string>(_selected.RelicIds),
                PotionIds = new List<string>(_selected.PotionIds),
                Gold = _selected.Gold,
                MaxHp = _selected.MaxHp,
                CurHp = _selected.CurHp,
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
            _suppressSearchChanged = true;
            _searchText = "";
            if (_searchEdit != null && GodotObject.IsInstanceValid(_searchEdit))
                _searchEdit.Text = "";
            _suppressSearchChanged = false;

            RebuildTemplateList();
            ShowEditorHint();
            Callable.From(RebuildTemplateList).CallDeferred();
        }

        private static void OnExportAll()
        {
            if (_templates == null || _templates.Count == 0)
            {
                GD.Print("[MP_PlayerManager] No templates to export.");
                return;
            }
            var dialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                Title = Loc.Get("tmpl.export_title", "Export Templates"),
                CurrentPath = "templates.json"
            };
            dialog.Mode = (FileDialog.ModeEnum)2; // SaveFile
            dialog.AddFilter("*.json ; JSON Files");
            dialog.FileSelected += path =>
            {
                try
                {
                    var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(_templates, opts));
                    GD.Print($"[MP_PlayerManager] Exported {_templates.Count} template(s) → {path}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[MP_PlayerManager] Export failed: {ex.Message}");
                }
            };
            NGame.Instance?.AddChild(dialog, false, Node.InternalMode.Disabled);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        private static void OnImportAll()
        {
            var dialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                Title = Loc.Get("tmpl.import_title", "Import Templates")
            };
            dialog.Mode = (FileDialog.ModeEnum)0; // OpenFile
            dialog.AddFilter("*.json ; JSON Files");
            dialog.FileSelected += path =>
            {
                try
                {
                    var imported = System.Text.Json.JsonSerializer.Deserialize<List<TemplateData>>(File.ReadAllText(path));
                    if (imported == null || imported.Count == 0)
                    {
                        GD.Print("[MP_PlayerManager] Import file is empty or invalid: " + path);
                        return;
                    }
                    // 用导入数据完全替换现有列表（避免重复和"新建模板"残留）
                    _templates = imported;
                    // 重新分配 Id，避免与旧数据冲突
                    foreach (var t in _templates)
                        t.Id = Guid.NewGuid().ToString();
                    SaveTemplates();
                    RebuildTemplateList();
                    _selected = null;
                    ShowEditorHint();
                    GD.Print($"[MP_PlayerManager] Imported {imported.Count} template(s) from: {path}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[MP_PlayerManager] Import failed: {ex.Message}");
                }
            };
            NGame.Instance?.AddChild(dialog, false, Node.InternalMode.Disabled);
            dialog.PopupCentered(new Vector2I(800, 600));
        }

        // ── 批量添加卡牌（Shift+点击触发） ────────────────────────────────────

        /// <summary>
        /// 从游戏卡牌库 Shift+点击时调用：打开批量选卡弹窗，
        /// 选完后将所有卡牌追加到当前模板的 CardIds 列表。
        /// </summary>
        internal static void OpenCardBrowserForShiftAdd(List<CardModel> allCards)
        {
            if (_selected == null)
            {
                GD.Print("[MP_PlayerManager] No template selected, cannot add cards.");
                return;
            }

            CardBrowserPanel.OpenForBatch(allCards, selected =>
            {
                if (selected == null || selected.Count == 0) return;
                var tmpl = _selected;
                if (tmpl == null) return;

                foreach (var card in selected)
                {
                    string id = card.Id?.Entry ?? "";
                    if (!string.IsNullOrEmpty(id) && !tmpl.CardIds.Contains(id))
                        tmpl.CardIds.Add(id);
                }
                SaveTemplates();
                GD.Print($"[MP_PlayerManager] Batch added {selected.Count} card(s) to template '{tmpl.Name}'");
            });
        }

        // ── 编辑器 ────────────────────────────────────────────────────────────

        private static void ShowEditorHint()
        {
            if (_editorVBox == null || !GodotObject.IsInstanceValid(_editorVBox)) return;
            _cardDeckSectionLabel = null;
            _characterButtons.Clear();
            _maxHpEdit = null;
            _curHpEdit = null;
            _energyEdit = null;
            _goldEdit = null;
            _suppressNameChanged = true;
            LoadoutPanel.ClearChildren(_editorVBox);
            _suppressNameChanged = false;
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
            if (_editorVBox == null || !GodotObject.IsInstanceValid(_editorVBox)) return;
            _characterButtons.Clear();
            _suppressNameChanged = true;
            LoadoutPanel.ClearChildren(_editorVBox);
            _suppressNameChanged = false;

            // 标题行：标签 + 名称输入
            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(titleRow, false, Node.InternalMode.Disabled);

            var nameLbl = new Label
            {
                Text = Loc.Get("tmpl.name_label", "Template name:"),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            nameLbl.AddThemeFontSizeOverride("font_size", 14);
            nameLbl.AddThemeColorOverride("font_color", SC.Cream);
            nameLbl.CustomMinimumSize = new Vector2(100, 0);
            titleRow.AddChild(nameLbl, false, Node.InternalMode.Disabled);

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

            // 角色选择（HFlow 自动换行；按钮文案用 LocString 格式化）
            _editorVBox.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("tmpl.character", "Character")), false, Node.InternalMode.Disabled);
            var charRow = new HFlowContainer();
            charRow.AddThemeConstantOverride("h_separation", 6);
            charRow.AddThemeConstantOverride("v_separation", 6);
            _editorVBox.AddChild(charRow, false, Node.InternalMode.Disabled);

            TemplateData tmplChar = t;
            foreach (var ci in _characterRegistry)
            {
                var charBtn = LoadoutPanel.CreateActionButton(ci.DisplayName, null);
                charBtn.CustomMinimumSize = new Vector2(80, 28);
                charBtn.ToggleMode = true;
                charBtn.ButtonPressed = ci.Id == t.CharacterId;
                charBtn.Pressed += () =>
                {
                    tmplChar.CharacterId = ci.Id;
                    tmplChar.CharacterName = ci.DisplayName;
                    // 选中角色时，若模板属性为 0，从角色注册表填充默认值
                    if (tmplChar.MaxHp <= 0 && ci.DefaultMaxHp > 0) tmplChar.MaxHp = ci.DefaultMaxHp;
                    if (tmplChar.CurHp <= 0 && ci.DefaultMaxHp > 0) tmplChar.CurHp = ci.DefaultMaxHp;
                    if (tmplChar.Energy <= 0 && ci.DefaultEnergy > 0) tmplChar.Energy = ci.DefaultEnergy;
                    SaveTemplates();
                    RefreshCharacterButtons(tmplChar);
                    RefreshEditorAllFields(tmplChar); // 重绘属性编辑器（显示新填入的值）
                };
                _characterButtons[ci.Id] = charBtn;
                charRow.AddChild(charBtn, false, Node.InternalMode.Disabled);
            }

            // 基础信息：每行「标签 | 输入框」，避免 Grid+spacer 造成错位
            _editorVBox.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("tmpl.basic_info", "Basic Info")), false, Node.InternalMode.Disabled);

            var infoCol = new VBoxContainer();
            infoCol.AddThemeConstantOverride("separation", 10);
            _editorVBox.AddChild(infoCol, false, Node.InternalMode.Disabled);

            AddInfoRow(infoCol, Loc.Get("tmpl.max_hp", "Max HP"), t.MaxHp.ToString(), v => { t.MaxHp = ParseInt(v, 0); if (t.CurHp > t.MaxHp || t.CurHp <= 0) t.CurHp = t.MaxHp; SaveTemplates(); }, out _maxHpEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.cur_hp", "Current HP"), t.CurHp.ToString(), v => { t.CurHp = ParseInt(v, 0); t.CurHp = Math.Max(0, Math.Min(t.CurHp, t.MaxHp)); SaveTemplates(); }, out _curHpEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.energy", "Energy"), t.Energy.ToString(), v => { t.Energy = ParseInt(v, 0); SaveTemplates(); }, out _energyEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.gold", "Gold"), t.Gold.ToString(), v => { t.Gold = ParseInt(v, 0); SaveTemplates(); }, out _goldEdit);

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

        /// <summary>
        /// 刷新右侧编辑器中 MaxHP / CurHP / Energy / Gold 四个字段的 Text 值。
        /// 在切换角色自动填默认值后调用，让用户看到新填入的数字。
        /// </summary>
        private static void RefreshEditorAllFields(TemplateData t)
        {
            if (_maxHpEdit != null && GodotObject.IsInstanceValid(_maxHpEdit))
                _maxHpEdit.Text = t.MaxHp.ToString();
            if (_curHpEdit != null && GodotObject.IsInstanceValid(_curHpEdit))
                _curHpEdit.Text = t.CurHp.ToString();
            if (_energyEdit != null && GodotObject.IsInstanceValid(_energyEdit))
                _energyEdit.Text = t.Energy.ToString();
            if (_goldEdit != null && GodotObject.IsInstanceValid(_goldEdit))
                _goldEdit.Text = t.Gold.ToString();
        }

        /// <summary>单行：标签（固定宽度）+ 数值输入（横向填充）。</summary>
        private static void AddInfoRow(VBoxContainer parent, string label, string value, Action<string> onChanged, out LineEdit? outEdit)
        {
            outEdit = null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var lbl = new Label { Text = label };
            lbl.CustomMinimumSize = new Vector2(120, 0);
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", SC.Cream);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);

            var edit = new LineEdit
            {
                Text = value,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(120, 32)
            };
            edit.AddThemeFontSizeOverride("font_size", 14);
            Action<string> capturedCallback = onChanged;
            edit.TextChanged += (newText) => { capturedCallback(newText); };
            row.AddChild(edit, false, Node.InternalMode.Disabled);
            outEdit = edit;

            parent.AddChild(row, false, Node.InternalMode.Disabled);
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

        // ── 角色数据（从 ModelDb.AllCharacters 动态构建） ─────────────────────

        /// <summary>
        /// 角色信息：Id / 显示名 / 初始 MaxHp / 初始 Energy。
        /// 值从 ModelDb.AllCharacters 动态读，读不到时用 Fallback 兜底。
        /// </summary>
        private static readonly List<CharacterInfo> _characterRegistry = new();

        private static readonly List<CharacterInfo> FallbackCharacters = new()
        {
            new("CHARACTER.IRONCLAD", "Ironclad", 80, 3),
            new("CHARACTER.SILENT",   "Silent",   70, 3),
            new("CHARACTER.DEFECT",   "Defect",   65, 3),
            new("CHARACTER.NECROBINDER", "Necrobinder", 72, 3),
            new("CHARACTER.REGENT",   "Regent",   65, 3),
        };

        private static void BuildCharacterRegistry()
        {
            if (_characterRegistry.Count > 0) return;
            try
            {
                var allChars = ModelDb.AllCharacters;
                if (allChars != null)
                {
                    foreach (var ch in allChars)
                    {
                        var id = ch.Id?.Entry ?? "";
                        string displayName = id;
                        try
                        {
                            // LocString.ToString() 会输出类型全名；必须用 GetFormattedText()
                            LocString? title = ch.Title;
                            if (title != null)
                            {
                                string formatted = title.GetFormattedText();
                                if (!string.IsNullOrWhiteSpace(formatted))
                                    displayName = formatted;
                            }
                        }
                        catch { /* 忽略单条解析失败 */ }

                        int maxHp = TryGetCharacterInt(ch, new[] { "BaseMaxHp", "MaxHp", "StartingMaxHp", "BaseMaxHealth" }, 0);
                        int energy = TryGetCharacterInt(ch, new[] { "BaseEnergy", "StartingEnergy", "MaxEnergy", "BaseMaxEnergy" }, 0);
                        _characterRegistry.Add(new CharacterInfo(id, displayName, maxHp, energy));
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] Failed to build character registry: " + ex.Message);
            }
            // 兜底：若读到的角色数为 0，用内置
            if (_characterRegistry.Count == 0)
            {
                foreach (var fc in FallbackCharacters)
                    _characterRegistry.Add(fc);
            }
        }

        /// <summary>
        /// 尝试从 CharacterModel 的字典属性中读取整数值（按多个可能的 key 顺序尝试）。
        /// </summary>
        private static int TryGetCharacterInt(object charModel, string[] keys, int fallback)
        {
            try
            {
                var dict = AccessTools.PropertyGetter(charModel.GetType(), "CardPool")
                    ?.Invoke(charModel, null) as IReadOnlyDictionary<string, object>;
                if (dict != null)
                {
                    // 尝试 CardPool 字典
                    foreach (var k in keys)
                    {
                        if (dict.TryGetValue(k, out var v))
                        {
                            if (v is int i) return i;
                            if (v is long l) return (int)l;
                            if (v is float f) return (int)f;
                            if (int.TryParse(v?.ToString(), out var parsed)) return parsed;
                        }
                    }
                }
            }
            catch { }
            try
            {
                // 直接反射公开属性
                var type = charModel.GetType();
                foreach (var propName in keys)
                {
                    var prop = AccessTools.Property(type, propName)
                        ?? AccessTools.Field(type, propName) as MemberInfo;
                    if (prop is PropertyInfo pi)
                    {
                        var v = pi.GetValue(charModel);
                        if (v is int i) return i;
                        if (v is long l) return (int)l;
                        if (v is float f) return (int)f;
                        if (int.TryParse(v?.ToString(), out var parsed)) return parsed;
                    }
                    else if (prop is FieldInfo fi)
                    {
                        var v = fi.GetValue(charModel);
                        if (v is int i) return i;
                        if (v is long l) return (int)l;
                        if (v is float f) return (int)f;
                        if (int.TryParse(v?.ToString(), out var parsed)) return parsed;
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>从注册表获取某角色的默认值，若角色不存在返回 null。</summary>
        private static CharacterInfo? GetCharacterInfo(string characterId)
        {
            foreach (var ci in _characterRegistry)
                if (ci.Id == characterId) return ci;
            return null;
        }

        private sealed record CharacterInfo(string Id, string DisplayName, int DefaultMaxHp, int DefaultEnergy);

        private static readonly Dictionary<string, Button> _characterButtons = new();

        private static void RefreshCharacterButtons(TemplateData t)
        {
            foreach (var kvp in _characterButtons)
            {
                if (GodotObject.IsInstanceValid(kvp.Value))
                    kvp.Value.ButtonPressed = kvp.Key == t.CharacterId;
            }
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
        private static LineEdit? _maxHpEdit;
        private static LineEdit? _curHpEdit;
        private static LineEdit? _energyEdit;
        private static LineEdit? _goldEdit;
    }
}
