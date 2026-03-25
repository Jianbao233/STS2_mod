using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace MP_PlayerManager
{
    internal static class Config
    {
        public static void Load()
        {
            _hotkeys = null;
            _flags = null;
            EnsureLoaded();
        }

        public static HotkeyBinding GetHotkey(string action)
        {
            EnsureLoaded();
            return _hotkeys.TryGetValue(action, out var binding) ? binding : HotkeyBinding.None;
        }

        public static bool GetFlag(string name)
        {
            EnsureLoaded();
            return _flags.TryGetValue(name, out var flag) ? flag : DefaultFlags.GetValueOrDefault(name, true);
        }

        public static bool MatchesAny(InputEventKey keyEvent)
        {
            EnsureLoaded();
            foreach (var binding in _hotkeys.Values)
            {
                if (binding.Matches(keyEvent)) return true;
            }
            return false;
        }

        /// <summary>Mod 界面语言：<c>game</c> 跟随游戏，<c>zho</c>/<c>eng</c> 强制简中或英文。</summary>
        internal static string ModUiLanguage
        {
            get
            {
                EnsureLoaded();
                return _modUiLanguage ?? DefaultModUiLanguage;
            }
        }

        internal static void EnsureLoaded()
        {
            if (_hotkeys != null) return;

            _hotkeys = new Dictionary<string, HotkeyBinding>();
            foreach (var kv in DefaultHotkeys)
                _hotkeys[kv.Key] = HotkeyBinding.Parse(kv.Value);

            _flags = new Dictionary<string, bool>(DefaultFlags);
            _modUiLanguage = DefaultModUiLanguage;

            string path = GetConfigPath();
            try
            {
                if (!File.Exists(path))
                {
                    WriteDefaults(path);
                    GD.Print("[MP_PlayerManager] Created default config.json: " + path);
                    return;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (doc.RootElement.TryGetProperty("hotkeys", out var hotkeysEl))
                {
                    foreach (var prop in hotkeysEl.EnumerateObject())
                    {
                        var binding = HotkeyBinding.Parse(prop.Value.GetString() ?? "");
                        if (!binding.IsNone)
                            _hotkeys[prop.Name] = binding;
                    }
                }

                if (doc.RootElement.TryGetProperty("flags", out var flagsEl))
                {
                    foreach (var prop in flagsEl.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                            _flags[prop.Name] = prop.Value.GetBoolean();
                    }
                }

                if (doc.RootElement.TryGetProperty("mod_ui_language", out var langEl))
                {
                    var s = langEl.GetString()?.Trim().ToLowerInvariant();
                    if (s == "game" || s == "zho" || s == "eng")
                        _modUiLanguage = s;
                }

                GD.Print($"[MP_PlayerManager] Loaded config.json: mod_ui_language={_modUiLanguage}, hotkeys=[{string.Join(", ", _hotkeys)}], flags=[{string.Join(", ", _flags)}]");
            }
            catch (System.Exception ex)
            {
                GD.Print("[MP_PlayerManager] Failed to load config.json: " + ex.Message);
            }
        }

        /// <summary>
        /// 配置必须放在 mods 目录之外：游戏会递归扫描 mods 下所有 .json 当作 mod_manifest，
        /// mods/MP_PlayerManager/config.json 会触发「缺少 id」错误并导致整批模组报错提示。
        /// </summary>
        private static string GetConfigPath()
        {
            try
            {
                string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "SlayTheSpire2", "mod_settings", "MP_PlayerManager");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "MP_PlayerManager_config.json");
            }
        }

        private static void WriteDefaults(string path)
        {
            try
            {
                var dict = new Dictionary<string, object>
                {
                    ["_readme"] = "Hotkey format: Key or Ctrl+Key or Shift+Alt+Key. mod_ui_language: game | zho | eng (game=follow game UI language).",
                    ["mod_ui_language"] = DefaultModUiLanguage,
                    ["hotkeys"] = new Dictionary<string, string>(DefaultHotkeys),
                    ["flags"] = new Dictionary<string, bool>(DefaultFlags)
                };
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (System.Exception ex)
            {
                GD.Print("[MP_PlayerManager] Failed to write default config.json: " + ex.Message);
            }
        }

        static Config()
        {
            DefaultHotkeys = new Dictionary<string, string> { ["toggle_panel"] = "F1" };
            DefaultFlags = new Dictionary<string, bool> { ["show_topbar_icon"] = true };
        }

        private static Dictionary<string, HotkeyBinding>? _hotkeys;
        private static Dictionary<string, bool>? _flags;
        private static string? _modUiLanguage;
        private const string DefaultModUiLanguage = "zho";

        private static readonly Dictionary<string, string> DefaultHotkeys;
        private static readonly Dictionary<string, bool> DefaultFlags;
    }
}
