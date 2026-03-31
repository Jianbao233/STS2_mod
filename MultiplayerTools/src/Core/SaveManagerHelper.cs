using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

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

        private static IEnumerable<string> EnumerateSaveRoots()
        {
            // Optional override (same as Python v2 save_io.py SLAY_THE_SPIRE2_APPDATA)
            string? envOverride = TryGetEnvSaveRoot();
            if (envOverride != null)
                yield return envOverride;

            // Priority: %APPDATA%\SlayTheSpire2  (matches Python v2 / Steam reality)
            string roaming = GetRoamingSts2Dir();
            if (Directory.Exists(roaming))
                yield return roaming;

            // Godot's own user data dir (fallback for dev/testing)
            string godotDir = OS.GetUserDataDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(godotDir, roaming, StringComparison.OrdinalIgnoreCase))
                yield return godotDir;
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

            foreach (var root in EnumerateSaveRoots())
            {
                var steamDir = Path.Combine(root, "steam");
                if (!Directory.Exists(steamDir)) continue;

                foreach (var steamFolder in Directory.GetDirectories(steamDir))
                {
                    string steamId = Path.GetFileName(steamFolder);

                    // Scan both modded and non-modded sub-trees
                    string[] subPaths = new[]
                    {
                        Path.Combine(steamFolder, "modded"),
                        steamFolder
                    };

                    foreach (var subPath in subPaths)
                    {
                        if (!Directory.Exists(subPath)) continue;

                        foreach (var profileDir in Directory.GetDirectories(subPath))
                        {
                            var profileName = Path.GetFileName(profileDir);
                            var savesDir = Path.Combine(profileDir, "saves");

                            // Resolve the savesDir path for deduplication
                            string resolvedSavesDir;
                            try { resolvedSavesDir = Path.GetFullPath(savesDir); }
                            catch { resolvedSavesDir = savesDir; }
                            if (!seenSavePaths.Add(resolvedSavesDir)) continue;

                            // Build enriched profile
                            var profile = new SaveProfile
                            {
                                SteamId = steamId,
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
                                    profile.RelPath = $"{steamId}/{moddedPrefix}{profileName}/saves/{Path.GetFileName(cand)}";
                                    profile.ProfileKey = $"{steamId}/{moddedPrefix}{profileName}";
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

            var roots = string.Join(" | ", EnumerateSaveRoots());
            GD.Print($"[MultiplayerTools] GetAllProfiles: {results.Count} profile(s), roots=[{roots}]");
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
        internal static string? FindCurrentSave(string steamId, string profileName)
        {
            foreach (var root in EnumerateSaveRoots())
            {
                // Try modded first, then non-modded
                string[] candidates = new[]
                {
                    Path.Combine(root, "steam", steamId, "modded", profileName, "saves", "current_run_mp.save"),
                    Path.Combine(root, "steam", steamId, profileName, "saves", "current_run_mp.save"),
                    Path.Combine(root, "steam", steamId, "modded", profileName, "saves", "current_run.save"),
                    Path.Combine(root, "steam", steamId, profileName, "saves", "current_run.save"),
                };
                foreach (var p in candidates)
                    if (File.Exists(p)) return p;
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

        /// <summary>Write a save file with CRLF line endings (no gzip).</summary>
        internal static bool WriteSaveFile(string path, Dictionary<string, object> data)
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, opts);
                json = json.Replace("\n", "\r\n");
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] WriteSaveFile({path}) failed: " + ex.Message);
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

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                // Write to %APPDATA%\SlayTheSpire2\backups\ (matches Python v2 backup_dir)
                var backupDir = Path.Combine(GetRoamingSts2Dir(), "backups", $"{profileName}_{timestamp}");
                Directory.CreateDirectory(backupDir);

                foreach (var f in Directory.GetFiles(savesDir))
                {
                    File.Copy(f, Path.Combine(backupDir, Path.GetFileName(f)), true);
                }
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
            return Path.Combine(GetRoamingSts2Dir(), "backups");
        }

        /// <summary>Get a human-readable size string.</summary>
        internal static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

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
                case JsonValueKind.String: return el.GetString() ?? "";
                case JsonValueKind.Number: return el.TryGetInt64(out var l) ? l : el.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Array: return el.EnumerateArray().Select(JsonValueToObject).ToList();
                case JsonValueKind.Object: return JsonElementToDict(el);
                default: return "";
            }
        }
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
