using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace MultiplayerTools.Core
{
    /// <summary>
    /// Save file I/O. Reads/writes SlayTheSpire2 save files.
    /// Save files are JSON with CRLF line endings.
    /// </summary>
    internal static class SaveManagerHelper
    {
        private static IEnumerable<string> EnumerateSaveRoots()
        {
            yield return OS.GetUserDataDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? roamingSts2 = null;
            try
            {
                var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                var sts2 = Path.Combine(appData, "SlayTheSpire2");
                if (Directory.Exists(sts2))
                    roamingSts2 = sts2;
            }
            catch { }
            if (roamingSts2 != null)
                yield return roamingSts2;
        }

        /// <summary>Get all Steam user profile directories.</summary>
        internal static List<SaveProfile> GetAllProfiles()
        {
            var profiles = new List<SaveProfile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var root in EnumerateSaveRoots())
                {
                    var steamDir = Path.Combine(root, "steam");
                    if (!Directory.Exists(steamDir)) continue;

                    foreach (var steamFolder in Directory.GetDirectories(steamDir))
                    {
                        var moddedDir = Path.Combine(steamFolder, "modded");
                        if (!Directory.Exists(moddedDir)) continue;

                        foreach (var profileDir in Directory.GetDirectories(moddedDir))
                        {
                            var profileName = Path.GetFileName(profileDir);
                            var savesDir = Path.Combine(profileDir, "saves");
                            if (!Directory.Exists(savesDir)) continue;

                            var key = Path.GetFullPath(savesDir);
                            if (!seen.Add(key)) continue;

                            var saves = GetSavesInProfile(savesDir);
                            var currentSave = Path.Combine(savesDir, "current_run_mp.save");
                            profiles.Add(new SaveProfile
                            {
                                SteamId = Path.GetFileName(steamFolder),
                                ProfileName = profileName,
                                ProfileDir = profileDir,
                                SavesDir = savesDir,
                                Saves = saves,
                                HasCurrentSave = File.Exists(currentSave)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetAllProfiles failed: " + ex.Message);
            }
            return profiles;
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

        /// <summary>Parse a save file as JSON dictionary.</summary>
        internal static Dictionary<string, object>? ParseSaveFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                return JsonElementToDict(doc.RootElement);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] ParseSaveFile({path}) failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Write a save file with CRLF line endings.</summary>
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

        /// <summary>Copy current save to a backup directory.</summary>
        internal static string? BackupCurrent(string steamId, string profileName)
        {
            try
            {
                string? srcDir = null;
                foreach (var root in EnumerateSaveRoots())
                {
                    var candidate = Path.Combine(root, "steam", steamId, "modded", profileName, "saves");
                    var currentSave = Path.Combine(candidate, "current_run_mp.save");
                    if (Directory.Exists(candidate) && File.Exists(currentSave))
                    {
                        srcDir = candidate;
                        break;
                    }
                }
                if (srcDir == null) return null;

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupBase = OS.GetUserDataDir().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var backupDir = Path.Combine(backupBase, "backups", $"{profileName}_{timestamp}");
                Directory.CreateDirectory(backupDir);

                foreach (var f in Directory.GetFiles(srcDir))
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

    internal class SaveProfile
    {
        internal string SteamId { get; set; } = "";
        internal string ProfileName { get; set; } = "";
        internal string ProfileDir { get; set; } = "";
        internal string SavesDir { get; set; } = "";
        internal List<SaveInfo> Saves { get; set; } = new();
        internal bool HasCurrentSave { get; set; }
    }

    internal class SaveInfo
    {
        internal string Path { get; set; } = "";
        internal string FileName { get; set; } = "";
        internal DateTime LastWriteTime { get; set; }
        internal long SizeBytes { get; set; }
    }
}
