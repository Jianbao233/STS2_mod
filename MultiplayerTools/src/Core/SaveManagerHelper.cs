using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MultiplayerTools.Platform;

namespace MultiplayerTools.Core
{
    /// <summary>
    /// Save file I/O. Reads/writes SlayTheSpire2 save files.
    /// Save files are JSON with CRLF line endings, sometimes gzip-compressed.
    ///
    /// All paths follow the same layout as the Python v2 tool:
    ///   %APPDATA%\SlayTheSpire2\steam\{steamId}\[modded\]profileN\saves\
    ///   %APPDATA%\SlayTheSpire2\backups\
    /// </summary>
    internal static class SaveManagerHelper
    {
        /// <summary>Written next to copied saves so backups without Steam ID in the folder name still group correctly.</summary>
        internal const string BackupMetaFileName = "mp_backup_meta.json";
        private static readonly string[] AccountPlatformDirs = { "steam", "default", "editor" };

        private static string GetRoamingSts2Dir()
        {
            try
            {
                var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "SlayTheSpire2");
            }
            catch
            {
                return "";
            }
        }

        internal static string GetPrimarySaveRoot()
        {
            string? envOverride = TryGetEnvSaveRoot();
            if (!string.IsNullOrEmpty(envOverride))
                return envOverride;

            string godotDir = OS.GetUserDataDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string roaming = GetRoamingSts2Dir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (PlatformInfo.IsMobile && !string.IsNullOrEmpty(godotDir))
                return godotDir;

            if (!string.IsNullOrEmpty(roaming))
                return roaming;

            return godotDir;
        }

        private static string? TryGetEnvSaveRoot()
        {
            try
            {
                var env = System.Environment.GetEnvironmentVariable("SLAY_THE_SPIRE2_APPDATA")?.Trim();
                if (string.IsNullOrEmpty(env)) return null;
                var p = env.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Directory.Exists(p) ? p : null;
            }
            catch
            {
                return null;
            }
        }

        internal static IEnumerable<string> EnumerateSaveRoots()
        {
            // Optional override (same as Python v2 save_io.py SLAY_THE_SPIRE2_APPDATA)
            string? envOverride = TryGetEnvSaveRoot();
            if (envOverride != null)
                yield return envOverride;

            string godotDir = OS.GetUserDataDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string roaming = GetRoamingSts2Dir();

            if (PlatformInfo.IsMobile)
            {
                if (Directory.Exists(godotDir))
                    yield return godotDir;
                if (!string.Equals(godotDir, roaming, StringComparison.OrdinalIgnoreCase) && Directory.Exists(roaming))
                    yield return roaming;
                yield break;
            }

            // Priority: %APPDATA%\SlayTheSpire2  (matches Python v2 / Steam reality)
            if (Directory.Exists(roaming))
                yield return roaming;

            // Godot's own user data dir (fallback for dev/testing)
            if (!string.Equals(godotDir, roaming, StringComparison.OrdinalIgnoreCase) && Directory.Exists(godotDir))
                yield return godotDir;
        }

        internal static IEnumerable<SaveAccountRoot> EnumerateAccountRoots()
        {
            foreach (var root in EnumerateSaveRoots())
            {
                foreach (var platformDirName in AccountPlatformDirs)
                {
                    var platformDir = Path.Combine(root, platformDirName);
                    if (!Directory.Exists(platformDir)) continue;

                    foreach (var accountDir in Directory.GetDirectories(platformDir))
                    {
                        yield return new SaveAccountRoot
                        {
                            Root = root,
                            PlatformDirName = platformDirName,
                            AccountId = Path.GetFileName(accountDir),
                            AccountDir = accountDir,
                        };
                    }
                }
            }
        }

