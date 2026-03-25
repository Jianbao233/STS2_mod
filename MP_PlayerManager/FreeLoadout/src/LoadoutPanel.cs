using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;
using MP_PlayerManager.Tabs;

namespace MP_PlayerManager
{
    /// <summary>
    /// 主面板 UI 壳。管理标签页切换、嵌入屏幕、上下文工具栏和公共 UI 辅助方法。
    /// </summary>
    internal static class LoadoutPanel
    {
        // === 游戏风格颜色（替代 StsColors） ===
        private static class SC
        {
            internal static readonly Color Gold    = new Color("E3A83D");
            internal static readonly Color Cream   = new Color("E3D5C1");
            internal static readonly Color Red     = new Color("C0392B");
            internal static readonly Color Blue    = new Color("2980B9");
            internal static readonly Color Green   = new Color("27AE60");
            internal static readonly Color Gray    = new Color("7F8C8D");
        }

        // === 公开状态属性 ===
        internal static bool CardsShowUpgraded { get; set; }
        internal static bool ShowMyCards { get; set; }
        internal static int RelicBatchCount { get; set; } = 1;
        internal static int PowerBatchCount { get; set; } = 1;

        internal static bool IsEmbeddedScreenActive
        {
            get
            {
                if (_relicScreen != null && _relicScreen.Visible) return true;
                if (_cardScreen != null && _cardScreen.Visible) return true;
                if (_potionScreen != null && _potionScreen.Visible) return true;
                return false;
            }
        }

        internal static bool IsOpen => _layer != null && GodotObject.IsInstanceValid(_layer) && _layer.Visible;

        // === 生命周期 ===
        internal static void Toggle()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            _layer.Visible = !_layer.Visible;
            if (_layer.Visible) { RefreshContextBar(); RefreshCurrentTab(); }
        }

