using System;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MultiplayerTools.Tabs;

namespace MultiplayerTools
{
    /// <summary>
    /// Main panel UI. CanvasLayer=100, KaylaMod-style dark theme.
    /// 5 tabs: Templates / Save / Backup / Players / Character.
    /// </summary>
    internal static class MpPanel
    {
        // 5 tabs: Templates=0, Save=1, Backup=2, Players=3, Character=4
        private const int TAB_TEMPLATES = 0;
        private const int TAB_SAVE      = 1;
        private const int TAB_BACKUP    = 2;
        private const int TAB_PLAYERS   = 3;
        private const int TAB_CHARACTER = 4;

        private static CanvasLayer? _layer;
        private static CanvasLayer? _hoverTipLayer;
        private static VBoxContainer? _contentContainer;
        private static HBoxContainer? _contextBar;
        private static VBoxContainer? _mainVBox;
        private static PanelContainer? _panel;
        private static StyleBoxFlat? _panelStyleNormal;
        private static ColorRect? _divider;
        private static Button[]? _tabButtons;
        private static Label? _headerTitleLabel;
        private static int _activeTab;

        internal static bool IsOpen => _layer != null && GodotObject.IsInstanceValid(_layer) && _layer.Visible;

        internal static void Toggle()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            _layer.Visible = !_layer.Visible;
            if (_layer.Visible)
            {
                Loc.Reload();
                RefreshChromeTexts();
                RefreshContextBar();
                RefreshCurrentTab();
            }
        }

