using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MultiplayerTools;

namespace MultiplayerTools.Core
{
    /// <summary>
    /// Resolves CHARACTER.* IDs to display names: game characters.json → mod player_template.json → embedded ui.json → humanized id.
    /// </summary>
    internal static class CharacterDisplayNames
    {
        private static readonly Regex RichTag = new(@"\[[^\]]+\]", RegexOptions.Compiled);

        private static Dictionary<string, string>? _gameFlat;
        private static string? _gameLangFolder;

        private static Dictionary<string, string>? _modNames;
        private static bool _modScanned;

        /// <summary>Localized title for UI (Steam persona still preferred by callers when applicable).</summary>
        public static string Resolve(string? characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return "???";

            string id = characterId.Trim();

            if (TryGameTitle(id, out var gameTitle) && !string.IsNullOrEmpty(gameTitle))
                return StripRichText(gameTitle);

            // Tool / legacy id: NEMESIS matches game REGENT
            if (string.Equals(ShortCharacterId(id), "NEMESIS", StringComparison.OrdinalIgnoreCase) &&
                TryGameTitle("CHARACTER.REGENT", out var regentTitle) && !string.IsNullOrEmpty(regentTitle))
                return StripRichText(regentTitle);

            if (TryModTemplateName(id, out var modName) && !string.IsNullOrEmpty(modName))
                return modName;

            string key = "char." + id;
            string embedded = Loc.Get(key, null);
            if (!string.IsNullOrEmpty(embedded) && embedded != key)
                return embedded;

            // Legacy ui key mistake: NECROMANCER vs NECROBINDER
            if (id.IndexOf("NECROBINDER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string legacy = Loc.Get("char.CHARACTER.NECROMANCER", null);
                if (!string.IsNullOrEmpty(legacy) && legacy != "char.CHARACTER.NECROMANCER")
                    return legacy;
            }

            return Humanize(id);
        }

        private static string Humanize(string id)
        {
            var s = id.StartsWith("CHARACTER.", StringComparison.OrdinalIgnoreCase)
                ? id.Substring("CHARACTER.".Length)
                : id;
            return s.Replace("_", " ");
        }

        private static string StripRichText(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('[') < 0)
                return s;
            return RichTag.Replace(s, "").Trim();
        }

        private static string ShortCharacterId(string characterId)
        {
            if (characterId.StartsWith("CHARACTER.", StringComparison.OrdinalIgnoreCase))
                return characterId.Substring("CHARACTER.".Length);
            return characterId;
        }

        /// <summary>Map LocManager / game language codes to folders under res://localization/.</summary>
        private static string MapToCharactersFolder(string? lang)
        {
            if (string.IsNullOrEmpty(lang))
                return "eng";
            var l = lang.Trim().ToLowerInvariant();
            if (l is "zho" or "chs" || l.StartsWith("zh", StringComparison.Ordinal))
                return "zhs";
            if (l is "eng" or "en")
                return "eng";
            string[] known =
            {
                "deu", "eng", "esp", "fra", "ita", "jpn", "kor", "pol", "ptb", "rus", "spa", "tha", "tur", "zhs"
            };
            foreach (var k in known)
            {
                if (l == k)
                    return k;
            }
            return "eng";
        }

        private static void EnsureGameCache(string folder)
        {
            if (_gameFlat != null && _gameLangFolder == folder)
                return;

            _gameFlat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _gameLangFolder = folder;

            void LoadInto(string pathLabel, string virtualPath)
            {
                if (!Godot.FileAccess.FileExists(virtualPath))
                    return;
                using var fa = Godot.FileAccess.Open(virtualPath, Godot.FileAccess.ModeFlags.Read);
                if (fa == null)
                    return;
                string text = fa.GetAsText();
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
                    if (dict == null)
                        return;
                    foreach (var kv in dict)
                    {
                        if (kv.Value.ValueKind == JsonValueKind.String)
                        {
                            var str = kv.Value.GetString();
                            if (!string.IsNullOrEmpty(str))
                                _gameFlat![kv.Key] = str;
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[MultiplayerTools] CharacterDisplayNames: failed to parse {pathLabel}: {ex.Message}");
                }
            }

            LoadInto(folder, $"res://localization/{folder}/characters.json");
            if (_gameFlat.Count == 0 && folder != "eng")
                LoadInto("eng-fallback", "res://localization/eng/characters.json");
        }

        private static bool TryGameTitle(string characterId, out string title)
        {
            title = "";
            string folder = MapToCharactersFolder(LocManager.Instance?.Language);
            EnsureGameCache(folder);

            string shortId = ShortCharacterId(characterId);
            string key = shortId + ".title";
            if (_gameFlat != null && _gameFlat.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t))
            {
                title = t;
                return true;
            }
            return false;
        }

        private static void EnsureModTemplates()
        {
            if (_modScanned)
                return;
            _modScanned = true;
            _modNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string? modsRoot = TryGetModsDirectory();
            if (string.IsNullOrEmpty(modsRoot) || !Directory.Exists(modsRoot))
                return;

            try
            {
                foreach (var path in Directory.EnumerateFiles(modsRoot, "player_template.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("character_id", out var cidEl) &&
                            root.TryGetProperty("name", out var nameEl) &&
                            cidEl.ValueKind == JsonValueKind.String &&
                            nameEl.ValueKind == JsonValueKind.String)
                        {
                            string cid = cidEl.GetString() ?? "";
                            string name = nameEl.GetString() ?? "";
                            if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(name))
                                _modNames[cid] = name;
                        }
                    }
                    catch
                    {
                        // ignore broken mod json
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] CharacterDisplayNames mod scan: " + ex.Message);
            }
        }

        private static bool TryModTemplateName(string characterId, out string name)
        {
            name = "";
            EnsureModTemplates();
            if (_modNames != null && _modNames.TryGetValue(characterId, out var n) && !string.IsNullOrEmpty(n))
            {
                name = n;
                return true;
            }
            return false;
        }

        private static string? TryGetModsDirectory()
        {
            try
            {
                string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
                if (string.IsNullOrEmpty(exeDir))
                    return null;
                string mods = Path.Combine(exeDir, "mods");
                if (Directory.Exists(mods))
                    return mods;
            }
            catch
            {
                // ignored
            }
            return null;
        }
    }
}
