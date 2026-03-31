using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Templates tab: manage character templates.
    /// Left: template list. Right: template editor + Apply button.
    /// </summary>
    internal static class TemplatesTab
    {
        private const float LeftColumnMinWidth = 300f;

        private static List<TemplateData> _templates = new();
        private static TemplateData? _selected;
        private static string _searchText = "";
        private static VBoxContainer? _templateListVBox;
        private static VBoxContainer? _editorVBox;
        private static LineEdit? _searchEdit;
        private static LineEdit? _nameEdit;
        private static LineEdit? _maxHpEdit, _curHpEdit, _energyEdit, _goldEdit;
        private static Label? _deckCountLabel;
        private static bool _suppressNameChanged;
        private static bool _suppressSearchChanged;
        private static ulong _lastNewClickTicks;
        private static readonly List<CharacterInfo> _characterRegistry = new();

        internal static void Build(VBoxContainer container)
        {
            if (_templates.Count == 0) LoadTemplates();
            BuildCharacterRegistry();

            if (_templateListVBox != null && !GodotObject.IsInstanceValid(_templateListVBox)) _templateListVBox = null;
            if (_editorVBox != null && !GodotObject.IsInstanceValid(_editorVBox)) _editorVBox = null;
            if (_searchEdit != null && !GodotObject.IsInstanceValid(_searchEdit)) _searchEdit = null;

            var hbox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            hbox.AddThemeConstantOverride("separation", 12);
            container.AddChild(hbox, false, Node.InternalMode.Disabled);

            // Left column: list
            var leftCol = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(LeftColumnMinWidth, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.38f
            };
            leftCol.AddThemeConstantOverride("separation", 6);
            hbox.AddChild(leftCol, false, Node.InternalMode.Disabled);

            leftCol.AddChild(MpPanel.CreateSectionHeader(Loc.Get("tmpl.title", "Character Templates")), false, Node.InternalMode.Disabled);

            // Search
            _searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("tmpl.filter_hint", "Search..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 32)
            };
            _searchEdit.AddThemeFontSizeOverride("font_size", 14);
            _searchEdit.TextChanged += OnSearchChanged;
            _searchEdit.Text = _searchText;
            leftCol.AddChild(_searchEdit, false, Node.InternalMode.Disabled);

            // Buttons
            var btnRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            btnRow.AddThemeConstantOverride("separation", 6);
            leftCol.AddChild(btnRow, false, Node.InternalMode.Disabled);

            AddTmplBtn(btnRow, Loc.Get("tmpl.new_template", "New"), null, OnNewTemplate, new Vector2(60, 30));
            AddTmplBtn(btnRow, Loc.Get("tmpl.copy", "Copy"), null, OnCopyTemplate, new Vector2(50, 30));
            AddTmplBtn(btnRow, Loc.Get("tmpl.delete", "Del"), Panel.Styles.Red, OnDeleteTemplate, new Vector2(40, 30));
            AddTmplBtn(btnRow, Loc.Get("tmpl.export", "Export"), Panel.Styles.Blue, OnExportAll, new Vector2(55, 30));
            AddTmplBtn(btnRow, Loc.Get("tmpl.import", "Import"), Panel.Styles.Green, OnImportAll, new Vector2(55, 30));

            // Template list scroll
            var listScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 200)
            };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftCol.AddChild(listScroll, false, Node.InternalMode.Disabled);
            _templateListVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            _templateListVBox.AddThemeConstantOverride("separation", 4);
            listScroll.AddChild(_templateListVBox, false, Node.InternalMode.Disabled);

            // Right column: editor
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

        private static void AddTmplBtn(HBoxContainer row, string text, Color? color, Action callback, Vector2 minSize)
        {
            var btn = MpPanel.CreateActionButton(text, color);
            btn.CustomMinimumSize = minSize;
            btn.Pressed += callback;
            row.AddChild(btn, false, Node.InternalMode.Disabled);
        }

        private static void OnSearchChanged(string text)
        {
            if (_suppressSearchChanged) return;
            _searchText = text;
            RebuildTemplateList();
        }

        private static void OnNewTemplate()
        {
            ulong now = Time.GetTicksMsec();
            if (_lastNewClickTicks > 0 && now - _lastNewClickTicks < 450) return;
            _lastNewClickTicks = now;

            var t = new TemplateData { Name = Loc.Get("tmpl.new_template", "New Template"), CharacterId = "CHARACTER.IRONCLAD" };
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
                CharacterId = _selected.CharacterId,
                CharacterName = _selected.CharacterName,
                CardIds = new List<string>(_selected.CardIds),
                RelicIds = new List<string>(_selected.RelicIds),
                MaxHp = _selected.MaxHp, CurHp = _selected.CurHp,
                Energy = _selected.Energy, Gold = _selected.Gold
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
            var id = _selected.Id;
            _selected = null;
            _templates.RemoveAll(t => t.Id == id);
            SaveTemplates();
            _suppressSearchChanged = true; _searchText = "";
            if (_searchEdit != null && GodotObject.IsInstanceValid(_searchEdit)) _searchEdit.Text = "";
            _suppressSearchChanged = false;
            RebuildTemplateList();
            ShowEditorHint();
        }

        private static void OnExportAll()
        {
            if (_templates.Count == 0) return;
            var dialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                Title = Loc.Get("tmpl.export_title", "Export Templates"),
                CurrentPath = "templates.json"
            };
            dialog.Mode = (FileDialog.ModeEnum)2;
            dialog.AddFilter("*.json ; JSON Files");
            dialog.FileSelected += path =>
            {
                try
                {
                    var opts = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(path, JsonSerializer.Serialize(_templates, opts));
                    GD.Print($"[MultiplayerTools] Exported {_templates.Count} templates");
                }
                catch (Exception ex) { GD.PrintErr("[MultiplayerTools] Export failed: " + ex.Message); }
            };
            NGame.Instance?.AddChild(dialog, false, Node.InternalMode.Disabled);
            dialog.PopupCentered(new Vector2I(700, 500));
        }

        private static void OnImportAll()
        {
            var dialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                Title = Loc.Get("tmpl.import_title", "Import Templates")
            };
            dialog.Mode = (FileDialog.ModeEnum)0;
            dialog.AddFilter("*.json ; JSON Files");
            dialog.FileSelected += path =>
            {
                try
                {
                    var imported = JsonSerializer.Deserialize<List<TemplateData>>(File.ReadAllText(path));
                    if (imported == null || imported.Count == 0) return;
                    foreach (var t in imported) t.Id = Guid.NewGuid().ToString();
                    _templates = imported;
                    SaveTemplates();
                    RebuildTemplateList();
                    _selected = null;
                    ShowEditorHint();
                    GD.Print($"[MultiplayerTools] Imported {imported.Count} templates");
                }
                catch (Exception ex) { GD.PrintErr("[MultiplayerTools] Import failed: " + ex.Message); }
            };
            NGame.Instance?.AddChild(dialog, false, Node.InternalMode.Disabled);
            dialog.PopupCentered(new Vector2I(700, 500));
        }

        private static void SelectTemplate(TemplateData t)
        {
            _selected = t;
            RebuildTemplateList();
            ShowTemplateEditor(t);
        }

        private static void RebuildTemplateList()
        {
            if (_templateListVBox == null || !GodotObject.IsInstanceValid(_templateListVBox)) return;
            MpPanel.ClearChildren(_templateListVBox);

            var q = string.IsNullOrWhiteSpace(_searchText)
                ? _templates
                : _templates.Where(t => t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            if (q.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("tmpl.no_templates", "No templates"), CustomMinimumSize = new Vector2(LeftColumnMinWidth - 24, 40) };
                lbl.AddThemeFontSizeOverride("font_size", 14);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                _templateListVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var t in q)
            {
                var btn = new Button
                {
                    Text = $"  {t.Name}",
                    CustomMinimumSize = new Vector2(LeftColumnMinWidth - 24, 36),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                btn.AddThemeFontSizeOverride("font_size", 14);
                btn.AddThemeColorOverride("font_color", _selected == t ? Panel.Styles.Gold : Panel.Styles.Cream);
                btn.AddThemeColorOverride("font_hover_color", Panel.Styles.Gold);
                Panel.Styles.ApplyListRowButton(btn);
                TemplateData captured = t;
                btn.Pressed += () => SelectTemplate(captured);
                _templateListVBox.AddChild(btn, false, Node.InternalMode.Disabled);
            }
        }

        private static void ShowEditorHint()
        {
            if (_editorVBox == null || !GodotObject.IsInstanceValid(_editorVBox)) return;
            _suppressNameChanged = true;
            MpPanel.ClearChildren(_editorVBox);
            _suppressNameChanged = false;
            var lbl = new Label { Text = Loc.Get("tmpl.select_hint", "Select or create a template"), HorizontalAlignment = HorizontalAlignment.Center };
            lbl.AddThemeFontSizeOverride("font_size", 18);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
            _editorVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void ShowTemplateEditor(TemplateData t)
        {
            if (_editorVBox == null || !GodotObject.IsInstanceValid(_editorVBox)) return;
            _suppressNameChanged = true;
            MpPanel.ClearChildren(_editorVBox);
            _suppressNameChanged = false;

            // Name row
            var nameRow = new HBoxContainer();
            nameRow.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(nameRow, false, Node.InternalMode.Disabled);
            var nameLbl = new Label { Text = Loc.Get("tmpl.name_label", "Template name:"), CustomMinimumSize = new Vector2(100, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            nameLbl.AddThemeFontSizeOverride("font_size", 14);
            nameLbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            nameRow.AddChild(nameLbl, false, Node.InternalMode.Disabled);
            _nameEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };
            _nameEdit.AddThemeFontSizeOverride("font_size", 18);
            _nameEdit.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            _nameEdit.TextChanged += name => { if (!_suppressNameChanged) { t.Name = name; SaveTemplates(); RebuildTemplateList(); } };
            nameRow.AddChild(_nameEdit, false, Node.InternalMode.Disabled);
            _suppressNameChanged = true; _nameEdit.Text = t.Name; _suppressNameChanged = false;

            // Character selector
            _editorVBox.AddChild(MpPanel.CreateSectionHeader(Loc.Get("tmpl.character", "Character")), false, Node.InternalMode.Disabled);
            var charRow = new HFlowContainer();
            charRow.AddThemeConstantOverride("h_separation", 6);
            charRow.AddThemeConstantOverride("v_separation", 6);
            _editorVBox.AddChild(charRow, false, Node.InternalMode.Disabled);

            foreach (var ci in _characterRegistry)
            {
                var charBtn = MpPanel.CreateActionButton(ci.DisplayName, null);
                charBtn.CustomMinimumSize = new Vector2(80, 28);
                charBtn.ToggleMode = true;
                charBtn.ButtonPressed = ci.Id == t.CharacterId;
                charBtn.Pressed += () =>
                {
                    t.CharacterId = ci.Id;
                    t.CharacterName = ci.DisplayName;
                    if (t.MaxHp <= 0 && ci.DefaultMaxHp > 0) t.MaxHp = ci.DefaultMaxHp;
                    if (t.CurHp <= 0 && ci.DefaultMaxHp > 0) t.CurHp = ci.DefaultMaxHp;
                    if (t.Energy <= 0 && ci.DefaultEnergy > 0) t.Energy = ci.DefaultEnergy;
                    SaveTemplates();
                    RefreshAllFields(t);
                };
                charRow.AddChild(charBtn, false, Node.InternalMode.Disabled);
            }

            // Basic info
            _editorVBox.AddChild(MpPanel.CreateSectionHeader(Loc.Get("tmpl.basic_info", "Basic Info")), false, Node.InternalMode.Disabled);
            var infoCol = new VBoxContainer();
            infoCol.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(infoCol, false, Node.InternalMode.Disabled);
            AddInfoRow(infoCol, Loc.Get("tmpl.max_hp", "Max HP"), t.MaxHp.ToString(), v => { t.MaxHp = ParseInt(v, 0); if (t.CurHp > t.MaxHp || t.CurHp <= 0) t.CurHp = t.MaxHp; SaveTemplates(); }, out _maxHpEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.cur_hp", "Current HP"), t.CurHp.ToString(), v => { t.CurHp = Math.Max(0, Math.Min(ParseInt(v, 0), t.MaxHp)); SaveTemplates(); }, out _curHpEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.energy", "Energy"), t.Energy.ToString(), v => { t.Energy = ParseInt(v, 0); SaveTemplates(); }, out _energyEdit);
            AddInfoRow(infoCol, Loc.Get("tmpl.gold", "Gold"), t.Gold.ToString(), v => { t.Gold = ParseInt(v, 0); SaveTemplates(); }, out _goldEdit);

            // Card deck
            _deckCountLabel = MpPanel.CreateSectionHeader(Loc.Fmt("tmpl.card_deck", t.CardIds.Count));
            _editorVBox.AddChild(_deckCountLabel, false, Node.InternalMode.Disabled);

            var cardsScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            cardsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _editorVBox.AddChild(cardsScroll, false, Node.InternalMode.Disabled);
            var cardsVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            cardsVBox.AddThemeConstantOverride("separation", 4);
            cardsScroll.AddChild(cardsVBox, false, Node.InternalMode.Disabled);
            RebuildCardList(cardsVBox, t);

            // Add card + Apply buttons
            var btnRow2 = new HBoxContainer();
            btnRow2.AddThemeConstantOverride("separation", 8);
            _editorVBox.AddChild(btnRow2, false, Node.InternalMode.Disabled);

            var addBtn = MpPanel.CreateActionButton(Loc.Get("tmpl.add_card", "+ Add Card"), Panel.Styles.Green);
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
                    RefreshDeckCount(tmpl);
                });
            };
            btnRow2.AddChild(addBtn, false, Node.InternalMode.Disabled);

            // Apply button
            var applyBtn = MpPanel.CreateActionButton(Loc.Get("tmpl.apply", "Apply Template"), Panel.Styles.Gold);
            applyBtn.CustomMinimumSize = new Vector2(130, 32);
            applyBtn.Pressed += async () =>
            {
                if (MpPanel.GetLocalPlayer() == null)
                {
                    GD.Print("[MultiplayerTools] Not in a run, cannot apply template");
                    return;
                }
                await TemplateApplier.ApplyToLocalAsync(t);
            };
            btnRow2.AddChild(applyBtn, false, Node.InternalMode.Disabled);
        }

        private static void RebuildCardList(VBoxContainer parent, TemplateData t)
        {
            MpPanel.ClearChildren(parent);
            if (t.CardIds.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("tmpl.no_cards", "No cards") };
                lbl.AddThemeFontSizeOverride("font_size", 13);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
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
                // Simple card label wrapper
                var cardNode = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(0, 80),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                var cardStyle = Panel.Styles.CreateFlat(new Color(0.10f, 0.08f, 0.14f, 0.9f), Panel.Styles.PanelBorder);
                cardStyle.SetCornerRadiusAll(4);
                cardNode.AddThemeStyleboxOverride("panel", cardStyle);
                var lbl = new Label { Text = card.Id.Entry, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", 10);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
                lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                cardNode.AddChild(lbl, false, Node.InternalMode.Disabled);
                grid.AddChild(cardNode, false, Node.InternalMode.Disabled);
                cardNode.GuiInput += ev =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                    { t.CardIds.Remove(cardId); SaveTemplates(); RebuildCardList(parent, t); RefreshDeckCount(t); }
                };
            }
        }

        private static void RefreshDeckCount(TemplateData t)
        {
            if (_deckCountLabel != null && GodotObject.IsInstanceValid(_deckCountLabel))
                _deckCountLabel.Text = Loc.Fmt("tmpl.card_deck", t.CardIds.Count);
        }

        private static void RefreshAllFields(TemplateData t)
        {
            if (_maxHpEdit != null && GodotObject.IsInstanceValid(_maxHpEdit)) _maxHpEdit.Text = t.MaxHp.ToString();
            if (_curHpEdit != null && GodotObject.IsInstanceValid(_curHpEdit)) _curHpEdit.Text = t.CurHp.ToString();
            if (_energyEdit != null && GodotObject.IsInstanceValid(_energyEdit)) _energyEdit.Text = t.Energy.ToString();
            if (_goldEdit != null && GodotObject.IsInstanceValid(_goldEdit)) _goldEdit.Text = t.Gold.ToString();
        }

        private static void AddInfoRow(VBoxContainer parent, string label, string value, Action<string> onChanged, out LineEdit? outEdit)
        {
            outEdit = null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(120, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
            var edit = new LineEdit { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 32) };
            edit.AddThemeFontSizeOverride("font_size", 14);
            edit.TextChanged += v => onChanged(v);
            row.AddChild(edit, false, Node.InternalMode.Disabled);
            outEdit = edit;
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }

        private static int ParseInt(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;

        private static void LoadTemplates()
        {
            _templates = TemplateStorage.LoadAll();
        }

        private static void SaveTemplates()
        {
            TemplateStorage.SaveAll(_templates);
        }

        private static void BuildCharacterRegistry()
        {
            if (_characterRegistry.Count > 0) return;
            var byId = new Dictionary<string, CharacterInfo>(StringComparer.Ordinal);
            var fallback = new[] {
                new CharacterInfo("CHARACTER.IRONCLAD", "Ironclad", 80, 3),
                new CharacterInfo("CHARACTER.SILENT",   "Silent",   70, 3),
                new CharacterInfo("CHARACTER.DEFECT",   "Defect",   65, 3),
                new CharacterInfo("CHARACTER.NECROBINDER", "Necrobinder", 72, 3),
                new CharacterInfo("CHARACTER.REGENT",   "Regent",   65, 3),
            };
            foreach (var f in fallback)
                byId[f.Id] = f;
            try
            {
                var allChars = ModelDb.AllCharacters;
                if (allChars != null)
                {
                    foreach (var ch in allChars)
                    {
                        var id = ch.Id?.Entry ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        string displayName = id;
                        try
                        {
                            var title = ch.Title;
                            if (title != null)
                            {
                                var formatted = title.GetFormattedText();
                                if (!string.IsNullOrWhiteSpace(formatted)) displayName = formatted;
                            }
                        }
                        catch { }

                        int maxHp = 0, energy = 3;
                        if (byId.TryGetValue(id, out var existing))
                        {
                            maxHp = existing.DefaultMaxHp;
                            energy = existing.DefaultEnergy;
                        }

                        byId[id] = new CharacterInfo(id, displayName, maxHp, energy > 0 ? energy : 3);
                    }
                }
            }
            catch (Exception ex) { GD.PrintErr("[MultiplayerTools] BuildCharacterRegistry failed: " + ex.Message); }

            foreach (var c in byId.Values.OrderBy(x => x.Id))
                _characterRegistry.Add(c);
        }

        private sealed record CharacterInfo(string Id, string DisplayName, int DefaultMaxHp, int DefaultEnergy);
    }
}
