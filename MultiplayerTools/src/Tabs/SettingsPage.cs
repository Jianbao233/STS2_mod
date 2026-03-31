using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Settings page — mirrors v2 _page_settings.
    ///
    /// Layout:
    ///   [title]
    ///   → Language selection (radio buttons)
    ///   → Path info (read-only)
    ///   → About / debug info
    /// </summary>
    internal static class SettingsPage
    {
        private static string _selectedLang = "game";

        internal static void Build(VBoxContainer container)
        {
            _selectedLang = Config.ModUiLanguage;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("settings.title", "Settings")), false, Node.InternalMode.Disabled);

            // ── Language section ──────────────────────────────────────────────
            var langTitle = new Label { Text = Loc.Get("settings.language", "Language") };
            langTitle.AddThemeFontSizeOverride("font_size", 18);
            langTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            container.AddChild(langTitle, false, Node.InternalMode.Disabled);

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

            // Game language
            var gameRadio = new CheckBox { Text = Loc.Get("settings.lang_game", "Follow Game") };
            gameRadio.ButtonPressed = _selectedLang == "game";
            gameRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _selectedLang = "game";
                ApplyLangChange("game");
            };
            langRow.AddChild(gameRadio, false, Node.InternalMode.Disabled);

            // English
            var engRadio = new CheckBox { Text = "English" };
            engRadio.ButtonPressed = _selectedLang == "eng";
            engRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _selectedLang = "eng";
                ApplyLangChange("eng");
            };
            langRow.AddChild(engRadio, false, Node.InternalMode.Disabled);

            // Chinese
            var zhoRadio = new CheckBox { Text = "简体中文" };
            zhoRadio.ButtonPressed = _selectedLang == "zho";
            zhoRadio.Toggled += pressed =>
            {
                if (!pressed) return;
                _selectedLang = "zho";
                ApplyLangChange("zho");
            };
            langRow.AddChild(zhoRadio, false, Node.InternalMode.Disabled);

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
            }
        }

        private static void ApplyLangChange(string lang)
        {
            // We can't modify Config.ModUiLanguage directly (read-only), but we can write config.json
            try
            {
                string? dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
                string cfgDll = dir != null ? Path.Combine(dir, "config.json") : "";
                string cfgExe = exeDir != null ? Path.Combine(exeDir, "mods", "MultiplayerTools", "config.json") : "";
                string cfgPath = File.Exists(cfgDll) ? cfgDll : (File.Exists(cfgExe) ? cfgExe : cfgDll);
                if (File.Exists(cfgPath))
                {
                    var json = File.ReadAllText(cfgPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var obj = new Dictionary<string, object>();
                    foreach (var prop in root.EnumerateObject())
                        obj[prop.Name] = prop.Value.ToString();
                    obj["mod_ui_language"] = lang;
                    var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    string newJson = System.Text.Json.JsonSerializer.Serialize(obj, opts);
                    File.WriteAllText(cfgPath, newJson);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ApplyLangChange failed: " + ex.Message);
            }
            Loc.Reload();
            MpPanel.RefreshCurrentPage();
        }

        private static void AddInfoRow(VBoxContainer container, string label, string value)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 12);
            container.AddChild(row, false, Node.InternalMode.Disabled);

            var lbl = new Label { Text = label + ":", CustomMinimumSize = new Vector2(140, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 17);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);

            var val = new Label { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            val.AddThemeFontSizeOverride("font_size", 17);
            val.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
            row.AddChild(val, false, Node.InternalMode.Disabled);
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
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "unknown";
        }
    }
}