        internal static void Show()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            Loc.Reload();
            RefreshChromeTexts();
            _layer.Visible = true;
            RefreshContextBar();
            RefreshCurrentTab();
        }

        internal static void Hide()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.Visible = false;
        }

        internal static void ClearChildren(Node parent)
        {
            foreach (var child in parent.GetChildren(false))
            {
                parent.RemoveChild(child);
                child.QueueFree();
            }
        }

        internal static Button CreateTabButton(string text)
        {
            var btn = new Button { Text = text };
            Panel.Styles.ApplyTabButton(btn);
            return btn;
        }

        internal static Button CreateActionButton(string text, Color? fontColor = null)
        {
            var btn = new Button { Text = text };
            Panel.Styles.ApplyActionButton(btn, fontColor);
            return btn;
        }

        internal static Button CreateToggleButton(string text, bool enabled)
        {
            var btn = new Button { Text = text };
            Panel.Styles.ApplyToggleButton(btn, enabled);
            return btn;
        }

        internal static Label CreateSectionHeader(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 18);
            label.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            label.AddThemeColorOverride("font_outline_color", Panel.Styles.OutlineColor);
            label.AddThemeConstantOverride("outline_size", 4);
            return label;
        }

        internal static Player? GetLocalPlayer()
        {
            var rm = RunManager.Instance;
            return rm?.DebugOnlyGetState() is { } state ? LocalContext.GetMe(state) : null;
        }

        private static void RefreshChromeTexts()
        {
            if (_headerTitleLabel != null && GodotObject.IsInstanceValid(_headerTitleLabel))
                _headerTitleLabel.Text = Loc.Get("panel.title", "MultiplayerTools");
            if (_tabButtons == null) return;
            string[] keys = { "tab.templates", "tab.save", "tab.backup", "tab.players", "tab.character" };
            string[] fb = { "Templates", "Save", "Backup", "Players", "Character" };
            for (int i = 0; i < _tabButtons.Length && i < keys.Length; i++)
                _tabButtons[i].Text = Loc.Get(keys[i], fb[i]);
        }

        private static void Build()
        {
            Loc.Reload();
            _layer = new CanvasLayer { Layer = 100, Name = "MpPanel" };

            // Backdrop
            var backdrop = new ColorRect
            {
                Color = Panel.Styles.Backdrop,
                AnchorRight = 1, AnchorBottom = 1,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backdrop.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    Hide();
                    backdrop.GetViewport()?.SetInputAsHandled();
                }
            };
            _layer.AddChild(backdrop, false, Node.InternalMode.Disabled);

            // Main panel
            _panel = new PanelContainer
            {
                AnchorLeft = 0.08f, AnchorRight = 0.92f,
                AnchorTop = 0.05f, AnchorBottom = 0.95f,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _panelStyleNormal = new StyleBoxFlat
            {
                BgColor = Panel.Styles.PanelBg,
                ShadowSize = 0, ShadowColor = Colors.Transparent
            };
            _panelStyleNormal.SetBorderWidthAll(0);
            _panelStyleNormal.SetCornerRadiusAll(8);
            _panelStyleNormal.SetContentMarginAll(12);
            _layer.AddChild(_panel, false, Node.InternalMode.Disabled);
            _panel.AddThemeStyleboxOverride("panel", _panelStyleNormal);

            _mainVBox = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
            _mainVBox.AddThemeConstantOverride("separation", 8);
            _panel.AddChild(_mainVBox, false, Node.InternalMode.Disabled);

            // Header: title + tabs + close
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 4);
            _mainVBox.AddChild(header, false, Node.InternalMode.Disabled);

            _headerTitleLabel = new Label { Text = Loc.Get("panel.title", "MultiplayerTools") };
            _headerTitleLabel.AddThemeFontSizeOverride("font_size", 20);
            _headerTitleLabel.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            _headerTitleLabel.AddThemeColorOverride("font_outline_color", Panel.Styles.OutlineColor);
            _headerTitleLabel.AddThemeConstantOverride("outline_size", 4);
            _headerTitleLabel.CustomMinimumSize = new Vector2(200, 0);
            header.AddChild(_headerTitleLabel, false, Node.InternalMode.Disabled);

            var sep = new VSeparator { CustomMinimumSize = new Vector2(2, 0) };
            sep.AddThemeColorOverride("color", Panel.Styles.Divider);
            header.AddChild(sep, false, Node.InternalMode.Disabled);

            // Tab buttons
            string[] tabNames = new[]
            {
                Loc.Get("tab.templates", "Templates"),
                Loc.Get("tab.save",      "Save"),
                Loc.Get("tab.backup",    "Backup"),
                Loc.Get("tab.players",   "Players"),
                Loc.Get("tab.character",  "Character"),
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

            var closeBtn = CreateTabButton("X");
            closeBtn.CustomMinimumSize = new Vector2(40, 36);
            closeBtn.Pressed += Hide;
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // Divider
            _divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Panel.Styles.Divider, MouseFilter = Control.MouseFilterEnum.Ignore };
            _mainVBox.AddChild(_divider, false, Node.InternalMode.Disabled);

            // Scroll container
            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _mainVBox.AddChild(scroll, false, Node.InternalMode.Disabled);
            _contentContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _contentContainer.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_contentContainer, false, Node.InternalMode.Disabled);

            // HoverTip layer
            _hoverTipLayer = new CanvasLayer { Layer = 101, Name = "MpHoverTips" };
            NGame.Instance?.AddChild(_layer, false, Node.InternalMode.Disabled);
            NGame.Instance?.AddChild(_hoverTipLayer, false, Node.InternalMode.Disabled);

            _activeTab = 0;
            UpdateTabHighlights();
            RefreshChromeTexts();
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
            ClearChildren(_contentContainer);

            switch (_activeTab)
            {
                case TAB_TEMPLATES:
                    TemplatesTab.Build(_contentContainer);
                    break;
                case TAB_SAVE:
                    SaveTab.Build(_contentContainer);
                    break;
                case TAB_BACKUP:
                    BackupTab.Build(_contentContainer);
                    break;
                case TAB_PLAYERS:
                    PlayerTab.Build(_contentContainer);
                    break;
                case TAB_CHARACTER:
                    CharacterTab.Build(_contentContainer);
                    break;
            }
        }

        private static void RefreshContextBar()
        {
            if (_contextBar == null) return;
            ClearChildren(_contextBar);
            // Context bar is per-tab if needed
        }

        private static void UpdateTabHighlights()
        {
            if (_tabButtons == null) return;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var btn = _tabButtons[i];
                btn.AddThemeColorOverride("font_color", i == _activeTab ? Panel.Styles.Gold : Panel.Styles.Cream);
                btn.AddThemeColorOverride("font_hover_color", Panel.Styles.Gold);
            }
        }
    }
}
