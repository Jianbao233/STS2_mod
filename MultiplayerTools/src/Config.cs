using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace MultiplayerTools
{
    /// <summary>
    /// Global configuration. Loaded from config.json next to the DLL.
    /// Hotkey format: "F1" or "Ctrl+F1" etc.
    /// </summary>
    internal static class Config
    {
        public static string ToggleHotkey { get; private set; } = "F1";
        /// <summary>UI language override: game | eng | zho</summary>
        public static string ModUiLanguage { get; internal set; } = "game";
        /// <summary>Font size step: 0 = default (smallest), 1-6 = progressively larger.</summary>
        public static int UiFontStep { get; internal set; } = 0;
        public static bool DebugMode { get; private set; } = false;

        private static readonly string ConfigPath;

        static Config()
        {
            string? dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
            string fromDll = dir != null ? Path.Combine(dir, "config.json") : "";
            string fromExe = exeDir != null ? Path.Combine(exeDir, "mods", "MultiplayerTools", "config.json") : "";
            ConfigPath = File.Exists(fromDll) ? fromDll : (File.Exists(fromExe) ? fromExe : fromDll);
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("toggle_hotkey", out var hk))
                        ToggleHotkey = hk.GetString() ?? "F1";

                    if (root.TryGetProperty("mod_ui_language", out var mul))
                        ModUiLanguage = mul.GetString() ?? "game";
                    else if (root.TryGetProperty("language", out var lang))
                    {
                        var l = lang.GetString() ?? "game";
                        ModUiLanguage = l.Equals("zho", StringComparison.OrdinalIgnoreCase) ? "zho"
                            : l.Equals("eng", StringComparison.OrdinalIgnoreCase) ? "eng" : "game";
                    }

                    if (root.TryGetProperty("ui_font_step", out var fs))
                        UiFontStep = Math.Clamp(fs.GetInt32(), 0, 6);

                    if (root.TryGetProperty("debug", out var dbg))
                    {
                        // Handle both JSON boolean (true/false) and string ("True"/"False"/"true"/"false")
                        try { DebugMode = dbg.GetBoolean(); }
                        catch
                        {
                            var s = dbg.GetString();
                            DebugMode = string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                else
                {
                    SaveDefault();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] Config.Load failed: " + ex.Message);
            }
            GD.Print($"[MultiplayerTools] Config loaded. Hotkey={ToggleHotkey}, mod_ui_language={ModUiLanguage}, ui_font_step={UiFontStep}");
        }

        /// <summary>Persist a field into config.json without disturbing other fields.</summary>
        internal static void SaveField(string key, object value)
        {
            try
            {
                var dict = new Dictionary<string, object>();
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        dict[prop.Name] = prop.Value.ToString() ?? "";
                }
                dict[key] = value;
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dict, opts));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] Config.SaveField({key}) failed: " + ex.Message);
            }
        }

        private static void SaveDefault()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var obj = new
                {
                    toggle_hotkey = "F1",
                    mod_ui_language = "game",
                    ui_font_step = 0,
                    debug = false,
                    _readme = "mod_ui_language: game (follow game) | eng | zho"
                };
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
                GD.Print("[MultiplayerTools] Created default config.json at: " + ConfigPath);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] SaveDefault failed: " + ex.Message);
            }
        }
    }
}
