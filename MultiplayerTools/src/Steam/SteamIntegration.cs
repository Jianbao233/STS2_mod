using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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

        // Steam Account ID -> SteamID64 conversion base
        private const ulong SteamIdBase = 76561197960265728UL;

        // Steam Web API for persona name lookup (no API key needed for public data)
        private const string SteamApiUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries_v0002/?key=&steamids=";

        // Shared HttpClient (disposed on shutdown)
        private static System.Net.Http.HttpClient? _httpClient;

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
        /// Convert Steam Account ID to SteamID64.
        /// Formula: SteamID64 = 76561197960265728 + accountId * 2 + (accountId & 1)
        /// </summary>
        private static ulong AccountIdToSteamId64(uint accountId)
        {
            return SteamIdBase + ((ulong)accountId * 2) + ((ulong)accountId & 1UL);
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

        /// <summary>Get persona name (nickname) for a Steam ID — two-level priority:
        /// 1. Local cache (PersonaNameCache, pre-populated by RefreshPersonaNameCacheAsync)
        /// 2. users.vdf (local Steam cache, fast, always available)
        ///
        /// This method is always synchronous and safe to call from the main thread.
        /// For async lookup use RefreshPersonaNameCacheAsync() and wait for it before rendering.
        /// </summary>
        internal static string GetPersonaName(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return steamId;
            if (PersonaNameCache.TryGetValue(steamId, out var cached)) return cached;

            // Local users.vdf (fast, no network, safe on main thread)
            var localName = GetPersonaNameFromUsersVdf(steamId);
            if (!string.IsNullOrEmpty(localName) && localName != steamId)
            {
                PersonaNameCache[steamId] = localName;
                return localName;
            }

            // Fallback: loginusers.vdf may contain persona names
            var loginName = GetPersonaNameFromLoginUsers(steamId);
            if (!string.IsNullOrEmpty(loginName) && loginName != steamId)
            {
                PersonaNameCache[steamId] = loginName;
                return loginName;
            }

            // No Web API here — avoid .Result blocking the main thread.
            // Call RefreshPersonaNameCacheAsync() during init if web names are needed.
            return steamId;
        }

        /// <summary>
        /// Asynchronously fetch persona names from Steam WebAPI for the given Steam IDs
        /// and populate the PersonaNameCache. Safe to fire-and-forget from the main thread.
        /// </summary>
        internal static async Task RefreshPersonaNameCacheAsync(params string[] steamIds)
        {
            foreach (var sid in steamIds)
            {
                if (string.IsNullOrEmpty(sid)) continue;
                if (PersonaNameCache.ContainsKey(sid)) continue;

                var name = await FetchPersonaNameFromWebApi(sid);
                if (!string.IsNullOrEmpty(name) && name != sid)
                    PersonaNameCache[sid] = name;
            }
        }

        /// <summary>
        /// Kick off async preload of persona names for all known multiplayer save Steam IDs.
        /// Safe to call from the main thread — fire and forget.
        /// </summary>
        internal static async Task PreloadPersonaNamesAsync()
        {
            try
            {
                var steamIds = Core.SaveManagerHelper.GetAllProfiles()
                    .Select(p => p.SteamId)
                    .Where(sid => !string.IsNullOrEmpty(sid) && !PersonaNameCache.ContainsKey(sid))
                    .Distinct()
                    .ToArray();

                if (steamIds.Length == 0) return;

                foreach (var sid in steamIds)
                {
                    var name = await FetchPersonaNameFromWebApi(sid);
                    if (!string.IsNullOrEmpty(name) && name != sid)
                        PersonaNameCache[sid] = name;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] PreloadPersonaNamesAsync failed: " + ex.Message);
            }
        }

        /// <summary>Fetch persona name from local users.vdf cache.</summary>
        private static string? GetPersonaNameFromUsersVdf(string steamId)
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return null;
                var usersVdf = Path.Combine(steamPath, "config", "users.vdf");
                if (File.Exists(usersVdf))
                {
                    var content = File.ReadAllText(usersVdf);
                    var pattern = $@"""{steamId}""[^}}]*?""PersonaName""\s+""([^""]+)""";
                    var match = Regex.Match(content, pattern, RegexOptions.Singleline);
                    if (match.Success)
                        return match.Groups[1].Value;
                }
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
                var idx = content.IndexOf($"\"{steamId}\"");
                if (idx < 0) return null;

                // Find the PersonaName within the same account block
                int blockStart = content.LastIndexOf('{', idx);
                int blockEnd = FindMatchingBrace(content, blockStart);
                if (blockEnd <= blockStart) return null;

                string block = content.Substring(blockStart, blockEnd - blockStart + 1);
                var nameMatch = Regex.Match(block, @"""PersonaName""\s+?""([^""]+)""");
                if (nameMatch.Success)
                    return nameMatch.Groups[1].Value;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] GetPersonaNameFromLoginUsers({steamId}) failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>Fetch persona name from Steam WebAPI (async). No API key required for public data.</summary>
        private static async Task<string?> FetchPersonaNameFromWebApi(string steamId)
        {
            try
            {
                if (_httpClient == null)
                    _httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };

                var url = SteamApiUrl + steamId;
                var response = await _httpClient.GetStringAsync(url);

                // Parse JSON: {"response": {"players": [{"personaname": "..."}]}}
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (root.TryGetProperty("response", out var resp) &&
                    resp.TryGetProperty("players", out var players) &&
                    players.ValueKind == JsonValueKind.Array &&
                    players.GetArrayLength() > 0)
                {
                    var player = players[0];
                    if (player.TryGetProperty("personaname", out var personaName))
                    {
                        var name = personaName.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            GD.Print($"[MultiplayerTools] GetPersonaName WebAPI: {steamId} -> {name}");
                            return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] FetchPersonaNameFromWebApi({steamId}) failed: " + ex.Message);
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
                GD.Print($"[MultiplayerTools] GetLocalFriends: accountId={accountId?.ToString() ?? "null"}, steamId64={steamId64 ?? "null"}");

                if (!accountId.HasValue)
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: no local Steam Account ID found (Steam not running?)");
                    return new List<SteamContact>();
                }

                if (FriendsCache.TryGetValue(accountId.Value.ToString(), out var cached))
                {
                    GD.Print($"[MultiplayerTools] GetLocalFriends: cache hit, returning {cached.Count} contacts");
                    return cached;
                }

                var steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: Steam install path not found");
                    return new List<SteamContact>();
                }

                // Use account ID (not SteamID64!) as folder name
                var localCfg = Path.Combine(steamPath, "userdata", accountId.Value.ToString(), "config", "localconfig.vdf");
                GD.Print($"[MultiplayerTools] GetLocalFriends: steamPath={steamPath}, userdata folder={accountId}, localCfg={localCfg}, exists={File.Exists(localCfg)}");

                if (!File.Exists(localCfg))
                {
                    GD.PrintErr("[MultiplayerTools] GetLocalFriends: localconfig.vdf not found for user " + accountId);
                    // Fallback: try SteamID64 folder name
                    var altPath = Path.Combine(steamPath, "userdata", steamId64 ?? "", "config", "localconfig.vdf");
                    GD.Print($"[MultiplayerTools] GetLocalFriends: trying SteamID64 path: {altPath}, exists={File.Exists(altPath)}");
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
                GD.Print($"[MultiplayerTools] GetLocalFriends: extracted {friendEntries.Count} entries from localconfig.vdf");
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
                    GD.Print("[MultiplayerTools] GetLocalFriends: no friends in current user folder, scanning all userdata folders...");
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
                {
                    GD.Print("[MultiplayerTools] GetLocalFriends: no friends found, falling back to GetLocalContacts");
                    list = GetLocalContacts();
                }

                // Sort: named first, then id
                list = list
                    .OrderBy(c => string.IsNullOrEmpty(c.PersonaName) || c.PersonaName == c.SteamId ? 1 : 0)
                    .ThenBy(c => c.PersonaName)
                    .ThenBy(c => c.SteamId)
                    .ToList();

                GD.Print($"[MultiplayerTools] GetLocalFriends: returning {list.Count} contacts (with fallback)");
                FriendsCache[accountId.Value.ToString()] = list;
                return list;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetLocalFriends failed: " + ex.Message);
                return new List<SteamContact>();
            }
        }

        private static List<(string steamId64, string name)> ExtractFriendEntriesFromLocalConfig(string content)
        {
            var results = new List<(string, string)>();
            if (string.IsNullOrEmpty(content)) return results;

            // Locate "friends" section
            int idx = IndexOfToken(content, "\"friends\"");
            if (idx < 0) idx = IndexOfToken(content, "\"Friends\"");
            if (idx < 0) return results;

            // Take a large snippet starting from the friends keyword (covers the entire friends block)
            string snippet = content.Substring(idx, Math.Min(content.Length - idx, 5_000_000));
            GD.Print($"[MultiplayerTools] ExtractFriends: snippet length={snippet.Length}, starts with: {snippet.Substring(0, Math.Min(200, snippet.Length))}");

            // Try VDF parse first (handles nested structures properly)
            var vdfResults = ExtractFriendsViaVdfParse(snippet);
            GD.Print($"[MultiplayerTools] ExtractFriends: VDF parse found {vdfResults.Count} entries");
            if (vdfResults.Count > 0) return vdfResults;

            // Fallback: regex for older flat format (account id or SteamID64 as keys)
            var regexResults = ExtractFriendsViaRegex(snippet);
            GD.Print($"[MultiplayerTools] ExtractFriends: regex found {regexResults.Count} entries");
            return regexResults;
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
            if (string.IsNullOrEmpty(block)) { GD.Print("[MultiplayerTools] VDF: block empty"); return results; }

            // Wrap in synthetic root so _tokenize_vdf produces a clean tree
            string wrapped = "\"_root\" " + block;
            GD.Print($"[MultiplayerTools] VDF: wrapped length={wrapped.Length}, first 200: {wrapped.Substring(0, Math.Min(200, wrapped.Length))}");
            var tokens = TokenizeVdf(wrapped);
            GD.Print($"[MultiplayerTools] VDF: {tokens.Count} tokens");
            var dict = BuildVdfDict(tokens);
            GD.Print($"[MultiplayerTools] VDF: dict has {dict.Count} top-level keys: [{string.Join(",", dict.Keys)}]");

            if (!dict.TryGetValue("_root", out var rootVal) || rootVal is not Dictionary<string, object> root)
                return results;

            GD.Print($"[MultiplayerTools] VDF: root has {root.Count} entries");
            foreach (var kvp in root)
            {
                if (!IsDigitKey(kvp.Key)) continue;
                if (kvp.Value is not Dictionary<string, object> entry) continue;
                if (!entry.TryGetValue("name", out var nameRaw)) {
                    GD.Print($"[MultiplayerTools] VDF: entry key={kvp.Key} has no 'name' field, keys={string.Join(",", entry.Keys)}");
                    // Fallback to persona_name
                    if (entry.TryGetValue("persona_name", out var pnRaw))
                    {
                        string pnName = pnRaw?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(pnName) && pnName.Length <= 100)
                        {
                            string sidResult = ConvertToSteamId64(kvp.Key);
                            if (!string.IsNullOrEmpty(sidResult) && !seen.Contains(sidResult))
                            {
                                seen.Add(sidResult);
                                results.Add((sidResult, pnName));
                                GD.Print($"[MultiplayerTools] VDF: added via persona_name: {pnName}");
                            }
                        }
                    }
                    continue;
                }
                string name = nameRaw?.ToString() ?? "";
                if (string.IsNullOrEmpty(name) || name.Length > 100) continue;

                string steamId64 = ConvertToSteamId64(kvp.Key);
                if (string.IsNullOrEmpty(steamId64)) continue;
                if (seen.Contains(steamId64)) continue;
                seen.Add(steamId64);
                results.Add((steamId64, name));
            }
            return results;
        }

        /// <summary>Regex fallback for older flat-format friends lists without nested objects.</summary>
        private static List<(string steamId64, string name)> ExtractFriendsViaRegex(string snippet)
        {
            var results = new List<(string, string)>();
            var seen = new HashSet<string>();

            void AddFriend(string idRaw, string name)
            {
                name = name.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > 100) return;
                string steamId64 = ConvertToSteamId64(idRaw);
                if (string.IsNullOrEmpty(steamId64) || seen.Contains(steamId64)) return;
                seen.Add(steamId64);
                results.Add((steamId64, name));
            }

            // New format: SteamID64 as key (17 digits starting 7656119)
            var sid64Matches = Regex.Matches(snippet, @"""(7656119\d{10})""\s*\{[^}]*?""name""\s+?""([^""]+)""", RegexOptions.Singleline);
            GD.Print($"[MultiplayerTools] Regex: found {sid64Matches.Count} SteamID64 entries");
            foreach (Match m in sid64Matches)
                AddFriend(m.Groups[1].Value, m.Groups[2].Value);

            // Legacy format: account id as key (8-11 digits)
            var legacyMatches = Regex.Matches(snippet, @"""(\d{8,11})""\s*\{[^}]*?""name""\s+?""([^""]+)""", RegexOptions.Singleline);
            GD.Print($"[MultiplayerTools] Regex: found {legacyMatches.Count} legacy account entries");
            foreach (Match m in legacyMatches)
                AddFriend(m.Groups[1].Value, m.Groups[2].Value);

            // Also try "persona_name" field (used in some Steam versions)
            var pnMatches = Regex.Matches(snippet, @"""(7656119\d{10})""\s*\{[^}]*?""persona_name""\s+?""([^""]+)""", RegexOptions.Singleline);
            GD.Print($"[MultiplayerTools] Regex: found {pnMatches.Count} persona_name entries");
            foreach (Match m in pnMatches)
                AddFriend(m.Groups[1].Value, m.Groups[2].Value);

            return results;
        }

        /// <summary>Convert a key to SteamID64 string.
        /// - If already a 17-digit SteamID64 (starts 7656119): return as-is.
        /// - If an account id (8-11 digits): convert using SteamID64 formula.</summary>
        private static string ConvertToSteamId64(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            key = key.Trim();
            if (key.StartsWith("7656119") && key.Length == 17 && IsDigitsOnly(key))
                return key;
            if (key.Length >= 8 && key.Length <= 11 && IsDigitsOnly(key))
            {
                try
                {
                    ulong accountId = ulong.Parse(key);
                    ulong steamId64 = SteamIdBase + (accountId * 2) + (accountId & 1);
                    return steamId64.ToString();
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
