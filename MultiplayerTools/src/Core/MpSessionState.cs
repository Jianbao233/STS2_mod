using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools
{
    /// <summary>
    /// Global session state — mirrors MP_PlayerManager v2's App.save_path / App.save_data / App.profiles.
    /// Maintains the "currently loaded save" context used by all pages.
    /// Replaces SaveTab.SelectedSavePath + SaveTab.SaveContextChanged scattered state.
    /// </summary>
    internal static class MpSessionState
    {
        // ── Current save context ──────────────────────────────────────────────────

        /// <summary>Path to the currently loaded save file, or null.</summary>
        internal static string? CurrentSavePath { get; private set; }

        /// <summary>In-memory save data dict (mirrors v2 App.save_data). Never null.</summary>
        internal static Dictionary<string, object> SaveData { get; private set; } = new();

        /// <summary>All scanned profiles (Steam user groups built by callers).</summary>
        internal static List<Core.SaveProfile> AllProfiles { get; private set; } = new();

        /// <summary>Short Steam ID shown in status bar.</summary>
        internal static string CurrentSteamIdShort => ShortenSteamId(CurrentSteamId);

        /// <summary>Current Steam ID derived from the loaded save's profile path.</summary>
        internal static string CurrentSteamId { get; private set; } = "";

        /// <summary>Current profile key derived from the loaded save's profile path.</summary>
        internal static string CurrentProfileKey { get; private set; } = "";

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>Fired when the save context changes (load / reload / clear).</summary>
        internal static event Action? SaveContextChanged;

        /// <summary>Fired when all profiles are re-scanned.</summary>
        internal static event Action? ProfilesChanged;

        // ── Profile scanning ─────────────────────────────────────────────────────

        /// <summary>Re-scan all save profiles from disk. Fires ProfilesChanged.</summary>
        internal static void RefreshProfiles()
        {
            AllProfiles = Core.SaveManagerHelper.GetAllProfiles();
            ProfilesChanged?.Invoke();
        }

        // ── Save loading ─────────────────────────────────────────────────────────

        /// <summary>Load a save file into session state. Fires SaveContextChanged.
        /// Mirrors v2 App._load_profile().</summary>
        internal static bool LoadSave(string path)
        {
            try
            {
                var data = Core.SaveManagerHelper.ParseSaveFile(path);
                if (data == null) return false;

                SaveData = data ?? new Dictionary<string, object>();
                CurrentSavePath = path;

                // Derive Steam ID + profile key from path
                DeriveProfileKeys(path);

                SaveContextChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] MpSessionState.LoadSave({path}) failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>Reload the current save from disk. Returns false if path is empty or parse fails.
        /// Fires SaveContextChanged, then ProfilesChanged to keep the save-list in sync.</summary>
        internal static bool ReloadSave()
        {
            if (string.IsNullOrEmpty(CurrentSavePath)) return false;
            var result = LoadSave(CurrentSavePath);
            if (result)
            {
                // Re-scan AllProfiles so SaveSelect re-reads player counts from disk
                // (AllProfiles[i].PlayerCount is used by IsMultiplayerRunProfile filter)
                RefreshProfiles();
            }
            return result;
        }

        /// <summary>Clear the save context. Fires SaveContextChanged.</summary>
        internal static void ClearSave()
        {
            CurrentSavePath = null;
            SaveData = new Dictionary<string, object>();
            CurrentSteamId = "";
            CurrentProfileKey = "";
            SaveContextChanged?.Invoke();
        }

        /// <summary>Write the current save data back to disk.</summary>
        internal static bool FlushSave()
        {
            if (string.IsNullOrEmpty(CurrentSavePath)) return false;
            // No automatic sidecar backup on every flush (avoid spam); PlayerOpsService uses backup on structured ops
            return Core.SaveManagerHelper.WriteSaveFile(CurrentSavePath, SaveData, makeBackup: false);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static void DeriveProfileKeys(string path)
        {
            try
            {
                // Path format: .../steam/{steamId}/[modded/]profileN/saves/current_run_mp.save
                // CurrentProfileKey must be only the profile folder segment(s), e.g. "modded/profile2"
                // or "profile2" — NOT "saves" or the save filename (BackupCurrent / FindCurrentSave depend on this).
                var parts = path.Replace('\\', '/').Split('/');
                int steamIdx = Array.IndexOf(parts, "steam");
                if (steamIdx >= 0 && steamIdx + 1 < parts.Length)
                {
                    CurrentSteamId = parts[steamIdx + 1];
                    var afterSteam = parts.Skip(steamIdx + 2).ToArray();
                    int savesIdx = Array.FindIndex(afterSteam, p =>
                        string.Equals(p, "saves", StringComparison.OrdinalIgnoreCase));
                    if (savesIdx >= 0)
                        CurrentProfileKey = string.Join("/", afterSteam.Take(savesIdx));
                    else
                        CurrentProfileKey = string.Join("/", afterSteam);
                }
                else
                {
                    CurrentSteamId = "";
                    CurrentProfileKey = "";
                }
            }
            catch
            {
                CurrentSteamId = "";
                CurrentProfileKey = "";
            }
        }

        /// <summary>Shorten Steam ID for display: first 5 + ... + last 4 chars.</summary>
        internal static string ShortenSteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId) || steamId.Length < 9) return steamId;
            return $"{steamId[..5]}...{steamId[^4..]}";
        }

        // ── Save data accessors ─────────────────────────────────────────────────

        /// <summary>Get players array from SaveData (same as v2 App.save_data.get("players", [])).</summary>
        internal static List<object> GetPlayers()
        {
            return SaveData.TryGetValue("players", out var v) && v is List<object> list ? list : new List<object>();
        }

        /// <summary>Get player dict at index, or null.</summary>
        internal static Dictionary<string, object>? GetPlayer(int index)
        {
            var players = GetPlayers();
            if (index < 0 || index >= players.Count) return null;
            return players[index] as Dictionary<string, object>;
        }

        /// <summary>Current act index (0-based), defaults to 0.</summary>
        internal static int ActIndex
        {
            get
            {
                if (SaveData.TryGetValue("current_act_index", out var v))
                    return Convert.ToInt32(v);
                return 0;
            }
        }

        /// <summary>Ascension level, defaults to 0.</summary>
        internal static int Ascension
        {
            get
            {
                if (SaveData.TryGetValue("ascension", out var v))
                    return Convert.ToInt32(v);
                return 0;
            }
        }

        /// <summary>Number of players in current save.</summary>
        internal static int PlayerCount => GetPlayers().Count;

        // ── Status text (mirrors v2 App._refresh_status) ───────────────────────

        /// <summary>Build status bar text matching v2 status.info / status.no_save.</summary>
        internal static string GetStatusText()
        {
            if (SaveData.Count == 0)
                return Loc.Get("status.no_save", "No save loaded");

            int n = PlayerCount;
            int act = ActIndex + 1;
            int asc = Ascension;
            var players = GetPlayers();

            if (n == 0)
                return Loc.Fmt("status.info", 0, act, asc, Loc.Get("status.no_players", "No players"));

            // Build player names list
            var names = new List<string>();
            foreach (Dictionary<string, object>? pl in players)
            {
                if (pl == null) continue;
                var netId = pl.TryGetValue("net_id", out var nid) ? nid?.ToString() ?? "" : "";
                var charId = pl.TryGetValue("character_id", out var cid) ? cid?.ToString() ?? "" : "";
                // Try Steam nickname
                var steamName = Steam.SteamIntegration.GetPersonaName(netId);
                if (!string.IsNullOrEmpty(steamName) && steamName != netId)
                    names.Add(steamName);
                else
                    names.Add(CharacterDisplayNames.Resolve(charId));
            }

            var listStr = string.Join(" | ", names);
            if (string.IsNullOrEmpty(listStr))
                listStr = Loc.Get("status.no_players", "No players");
            return Loc.Fmt("status.info", n, act, asc, listStr);
        }

    }
}