        internal static bool TryParseSaveIdentity(string path, out string accountId, out string profileKey)
        {
            accountId = "";
            profileKey = "";
            try
            {
                var parts = path.Replace('\\', '/').Split('/');
                int platformIdx = Array.FindIndex(parts, p => AccountPlatformDirs.Contains(p, StringComparer.OrdinalIgnoreCase));
                if (platformIdx < 0 || platformIdx + 1 >= parts.Length)
                    return false;

                accountId = parts[platformIdx + 1];
                var afterAccount = parts.Skip(platformIdx + 2).ToArray();
                int savesIdx = Array.FindIndex(afterAccount, p => string.Equals(p, "saves", StringComparison.OrdinalIgnoreCase));
                profileKey = savesIdx >= 0
                    ? string.Join("/", afterAccount.Take(savesIdx))
                    : string.Join("/", afterAccount);
                return !string.IsNullOrEmpty(accountId) && !string.IsNullOrEmpty(profileKey);
            }
            catch
            {
                accountId = "";
                profileKey = "";
                return false;
            }
        }

        /// <summary>
        /// Scan all Steam profiles and their save files.
        /// Mirrors Python v2 save_io.scan_save_profiles:
        ///   - discovers current_run_mp.save in profile*/saves/ and modded/profile*/saves/
        ///   - loads lightweight metadata (run_time, player_count, act, ascension, save_time)
        ///   - is_active = run_time > 0
        ///   - profile_key = "steamId/subKey" format
        /// Returns flat list sorted by file mtime descending.
        /// </summary>
        internal static List<SaveProfile> GetAllProfiles()
        {
            var results = new List<SaveProfile>();
            var seenSavePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var accountRoot in EnumerateAccountRoots())
            {
                // Scan both modded and non-modded sub-trees
                string[] subPaths = new[]
                {
                    Path.Combine(accountRoot.AccountDir, "modded"),
                    accountRoot.AccountDir
                };

                foreach (var subPath in subPaths)
                {
                    if (!Directory.Exists(subPath)) continue;

                    foreach (var profileDir in Directory.GetDirectories(subPath))
                    {
                        var profileName = Path.GetFileName(profileDir);
                        if (!profileName.StartsWith("profile", StringComparison.OrdinalIgnoreCase)) continue;

                        var savesDir = Path.Combine(profileDir, "saves");

                        // Resolve the savesDir path for deduplication
                        string resolvedSavesDir;
                        try { resolvedSavesDir = Path.GetFullPath(savesDir); }
                        catch { resolvedSavesDir = savesDir; }
                        if (!seenSavePaths.Add(resolvedSavesDir)) continue;

                        // Build enriched profile
                        var profile = new SaveProfile
                        {
                            SteamId = accountRoot.AccountId,
                            ProfileName = profileName,
                            ProfileDir = profileDir,
                            SavesDir = Directory.Exists(savesDir) ? savesDir : "",
                            IsModded = subPath.EndsWith("modded", StringComparison.OrdinalIgnoreCase),
                        };

                        if (Directory.Exists(savesDir))
                        {
                            profile.Saves = GetSavesInProfile(savesDir);

                            // Try to find current_run_mp.save (or current_run.save) for v2-style enrichment
                            string[] candidates = new[]
                            {
                                Path.Combine(savesDir, "current_run_mp.save"),
                                Path.Combine(savesDir, "current_run.save"),
                            };

                            foreach (var cand in candidates)
                            {
                                if (!File.Exists(cand)) continue;
                                var data = ParseSaveFile(cand);
                                if (data == null) continue;

                                string moddedPrefix = profile.IsModded ? "modded/" : "";
                                profile.SavePath = cand;
                                profile.RelPath = $"{accountRoot.PlatformDirName}/{accountRoot.AccountId}/{moddedPrefix}{profileName}/saves/{Path.GetFileName(cand)}";
                                profile.ProfileKey = $"{accountRoot.AccountId}/{moddedPrefix}{profileName}";
                                profile.PlayerCount = GetList(data, "players").Count;
                                profile.ActIndex = GetInt(data, "current_act_index", 0);
                                profile.Ascension = GetInt(data, "ascension", 0);
                                profile.PlayersSummary = BuildPlayersSummary(GetList(data, "players"));
                                long stUnix = GetLong(data, "save_time", 0);
                                profile.SaveTime = stUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(stUnix).LocalDateTime : null;
                                profile.IsActive = GetInt(data, "run_time", 0) > 0;
                                break;
                            }
                        }

                        results.Add(profile);
                    }
                }
            }

