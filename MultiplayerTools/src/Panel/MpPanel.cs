using System;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MultiplayerTools.Tabs;
using MultiplayerTools.Panel;

// Type alias — Godot's main loop cast to SceneTree for adding UI nodes
using NGame = Godot.SceneTree;

namespace MultiplayerTools
{
    /// <summary>
    /// Main panel UI. CanvasLayer=100. Layout matches MP_PlayerManager v2:
    /// top bar (title + status bar + close) + left nav + content area.
    ///
    /// Page routing via string keys:
    ///   save_select | takeover | add_player | remove_player | backup | settings
    ///
    /// Legacy tab indices (0-4) are mapped to page keys for backward compat.
    /// </summary>
    internal static class MpPanel
    {
        // ── Page keys (v2 layout) ─────────────────────────────────────────────────
        public const string PAGE_SAVE_SELECT    = "save_select";
        public const string PAGE_TAKEOVER       = "takeover";
        public const string PAGE_ADD_PLAYER     = "add_player";
        public const string PAGE_REMOVE_PLAYER  = "remove_player";
        public const string PAGE_BACKUP         = "backup";
        public const string PAGE_SETTINGS       = "settings";
        // Legacy mapping
        private const string PAGE_TEMPLATES     = "templates";
        private const string PAGE_PLAYERS_LEGACY= "players";

        private const int NavWidth = 200;

        private static CanvasLayer? _layer;
        private static CanvasLayer? _hoverTipLayer;
        private static VBoxContainer? _contentContainer;
        private static VBoxContainer? _mainVBox;
        private static PanelContainer? _panel;
        private static StyleBoxFlat? _panelStyleNormal;
        private static Label? _statusBar;
        private static Label? _headerTitleLabel;
        private static Label? _headerSubtitleLabel;
        private static Button[]? _navButtons;
        private static string _currentPage = PAGE_SAVE_SELECT;

        // Nav key → button index map
        private static readonly (string key, string locKey, string fallback)[] NavItems = new[]
        {
            (PAGE_SAVE_SELECT,   "nav.save_select",    "Save Select"),
            (PAGE_TAKEOVER,     "nav.takeover",       "Takeover"),
            (PAGE_ADD_PLAYER,   "nav.add_player",     "Add Player"),
            (PAGE_REMOVE_PLAYER,"nav.remove_player",  "Remove Player"),
            (PAGE_BACKUP,       "nav.backup",         "Backup"),
            (PAGE_SETTINGS,     "nav.settings",       "Settings"),
        };

        internal static bool IsOpen => _layer != null && GodotObject.IsInstanceValid(_layer) && _layer.Visible;

        internal static void Toggle()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            _layer.Visible = !_layer.Visible;
            if (_layer.Visible)
            {
                Loc.Reload();
                RefreshChromeTexts();
                RefreshStatusBar();
                RefreshCurrentPage();
            }
        }