        internal static void Show()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            _layer.Visible = true;
            RefreshContextBar();
            RefreshCurrentTab();
        }

        internal static void Hide()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.Visible = false;
        }

        internal static void HideForInspect()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.Visible = false;
            if (_hoverTipLayer != null && GodotObject.IsInstanceValid(_hoverTipLayer)) _hoverTipLayer.Visible = false;
        }

        internal static void ShowAfterInspect()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.Visible = true;
            if (_hoverTipLayer != null && GodotObject.IsInstanceValid(_hoverTipLayer)) _hoverTipLayer.Visible = true;
            RefreshCurrentTab();
        }

        internal static void ReparentToHoverTipLayer(Node tipSet)
        {
            if (_hoverTipLayer == null || !GodotObject.IsInstanceValid(_hoverTipLayer) || tipSet.GetParent() == _hoverTipLayer) return;
            tipSet.GetParent()?.RemoveChild(tipSet);
            _hoverTipLayer.AddChild(tipSet, false, Node.InternalMode.Disabled);
        }

        // === HoverTip ===
        internal static void ShowHoverTip(Control owner, IHoverTip tip, HoverTipAlignment alignment = HoverTipAlignment.Right)
        {
            ShowHoverTips(owner, new IHoverTip[] { tip }, alignment);
        }

        internal static void ShowHoverTips(Control owner, IEnumerable<IHoverTip> tips, HoverTipAlignment alignment = HoverTipAlignment.Right)
        {
            try
            {
                var nhoverTipSet = NHoverTipSet.CreateAndShow(owner, tips, alignment);
                if (_hoverTipLayer != null && GodotObject.IsInstanceValid(_hoverTipLayer) && nhoverTipSet != null && GodotObject.IsInstanceValid(nhoverTipSet))
                {
                    nhoverTipSet.GetParent()?.RemoveChild(nhoverTipSet);
                    _hoverTipLayer.AddChild(nhoverTipSet, false, Node.InternalMode.Disabled);
                }
            }
            catch { }
        }

        // === 游戏上下文 ===
        internal static Player? GetPlayer()
        {
            var rm = RunManager.Instance;
            return rm?.DebugOnlyGetState() is { } state ? LocalContext.GetMe(state) : null;
        }

        internal static void RequestRefresh()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer) && _layer.Visible) RefreshCurrentTab();
        }

        // === UI 辅助方法 ===
        internal static void ClearChildren(Node parent)
        {
            foreach (var child in parent.GetChildren(false))
            {
                parent.RemoveChild(child);
                child.QueueFree();
            }
        }

        internal static Label CreateSectionHeader(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 18);
            label.AddThemeColorOverride("font_color", SC.Gold);
            label.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
            label.AddThemeConstantOverride("outline_size", 4);
            var font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
            if (font != null) label.AddThemeFontOverride("font", font);
            return label;
        }

        internal static Button CreateItemButton(string text, Vector2? minSize = null, int fontSize = 14)
        {
            var btn = new Button { Text = text };
            btn.CustomMinimumSize = minSize ?? new Vector2(0, 32);
            btn.AddThemeFontSizeOverride("font_size", fontSize);
            btn.AddThemeColorOverride("font_color", SC.Cream);
            btn.AddThemeColorOverride("font_hover_color", SC.Gold);
            btn.AddThemeColorOverride("font_pressed_color", SC.Gray);
            btn.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
            btn.AddThemeConstantOverride("outline_size", 4);
            ApplyFlatStyle(btn);
            return btn;
        }

        internal static Button CreateActionButton(string text, Color? fontColor = null)
        {
            var btn = new Button { Text = text };
            btn.CustomMinimumSize = new Vector2(60, 28);
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.AddThemeColorOverride("font_color", fontColor ?? SC.Cream);
            btn.AddThemeColorOverride("font_hover_color", SC.Gold);
            btn.AddThemeColorOverride("font_pressed_color", SC.Gray);
            ApplyFlatStyle(btn);
            return btn;
        }

        internal static Button CreateToggleButton(string text, bool enabled)
        {
            var btn = CreateTabButton(text);
            btn.CustomMinimumSize = new Vector2(100, 36);
            UpdateToggleButton(btn, enabled);
            return btn;
        }

        internal static void UpdateToggleButton(Button btn, bool enabled)
        {
            if (enabled)
                btn.AddThemeStyleboxOverride("normal", CreateStyleBox(new Color(0.15f, 0.25f, 0.15f, 0.9f), new Color(0.3f, 0.6f, 0.3f, 0.7f)));
            else
                btn.AddThemeStyleboxOverride("normal", CreateStyleBox(new Color(0.12f, 0.1f, 0.15f, 0.85f), new Color(0.35f, 0.3f, 0.25f, 0.5f)));
        }

        // === 内部 UI 构建 ===
        private static Button CreateTabButton(string text)
        {
            var btn = new Button { Text = text };
            btn.CustomMinimumSize = new Vector2(80, 36);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", SC.Cream);
            btn.AddThemeColorOverride("font_hover_color", SC.Gold);
            btn.AddThemeColorOverride("font_pressed_color", SC.Gray);
            btn.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
            btn.AddThemeConstantOverride("outline_size", 4);
            var font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
            if (font != null) btn.AddThemeFontOverride("font", font);
            ApplyFlatStyle(btn);
            return btn;
        }

        internal static void ApplyFlatStyle(Button btn)
        {
            btn.AddThemeStyleboxOverride("normal", CreateStyleBox(new Color(0.12f, 0.1f, 0.15f, 0.85f), new Color(0.35f, 0.3f, 0.25f, 0.5f)));
            btn.AddThemeStyleboxOverride("hover", CreateStyleBox(new Color(0.18f, 0.15f, 0.22f, 0.92f), SC.Gold));
            btn.AddThemeStyleboxOverride("pressed", CreateStyleBox(new Color(0.08f, 0.06f, 0.1f, 0.95f), new Color("B89840")));
            btn.AddThemeStyleboxOverride("focus", CreateStyleBox(new Color(0.18f, 0.15f, 0.22f, 0.92f), SC.Gold));
        }

        private static StyleBoxFlat CreateStyleBox(Color bg, Color border)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            sb.SetBorderWidthAll(2);
            sb.SetCornerRadiusAll(6);
            sb.SetContentMarginAll(6);
            return sb;
        }

        private static void Build()
        {
            _layer = new CanvasLayer { Layer = 100, Name = "LoadoutPanel" };

            // 半透明遮罩
            var backstop = new ColorRect
            {
                Color = new Color(0, 0, 0, 0.5f),
                AnchorRight = 1, AnchorBottom = 1,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backstop.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    Hide();
                    backstop.GetViewport()?.SetInputAsHandled();
                }
            };
            _layer.AddChild(backstop, false, Node.InternalMode.Disabled);

            // 主面板
            _panel = new PanelContainer
            {
                AnchorLeft = 0.05f, AnchorRight = 0.95f,
                AnchorTop = 0.05f, AnchorBottom = 0.95f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _panelStyleNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.06f, 0.1f, 0.95f),
                ShadowSize = 0, ShadowColor = Colors.Transparent
            };
            _panelStyleNormal.SetBorderWidthAll(0);
            _panelStyleNormal.SetCornerRadiusAll(8);
            _panelStyleNormal.SetContentMarginAll(12);
            _panelStyleNormal.SetExpandMarginAll(0);
            _panel.AddThemeStyleboxOverride("panel", _panelStyleNormal);
            _panelStyleClear = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowSize = 0, ShadowColor = Colors.Transparent };
            _panelStyleClear.SetBorderWidthAll(0);
            _panelStyleClear.SetContentMarginAll(0);
            _panelStyleClear.SetExpandMarginAll(0);
            _layer.AddChild(_panel, false, Node.InternalMode.Disabled);

            _mainVBox = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
            _mainVBox.AddThemeConstantOverride("separation", 8);
            _panel.AddChild(_mainVBox, false, Node.InternalMode.Disabled);

            // 标签栏
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 4);
            _mainVBox.AddChild(header, false, Node.InternalMode.Disabled);

            string[] tabNames = new[]
            {
                Loc.Get("tab.cards", "Cards"),
                Loc.Get("tab.relics", "Relics"),
                Loc.Get("tab.potions", "Potions"),
                Loc.Get("tab.powers", "Powers"),
                Loc.Get("tab.events", "Events"),
                Loc.Get("tab.encounters", "Encounters"),
                Loc.Get("tab.character", "Character"),
                Loc.Get("tab.templates", "Templates")
            };
            _tabButtons = new Button[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                int idx = i;
                var btn = CreateTabButton(tabNames[i]);
                btn.Pressed += () => SwitchTab(idx);
                header.AddChild(btn, false, Node.InternalMode.Disabled);
                _tabButtons[i] = btn;
            }
            header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            _contextBar = new HBoxContainer();
            _contextBar.AddThemeConstantOverride("separation", 4);
            header.AddChild(_contextBar, false, Node.InternalMode.Disabled);

            var closeBtn = CreateTabButton("✕");
            closeBtn.CustomMinimumSize = new Vector2(40, 36);
            closeBtn.Pressed += Hide;
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // 分隔线
            _divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = new Color(0.91f, 0.86f, 0.75f, 0.25f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _mainVBox.AddChild(_divider, false, Node.InternalMode.Disabled);

            // 滚动容器
            _scrollContainer = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _mainVBox.AddChild(_scrollContainer, false, Node.InternalMode.Disabled);
            _contentContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _contentContainer.AddThemeConstantOverride("separation", 6);
            _scrollContainer.AddChild(_contentContainer, false, Node.InternalMode.Disabled);

            // HoverTip 层
            _hoverTipLayer = new CanvasLayer { Layer = 101, Name = "LoadoutHoverTips" };

            NGame.Instance?.AddChild(_layer, false, Node.InternalMode.Disabled);
            NGame.Instance?.AddChild(_hoverTipLayer, false, Node.InternalMode.Disabled);

            _activeTab = 0;
            UpdateTabHighlights();
            RefreshContextBar();
            RefreshCurrentTab();
        }

        private static void SwitchTab(int idx)
        {
            _activeTab = idx;
            UpdateTabHighlights();
            RefreshContextBar();
            RefreshCurrentTab();
        }

        private static void RefreshCurrentTab()
        {
            if (_contentContainer == null || _mainVBox == null) return;

            switch (_activeTab)
            {
                case 0: // Cards
                    if (ShowMyCards)
                    {
                        HideEmbeddedScreens();
                        ClearChildren(_contentContainer);
                        var player = GetPlayer();
                        if (player != null) BuildMyCardsView(_contentContainer, player);
                    }
                    else
                    {
                        ShowEmbeddedCardScreen();
                    }
                    return;
                case 1: // Relics
                    ShowEmbeddedRelicScreen();
                    return;
                case 2: // Potions
                    ShowEmbeddedPotionScreen();
                    return;
                default: // Powers / Events / Encounters / Character / Templates
                    HideEmbeddedScreens();
                    ClearChildren(_contentContainer);
                    // 角色模板仅依赖 ModelDb + 本地 JSON，主菜单即可编辑，不要求局内 Player
                    if (_activeTab == 7)
                    {
                        TemplatesTab.Build(_contentContainer, null);
                        return;
                    }
                    var p = GetPlayer();
                    if (p == null)
                    {
                        var lbl = new Label { Text = Loc.Get("not_in_game", "Not in a run") };
                        lbl.AddThemeFontSizeOverride("font_size", 20);
                        lbl.AddThemeColorOverride("font_color", SC.Cream);
                        _contentContainer.AddChild(lbl, false, Node.InternalMode.Disabled);
                        return;
                    }
                    switch (_activeTab)
                    {
                        case 3: PowersTab.Build(_contentContainer, p); break;
                        case 4: EventsTab.Build(_contentContainer, p); break;
                        case 5: EncountersTab.Build(_contentContainer, p); break;
                        case 6: CharacterTab.Build(_contentContainer, p); break;
                    }
                    return;
            }
        }

        private static void RefreshContextBar()
        {
            if (_contextBar == null) return;
            ClearChildren(_contextBar);

            switch (_activeTab)
            {
                case 0:
                    var myCardsToggle = CreateToggleButton(Loc.Get("pile.my_cards", "My Cards"), ShowMyCards);
                    myCardsToggle.CustomMinimumSize = new Vector2(90, 36);
                    myCardsToggle.Pressed += () =>
                    {
                        ShowMyCards = !ShowMyCards;
                        UpdateToggleButton(myCardsToggle, ShowMyCards);
                        RefreshCurrentTab();
                    };
                    _contextBar.AddChild(myCardsToggle, false, Node.InternalMode.Disabled);
                    return;
                case 1: BuildBatchBar("relics"); return;
                case 3: BuildBatchBar("powers"); BuildPowersPresetBar(); break;
            }
        }

        private static void BuildMyCardsView(VBoxContainer container, Player player)
        {
            var cm = CombatManager.Instance;
            if (cm != null && cm.IsInProgress)
            {
                BuildMyCardsPileGroup(container, player, PileType.Hand, Loc.Get("pile.hand", "Hand"));
                BuildMyCardsPileGroup(container, player, PileType.Draw, Loc.Get("pile.draw", "Draw"));
                BuildMyCardsPileGroup(container, player, PileType.Discard, Loc.Get("pile.discard", "Discard"));
                BuildMyCardsPileGroup(container, player, PileType.Exhaust, Loc.Get("pile.exhaust", "Exhaust"));
            }
            BuildMyCardsPileGroup(container, player, PileType.Deck, Loc.Get("pile.deck", "Deck"));
        }

        private static void BuildMyCardsPileGroup(VBoxContainer container, Player player, PileType pileType, string label)
        {
            var pile = CardPile.Get(pileType, player);
            if (pile == null) return;

            var cards = pile.Cards;
            container.AddChild(CreateSectionHeader($"{label} ({cards.Count})"), false, Node.InternalMode.Disabled);

            if (cards.Count == 0)
            {
                var empty = new Label { Text = Loc.Get("empty", "Empty") };
                empty.AddThemeFontSizeOverride("font_size", 13);
                empty.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(empty, false, Node.InternalMode.Disabled);
                return;
            }

            var grouped = cards.GroupBy(c => c.Type).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                Color color = group.Key switch
                {
                    CardType.Attack => SC.Red,
                    CardType.Skill => SC.Blue,
                    CardType.Power => new Color("CC77FF"),
                    _ => SC.Gray
                };
                var typeLabel = new Label { Text = $"  {Loc.Get($"type.{group.Key}", group.Key.ToString())} ({group.Count()})" };
                typeLabel.AddThemeFontSizeOverride("font_size", 14);
                typeLabel.AddThemeColorOverride("font_color", color);
                container.AddChild(typeLabel, false, Node.InternalMode.Disabled);

                var grid = new GridContainer { Columns = 6, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                grid.AddThemeConstantOverride("h_separation", 4);
                grid.AddThemeConstantOverride("v_separation", 4);
                container.AddChild(grid, false, Node.InternalMode.Disabled);

                foreach (var card in group.OrderBy(c => c.Title))
                {
                    var wrap = CardsTab.CreateNCardWrapperPublic(card, pileType, cards.ToList());
                    grid.AddChild(wrap, false, Node.InternalMode.Disabled);
                }
            }
        }

        private static void BuildBatchBar(string target)
        {
            int count = target == "relics" ? RelicBatchCount : PowerBatchCount;
            var lbl = new Label { Text = Loc.Get("quantity", "Qty") };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.AddThemeColorOverride("font_color", SC.Cream);
            lbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            _contextBar!.AddChild(lbl, false, Node.InternalMode.Disabled);

            foreach (int n in new[] { 1, 5, 10, 20 })
            {
                int c = n;
                bool active = count == c;
                var btn = CreateToggleButton($"×{n}", active);
                btn.CustomMinimumSize = new Vector2(50, 36);
                btn.Pressed += () =>
                {
                    if (target == "relics") RelicBatchCount = c; else PowerBatchCount = c;
                    RefreshContextBar();
                    RequestRefresh();
                };
                _contextBar.AddChild(btn, false, Node.InternalMode.Disabled);
            }
        }

        private static void BuildPowersPresetBar()
        {
            var sep = new VSeparator { CustomMinimumSize = new Vector2(2, 0) };
            _contextBar!.AddChild(sep, false, Node.InternalMode.Disabled);

            var toggleBtn = CreateToggleButton(Loc.Get("presets", "Presets"), PowerPresets.Enabled);
            toggleBtn.CustomMinimumSize = new Vector2(70, 36);
            toggleBtn.Pressed += () =>
            {
                PowerPresets.Enabled = !PowerPresets.Enabled;
                UpdateToggleButton(toggleBtn, PowerPresets.Enabled);
            };
            _contextBar.AddChild(toggleBtn, false, Node.InternalMode.Disabled);

            var toPlayer = CreateToggleButton(Loc.Get("to_player", "To Player"), PowerPresets.PresetTarget == 0);
            toPlayer.CustomMinimumSize = new Vector2(80, 36);
            toPlayer.Pressed += () => { PowerPresets.PresetTarget = 0; RefreshContextBar(); RequestRefresh(); };
            _contextBar.AddChild(toPlayer, false, Node.InternalMode.Disabled);

            var toEnemy = CreateToggleButton(Loc.Get("to_enemy", "To Enemy"), PowerPresets.PresetTarget == 1);
            toEnemy.CustomMinimumSize = new Vector2(80, 36);
            toEnemy.Pressed += () => { PowerPresets.PresetTarget = 1; RefreshContextBar(); RequestRefresh(); };
            _contextBar.AddChild(toEnemy, false, Node.InternalMode.Disabled);
        }

        private static void UpdateTabHighlights()
        {
            if (_tabButtons == null) return;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var btn = _tabButtons[i];
                if (i == _activeTab)
                {
                    btn.AddThemeColorOverride("font_color", SC.Gold);
                    btn.AddThemeColorOverride("font_hover_color", SC.Gold);
                }
                else
                {
                    btn.AddThemeColorOverride("font_color", SC.Cream);
                    btn.AddThemeColorOverride("font_hover_color", SC.Gold);
                }
            }
        }

        // === 嵌入屏幕（非泛型，避免 ref lambda 问题） ===
        private static void ShowEmbeddedCardScreen()
        {
            HideEmbeddedScreens();
            if (_scrollContainer != null) _scrollContainer.Visible = false;

            if (_cardScreen == null || !GodotObject.IsInstanceValid(_cardScreen))
            {
                _cardScreen = NCardLibrary.Create();
                _cardScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                _cardScreen.Visible = false;
                _mainVBox!.AddChild(_cardScreen, false, Node.InternalMode.Disabled);
            }
            _cardScreen.Visible = true;
            try { _cardScreen.Call("OnSubmenuOpened"); } catch { }

            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(_cardScreen!)) return;
                _cardScreen!.GetNodeOrNull<Control>("BackButton")?.Hide();
                HideScreenShadows(_cardScreen!);
            }).CallDeferred();

            _panel?.AddThemeStyleboxOverride("panel", _panelStyleClear!);
            _divider?.Hide();
            SetTabButtonsChrome(false);
        }

        private static void ShowEmbeddedRelicScreen()
        {
            HideEmbeddedScreens();
            if (_scrollContainer != null) _scrollContainer.Visible = false;

            if (_relicScreen == null || !GodotObject.IsInstanceValid(_relicScreen))
            {
                _relicScreen = NRelicCollection.Create();
                _relicScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                _relicScreen.Visible = false;
                _mainVBox!.AddChild(_relicScreen, false, Node.InternalMode.Disabled);
            }
            _relicScreen.Visible = true;
            try { _relicScreen.Call("OnSubmenuOpened"); } catch { }

            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(_relicScreen!)) return;
                _relicScreen!.GetNodeOrNull<Control>("BackButton")?.Hide();
                HideScreenShadows(_relicScreen!);
            }).CallDeferred();

            _panel?.AddThemeStyleboxOverride("panel", _panelStyleClear!);
            _divider?.Hide();
            SetTabButtonsChrome(false);
        }

        private static void ShowEmbeddedPotionScreen()
        {
            HideEmbeddedScreens();
            if (_scrollContainer != null) _scrollContainer.Visible = false;

            if (_potionScreen == null || !GodotObject.IsInstanceValid(_potionScreen))
            {
                _potionScreen = NPotionLab.Create();
                _potionScreen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                _potionScreen.Visible = false;
                _mainVBox!.AddChild(_potionScreen, false, Node.InternalMode.Disabled);
            }
            _potionScreen.Visible = true;
            try { _potionScreen.Call("OnSubmenuOpened"); } catch { }

            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(_potionScreen!)) return;
                _potionScreen!.GetNodeOrNull<Control>("BackButton")?.Hide();
                HideScreenShadows(_potionScreen!);
            }).CallDeferred();

            _panel?.AddThemeStyleboxOverride("panel", _panelStyleClear!);
            _divider?.Hide();
            SetTabButtonsChrome(false);
        }

        private static void HideEmbeddedScreens()
        {
            Action<Control?> hide = s =>
            {
                if (s != null && s.Visible)
                {
                    try { s.Call("OnSubmenuClosed"); } catch { }
                    s.Visible = false;
                }
            };
            hide(_relicScreen);
            hide(_cardScreen);
            hide(_potionScreen);

            if (_scrollContainer != null) _scrollContainer.Visible = true;
            if (_panel != null && _panelStyleNormal != null) _panel.AddThemeStyleboxOverride("panel", _panelStyleNormal);
            _divider?.Show();
            SetTabButtonsChrome(true);
        }

        private static void SetTabButtonsChrome(bool visible)
        {
            if (_tabButtons == null) return;
            foreach (var btn in _tabButtons)
            {
                if (visible)
                {
                    ApplyFlatStyle(btn);
                    btn.AddThemeConstantOverride("outline_size", 4);
                }
                else
                {
                    var empty = new StyleBoxEmpty();
                    btn.AddThemeStyleboxOverride("normal", empty);
                    btn.AddThemeStyleboxOverride("hover", empty);
                    btn.AddThemeStyleboxOverride("pressed", empty);
                    btn.AddThemeStyleboxOverride("focus", empty);
                    btn.AddThemeConstantOverride("outline_size", 0);
                }
            }
            UpdateTabHighlights();
        }

        private static void HideScreenShadows(Node screen)
        {
            foreach (string pattern in new[] { "*Shadow*", "*shadow*", "*Gradient*", "*gradient*", "*Fade*", "*fade*", "*Vignette*", "*Darkener*" })
            {
                var child = screen.FindChild(pattern, true, false) as Control;
                if (child != null) child.Visible = false;
            }
            HideBottomTextures(screen);
        }

        private static void HideBottomTextures(Node parent)
        {
            foreach (var node in parent.GetChildren(false))
            {
                if (node is TextureRect tr && tr.AnchorTop >= 0.8f) { tr.Visible = false; continue; }
                if (node is Control ctrl)
                {
                    foreach (var child in ctrl.GetChildren(false))
                    {
                        if (child is TextureRect tr2 && tr2.AnchorTop >= 0.8f) tr2.Visible = false;
                    }
                }
            }
        }

        // === 私有字段 ===
        private static CanvasLayer? _layer;
        private static CanvasLayer? _hoverTipLayer;
        private static VBoxContainer? _contentContainer;
        private static ScrollContainer? _scrollContainer;
        private static HBoxContainer? _contextBar;
        private static VBoxContainer? _mainVBox;
        private static PanelContainer? _panel;
        private static StyleBoxFlat? _panelStyleNormal;
        private static StyleBoxFlat? _panelStyleClear;
        private static ColorRect? _divider;
        private static NRelicCollection? _relicScreen;
        private static NCardLibrary? _cardScreen;
        private static NPotionLab? _potionScreen;
        private static int _activeTab;
        private static Button[]? _tabButtons;
    }
}
