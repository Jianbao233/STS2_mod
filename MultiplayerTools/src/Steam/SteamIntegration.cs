using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        private static uint? _cachedAccountId;
        private static readonly Dictionary<string, string> PersonaNameCache = new();
        private static readonly Dictionary<string, SteamContact> ContactCache = new();
        private static readonly Dictionary<string, List<SteamContact>> FriendsCache = new();

        /// <summary>
        /// Bulk-import a dictionary of SteamID64 → persona name into the persona cache.
        /// Called after loading steam_names.json so cached names are used by GetPersonaName
        /// without re-parsing VDF files.
        /// </summary>
        internal static void MergeSteamNames(Dictionary<string, string> names)
        {
            if (names == null) return;
            foreach (var kvp in names)
            {
                var sid = NormalizeSteamIdForApi(kvp.Key);
                if (string.IsNullOrEmpty(sid)) continue;
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                    PersonaNameCache[sid] = kvp.Value;
            }
        }

        /// <summary>Clear cached friends list so VDF parsing fixes take effect without restarting the game.</summary>
        internal static void ClearFriendsListCache()
        {
            FriendsCache.Clear();
            InvalidateFriendListDisplayMap();
        }

        /// <summary>Clear persona cache (e.g. after load) so corrected VDF parsing replaces stale nicknames.</summary>
        internal static void ClearPersonaNameCache()
        {
            PersonaNameCache.Clear();
            InvalidateFriendListDisplayMap();
        }

        /// <summary>Steam client shows friends under custom aliases from localconfig.vdf; invalidate when caches clear.</summary>
        private static Dictionary<string, string>? _friendListDisplayBySteamId;

        private static uint? _friendListDisplayLoadedForAccount;

        private static void InvalidateFriendListDisplayMap()
        {
            _friendListDisplayBySteamId = null;
            _friendListDisplayLoadedForAccount = null;
        }

        // Steam Account ID -> SteamID64 conversion base
        private const ulong SteamIdBase = 76561197960265728UL;

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

        /// <summary>
        /// Get current user's Steam Account ID (the integer folder name in userdata/).
        /// Returns null if Steam is not running or no user is logged in.
        /// </summary>
        internal static uint? GetCurrentSteamAccountId()
        {
            if (_cachedAccountId.HasValue) return _cachedAccountId;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                if (key == null) return null;
                var activeUser = key.GetValue("ActiveUser");
                if (activeUser == null) return null;
                uint accountId;
                if (activeUser is uint uintVal) accountId = uintVal;
                else if (activeUser is long longVal) accountId = (uint)longVal;
                else if (uint.TryParse(activeUser.ToString(), out accountId)) { /* ok */ }
                else return null;
                if (accountId == 0) return null;
                _cachedAccountId = accountId;
                return accountId;
            }
            catch { return null; }
        }

        /// <summary>
        /// Get current user's Steam ID — three-level priority:
        /// 1. Registry HKCU\Software\Valve\Steam\ActiveProcess\ActiveUser (most accurate)
        /// 2. loginusers.vdf MostRecent=1
        /// 3. localconfig.vdf SteamID field
        /// </summary>
        internal static string? GetCurrentSteamId()
        {
            if (_steamIdCache != null) return _steamIdCache;
            try
            {
                // Priority 1: Registry ActiveUser (Steam Account ID -> SteamID64)
                uint? accountId = GetCurrentSteamAccountId();
                if (accountId.HasValue)
                {
                    var id = AccountIdToSteamId64(accountId.Value).ToString();
                    _steamIdCache = id;
                    return id;
                }

                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return null;

                // Priority 2: loginusers.vdf
                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (File.Exists(loginUsersPath))
                {
                    var content = File.ReadAllText(loginUsersPath);
                    var id = ExtractCurrentSteamIdFromLoginUsers(content);
                    if (!string.IsNullOrEmpty(id)) { _steamIdCache = id; return id; }
                }

                // Priority 3: global Steam/config/localconfig.vdf
                var configPath = Path.Combine(steamPath, "config", "localconfig.vdf");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    var match = Regex.Match(content, @"SteamID[""\s]+""(\d+)""");
                    if (match.Success)
                    {
                        _steamIdCache = match.Groups[1].Value;
                        return _steamIdCache;
                    }
                }
            }
            catch (Exception ex) { GD.PrintErr("[MultiplayerTools] GetCurrentSteamId failed: " + ex.Message); }
            return null;
        }
        private static string? _steamIdCache;

        /// <summary>
        /// Convert Steam Account ID (registry ActiveUser, userdata folder name) to SteamID64.
        /// Matches MP_PlayerManager_v2 steam_api._account_id_to_steam64:
        /// <c>76561197960265728 + (account_id &gt;&gt; 1) * 2 + (account_id &amp; 1)</c> ≡ <c>base + account_id</c>.
        /// </summary>
        private static ulong AccountIdToSteamId64(uint accountId)
        {
            return SteamIdBase + accountId;
        }

        private static string? ExtractCurrentSteamIdFromLoginUsers(string content)
        {
            // loginusers.vdf structure:
            //   "accounts" { "7656..." { "SteamID" "7656..." "MostRecent" "1" ... } }
            // We find the block with MostRecent = "1".
            var accountIdx = content.IndexOf("\"accounts\"", StringComparison.OrdinalIgnoreCase);
            if (accountIdx < 0) return null;

            int accountsOpen = content.IndexOf('{', accountIdx);
            if (accountsOpen < 0) return null;
            int accountsClose = FindMatchingBrace(content, accountsOpen);
            if (accountsClose <= accountsOpen) return null;

            string accountsBlock = content.Substring(accountsOpen, accountsClose - accountsOpen + 1);

            // Find each top-level sub-block under "accounts".
            int pos = 0;
            while ((pos = accountsBlock.IndexOf('"', pos)) >= 0)
            {
                int idStart = pos + 1;
                int idEnd = accountsBlock.IndexOf('"', idStart);
                if (idEnd < 0) break;
                string candidate = accountsBlock.Substring(idStart, idEnd - idStart);
                if (!Regex.IsMatch(candidate, @"^\d{15,}$")) { pos = idEnd + 1; continue; }

                int blockStart = accountsBlock.IndexOf('{', idEnd);
                if (blockStart < 0) { pos = idEnd + 1; continue; }
                int blockEnd = FindMatchingBrace(accountsBlock, blockStart);
                if (blockEnd <= blockStart) { pos = idEnd + 1; continue; }

                string block = accountsBlock.Substring(blockStart, blockEnd - blockStart + 1);
                if (Regex.IsMatch(block, @"MostRecent[""\s]+""1""", RegexOptions.IgnoreCase))
                    return candidate;

                pos = blockEnd + 1;
            }
            return null;
        }

        /// <summary>Canonical SteamID64 string from save <c>net_id</c> or UI input (digits only).</summary>
        internal static string NormalizeSteamIdForApi(string? steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId)) return "";
            var s = steamId.Trim();
            if (s.Length >= 15 && s.Length <= 20 && IsDigitsOnly(s)) return s;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var li) && li > 0)
                return li.ToString(CultureInfo.InvariantCulture);
            return s;
        }

        internal static string GetPersonaName(string steamId)
        {
            steamId = NormalizeSteamIdForApi(steamId);
            if (string.IsNullOrEmpty(steamId)) return "";

            // Priority 1: cache (populated by steam_names.json merge + PreloadFromLocalVdf).
            if (PersonaNameCache.TryGetValue(steamId, out var cached) &&
                !string.IsNullOrWhiteSpace(cached) && cached != steamId)
            {
                return cached;
            }

            // Priority 2: Friend-list label (localconfig.vdf) — matches Steam overlay / 好友.
            // Often differs from global PersonaName in users.vdf (e.g. custom alias vs seller default name).
            if (TryGetFriendListDisplayName(steamId, out var friendLabel) &&
                !string.IsNullOrWhiteSpace(friendLabel) &&
                !string.Equals(friendLabel, steamId, StringComparison.Ordinal))
            {
                PersonaNameCache[steamId] = friendLabel;
                return friendLabel;
            }

            // Priority 3: Local users.vdf — correct nested blocks; overwrites any stale cache.
            var localName = GetPersonaNameFromUsersVdf(steamId);
            if (!string.IsNullOrEmpty(localName) && localName != steamId)
            {
                PersonaNameCache[steamId] = localName;
                return localName;
            }

            // Priority 4: loginusers.vdf (current user account list).
            var loginName = GetPersonaNameFromLoginUsers(steamId);
            if (!string.IsNullOrEmpty(loginName) && loginName != steamId)
            {
                PersonaNameCache[steamId] = loginName;
                return loginName;
            }

            return steamId;
        }

        /// <summary>
        /// Preload persona names for all known profile Steam IDs from local VDF files.
        /// Mirrors v2 get_all_contacts() which uses only offline data sources.
        /// </summary>
        internal static void PreloadFromLocalVdf()
        {
            // Re-parse all userdata localconfig.vdf files to populate the friend display map
            // so that GetPersonaName can find names even for IDs we haven't explicitly looked up yet.
            InvalidateFriendListDisplayMap();
            // Also populate the friend display map now (it will be lazily built on first TryGetFriendListDisplayName call).
            // But since TryGetFriendListDisplayName always re-scans, just call it once to warm up.
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath)) return;
            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (!Directory.Exists(userdataRoot)) return;
            foreach (var folder in Directory.GetDirectories(userdataRoot))
            {
                try
                {
                    var cfg = Path.Combine(folder, "config", "localconfig.vdf");
                    if (!File.Exists(cfg)) continue;
                    var entries = ExtractFriendEntriesFromLocalConfig(File.ReadAllText(cfg));
                    foreach (var (sid, nm) in entries)
                    {
                        if (!string.IsNullOrWhiteSpace(nm) && !string.IsNullOrEmpty(sid))
                            PersonaNameCache[sid] = nm.Trim();
                    }
                }
                catch { /* skip */ }
            }
        }

        /// <summary>Read PersonaName from the VDF object keyed exactly by <paramref name="steamId"/>.</summary>
        private static string? GetPersonaNameFromKeyedBlock(string content, string steamId)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(steamId)) return null;
            string needle = "\"" + steamId + "\"";
            for (int keyIdx = 0; keyIdx < content.Length; )
            {
                keyIdx = content.IndexOf(needle, keyIdx, StringComparison.Ordinal);
                if (keyIdx < 0) return null;
                int afterQuotedKey = keyIdx + needle.Length;
                if (afterQuotedKey < content.Length && content[afterQuotedKey] == '"')
                {
                    keyIdx++;
                    continue;
                }

                int j = afterQuotedKey;
                while (j < content.Length && char.IsWhiteSpace(content[j])) j++;
                if (j >= content.Length || content[j] != '{')
                {
                    keyIdx++;
                    continue;
                }

                string block = ExtractVdfBracedBlock(content, j);
                if (string.IsNullOrEmpty(block))
                {
                    keyIdx++;
                    continue;
                }

                var m = Regex.Match(block, @"""PersonaName""\s+""([^""]*)""", RegexOptions.IgnoreCase);
                if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    return m.Groups[1].Value.Trim();

                keyIdx++;
            }

            return null;
        }

        /// <summary>Fetch persona name from local users.vdf cache.</summary>
        private static string? GetPersonaNameFromUsersVdf(string steamId)
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return null;
                var usersVdf = Path.Combine(steamPath, "config", "users.vdf");
                if (!File.Exists(usersVdf)) return null;
                var content = File.ReadAllText(usersVdf);
                return GetPersonaNameFromKeyedBlock(content, steamId);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] GetPersonaNameFromUsersVdf({steamId}) failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>Fetch persona name from loginusers.vdf for a given Steam ID.</summary>
        private static string? GetPersonaNameFromLoginUsers(string steamId)
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return null;
                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath)) return null;
                var content = File.ReadAllText(loginUsersPath);
                return GetPersonaNameFromKeyedBlock(content, steamId);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] GetPersonaNameFromLoginUsers({steamId}) failed: " + ex.Message);
            }
            return null;
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
                var seen = new HashSet<string>(StringComparer.Ordinal);

                void AddLoginUser(string steamId64, string personaName)
                {
                    personaName = personaName.Trim();
                    if (string.IsNullOrEmpty(personaName) || !seen.Add(steamId64)) return;
                    if (!ContactCache.TryGetValue(steamId64, out var contact))
                    {
                        contact = new SteamContact { SteamId = steamId64, PersonaName = personaName };
                        ContactCache[steamId64] = contact;
                    }
                    else if (string.IsNullOrEmpty(contact.PersonaName))
                        contact.PersonaName = personaName;
                    contacts.Add(contact);
                }

                foreach (Match m in Regex.Matches(content, @"""(7656119\d{10})""\s*\{", RegexOptions.None))
                {
                    int braceAt = m.Index + m.Length - 1;
                    string block = ExtractVdfBracedBlock(content, braceAt);
                    if (string.IsNullOrEmpty(block)) continue;
                    var pn = Regex.Match(block, @"""PersonaName""\s+""([^""]*)""", RegexOptions.IgnoreCase);
                    if (!pn.Success) continue;
                    AddLoginUser(m.Groups[1].Value, pn.Groups[1].Value);
                }

                foreach (Match m in Regex.Matches(content, @"""(\d{8,10})""\s*\{", RegexOptions.None))
                {
                    string raw = m.Groups[1].Value;
                    string id64 = ConvertToSteamId64(raw);
                    if (string.IsNullOrEmpty(id64)) continue;
                    int braceAt = m.Index + m.Length - 1;
                    string block = ExtractVdfBracedBlock(content, braceAt);
                    if (string.IsNullOrEmpty(block)) continue;
                    var pn = Regex.Match(block, @"""PersonaName""\s+""([^""]*)""", RegexOptions.IgnoreCase);
                    if (!pn.Success) continue;
                    AddLoginUser(id64, pn.Groups[1].Value);
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
        /// Reads userdata/&lt;accountId&gt;/config/localconfig.vdf and extracts SteamIDs under the Friends section.
        /// userdata folder uses Steam Account ID (int), NOT SteamID64.
        /// </summary>
        internal static List<SteamContact> GetLocalFriends()
        {
            try
            {
                // Get Steam Account ID (int folder name in userdata/)
                uint? accountId = GetCurrentSteamAccountId();
                string? steamId64 = GetCurrentSteamId();

                if (!accountId.HasValue)
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: no local Steam Account ID found (Steam not running?)");
                    return new List<SteamContact>();
                }

                if (FriendsCache.TryGetValue(accountId.Value.ToString(), out var cached))
                    return cached;

                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: Steam install path not found");
                    return new List<SteamContact>();
                }

                // Use account ID (not SteamID64!) as folder name
                var localCfg = Path.Combine(steamPath, "userdata", accountId.Value.ToString(), "config", "localconfig.vdf");

                if (!File.Exists(localCfg))
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: localconfig.vdf not found for user " + accountId);
                    // Fallback: try SteamID64 folder name
                    var altPath = Path.Combine(steamPath, "userdata", steamId64 ?? "", "config", "localconfig.vdf");
                    if (!string.IsNullOrEmpty(steamId64) && File.Exists(altPath))
                        localCfg = altPath;
                    else
                    {
                        FriendsCache[accountId.Value.ToString()] = new List<SteamContact>();
                        return FriendsCache[accountId.Value.ToString()];
                    }
                }

                var content = File.ReadAllText(localCfg);
                var friendEntries = ExtractFriendEntriesFromLocalConfig(content);
                var list = new List<SteamContact>();

                foreach (var (sid, name) in friendEntries)
                {
                    if (!ContactCache.TryGetValue(sid, out var contact))
                    {
                        contact = new SteamContact { SteamId = sid, PersonaName = name };
                        ContactCache[sid] = contact;
                    }
                    else if (string.IsNullOrEmpty(contact.PersonaName) && !string.IsNullOrEmpty(name))
                    {
                        contact.PersonaName = name;
                    }
                    list.Add(contact);
                }

                // Fallback: if no friends found, scan all userdata folders
                if (list.Count == 0)
                {
                    var userdataRoot = Path.Combine(steamPath, "userdata");
                    if (Directory.Exists(userdataRoot))
                    {
                        foreach (var folder in Directory.GetDirectories(userdataRoot))
                        {
                            try
                            {
                                var folderName = Path.GetFileName(folder);
                                if (!uint.TryParse(folderName, out var folderId)) continue;
                                var altCfg = Path.Combine(folder, "config", "localconfig.vdf");
                                if (!File.Exists(altCfg)) continue;
                                var altContent = File.ReadAllText(altCfg);
                                var altEntries = ExtractFriendEntriesFromLocalConfig(altContent);
                                foreach (var (sid, nm) in altEntries)
                                {
                                    if (!ContactCache.TryGetValue(sid, out var contact))
                                    {
                                        contact = new SteamContact { SteamId = sid, PersonaName = nm };
                                        ContactCache[sid] = contact;
                                    }
                                    else if (string.IsNullOrEmpty(contact.PersonaName) && !string.IsNullOrEmpty(nm))
                                    {
                                        contact.PersonaName = nm;
                                    }
                                    if (!list.Any(c => c.SteamId == sid))
                                        list.Add(contact);
                                }
                                if (list.Count > 0) break;
                            }
                            catch { /* skip invalid folders */ }
                        }
                    }
                }

                // Final fallback: use GetLocalContacts (all accounts from loginusers.vdf)
                if (list.Count == 0)
                    list = GetLocalContacts();

                // Sort: named first, then id
                list = list
                    .OrderBy(c => string.IsNullOrEmpty(c.PersonaName) || c.PersonaName == c.SteamId ? 1 : 0)
                    .ThenBy(c => c.PersonaName)
                    .ThenBy(c => c.SteamId)
                    .ToList();

                FriendsCache[accountId.Value.ToString()] = list;
                return list;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetLocalFriends failed: " + ex.Message);
                return new List<SteamContact>();
            }
        }

        /// <summary>
        /// Name shown in Steam Friends for <paramref name="steamId64"/> on this PC.
        /// Scans ALL userdata/localconfig.vdf files — mirrors v2 get_all_contacts() behavior
        /// (friends can be stored in ANY Steam account's localconfig, not just the current one).
        /// </summary>
        private static bool TryGetFriendListDisplayName(string steamId64, out string displayName)
        {
            displayName = "";
            if (string.IsNullOrEmpty(steamId64)) return false;

            // Use a sentinel value (0) so we always scan all localconfig.vdf files regardless of active account.
            const uint AllAccountsSentinel = 0;
            if (_friendListDisplayBySteamId == null || _friendListDisplayLoadedForAccount != AllAccountsSentinel)
                LoadFriendListDisplayMap();

            if (_friendListDisplayBySteamId != null &&
                _friendListDisplayBySteamId.TryGetValue(steamId64, out var nm) &&
                !string.IsNullOrWhiteSpace(nm))
            {
                displayName = nm;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Load friend display names from ALL localconfig.vdf files under userdata/.
        /// Unlike the original that only checked the active account, this mirrors v2
        /// by scanning every account — players can be friends with any Steam account on this PC.
        /// </summary>
        private static void LoadFriendListDisplayMap()
        {
            _friendListDisplayBySteamId = new Dictionary<string, string>(StringComparer.Ordinal);
            // Sentinel ensures we always re-scan when called; no stale cache.
            _friendListDisplayLoadedForAccount = 0;

            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath)) return;

            void MergeEntries(string vdfText)
            {
                foreach (var (sid, nm) in ExtractFriendEntriesFromLocalConfig(vdfText))
                {
                    if (string.IsNullOrWhiteSpace(nm) || string.IsNullOrEmpty(sid)) continue;
                    _friendListDisplayBySteamId![sid] = nm.Trim();
                }
            }

            var userdataRoot = Path.Combine(steamPath, "userdata");
            if (!Directory.Exists(userdataRoot)) return;

            foreach (var folder in Directory.GetDirectories(userdataRoot))
            {
                try
                {
                    var folderName = Path.GetFileName(folder);
                    // Accept both uint (Steam Account ID) and string (SteamID64) folder names
                    if (!uint.TryParse(folderName, out _) &&
                        !folderName.StartsWith("7656119", StringComparison.Ordinal))
                        continue;
                    var cfg = Path.Combine(folder, "config", "localconfig.vdf");
                    if (!File.Exists(cfg)) continue;
                    MergeEntries(File.ReadAllText(cfg));
                }
                catch { /* skip invalid folders */ }
            }
        }

        private static List<(string steamId64, string name)> ExtractFriendEntriesFromLocalConfig(string content)
        {
            var results = new List<(string, string)>();
            if (string.IsNullOrEmpty(content)) return results;

            // Locate "friends" section
            int idx = IndexOfToken(content, "\"friends\"");
            if (idx < 0) idx = IndexOfToken(content, "\"Friends\"");
            if (idx < 0)
                return results;

            // Take a large snippet starting from the friends keyword (covers the entire friends block)
            string snippet = content.Substring(idx, Math.Min(content.Length - idx, 5_000_000));

            // Try VDF parse first (handles nested structures properly)
            var vdfResults = ExtractFriendsViaVdfParse(snippet);
            if (vdfResults.Count > 0)
                return vdfResults;

            // Fallback: scan keyed blocks (regex [^}] breaks on nested VDF)
            return ExtractFriendsViaRegex(snippet);
        }

        /// <summary>VDF-aware parse of friends block. Wraps snippet in a synthetic root key and
        /// parses it as proper VDF to handle nested { } correctly.</summary>
        private static List<(string steamId64, string name)> ExtractFriendsViaVdfParse(string snippet)
        {
            var results = new List<(string, string)>();
            var seen = new HashSet<string>();

            int braceStart = snippet.IndexOf('{');
            if (braceStart < 0) return results;

            string block = ExtractVdfBracedBlock(snippet, braceStart);
            if (string.IsNullOrEmpty(block)) return results;

            // Wrap in synthetic root so _tokenize_vdf produces a clean tree
            string wrapped = "\"_root\" " + block;
            var tokens = TokenizeVdf(wrapped);
            var dict = BuildVdfDict(tokens);

            if (!dict.TryGetValue("_root", out var rootVal) || rootVal is not Dictionary<string, object> root)
                return results;

            foreach (var kvp in root)
            {
                if (!IsDigitKey(kvp.Key)) continue;
                if (kvp.Value is not Dictionary<string, object> entry) continue;

                string? name = null;
                if (TryGetVdfChildString(entry, "name", out var n0)) name = n0;
                else if (TryGetVdfChildString(entry, "persona_name", out var n1)) name = n1;

                if (string.IsNullOrEmpty(name) || name.Length > 100) continue;

                string steamId64 = ConvertToSteamId64(kvp.Key);
                if (string.IsNullOrEmpty(steamId64) || seen.Contains(steamId64)) continue;
                seen.Add(steamId64);
                results.Add((steamId64, name));
            }
            return results;
        }

        private static bool TryGetVdfChildString(Dictionary<string, object> d, string key, out string? value)
        {
            foreach (var k in d.Keys)
            {
                if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
                value = d[k]?.ToString();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>Scan friend entries by SteamID/account key + balanced <c>{ }</c> block (nested VDF safe).</summary>
        private static List<(string steamId64, string name)> ExtractFriendsViaRegex(string snippet)
        {
            var results = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddFromBlock(string idRaw, int openBraceIndex)
            {
                if (openBraceIndex < 0 || openBraceIndex >= snippet.Length || snippet[openBraceIndex] != '{')
                    return;
                string block = ExtractVdfBracedBlock(snippet, openBraceIndex);
                if (string.IsNullOrEmpty(block)) return;
                if (!block.Contains("\"name\"", StringComparison.OrdinalIgnoreCase) &&
                    !block.Contains("\"persona_name\"", StringComparison.OrdinalIgnoreCase))
                    return;

                string? name = TryExtractFriendDisplayNameFromBlock(block);
                if (string.IsNullOrEmpty(name) || name.Length > 100) return;

                string steamId64 = ConvertToSteamId64(idRaw);
                if (string.IsNullOrEmpty(steamId64) || seen.Contains(steamId64)) return;
                seen.Add(steamId64);
                results.Add((steamId64, name.Trim()));
            }

            foreach (Match m in Regex.Matches(snippet, @"""(7656119\d{10})""\s*\{", RegexOptions.None))
                AddFromBlock(m.Groups[1].Value, m.Index + m.Length - 1);

            foreach (Match m in Regex.Matches(snippet, @"""(\d{8,11})""\s*\{", RegexOptions.None))
                AddFromBlock(m.Groups[1].Value, m.Index + m.Length - 1);

            return results;
        }

        private static string? TryExtractFriendDisplayNameFromBlock(string block)
        {
            var nm = Regex.Match(block, @"""name""\s+""([^""]*)""", RegexOptions.IgnoreCase);
            if (nm.Success && !string.IsNullOrWhiteSpace(nm.Groups[1].Value))
                return nm.Groups[1].Value;
            var pn = Regex.Match(block, @"""persona_name""\s+""([^""]*)""", RegexOptions.IgnoreCase);
            if (pn.Success && !string.IsNullOrWhiteSpace(pn.Groups[1].Value))
                return pn.Groups[1].Value;
            return null;
        }

        /// <summary>Convert a key to SteamID64 string.
        /// - If already a 17-digit SteamID64 (starts 7656119): return as-is.
        /// - If a short account id (8-11 digits): <c>SteamID64 = base + account_id</c> (same as v2 _account_id_to_steam64).
        /// </summary>
        private static string ConvertToSteamId64(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            key = key.Trim();
            if (key.StartsWith("7656119") && key.Length == 17 && IsDigitsOnly(key))
                return key;
            // Accept 8-11 digit account IDs (mirrors Python v2 steam_api._friend_key_to_account_id_or_none
            // and _friends_regex_fallback which uses {8,11}).
            if (key.Length >= 8 && key.Length <= 11 && IsDigitsOnly(key))
            {
                try
                {
                    ulong accountId = ulong.Parse(key);
                    return (SteamIdBase + accountId).ToString();
                }
                catch { return ""; }
            }
            return "";
        }

        private static bool IsDigitKey(string s) => !string.IsNullOrEmpty(s) && IsDigitsOnly(s);
        private static bool IsDigitsOnly(string s) { foreach (char c in s) if (c < '0' || c > '9') return false; return s.Length > 0; }

        /// <summary>Tokenize a VDF string into a flat token list (handles escape sequences).</summary>
        private static List<string> TokenizeVdf(string content)
        {
            var tokens = new List<string>();
            int i = 0, n = content.Length;

            while (i < n)
            {
                char c = content[i];

                // Skip whitespace but preserve structure
                if (c == ' ' || c == '\t' || c == '\r')
                { i++; continue; }

                // Newlines are skipped (token-less)
                if (c == '\n')
                { i++; continue; }

                // Single-line comment
                if (c == '/' && i + 1 < n && content[i + 1] == '/')
                { i = content.IndexOf('\n', i); if (i < 0) i = n; continue; }

                // Block comment
                if (c == '/' && i + 1 < n && content[i + 1] == '*')
                {
                    int end = content.IndexOf("*/", i + 2);
                    i = end < 0 ? n : end + 2;
                    continue;
                }

                // Brace tokens
                if (c == '{' || c == '}')
                { tokens.Add(c.ToString()); i++; continue; }

                // Quoted string
                if (c == '"')
                {
                    i++;
                    var chars = new System.Text.StringBuilder();
                    while (i < n)
                    {
                        char ch = content[i];
                        if (ch == '\\' && i + 1 < n)
                        {
                            i++;
                            char nc = content[i];
                            string es = nc switch { 'n' => "\n", 't' => "\t", 'r' => "\r", '\\' => "\\", '"' => "\"", _ => nc.ToString() };
                            chars.Append(es);
                            i++;
                            continue;
                        }
                        if (ch == '"') { i++; break; }
                        chars.Append(ch);
                        i++;
                    }
                    tokens.Add(chars.ToString());
                    continue;
                }

                i++;
            }
            return tokens;
        }

        /// <summary>Build a nested dictionary from a VDF token sequence.</summary>
        private static Dictionary<string, object> BuildVdfDict(List<string> tokens)
        {
            var root = new Dictionary<string, object>();
            var stack = new List<Dictionary<string, object>> { root };
            string? pendingKey = null;

            for (int i = 0; i < tokens.Count; i++)
            {
                string tok = tokens[i];
                if (tok == "{")
                {
                    if (pendingKey != null)
                    {
                        var newDict = new Dictionary<string, object>();
                        stack[^1][pendingKey] = newDict;
                        stack.Add(newDict);
                        pendingKey = null;
                    }
                }
                else if (tok == "}")
                {
                    if (stack.Count > 1) stack.RemoveAt(stack.Count - 1);
                    pendingKey = null;
                }
                else
                {
                    // Quoted string token
                    if (pendingKey != null)
                    {
                        stack[^1][pendingKey] = tok;
                        pendingKey = null;
                    }
                    else
                    {
                        pendingKey = tok;
                    }
                }
            }
            return root;
        }

        /// <summary>Extract a complete { ... } block starting from openBraceIdx (exclusive of the brace itself).</summary>
        private static string ExtractVdfBracedBlock(string text, int openBraceIdx)
        {
            if (openBraceIdx < 0 || openBraceIdx >= text.Length || text[openBraceIdx] != '{')
                return "";
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openBraceIdx; i < text.Length; i++)
            {
                char c = text[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(openBraceIdx, i - openBraceIdx + 1);
                }
            }
            return "";
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
