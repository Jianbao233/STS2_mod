using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace MultiplayerTools
{
    /// <summary>
    /// UI strings: embedded ui.json in DLL, optional res:// from PCK, follows game language when mod_ui_language=game.
    /// </summary>
    internal static class Loc
    {
        private static Dictionary<string, string>? _strings;
        private static string? _loadedLanguage;

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
            var forced = Config.ModUiLanguage;
            if (string.Equals(forced, "zho", StringComparison.OrdinalIgnoreCase))
                return "zho";
            if (string.Equals(forced, "eng", StringComparison.OrdinalIgnoreCase))
                return "eng";
            return NormalizeGameLanguage(LocManager.Instance?.Language);
        }

        private static Dictionary<string, string> LoadTableFromRes(string language)
        {
            string path = $"res://localization/{language}/ui.json";
            if (!Godot.FileAccess.FileExists(path))
                return new Dictionary<string, string>();
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
                return new Dictionary<string, string>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText()) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private static Dictionary<string, string> LoadTableEmbedded(string language)
        {
            var asm = Assembly.GetExecutingAssembly();
            string suffix = $".localization.{language}.ui.json";
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
                           ?? new Dictionary<string, string>();
                }
                catch
                {
                    return new Dictionary<string, string>();
                }
            }
            return new Dictionary<string, string>();
        }

        private static Dictionary<string, string> GetStrings()
        {
            string lang = ResolveUiLanguage();
            if (_strings != null && _loadedLanguage == lang)
                return _strings;

            var dict = LoadTableFromRes(lang);
            if (dict.Count == 0)
                dict = LoadTableEmbedded(lang);
            if (dict.Count == 0 && lang != "eng")
            {
                dict = LoadTableFromRes("eng");
                if (dict.Count == 0)
                    dict = LoadTableEmbedded("eng");
            }

            _strings = dict;
            _loadedLanguage = lang;
            GD.Print($"[MultiplayerTools] Loc: lang={lang}, entries={dict.Count}, resolved={ResolveUiLanguage()}");
            return _strings;
        }

        public static void Reload()
        {
            _strings = null;
            _loadedLanguage = null;
        }

        public static string Get(string key, string? fallback = null)
        {
            if (GetStrings().TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
                return text;
            return fallback ?? key;
        }

        public static string Fmt(string key, params object[] args)
        {
            var template = Get(key, null);
            if (string.IsNullOrEmpty(template) || template == key)
            {
                if (args.Length > 0)
                    return args[0]?.ToString() ?? "";
                return key;
            }
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }
    }
}
