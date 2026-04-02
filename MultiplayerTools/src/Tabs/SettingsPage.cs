using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MultiplayerTools.Core;
using MultiplayerTools.Panel;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Settings page — mirrors v2 _page_settings.
    ///
    /// Layout:
    ///   [title]
    ///   → Language selection (radio buttons)
    ///   → Font size slider (0-6)
    ///   → Path info (read-only)
    ///   → About / debug info
    /// </summary>
    internal static class SettingsPage
    {
        private static string _selectedLang = "game";
        private static HSlider? _fontSlider;
        private static Label? _fontLabel;
        private static int _selectedFontStep = 0;

        internal static void Build(VBoxContainer container)
        {
            _selectedLang = Config.ModUiLanguage;
            _selectedFontStep = Config.UiFontStep;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("settings.title", "Settings")), false, Node.InternalMode.Disabled);

            // ── Language section ──────────────────────────────────────────────
            var langTitle = new Label { Text = Loc.Get("settings.language", "Language") };
            langTitle.AddThemeFontSizeOverride("font_size", 18);
            langTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            container.AddChild(langTitle, false, Node.InternalMode.Disabled);

            BuildLangCard(container);

            // ── Font size section ─────────────────────────────────────────────
            var fontTitle = new Label { Text = Loc.Get("settings.font_size", "Font Size") };
            fontTitle.AddThemeFontSizeOverride("font_size", 18);
            fontTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            container.AddChild(fontTitle, false, Node.InternalMode.Disabled);

            BuildFontSlider(container);

            // ── Path info ────────────────────────────────────────────────────
            var pathTitle = new Label { Text = Loc.Get("settings.paths", "Paths") };
            pathTitle.AddThemeFontSizeOverride("font_size", 18);
            pathTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            pathTitle.AddThemeConstantOverride("custom_minimum_size_y", 30);
            container.AddChild(pathTitle, false, Node.InternalMode.Disabled);

            AddInfoRow(container, "APPDATA", GetAppDataPath());
            AddInfoRow(container, "Save root", GetSaveRootPath());
            AddInfoRow(container, "Backup root", SaveManagerHelper.GetBackupRoot());
            AddInfoRow(container, "Tool dir", GetToolDir());
            AddInfoRow(container, "Steam", Steam.SteamIntegration.GetSteamInstallPath() ?? "Not found");

            // Author row with Bilibili link
            AddLinkRow(container, "Author:", "Bilibili @ 我叫煎包", "https://space.bilibili.com/234054413");

            // ── Debug info ──────────────────────────────────────────────────
            if (Config.DebugMode)
            {
                var debugTitle = new Label { Text = "Debug" };
                debugTitle.AddThemeFontSizeOverride("font_size", 18);
                debugTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
                container.AddChild(debugTitle, false, Node.InternalMode.Disabled);

                AddInfoRow(container, "Current save", MpSessionState.CurrentSavePath ?? "(none)");
                AddInfoRow(container, "Profiles", MpSessionState.AllProfiles.Count.ToString());
                AddInfoRow(container, "Game lang", LocManager.Instance?.Language ?? "unknown");
                AddInfoRow(container, "Font step", $"{Config.UiFontStep} (Δ = {Config.UiFontStep * UiFont.StepPx}px)");
            }
        }

        private static void BuildLangCard(VBoxContainer container)
        {
            var langCard = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 60)
            };
            var langStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpNavSelected,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            langStyle.SetBorderWidthAll(0);
            langStyle.SetCornerRadiusAll(8);
            langCard.AddThemeStyleboxOverride("panel", langStyle);
            container.AddChild(langCard, false, Node.InternalMode.Disabled);

            var langMargin = new MarginContainer();
            langMargin.AddThemeConstantOverride("margin_left", 14);
            langMargin.AddThemeConstantOverride("margin_right", 14);
            langMargin.AddThemeConstantOverride("margin_top", 10);
            langMargin.AddThemeConstantOverride("margin_bottom", 10);
            langCard.AddChild(langMargin, false, Node.InternalMode.Disabled);

            var langRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            langRow.AddThemeConstantOverride("separation", 20);
            langMargin.AddChild(langRow, false, Node.InternalMode.Disabled);

            void AddLangRadio(string lang, string text)
            {
                var radio = new CheckBox { Text = text, ButtonPressed = _selectedLang == lang };
                radio.Toggled += pressed =>
                {
                    if (!pressed) return;
                    _selectedLang = lang;
                    ApplyLangChange(lang);
                };
                langRow.AddChild(radio, false, Node.InternalMode.Disabled);
            }

            AddLangRadio("game", Loc.Get("settings.lang_game", "Follow Game"));
            AddLangRadio("eng", "English");
            AddLangRadio("zho", "简体中文");
        }

        private static void BuildFontSlider(VBoxContainer container)
        {
            var fontCard = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 70)
            };
            var fontStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpNavSelected,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            fontStyle.SetBorderWidthAll(0);
            fontStyle.SetCornerRadiusAll(8);
            fontCard.AddThemeStyleboxOverride("panel", fontStyle);
            container.AddChild(fontCard, false, Node.InternalMode.Disabled);

            var fontMargin = new MarginContainer();
            fontMargin.AddThemeConstantOverride("margin_left", 14);
            fontMargin.AddThemeConstantOverride("margin_right", 14);
            fontMargin.AddThemeConstantOverride("margin_top", 8);
            fontMargin.AddThemeConstantOverride("margin_bottom", 8);
            fontCard.AddChild(fontMargin, false, Node.InternalMode.Disabled);

            var fontV = new VBoxContainer();
            fontV.AddThemeConstantOverride("separation", 4);
            fontMargin.AddChild(fontV, false, Node.InternalMode.Disabled);

            // Hint label
            var fontHint = new Label { Text = Loc.Get("settings.font_hint", "Adjust font size for the entire interface") };
            fontHint.AddThemeFontSizeOverride("font_size", 15);
            fontHint.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            fontV.AddChild(fontHint, false, Node.InternalMode.Disabled);

            // Slider row: min label | slider | max label | current label
            var sliderRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            sliderRow.AddThemeConstantOverride("separation", 8);
            fontV.AddChild(sliderRow, false, Node.InternalMode.Disabled);

            var minLbl = new Label { Text = Loc.Get("settings.font_min", "小"), CustomMinimumSize = new Vector2(28, 0) };
            minLbl.AddThemeFontSizeOverride("font_size", 15);
            minLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            minLbl.VerticalAlignment = VerticalAlignment.Center;
            sliderRow.AddChild(minLbl, false, Node.InternalMode.Disabled);

            _fontSlider = new HSlider
            {
                MinValue = 0,
                MaxValue = UiFont.MaxStep,
                Step = 1,
                Value = _selectedFontStep,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(200, 0)
            };
            _fontSlider.ValueChanged += OnFontSliderChanged;
            sliderRow.AddChild(_fontSlider, false, Node.InternalMode.Disabled);

            var maxLbl = new Label { Text = Loc.Get("settings.font_max", "大"), CustomMinimumSize = new Vector2(28, 0) };
            maxLbl.AddThemeFontSizeOverride("font_size", 15);
            maxLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            maxLbl.VerticalAlignment = VerticalAlignment.Center;
            sliderRow.AddChild(maxLbl, false, Node.InternalMode.Disabled);

            // Current step display
            int currentDelta = _selectedFontStep * UiFont.StepPx;
            _fontLabel = new Label
            {
                Text = Loc.Fmt("settings.font_current", _selectedFontStep, UiFont.MaxStep, 100 + currentDelta),
                CustomMinimumSize = new Vector2(140, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _fontLabel.AddThemeFontSizeOverride("font_size", 15);
            _fontLabel.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
            _fontLabel.VerticalAlignment = VerticalAlignment.Center;
            sliderRow.AddChild(_fontLabel, false, Node.InternalMode.Disabled);
        }

        private static void OnFontSliderChanged(double value)
        {
            int step = (int)value;
            if (step == _selectedFontStep) return;
            _selectedFontStep = step;
            ApplyFontStep(step);
        }

        private static void ApplyFontStep(int step)
        {
            Config.UiFontStep = step;
            Config.SaveField("ui_font_step", step);

            if (_fontLabel != null)
            {
                int delta = step * UiFont.StepPx;
                _fontLabel.Text = Loc.Fmt("settings.font_current", step, UiFont.MaxStep, 100 + delta);
            }

            // Refresh the entire panel (chrome + content) so font sizes update immediately
            MpPanel.FullRefresh();
        }

        private static void ApplyLangChange(string lang)
        {
            Config.ModUiLanguage = lang;
            Config.SaveField("mod_ui_language", lang);
            Loc.Reload();
            MpPanel.RefreshCurrentPage();
        }

        private static void AddInfoRow(VBoxContainer container, string label, string value)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 12);
            container.AddChild(row, false, Node.InternalMode.Disabled);

            var lbl = new Label
            {
                Text = label + ":",
                CustomMinimumSize = new Vector2(140, 0),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            lbl.AddThemeFontSizeOverride("font_size", 17);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);

            var val = new Label { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            val.AddThemeFontSizeOverride("font_size", 17);
            val.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
            row.AddChild(val, false, Node.InternalMode.Disabled);
        }

        private static void AddLinkRow(VBoxContainer container, string label, string linkText, string url)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 12);
            container.AddChild(row, false, Node.InternalMode.Disabled);

            var lbl = new Label
            {
                Text = label,
                CustomMinimumSize = new Vector2(140, 0),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            lbl.AddThemeFontSizeOverride("font_size", 17);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);

            var link = new LinkButton { Text = linkText, Uri = url, Underline = LinkButton.UnderlineMode.Always };
            link.AddThemeFontSizeOverride("font_size", 17);
            link.AddThemeColorOverride("font_color", Panel.Styles.MpBlueAccent);
            row.AddChild(link, false, Node.InternalMode.Disabled);
        }

        private static string GetAppDataPath()
        {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData) + "\\SlayTheSpire2";
        }

        private static string GetSaveRootPath()
        {
            var env = System.Environment.GetEnvironmentVariable("SLAY_THE_SPIRE2_APPDATA");
            if (!string.IsNullOrEmpty(env)) return env;
            return GetAppDataPath();
        }

        private static string GetToolDir()
        {
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "unknown";
        }
    }
}