            // Sort by file mtime descending (Python v2: reverse=True)
            results.Sort((a, b) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(a.SavePath) && File.Exists(a.SavePath) &&
                        !string.IsNullOrEmpty(b.SavePath) && File.Exists(b.SavePath))
                    {
                        return DateTime.Compare(File.GetLastWriteTimeUtc(b.SavePath), File.GetLastWriteTimeUtc(a.SavePath));
                    }
                }
                catch { }
                return 0;
            });

            return results;
        }

        /// <summary>
        /// 存档选择页「存档选择」列表过滤：仅保留当前存在多人联机主存档的档位。
        /// 与 MP_PlayerManager v2 的 scan_save_profiles（只认 current_run_mp.save）一致：
        /// 排除仅 current_run.save 的单人档、无多人存档的空档位；备份 *.backup.* 不作为独立档位扫描。
        /// </summary>
        internal static bool IsMultiplayerRunProfile(SaveProfile p)
        {
            if (string.IsNullOrEmpty(p.SavePath)) return false;
            if (!File.Exists(p.SavePath)) return false;
            if (!p.SavePath.EndsWith("current_run_mp.save", StringComparison.OrdinalIgnoreCase)) return false;
            return p.PlayerCount >= 2;
        }

        private static List<SaveInfo> GetSavesInProfile(string savesDir)
        {
            var saves = new List<SaveInfo>();
            try
            {
                foreach (var file in Directory.GetFiles(savesDir, "*.save").Concat(
                    Directory.GetFiles(savesDir, "*.run")))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        saves.Add(new SaveInfo
                        {
                            Path = file,
                            FileName = Path.GetFileName(file),
                            LastWriteTime = fi.LastWriteTime,
                            SizeBytes = fi.Length
                        });
                    }
                    catch { }
                }
                saves = saves.OrderByDescending(s => s.LastWriteTime).ToList();
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetSavesInProfile failed: " + ex.Message);
            }
            return saves;
        }

        private static string BuildPlayersSummary(List<object> players)
        {
            if (players.Count == 0) return Loc.Get("summary.no_players", "No players");
            var chars = new List<string>();
            foreach (var p in players)
            {
                if (p is Dictionary<string, object> pd)
                {
                    var charId = pd.TryGetValue("character_id", out var v) ? v?.ToString() ?? "?" : "?";
                    chars.Add(CharacterDisplayNames.Resolve(charId));
                }
            }
            return string.Join(", ", chars);
        }

        private static int GetInt(Dictionary<string, object> d, string key, int defaultVal)
        {
            if (!d.TryGetValue(key, out var v)) return defaultVal;
            return v switch
            {
                int i => i,
                long l => (int)l,
                double dbl => (int)dbl,
                _ => defaultVal
            };
        }

        private static long GetLong(Dictionary<string, object> d, string key, long defaultVal)
        {
            if (!d.TryGetValue(key, out var v)) return defaultVal;
            return v switch
            {
                int i => i,
                long l => l,
                double dbl => (long)dbl,
                _ => defaultVal
            };
        }

        private static List<object> GetList(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var v) && v is List<object> list ? list : new List<object>();
        }
        /// <summary>
        /// Find the current_run_mp.save (or current_run.save) for a given profile.
        /// Returns the full path, or null if not found.
        /// </summary>
        /// <param name="profileSpecifier">
        /// Profile folder path under <c>steam/{steamId}/</c>, e.g. <c>profile2</c> or <c>modded/profile2</c>.
        /// If there is no slash, tries <c>modded/profile</c> first then vanilla <c>profile</c>.
        /// </param>
        internal static string? FindCurrentSave(string steamId, string profileSpecifier)
        {
            profileSpecifier = profileSpecifier?.Replace('\\', '/').Trim('/') ?? "";
            if (string.IsNullOrEmpty(profileSpecifier)) return null;

            foreach (var root in EnumerateSaveRoots())
            {
                foreach (var platformDirName in AccountPlatformDirs)
                {
                    string baseDir = Path.Combine(root, platformDirName, steamId);
                    if (!Directory.Exists(baseDir)) continue;

                    IEnumerable<string> savesDirs;
                    if (profileSpecifier.Contains('/'))
                        savesDirs = new[] { Path.Combine(baseDir, profileSpecifier, "saves") };
                    else
                    {
                        savesDirs = new[]
                        {
                            Path.Combine(baseDir, "modded", profileSpecifier, "saves"),
                            Path.Combine(baseDir, profileSpecifier, "saves"),
                        };
                    }

                    foreach (var savesDir in savesDirs)
                    {
                        foreach (var name in new[] { "current_run_mp.save", "current_run.save" })
                        {
                            var p = Path.Combine(savesDir, name);
                            if (File.Exists(p)) return p;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Load current_run_mp.save (or current_run.save), auto-detecting gzip.
        /// Returns null if not found or parse fails.
        /// </summary>
        internal static Dictionary<string, object>? LoadCurrentRunJson(string steamId, string profileName)
        {
            var path = FindCurrentSave(steamId, profileName);
            if (path == null) return null;
            return ParseSaveFile(path);
        }

        /// <summary>
        /// Load a save file, auto-detecting gzip compression.
        /// Returns null on failure.
        /// </summary>
        internal static Dictionary<string, object>? ParseSaveFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                byte[] raw = File.ReadAllBytes(path);
                string json;

                if (raw.Length >= 2 && raw[0] == 0x1f && raw[1] == 0x8b)
                {
                    // gzip magic — decompress first
                    using var gzipStream = new GZipStream(new MemoryStream(raw), CompressionMode.Decompress);
                    using var outStream = new MemoryStream();
                    gzipStream.CopyTo(outStream);
                    json = System.Text.Encoding.UTF8.GetString(outStream.ToArray());
                }
                else
                {
                    json = System.Text.Encoding.UTF8.GetString(raw);
                }

                var doc = JsonDocument.Parse(json);
                return JsonElementToDict(doc.RootElement);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] ParseSaveFile({path}) failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Write a save file as plain UTF-8 JSON with CRLF (same as Python v2 <c>save_io.write_save</c>).
        /// <para>
        /// We always write <b>uncompressed</b> JSON even if the file on disk was gzip: v2 does the same
        /// ("游戏原始存档格式为明文 JSON…写入时不压缩") and the game accepts it; re-gzipping from the mod
        /// has been linked to <c>JsonParseError</c> / corrupt-save dialogs.
        /// </para>
        /// </summary>
        internal static bool WriteSaveFile(string path, Dictionary<string, object> data, bool makeBackup = false)
        {
            try
            {
                if (makeBackup && File.Exists(path))
                {
                    var dir = Path.GetDirectoryName(path) ?? ".";
                    var name = Path.GetFileName(path);
                    var backupPath = Path.Combine(dir, name + ".backup." + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    File.Copy(path, backupPath, true);
                }

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                };
                string json = JsonSerializer.Serialize(data, opts).Replace("\n", "\r\n");

                using (JsonDocument.Parse(json)) { }

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

                // Write via temp + replace (matches Python v2; avoids half-written files if the game reads mid-write).
                string tmpPath = path + ".mpwrite.tmp";
                File.WriteAllBytes(tmpPath, bytes);
                File.Move(tmpPath, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] WriteSaveFile({path}) failed: " + ex.Message);
                try
                {
                    string tmpPath = path + ".mpwrite.tmp";
                    if (File.Exists(tmpPath)) File.Delete(tmpPath);
                }
                catch { /* ignore */ }
                return false;
            }
        }

        /// <summary>
        /// Backup the current run to %APPDATA%\SlayTheSpire2\backups\.
        /// Creates a timestamped sub-folder containing all saves in the profile's saves dir.
        /// </summary>
        internal static string? BackupCurrent(string steamId, string profileName)
        {
            try
            {
                var savesDir = Path.GetDirectoryName(FindCurrentSave(steamId, profileName));
                if (savesDir == null || !Directory.Exists(savesDir)) return null;

                // Folder name: steamId + encoded profile + compact time so BackupPage can group by Steam ID.
                // Use '+' instead of '/' in profile (e.g. modded/profile2 → modded+profile2); no '_' inside timestamp.
                string tsCompact = DateTime.Now.ToString("yyyyMMddHHmmss");
                string safeProfile = profileName.Replace('\\', '+').Replace('/', '+');
                if (string.IsNullOrEmpty(steamId)) steamId = "unknown";
                var backupDir = Path.Combine(GetBackupRoot(), $"{steamId}_{safeProfile}_{tsCompact}");
                Directory.CreateDirectory(backupDir);

                foreach (var f in Directory.GetFiles(savesDir))
                {
                    File.Copy(f, Path.Combine(backupDir, Path.GetFileName(f)), true);
                }

                try
                {
                    string profileJsonKey = profileName.Replace('\\', '/');
                    string metaJson = JsonSerializer.Serialize(
                        new Dictionary<string, string>
                        {
                            ["steam_id"] = steamId,
                            ["profile_key"] = profileJsonKey,
                        },
                        new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(Path.Combine(backupDir, BackupMetaFileName), metaJson.Replace("\n", "\r\n"));
                }
                catch { /* meta is optional */ }

                return backupDir;
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] BackupCurrent failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Get the backup root directory (%APPDATA%\SlayTheSpire2\backups).</summary>
        internal static string GetBackupRoot()
        {
            return Path.Combine(GetPrimarySaveRoot(), "backups");
        }

        /// <summary>
        /// Load <c>steam_names.json</c> from the saves directory of the given save path.
        /// This file is maintained by the v2 Python tool and stores Steam persona names
        /// indexed by SteamID64 — mirroring v2 App._load_profile + get_all_contacts.
        /// Returns null if the file does not exist or cannot be parsed.
        /// </summary>
        internal static Dictionary<string, string>? LoadSteamNames(string savePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(dir)) return null;

                var namesFile = Path.Combine(dir, "steam_names.json");
                if (!File.Exists(namesFile)) return null;

                var text = File.ReadAllText(namesFile);
                if (string.IsNullOrWhiteSpace(text)) return null;

                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);
                if (raw == null) return null;

                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kvp in raw)
                {
                    if (kvp.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = kvp.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            result[kvp.Key] = s;
                    }
                }
                return result.Count > 0 ? result : null;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] LoadSteamNames failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Get a human-readable size string.</summary>
        internal static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

#nullable disable
        // Nullable disabled: JSON null must round-trip as C# null in Dictionary&lt;string, object&gt; values.
        private static Dictionary<string, object> JsonElementToDict(JsonElement el)
        {
            var dict = new Dictionary<string, object>();
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in el.EnumerateObject())
                {
                    dict[prop.Name] = JsonValueToObject(prop.Value);
                }
            }
            return dict;
        }

        private static object JsonValueToObject(JsonElement el)
        {
            switch (el.ValueKind)
            {
                // Critical: previously `default` turned null into "" and broke Godot's save schema.
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.String: return el.GetString() ?? "";
                // Prefer exact integer parsing from raw text so Steam IDs / large ints are not rounded via double.
                case JsonValueKind.Number:
                    if (el.TryGetInt64(out var i64)) return i64;
                    if (el.TryGetUInt64(out var u64) && u64 <= long.MaxValue) return (long)u64;
                    {
                        var raw = el.GetRawText();
                        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lp))
                            return lp;
                        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl))
                            return dbl;
                        return 0L;
                    }
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Array: return el.EnumerateArray().Select(JsonValueToObject).ToList();
                case JsonValueKind.Object: return JsonElementToDict(el);
                default: return null;
            }
        }