        internal static void Show()
        {
            if (_layer == null || !GodotObject.IsInstanceValid(_layer)) { Build(); return; }
            Loc.Reload();
            RefreshChromeTexts();
            _layer.Visible = true;
            RefreshStatusBar();
            RefreshCurrentPage();
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

        /// <summary>Switch to a page by key string (v2 routing).</summary>
        internal static void SwitchPage(string pageKey)
        {
            if (string.IsNullOrEmpty(pageKey)) return;
            _currentPage = pageKey;
            UpdateNavHighlights();
            RefreshCurrentPage();
        }

        /// <summary>Switch to a tab by legacy index (0-4). Maps to page key.</summary>
        internal static void SwitchToTab(int idx)
        {
            string? key = idx switch
            {
                0 => PAGE_SAVE_SELECT,   // Save → Save Select
                1 => PAGE_SAVE_SELECT,   // Templates → Save Select
                2 => PAGE_BACKUP,        // Backup
                3 => PAGE_PLAYERS_LEGACY,// Players (legacy warning)
                4 => PAGE_TAKEOVER,      // Character → Takeover
                _ => null
            };
            if (key != null) SwitchPage(key);
        }

        // Legacy tab indices (kept for backward compat with old tabs)
        public const int TAB_TEMPLATES = 0;
        public const int TAB_SAVE      = 1;
        public const int TAB_BACKUP    = 2;
        public const int TAB_PLAYERS   = 3;
        public const int TAB_CHARACTER = 4;

        /// <summary>Legacy alias for RefreshCurrentPage.</summary>
        internal static void RefreshCurrentTab() => RefreshCurrentPage();

        /// <summary>Legacy alias for RefreshStatusBar.</summary>
        internal static void RefreshContextBar() { } // no-op, context bar removed in v2 layout

        /// <summary>Force a full re-render of the current page.</summary>
        internal static void RefreshCurrentPage()
        {
            if (_contentContainer == null || _mainVBox == null) return;
            ClearChildren(_contentContainer);

            switch (_currentPage)
            {
                case PAGE_SAVE_SELECT:
                    SaveSelectPage.Build(_contentContainer);
                    break;
                case PAGE_TAKEOVER:
                    TakeoverPage.Build(_contentContainer);
                    break;
                case PAGE_ADD_PLAYER:
                    AddPlayerPage.Build(_contentContainer);
                    break;
                case PAGE_REMOVE_PLAYER:
                    RemovePlayerPage.Build(_contentContainer);
                    break;
                case PAGE_BACKUP:
                    BackupPage.Build(_contentContainer);
                    break;
                case PAGE_SETTINGS:
                    SettingsPage.Build(_contentContainer);
                    break;
                case PAGE_TEMPLATES:
                case PAGE_PLAYERS_LEGACY:
                    SaveSelectPage.Build(_contentContainer);
                    break;
            }
        }

        /// <summary>
        /// Full refresh: applies the current font step to chrome elements (header, nav, status bar)
        /// then rebuilds the current page content. Use after runtime changes like font step.
        /// </summary>
        internal static void FullRefresh()
        {
            if (_mainVBox == null) return;
            RefreshChromeTexts();
            RefreshStatusBar();
            RefreshCurrentPage();
        }

        /// <summary>Show a temporary status message overlay. Used by other tabs.</summary>
        internal static void ShowStatusMessage(string text, Color color)
        {
            if (_mainVBox == null) return;
            var msg = new Label { Text = text };
            UiFont.ApplyTo(msg, 14);
            msg.AddThemeColorOverride("font_color", color);
            _mainVBox.AddChild(msg, false, Node.InternalMode.Disabled);
        }

        internal static Label CreateSectionHeader(string text)
        {
            var label = new Label { Text = text };
            UiFont.ApplyTo(label, 18);
            label.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            label.AddThemeColorOverride("font_outline_color", Panel.Styles.OutlineColor);
            label.AddThemeConstantOverride("outline_size", 2);
            return label;
        }

        internal static Player? GetLocalPlayer()
        {
            var rm = RunManager.Instance;
            return rm?.DebugOnlyGetState() is { } state ? LocalContext.GetMe(state) : null;
        }

        // ── Status bar (mirrors v2 App._status_bar) ─────────────────────────────

        internal static void RefreshStatusBar()
        {
            if (_statusBar == null || !GodotObject.IsInstanceValid(_statusBar)) return;
            _statusBar.Text = MpSessionState.GetStatusText();
        }

        private static void RefreshChromeTexts()
        {
            if (_headerTitleLabel != null && GodotObject.IsInstanceValid(_headerTitleLabel))
                _headerTitleLabel.Text = Loc.Get("panel.title", "MultiplayerTools");
            if (_headerSubtitleLabel != null && GodotObject.IsInstanceValid(_headerSubtitleLabel))
                _headerSubtitleLabel.Text = Loc.Get("panel.subtitle", "Saves · Templates · Backups");

            // Refresh chrome font sizes with current UiFontStep (AddThemeFontSizeOverride replaces previous override)
            if (_headerTitleLabel != null && GodotObject.IsInstanceValid(_headerTitleLabel))
            {
                _headerTitleLabel.AddThemeFontSizeOverride("font_size", UiFont.Scaled(19));
            }
            if (_headerSubtitleLabel != null && GodotObject.IsInstanceValid(_headerSubtitleLabel))
            {
                _headerSubtitleLabel.AddThemeFontSizeOverride("font_size", UiFont.Scaled(17));
            }
            if (_statusBar != null && GodotObject.IsInstanceValid(_statusBar))
            {
                _statusBar.AddThemeFontSizeOverride("font_size", UiFont.Scaled(17));
            }

            // Refresh nav button labels
            if (_navButtons == null) return;
            for (int i = 0; i < NavItems.Length && i < _navButtons.Length; i++)
            {
                _navButtons[i].Text = Loc.Get(NavItems[i].locKey, NavItems[i].fallback);
                _navButtons[i].AddThemeFontSizeOverride("font_size", UiFont.Scaled(17));
            }
        }

        private static void UpdateNavHighlights()
        {
            if (_navButtons == null) return;
            for (int i = 0; i < NavItems.Length && i < _navButtons.Length; i++)
                Panel.Styles.ApplyNavTabButton(_navButtons[i], NavItems[i].key == _currentPage);
        }

        private static void Build()
        {
            Loc.Reload();

            // Subscribe to session events
            MpSessionState.SaveContextChanged += OnSessionSaveChanged;
            MpSessionState.ProfilesChanged += OnSessionProfilesChanged;

            _layer = new CanvasLayer { Layer = 200, Name = "MpPanel" };
            // CanvasLayer must be in the scene tree to render
            (Engine.GetMainLoop() as SceneTree)?.Root?.AddChild(_layer, false, Node.InternalMode.Disabled);

            // Backdrop — must use MouseFilter = Ignore so clicks on the panel area
            // reach the panel's children (buttons etc.). Empty-area close is handled via
            // the panel's GuiInput below, which checks if the click was inside or outside.
            var backdrop = new ColorRect
            {
                AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
                OffsetRight = 0, OffsetBottom = 0,
                Color = Panel.Styles.Backdrop,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            backdrop.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    // Close only when clicking the true empty area (outside the panel).
                    if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;
                    var panelRect = _panel.GetGlobalRect();
                    var clickPos = mb.GlobalPosition;
                    if (!panelRect.HasPoint(clickPos))
                    {
                        Hide();
                        backdrop.GetViewport()?.SetInputAsHandled();
                    }
                }
            };
            _layer.AddChild(backdrop, false, Node.InternalMode.Disabled);

            // Main panel — anchored at 8..92% of screen (centered)
            _panel = new PanelContainer
            {
                AnchorLeft = 0.08f, AnchorRight = 0.92f,
                AnchorTop = 0.05f, AnchorBottom = 0.95f,
                OffsetRight = 0, OffsetBottom = 0,
                GrowHorizontal = Control.GrowDirection.Both,
                GrowVertical = Control.GrowDirection.Both,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _panelStyleNormal = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpContentBg,
                ShadowSize = 0, ShadowColor = Colors.Transparent
            };
            _panelStyleNormal.SetBorderWidthAll(0);
            _panelStyleNormal.SetCornerRadiusAll(10);
            _panelStyleNormal.SetContentMarginAll(0);
            _layer.AddChild(_panel, false, Node.InternalMode.Disabled);
            _panel.AddThemeStyleboxOverride("panel", _panelStyleNormal);

            _mainVBox = new VBoxContainer
            {
                AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
                OffsetRight = 0, OffsetBottom = 0
            };
            _mainVBox.AddThemeConstantOverride("separation", 0);
            _panel.AddChild(_mainVBox, false, Node.InternalMode.Disabled);

            // ── Top bar (MP v2: #1A2A3A) ─────────────────────────────────────
            var topBar = new PanelContainer { CustomMinimumSize = new Vector2(0, 52) };
            var topBarStyle = new StyleBoxFlat { BgColor = Panel.Styles.MpTopBar, ShadowSize = 0, ShadowColor = Colors.Transparent };
            topBarStyle.SetBorderWidthAll(0);
            topBarStyle.SetCornerRadiusAll(0);
            topBar.AddThemeStyleboxOverride("panel", topBarStyle);
            _mainVBox.AddChild(topBar, false, Node.InternalMode.Disabled);

            var topMargin = new MarginContainer();
            topMargin.AddThemeConstantOverride("margin_left", 16);
            topMargin.AddThemeConstantOverride("margin_right", 12);
            topMargin.AddThemeConstantOverride("margin_top", 6);
            topMargin.AddThemeConstantOverride("margin_bottom", 6);
            topBar.AddChild(topMargin, false, Node.InternalMode.Disabled);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            header.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            topMargin.AddChild(header, false, Node.InternalMode.Disabled);

            var titleCol = new VBoxContainer();
            titleCol.AddThemeConstantOverride("separation", 2);
            header.AddChild(titleCol, false, Node.InternalMode.Disabled);

            _headerTitleLabel = new Label { Text = Loc.Get("panel.title", "MultiplayerTools") };
            _headerTitleLabel.AddThemeFontSizeOverride("font_size", 19);
            _headerTitleLabel.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            _headerTitleLabel.AddThemeColorOverride("font_outline_color", Panel.Styles.OutlineColor);
            _headerTitleLabel.AddThemeConstantOverride("outline_size", 2);
            titleCol.AddChild(_headerTitleLabel, false, Node.InternalMode.Disabled);

            _headerSubtitleLabel = new Label
            {
                Text = Loc.Get("panel.subtitle", "Saves · Templates · Backups")
            };
            _headerSubtitleLabel.AddThemeFontSizeOverride("font_size", 17);
            _headerSubtitleLabel.AddThemeColorOverride("font_color", Panel.Styles.MpBlueAccent);
            titleCol.AddChild(_headerSubtitleLabel, false, Node.InternalMode.Disabled);

            // Version + author link row
            var verRow = new HBoxContainer();
            verRow.AddThemeConstantOverride("separation", 6);
            verRow.AddThemeConstantOverride("custom_minimum_size_y", 24);
            titleCol.AddChild(verRow, false, Node.InternalMode.Disabled);

            var verLbl = new Label { Text = $"v{ModInfo.Version}" };
            verLbl.AddThemeFontSizeOverride("font_size", 17);
            verLbl.AddThemeColorOverride("font_color", Panel.Styles.MpBlueAccent);
            verRow.AddChild(verLbl, false, Node.InternalMode.Disabled);

            var authorLink = new LinkButton
            {
                Text = "@ 我叫煎包",
                Uri = "https://space.bilibili.com/234054413",
                Underline = LinkButton.UnderlineMode.Always
            };
            authorLink.AddThemeFontSizeOverride("font_size", 17);
            authorLink.AddThemeColorOverride("font_color", Panel.Styles.MpBlueAccent);
            verRow.AddChild(authorLink, false, Node.InternalMode.Disabled);

            // Warning row
            var warningLbl = new Label
            {
                Text = Loc.Get("panel.warning", "⚠ Only use on the main menu — never during gameplay"),
                CustomMinimumSize = new Vector2(0, 22)
            };
            warningLbl.AddThemeFontSizeOverride("font_size", 14);
            warningLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            titleCol.AddChild(warningLbl, false, Node.InternalMode.Disabled);

            header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            // GitHub repo link button
            var repoLink = new LinkButton
            {
                Text = "GitHub",
                Uri = "https://github.com/Jianbao233/STS2_mod",
                Underline = LinkButton.UnderlineMode.Never
            };
            repoLink.AddThemeFontSizeOverride("font_size", 16);
            repoLink.AddThemeColorOverride("font_color", Panel.Styles.MpBlueAccent);
            header.AddChild(repoLink, false, Node.InternalMode.Disabled);

            // Status bar (mirrors v2 _status_bar)
            _statusBar = new Label { Text = Loc.Get("status.scanning", "Scanning saves...") };
            _statusBar.AddThemeFontSizeOverride("font_size", 17);
            _statusBar.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            header.AddChild(_statusBar, false, Node.InternalMode.Disabled);

            header.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) }, false, Node.InternalMode.Disabled);

            var closeBtn = new Button { Text = "×" };
            Panel.Styles.ApplyCloseButton(closeBtn);
            closeBtn.Pressed += Hide;
            header.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // ── Body: left nav (#111827) + content ──────────────────────────
            var body = new HBoxContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            body.AddThemeConstantOverride("separation", 0);
            _mainVBox.AddChild(body, false, Node.InternalMode.Disabled);

            var navPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(NavWidth, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            var navPanelStyle = new StyleBoxFlat { BgColor = Panel.Styles.MpNavBg, ShadowSize = 0, ShadowColor = Colors.Transparent };
            navPanelStyle.SetBorderWidthAll(0);
            navPanelStyle.SetCornerRadiusAll(0);
            navPanel.AddThemeStyleboxOverride("panel", navPanelStyle);
            body.AddChild(navPanel, false, Node.InternalMode.Disabled);

            var navMargin = new MarginContainer();
            navMargin.AddThemeConstantOverride("margin_left", 10);
            navMargin.AddThemeConstantOverride("margin_right", 10);
            navMargin.AddThemeConstantOverride("margin_top", 14);
            navMargin.AddThemeConstantOverride("margin_bottom", 14);
            navPanel.AddChild(navMargin, false, Node.InternalMode.Disabled);

            var navVBox = new VBoxContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            navVBox.AddThemeConstantOverride("separation", 4);
            navMargin.AddChild(navVBox, false, Node.InternalMode.Disabled);

            // Nav label "MENU"
            var navLabel = new Label { Text = Loc.Get("nav.menu", "MENU") };
            navLabel.AddThemeFontSizeOverride("font_size", 16);
            navLabel.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            navVBox.AddChild(navLabel, false, Node.InternalMode.Disabled);

            // Build nav buttons (v2 keys)
            _navButtons = new Button[NavItems.Length];
            for (int i = 0; i < NavItems.Length; i++)
            {
                int idx = i;
                var (key, locKey, fallback) = NavItems[i];
                var btn = new Button { Text = Loc.Get(locKey, fallback) };
                Panel.Styles.ApplyNavTabButton(btn, key == _currentPage);
                btn.Pressed += () => SwitchPage(key);
                navVBox.AddChild(btn, false, Node.InternalMode.Disabled);
                _navButtons[i] = btn;
            }

            navVBox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            // Separator
            var vSep = new ColorRect
            {
                CustomMinimumSize = new Vector2(2, 0),
                Color = Panel.Styles.MpSeparator,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            body.AddChild(vSep, false, Node.InternalMode.Disabled);

            var rightPanel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            var rightStyle = new StyleBoxFlat { BgColor = Panel.Styles.MpContentBg, ShadowSize = 0, ShadowColor = Colors.Transparent };
            rightStyle.SetBorderWidthAll(0);
            rightStyle.SetCornerRadiusAll(0);
            rightStyle.SetContentMarginAll(10);
            rightPanel.AddThemeStyleboxOverride("panel", rightStyle);
            body.AddChild(rightPanel, false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            rightPanel.AddChild(scroll, false, Node.InternalMode.Disabled);

            _contentContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _contentContainer.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_contentContainer, false, Node.InternalMode.Disabled);

            // HoverTip layer
            _hoverTipLayer = new CanvasLayer { Layer = 201, Name = "MpHoverTips" };
            (Engine.GetMainLoop() as SceneTree)?.Root?.AddChild(_hoverTipLayer, false, Node.InternalMode.Disabled);

            // Initial data load
            MpSessionState.RefreshProfiles();
            RefreshChromeTexts();
            RefreshStatusBar();
            UpdateNavHighlights();
            RefreshCurrentPage();
        }

        private static void _DebugLogPanelSize()
        {
            if (_panel != null)
                GD.Print($"[MultiplayerTools] DEBUG _panel.Size={_panel.Size} _panel.GlobalRect={_panel.GetGlobalRect()} _panel.Visible={_panel.Visible}");
        }

        private static void OnSessionSaveChanged()
        {
            RefreshStatusBar();
            RefreshCurrentPage();
        }

        private static void OnSessionProfilesChanged()
        {
            RefreshStatusBar();
            if (_currentPage == PAGE_SAVE_SELECT)
                RefreshCurrentPage();
        }
    }
}
