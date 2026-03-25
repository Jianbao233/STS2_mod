using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace MP_PlayerManager
{
    internal static class Loc
    {
        /// <summary>将游戏语言代码归一到 ui.json 目录名（eng / zho）。</summary>
        private static string NormalizeGameLanguage(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "eng";
            if (raw.Equals("zho", StringComparison.OrdinalIgnoreCase)) return "zho";
            if (raw.Equals("chs", StringComparison.OrdinalIgnoreCase)) return "zho";
            if (raw.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zho";
            return raw;
        }

        private static string ResolveUiLanguage()
        {
            Config.EnsureLoaded();
            var forced = Config.ModUiLanguage;
            if (string.Equals(forced, "zho", StringComparison.OrdinalIgnoreCase))
                return "zho";
            if (string.Equals(forced, "eng", StringComparison.OrdinalIgnoreCase))
                return "eng";
            // game：跟随游戏 LocManager
            return NormalizeGameLanguage(LocManager.Instance?.Language);
        }

        private static Dictionary<string, string> LoadTable(string language)
        {
            // 与 Godot 项目根一致：打包进 PCK 后为 res://localization/{lang}/ui.json
            // 勿在 mods/ 下放 localization/*.json，游戏会递归当作 mod_manifest 解析并报错
            string path = $"res://localization/{language}/ui.json";
            if (!FileAccess.FileExists(path))
                return new Dictionary<string, string>();

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText(false)) ?? new Dictionary<string, string>();
        }

        private static Dictionary<string, string> GetStrings()
        {
            string lang = ResolveUiLanguage();
            if (_strings != null && _loadedLanguage == lang)
                return _strings;

            _strings = LoadTable(lang);
            if (_strings.Count == 0 && lang != "eng")
                _strings = LoadTable("eng");

            _loadedLanguage = lang;
            return _strings;
        }

        public static void Reload()
        {
            _strings = null;
            _loadedLanguage = null;
        }

        public static string Get(string key, string fallback = null)
        {
            if (GetStrings().TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
                return text;
            return fallback ?? key;
        }

        public static string Fmt(string key, params object[] args)
        {
            return string.Format(Get(key, null), args);
        }

        private static Dictionary<string, string>? _strings;
        private static string? _loadedLanguage;
    }
}
