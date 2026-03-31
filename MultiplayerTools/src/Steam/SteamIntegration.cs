using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using Microsoft.Win32;

namespace MultiplayerTools.Steam
{
    /// <summary>
    /// Steam integration: read Steam ID, friends, persona names from local VDF files.
    /// </summary>
    internal static class SteamIntegration
    {
        private static string? _steamPath;
        private static readonly Dictionary<string, string> PersonaNameCache = new();
        private static readonly Dictionary<string, SteamContact> ContactCache = new();
        private static readonly Dictionary<string, List<SteamContact>> FriendsCache = new();

        /// <summary>Get the Steam installation path from registry.</summary>
        internal static string? GetSteamInstallPath()
        {
            if (_steamPath != null) return _steamPath;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                _steamPath = key?.GetValue("SteamPath") as string;
                if (string.IsNullOrEmpty(_steamPath))
                    _steamPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam");
            }
            catch { _steamPath = @"C:\Program Files (x86)\Steam"; }
            return _steamPath;
        }

        /// <summary>Get current user's Steam ID from localconfig.vdf.</summary>
        internal static string? GetCurrentSteamId()
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return null;
                var configPath = Path.Combine(steamPath, "config", "localconfig.vdf");
                if (!File.Exists(configPath)) return null;
                var content = File.ReadAllText(configPath);
                var match = Regex.Match(content, @"SteamID[""\s]+""(\d+)""");
                if (match.Success) return match.Groups[1].Value;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetCurrentSteamId failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>Get persona name (nickname) for a Steam ID.</summary>
        internal static string GetPersonaName(string steamId)
        {
            if (PersonaNameCache.TryGetValue(steamId, out var cached)) return cached;
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return steamId;
                var usersVdf = Path.Combine(steamPath, "config", "users.vdf");
                if (File.Exists(usersVdf))
                {
                    var content = File.ReadAllText(usersVdf);
                    var pattern = $@"""{steamId}""[^}}]*?""PersonaName""\s+""([^""]+)""";
                    var match = Regex.Match(content, pattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        PersonaNameCache[steamId] = match.Groups[1].Value;
                        return match.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] GetPersonaName({steamId}) failed: " + ex.Message);
            }
            return steamId;
        }

        /// <summary>Get all local Steam contacts from loginusers.vdf.</summary>
        internal static List<SteamContact> GetLocalContacts()
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return new List<SteamContact>();
                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath)) return new List<SteamContact>();

                var content = File.ReadAllText(loginUsersPath);
                var contacts = new List<SteamContact>();
                var blockPattern = @"\""(\d+)\""\s*\{[^}]*?\""PersonaName\""\s+?\""([^\""]+)\""";
                foreach (Match m in Regex.Matches(content, blockPattern, RegexOptions.Singleline))
                {
                    var id = m.Groups[1].Value;
                    var name = m.Groups[2].Value;
                    if (!ContactCache.ContainsKey(id))
                        ContactCache[id] = new SteamContact { SteamId = id, PersonaName = name };
                    contacts.Add(ContactCache[id]);
                }
                return contacts.OrderBy(c => c.PersonaName).ToList();
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetLocalContacts failed: " + ex.Message);
                return new List<SteamContact>();
            }
        }

        /// <summary>
        /// Get Steam friends for the current local Steam user (best-effort, offline).
        /// Reads <c>userdata/&lt;steamId&gt;/config/localconfig.vdf</c> and extracts SteamIDs under the Friends section.
        /// </summary>
        internal static List<SteamContact> GetLocalFriends()
        {
            try
            {
                var me = GetCurrentSteamId();
                if (string.IsNullOrEmpty(me)) return new List<SteamContact>();
                if (FriendsCache.TryGetValue(me, out var cached)) return cached;

                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return new List<SteamContact>();

                var localCfg = Path.Combine(steamPath, "userdata", me, "config", "localconfig.vdf");
                if (!File.Exists(localCfg))
                {
                    FriendsCache[me] = new List<SteamContact>();
                    return FriendsCache[me];
                }

                var content = File.ReadAllText(localCfg);
                var friendIds = ExtractFriendSteamIdsFromLocalConfig(content);
                var list = new List<SteamContact>();

                foreach (var id in friendIds)
                {
                    var name = GetPersonaName(id);
                    if (!ContactCache.ContainsKey(id))
                        ContactCache[id] = new SteamContact { SteamId = id, PersonaName = name };
                    else if (string.IsNullOrEmpty(ContactCache[id].PersonaName))
                        ContactCache[id].PersonaName = name;

                    list.Add(ContactCache[id]);
                }

                // Fallback: if file format changes or friends section missing, return local contacts as a reasonable UX default.
                if (list.Count == 0)
                    list = GetLocalContacts();

                // Sort: persona first, then id
                list = list
                    .OrderBy(c => string.IsNullOrEmpty(c.PersonaName) ? 1 : 0)
                    .ThenBy(c => c.PersonaName)
                    .ThenBy(c => c.SteamId)
                    .ToList();

                FriendsCache[me] = list;
                return list;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetLocalFriends failed: " + ex.Message);
                return new List<SteamContact>();
            }
        }

        private static HashSet<string> ExtractFriendSteamIdsFromLocalConfig(string content)
        {
            var ids = new HashSet<string>();
            if (string.IsNullOrEmpty(content)) return ids;

            // Best-effort: locate a "Friends" section and extract all 15+ digit keys within its brace block.
            int idx = IndexOfToken(content, "\"Friends\"");
            if (idx < 0) idx = IndexOfToken(content, "\"friends\"");
            if (idx < 0) return ids;

            int open = content.IndexOf('{', idx);
            if (open < 0) return ids;

            int close = FindMatchingBrace(content, open);
            if (close <= open) return ids;

            string block = content.Substring(open, close - open + 1);
            foreach (Match m in Regex.Matches(block, "\"(\\d{15,})\""))
                ids.Add(m.Groups[1].Value);

            return ids;
        }

        private static int IndexOfToken(string s, string token)
        {
            return s.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        }

        private static int FindMatchingBrace(string s, int openBraceIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openBraceIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
    }

    internal class SteamContact
    {
        internal string SteamId { get; set; } = "";
        internal string PersonaName { get; set; } = "";
        internal bool IsOnline { get; set; }
    }
}
