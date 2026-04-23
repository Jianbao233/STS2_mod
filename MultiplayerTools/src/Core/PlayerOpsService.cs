using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerTools.Core
{
    /// <summary>
    /// Player operations service: takeover, add, remove players via save file manipulation.
    /// Works by directly editing the save JSON (players[] / map_point_history).
    /// Safe for offline/disconnected players; host (Player 1) is protected from takeover.
    /// </summary>
    internal static class PlayerOpsService
    {
        private static readonly Assembly? Sts2Assembly = AppDomain.CurrentDomain
            .GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        // ─── Public API ─────────────────────────────────────────────────────────

        /// <summary>Take over (replace) a player at index with a new Steam ID.
        /// Player 0 (host) is protected and cannot be taken over.</summary>
        internal static OperationResult TakeOverPlayer(int playerIndex, string newSteamId, string savePath)
        {
            if (playerIndex <= 0)
                return OperationResult.Fail($"Player {playerIndex} is host and cannot be taken over");

            try
            {
                var data = LoadSave(savePath);
                if (data == null) return OperationResult.Fail("Failed to load save file");

                if (!data.TryGetValue("players", out var playersObj) || playersObj is not List<object> players)
                    return OperationResult.Fail("Invalid save: no players array");

                if (playerIndex >= players.Count)
                    return OperationResult.Fail($"Player index {playerIndex} out of range (total: {players.Count})");

                if (players[playerIndex] is not Dictionary<string, object> player)
                    return OperationResult.Fail("Invalid player data");

                string oldSteamId = player.TryGetValue("net_id", out var nidOld) ? nidOld?.ToString() ?? "" : "";

                for (int i = 0; i < players.Count; i++)
                {
                    if (i == playerIndex) continue;
                    if (players[i] is Dictionary<string, object> other &&
                        other.TryGetValue("net_id", out var existingId) &&
                        SteamIdsMatchDigits(existingId, newSteamId))
                    {
                        return OperationResult.Fail($"Steam ID {newSteamId} already exists in this save");
                    }
                }

                object newNetIdValue = NetIdToSaveValue(newSteamId);
                player["net_id"] = newNetIdValue;

                // Keep history stats intact; only remap identity references.
                RemapPlayerIdInMapHistory(data, oldSteamId, newNetIdValue);

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                return OperationResult.Ok($"Player {playerIndex} taken over (ID: {oldSteamId} → {newSteamId})");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] TakeOverPlayer failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        /// <summary>Remove a player at index. Now routed to RemovePlayerFull.
        /// Player 0 (host) is protected and cannot be removed.</summary>
        internal static OperationResult RemovePlayer(int playerIndex, string savePath) =>
            RemovePlayerFull(playerIndex, savePath);

        /// <summary>List all players in a save file (read-only, does not modify).</summary>
        internal static List<PlayerInfo> GetPlayersInSave(string savePath)
        {
            var result = new List<PlayerInfo>();
            try
            {
                var data = LoadSave(savePath);
                if (data == null) return result;

                if (!data.TryGetValue("players", out var playersObj) || playersObj is not List<object> players)
                    return result;

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] is not Dictionary<string, object> p) continue;
                    result.Add(new PlayerInfo
                    {
                        Index = i,
                        SteamId = p.TryGetValue("net_id", out var nid) ? nid?.ToString() ?? "" : "",
                        CharacterName = p.TryGetValue("character_name", out var cn) ? cn?.ToString() ?? "" : "Unknown",
                        IsHost = i == 0
                    });
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] GetPlayersInSave failed: " + ex.Message);
            }
            return result;
        }

        // ─── Internal types ─────────────────────────────────────────────────────

        internal class PlayerInfo
        {
            internal int Index;
            internal string SteamId = "";
            internal string CharacterName = "";
            internal bool IsHost;
        }

        internal class OperationResult
        {
            internal bool Success { get; private set; }
            internal string Message { get; private set; } = "";
            private OperationResult(bool success, string msg) { Success = success; Message = msg; }
            internal static OperationResult Ok(string msg) => new(true, msg);
            internal static OperationResult Fail(string msg) => new(false, msg);
        }

        // ─── Private helpers ───────────────────────────────────────────────────

        /// <summary>Load save JSON; gzip + UTF-8 same as <see cref="SaveManagerHelper.ParseSaveFile"/> (matches Python v2 <c>_load_raw</c>).</summary>
        private static Dictionary<string, object>? LoadSave(string path) =>
            SaveManagerHelper.ParseSaveFile(path);

        private static bool SaveSave(string path, Dictionary<string, object> data) =>
            SaveManagerHelper.WriteSaveFile(path, data);

        private static void RemapPlayerIdInMapHistory(Dictionary<string, object> data, string oldSteamId, object newNetIdValue)
        {
            if (string.IsNullOrEmpty(oldSteamId)) return;
            if (!data.TryGetValue("map_point_history", out var mph) || mph is not List<object> history)
                return;

            foreach (var node in EnumerateMapHistoryNodes(history))
            {
                if (!node.TryGetValue("player_stats", out var psObj) || psObj is not List<object> stats)
                    continue;

                foreach (var stat in stats)
                {
                    if (stat is not Dictionary<string, object> statDict) continue;
                    if (!statDict.TryGetValue("player_id", out var playerId)) continue;
                    if (!SteamIdsMatchDigits(playerId, oldSteamId)) continue;
                    statDict["player_id"] = newNetIdValue;
                }
            }
        }

        private static void TryRemapPlayerIdInMapDrawings(Dictionary<string, object> data, string oldSteamId, object newNetIdValue)
        {
            if (string.IsNullOrEmpty(oldSteamId)) return;
            if (!data.TryGetValue("map_drawings", out var mdObj)) return;
            var b64 = mdObj?.ToString();
            if (string.IsNullOrEmpty(b64)) return;

            try
            {
                byte[] raw = Convert.FromBase64String(b64.Trim());
                if (raw.Length >= 2 && raw[0] == 0x1f && raw[1] == 0x8b)
                {
                    using var ms = new MemoryStream(raw);
                    using var gz = new GZipStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    gz.CopyTo(outMs);
                    raw = outMs.ToArray();
                }

                string text = System.Text.Encoding.UTF8.GetString(raw);
                var root = JsonNode.Parse(text);
                if (root is not JsonObject jo) return;
                if (jo["drawings"] is not JsonArray drawings) return;

                string newId = newNetIdValue.ToString() ?? "";
                if (string.IsNullOrEmpty(newId)) return;

                foreach (var item in drawings)
                {
                    if (item is not JsonObject d || d["player_id"] is null)
                        continue;
                    if (!string.Equals(d["player_id"]!.ToString(), oldSteamId, StringComparison.Ordinal))
                        continue;
                    d["player_id"] = newId;
                }

                var drawOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(jo.ToJsonString(drawOpts));
                using var outGz = new MemoryStream();
                using (var gz = new GZipStream(outGz, CompressionLevel.SmallestSize, leaveOpen: true))
                    gz.Write(payload, 0, payload.Length);
                data["map_drawings"] = Convert.ToBase64String(outGz.ToArray());
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] TryRemapPlayerIdInMapDrawings: " + ex.Message);
            }
        }

        /// <summary>Add a new player by copying a source player (full copy, full HP, no potions).
        /// Mirrors Python core.add_player_copy().</summary>
        internal static OperationResult AddPlayerCopy(int sourcePlayerIndex, string newSteamId, string savePath)
        {
            if (sourcePlayerIndex < 0)
                return OperationResult.Fail($"Invalid source player index {sourcePlayerIndex}");

            try
            {
                var data = LoadSave(savePath);
                if (data == null) return OperationResult.Fail("Failed to load save file");

                if (!data.TryGetValue("players", out var playersObj) || playersObj is not List<object> players)
                    return OperationResult.Fail("Invalid save: no players array");

                if (sourcePlayerIndex >= players.Count)
                    return OperationResult.Fail($"Source player index {sourcePlayerIndex} out of range (total: {players.Count})");

                // Check ID conflict (net_id may be stored as JSON number or string — compare as digits)
                foreach (var p in players)
                    if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) && SteamIdsMatchDigits(nid, newSteamId))
                        return OperationResult.Fail($"Steam ID {newSteamId} already exists in this save");

                if (players[sourcePlayerIndex] is not Dictionary<string, object> source)
                    return OperationResult.Fail("Invalid source player data");

                // Deep copy the source player
                var newPlayer = DeepCopyDict(source);
                newPlayer["net_id"] = NetIdToSaveValue(newSteamId);
                newPlayer["current_hp"] = newPlayer.TryGetValue("max_hp", out var mh) ? mh : (newPlayer.TryGetValue("current_hp", out var ch) ? ch : 0);
                newPlayer["potions"] = new List<object>();

                string charId = newPlayer.TryGetValue("character_id", out var cid) ? cid?.ToString() ?? "?" : "?";

                players.Add(newPlayer);
                data["players"] = players;

                // Inject into map_point_history
                InjectPlayerIntoMapHistory(data, newPlayer);

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                return OperationResult.Ok($"Player added (copy): {newSteamId}, char={charId}");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] AddPlayerCopy failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        /// <summary>Add a new player with a fresh starter deck from a character template.
        /// Mirrors Python core.add_player_fresh().</summary>
        internal static OperationResult AddPlayerFresh(string newSteamId, string characterId, int maxHp, List<string> starterDeck, string? starterRelic, string savePath, int gold = 100)
        {
            if (string.IsNullOrEmpty(newSteamId))
                return OperationResult.Fail("Steam ID is required");
            if (string.IsNullOrEmpty(characterId))
                return OperationResult.Fail("Character ID is required");

            try
            {
                var data = LoadSave(savePath);
                if (data == null) return OperationResult.Fail("Failed to load save file");

                if (!data.TryGetValue("players", out var playersObj) || playersObj is not List<object> players)
                    return OperationResult.Fail("Invalid save: no players array");

                // Check ID conflict
                foreach (var p in players)
                    if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) && SteamIdsMatchDigits(nid, newSteamId))
                        return OperationResult.Fail($"Steam ID {newSteamId} already exists in this save");

                // Build starter deck
                var deck = new List<object>();
                int floor = 1;
                if (starterDeck != null)
                    foreach (var cardId in starterDeck)
                        deck.Add(new Dictionary<string, object> { ["id"] = cardId, ["floor_added_to_deck"] = floor });

                // Build relics
                var relics = new List<object>();
                if (!string.IsNullOrEmpty(starterRelic))
                    relics.Add(new Dictionary<string, object> { ["id"] = starterRelic, ["floor_added_to_deck"] = floor });

                var newPlayer = new Dictionary<string, object>
                {
                    ["net_id"] = NetIdToSaveValue(newSteamId),
                    ["character_id"] = characterId,
                    ["current_hp"] = maxHp,
                    ["max_hp"] = maxHp,
                    ["gold"] = gold,
                    ["max_energy"] = 3,
                    ["max_potion_slot_count"] = 3,
                    ["base_orb_slot_count"] = 0,
                    ["deck"] = deck,
                    ["relics"] = relics,
                    ["potions"] = new List<object>(),
                    ["rng"] = new Dictionary<string, object>
                    {
                        ["counters"] = new Dictionary<string, object>
                        {
                            ["rewards"] = 0, ["shops"] = 0, ["transformations"] = 0
                        },
                        ["seed"] = 0
                    },
                    ["odds"] = new Dictionary<string, object>
                    {
                        ["card_rarity_odds_value"] = 0.0,
                        ["potion_reward_odds_value"] = 0.5
                    },
                    ["relic_grab_bag"] = new Dictionary<string, object>
                    {
                        ["relic_id_lists"] = new Dictionary<string, object>
                        {
                            ["common"] = new List<object>(),
                            ["uncommon"] = new List<object>(),
                            ["rare"] = new List<object>(),
                            ["shop"] = new List<object>()
                        }
                    },
                    ["discovered_cards"] = new List<object>(),
                    ["discovered_relics"] = new List<object>(),
                    ["discovered_enemies"] = new List<object>(),
                    ["discovered_epochs"] = new List<object>(),
                    ["unlock_state"] = new Dictionary<string, object>
                    {
                        ["number_of_runs"] = 0,
                        ["unlocked_epochs"] = new List<object>(),
                        ["encounters_seen"] = new List<object>()
                    },
                    ["extra_fields"] = new Dictionary<string, object>()
                };

                players.Add(newPlayer);
                data["players"] = players;

                InjectPlayerIntoMapHistory(data, newPlayer);
                // map_drawings may be binary on newer saves; do not parse. Fresh add keeps drawings blank.
                data["map_drawings"] = "";

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                return OperationResult.Ok($"Player added (fresh): {newSteamId}, char={characterId}");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] AddPlayerFresh failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        /// <summary>Remove a player at index. Removes from players[], cleans map_history and map_drawings.
        /// Player 0 (host) is protected.</summary>
        internal static OperationResult RemovePlayerFull(int playerIndex, string savePath)
        {
            if (playerIndex <= 0)
                return OperationResult.Fail("Host player cannot be removed");

            try
            {
                var data = LoadSave(savePath);
                if (data == null) return OperationResult.Fail("Failed to load save file");

                if (!data.TryGetValue("players", out var playersObj) || playersObj is not List<object> players)
                    return OperationResult.Fail("Invalid save: no players array");

                if (playerIndex >= players.Count)
                    return OperationResult.Fail($"Player index {playerIndex} out of range");

                var removed = players[playerIndex];
                players.RemoveAt(playerIndex);
                string removedId = "";
                string removedChar = "?";
                if (removed is Dictionary<string, object> rd)
                {
                    removedId = rd.TryGetValue("net_id", out var rid) ? rid?.ToString() ?? "" : "";
                    removedChar = rd.TryGetValue("character_id", out var rcid) ? rcid?.ToString() ?? "?" : "?";
                }

                // 1. Clean map_point_history player_stats
                if (data.TryGetValue("map_point_history", out var mph) && mph is List<object> history)
                {
                    foreach (var node in EnumerateMapHistoryNodes(history))
                    {
                        if (!node.TryGetValue("player_stats", out var psObj) || psObj is not List<object> stats)
                            continue;
                        node["player_stats"] = stats.Where(s =>
                            !(s is Dictionary<string, object> sd && sd.TryGetValue("player_id", out var sid) && SteamIdsMatchDigits(sid, removedId))
                        ).ToList();
                    }
                }

                // 2. map_drawings
                // Do not parse/transform drawings payload. Remove the whole field on player deletion.
                RemoveMapDrawingsField(data);

                var writeOk = SaveSave(savePath, data);

                return writeOk
                    ? OperationResult.Ok($"Player {playerIndex} removed (ID: {removedId}, char: {removedChar})")
                    : OperationResult.Fail("Failed to write save file");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] RemovePlayerFull failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        // ─── Internal helpers (mirrors Python core.py) ─────────────────────────

        /// <summary>Store SteamID64 the same way the game JSON does: numeric when it fits in <see cref="long"/>.</summary>
        private static object NetIdToSaveValue(string steamId64)
        {
            if (string.IsNullOrEmpty(steamId64)) return steamId64;
            if (ulong.TryParse(steamId64, out var u) && u <= long.MaxValue)
                return (long)u;
            return steamId64;
        }

        private static bool SteamIdsMatchDigits(object? a, string b)
        {
            if (a == null || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.ToString(), b, StringComparison.Ordinal);
        }

        private static void RemoveMapDrawingsField(Dictionary<string, object> data)
        {
            if (data.ContainsKey("map_drawings"))
                data.Remove("map_drawings");
        }

        /// <summary>v2 <c>core.remove_player</c> step 4: strip drawings for removed <c>player_id</c>.</summary>
        private static void TryRemovePlayerFromMapDrawings(Dictionary<string, object> data, object? removedNetId)
        {
            if (removedNetId == null) return;
            string removedStr = removedNetId.ToString() ?? "";
            if (string.IsNullOrEmpty(removedStr)) return;
            if (!data.TryGetValue("map_drawings", out var mdObj)) return;
            var b64 = mdObj?.ToString();
            if (string.IsNullOrEmpty(b64)) return;
            try
            {
                byte[] raw = Convert.FromBase64String(b64.Trim());
                if (raw.Length >= 2 && raw[0] == 0x1f && raw[1] == 0x8b)
                {
                    using var ms = new MemoryStream(raw);
                    using var gz = new GZipStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    gz.CopyTo(outMs);
                    raw = outMs.ToArray();
                }

                string text = System.Text.Encoding.UTF8.GetString(raw);
                var root = JsonNode.Parse(text);
                if (root is not JsonObject jo) return;
                if (jo["drawings"] is not JsonArray drawings) return;

                var next = new JsonArray();
                foreach (var item in drawings)
                {
                    if (item is JsonObject d && d["player_id"] is { } pid)
                    {
                        if (string.Equals(pid.ToString(), removedStr, StringComparison.Ordinal))
                            continue;
                    }
                    next.Add(item);
                }
                jo["drawings"] = next;

                var drawOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(jo.ToJsonString(drawOpts));
                using var outGz = new MemoryStream();
                using (var gz = new GZipStream(outGz, CompressionLevel.SmallestSize, leaveOpen: true))
                    gz.Write(payload, 0, payload.Length);
                data["map_drawings"] = Convert.ToBase64String(outGz.ToArray());
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] TryRemovePlayerFromMapDrawings: " + ex.Message);
            }
        }

        /// <summary>
        /// Yield every map node in <paramref name="history"/> whether stored as a flat list of dicts
        /// or as a list of floors (each floor a list of node dicts).
        /// </summary>
        private static IEnumerable<Dictionary<string, object>> EnumerateMapHistoryNodes(List<object> history)
        {
            foreach (var item in history)
            {
                if (item is Dictionary<string, object> node)
                {
                    yield return node;
                }
                else if (item is List<object> floorEntries)
                {
                    foreach (var e in floorEntries)
                    {
                        if (e is Dictionary<string, object> n)
                            yield return n;
                    }
                }
            }
        }

        private static void InjectPlayerIntoMapHistory(Dictionary<string, object> saveData, Dictionary<string, object> player)
        {
            string? newId = player.TryGetValue("net_id", out var nid) ? nid?.ToString() : null;
            if (string.IsNullOrEmpty(newId)) return;

            if (!saveData.TryGetValue("map_point_history", out var mphObj) || mphObj is not List<object> history)
                return;

            foreach (var node in EnumerateMapHistoryNodes(history))
            {
                var stats = node.TryGetValue("player_stats", out var psObj) && psObj is List<object> s ? s : null;
                if (stats == null) continue;
                if (stats.Any(s => s is Dictionary<string, object> sd && sd.TryGetValue("player_id", out var pid) && pid?.ToString() == newId))
                    continue;
                string mpt = node.TryGetValue("map_point_type", out var mptObj) ? mptObj?.ToString() ?? "" : "";
                stats.Add(BuildPlayerStatEntry(player, mpt));
            }
        }

        private static Dictionary<string, object> BuildPlayerStatEntry(Dictionary<string, object> player, string mapPointType)
        {
            int gold = GetInt(player, "gold");
            int currentHp = GetInt(player, "current_hp");
            int maxHp = GetInt(player, "max_hp");
            int healAmt = maxHp;
            var stat = new Dictionary<string, object>
            {
                ["player_id"] = player.TryGetValue("net_id", out var nid) ? nid! : (object)0,
                ["current_gold"] = gold,
                ["current_hp"] = currentHp,
                ["max_hp"] = maxHp,
                ["damage_taken"] = 0,
                ["gold_gained"] = 0,
                ["gold_lost"] = 0,
                ["gold_spent"] = 0,
                ["gold_stolen"] = 0,
                ["hp_healed"] = healAmt,
                ["max_hp_gained"] = 0,
                ["max_hp_lost"] = 0,
                ["cards_gained"] = new List<object>(),
                ["relic_choices"] = new List<object>(),
                ["event_choices"] = new List<object>(),
            };
            if (mapPointType == "ancient")
                stat["ancient_choice"] = new List<object>();
            return stat;
        }

        private static int GetInt(Dictionary<string, object> d, string key)
        {
            if (!d.TryGetValue(key, out var v)) return 0;
            return v switch { int i => i, long l => (int)l, double dbl => (int)dbl, _ => 0 };
        }

        private static Dictionary<string, object> DeepCopyDict(Dictionary<string, object> source)
        {
            var copy = new Dictionary<string, object>();
            foreach (var kvp in source)
                copy[kvp.Key] = DeepCopyValue(kvp.Value);
            return copy;
        }

        private static object DeepCopyValue(object value)
        {
            return value switch
            {
                Dictionary<string, object> dict => DeepCopyDict(dict),
                List<object> list => list.Select(DeepCopyValue).ToList(),
                _ => value
            };
        }

        private static class AccessTools
        {
            internal static Type? TypeByName(string name) =>
                Sts2Assembly?.GetType(name);

            internal static MethodInfo? Method(Type type, string name, Type[] args) =>
                type?.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, args, null);
        }
    }
}