#nullable enable
    }

    internal sealed class SaveAccountRoot
    {
        internal string Root { get; set; } = "";
        internal string PlatformDirName { get; set; } = "";
        internal string AccountId { get; set; } = "";
        internal string AccountDir { get; set; } = "";
    }

    /// <summary>
    /// Represents a Steam profile's saves directory.
    /// Mirrors Python v2 save_io.SaveProfile.
    /// </summary>
    internal class SaveProfile
    {
        internal string SteamId { get; set; } = "";

        /// <summary>Full path to the primary save file (current_run_mp.save or current_run_mp.save).</summary>
        internal string SavePath { get; set; } = "";

        /// <summary>Path relative to the STS2 root, for display.</summary>
        internal string RelPath { get; set; } = "";

        /// <summary>Profile key in Python v2 format: "steamId/subKey" or "steamId/modded/subKey".</summary>
        internal string ProfileKey { get; set; } = "";

        internal int PlayerCount { get; set; }
        internal int ActIndex { get; set; }
        internal int Ascension { get; set; }

        /// <summary>Comma-joined character IDs, e.g. "CHARACTER.IRONCLAD, CHARACTER.SILENT".</summary>
        internal string PlayersSummary { get; set; } = "";

        internal DateTime? SaveTime { get; set; }

        /// <summary>True when run_time > 0 in save JSON (game in progress).</summary>
        internal bool IsActive { get; set; }

        /// <summary>True = modded/profileN path, False = profileN path.</summary>
        internal bool IsModded { get; set; }

        internal string ProfileName { get; set; } = "";
        internal string ProfileDir { get; set; } = "";
        internal string SavesDir { get; set; } = "";
        internal List<SaveInfo> Saves { get; set; } = new();
    }

    /// <summary>
    /// Represents a single save file on disk.
    /// </summary>
    internal class SaveInfo
    {
        internal string Path { get; set; } = "";
        internal string FileName { get; set; } = "";
        internal DateTime LastWriteTime { get; set; }
        internal long SizeBytes { get; set; }
    }


    /// <summary>
    /// Virtual player extracted from a save file JSON (game-independent, no Player object).
    /// Used when there is no active run (main menu / outside of game).
    /// </summary>
    internal class VPlayer
    {
        internal int Index { get; set; }
        internal string Name { get; set; } = "";
        internal string CharacterId { get; set; } = "";
        internal int CurrentHp { get; set; }
        internal int MaxHp { get; set; }
        internal int Gold { get; set; }
        internal int DeckCount { get; set; }
        internal string SavePath { get; set; } = "";

        /// <summary>Extract all players from a save JSON's "players" array.</summary>
        internal static List<VPlayer> FromSaveJson(Dictionary<string, object>? saveData, string savePath)
        {
            var players = new List<VPlayer>();
            if (saveData == null) return players;

            if (saveData.TryGetValue("players", out var raw) && raw is List<object> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Dictionary<string, object> pd)
                    {
                        players.Add(new VPlayer
                        {
                            Index = i,
                            Name = GetString(pd, "name") ?? $"Player {i + 1}",
                            CharacterId = GetString(pd, "character_id") ?? GetString(pd, "characterId") ?? "",
                            CurrentHp = GetInt(pd, "current_hp") ?? GetInt(pd, "currentHp") ?? 0,
                            MaxHp = GetInt(pd, "max_hp") ?? GetInt(pd, "maxHp") ?? 0,
                            Gold = GetInt(pd, "gold") ?? 0,
                            DeckCount = GetDeckCount(pd),
                            SavePath = savePath
                        });
                    }
                }
            }
            return players;
        }

        private static string? GetString(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var v) ? v?.ToString() : null;
        }

        private static int? GetInt(Dictionary<string, object> d, string key)
        {
            if (!d.TryGetValue(key, out var v)) return null;
            return v switch
            {
                int i => i,
                long l => (int)l,
                double dbl => (int)dbl,
                _ => null
            };
        }

        private static int GetDeckCount(Dictionary<string, object> pd)
        {
            if (pd.TryGetValue("deck", out var raw) && raw is List<object> deck)
                return deck.Count;
            return 0;
        }
    }
}
