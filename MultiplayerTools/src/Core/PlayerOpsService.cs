using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

                string oldSteamId = "";
                if (player.TryGetValue("net_id", out var nid) && nid is string old)
                    oldSteamId = old;

                player["net_id"] = newSteamId;

                // Clear map history stats for this player
                if (data.TryGetValue("map_point_history", out var mph) && mph is List<object> history)
                {
                    foreach (var point in history)
                    {
                        if (point is Dictionary<string, object> mp && mp.TryGetValue("player_stats", out var ps) && ps is List<object> stats)
                        {
                            if (playerIndex < stats.Count)
                                stats[playerIndex] = MakeDefaultPlayerStats();
                        }
                    }
                }

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                GD.Print($"[MultiplayerTools] TakeOver: player {playerIndex} (old={oldSteamId}) → {newSteamId}");
                return OperationResult.Ok($"Player {playerIndex} taken over (ID: {oldSteamId} → {newSteamId})");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] TakeOverPlayer failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        /// <summary>Remove a player at index from the save.
        /// Player 0 (host) is protected and cannot be removed.</summary>
        internal static OperationResult RemovePlayer(int playerIndex, string savePath)
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

                // Reindex map_point_history
                if (data.TryGetValue("map_point_history", out var mph) && mph is List<object> history)
                {
                    foreach (var point in history)
                    {
                        if (point is Dictionary<string, object> mp && mp.TryGetValue("player_stats", out var ps) && ps is List<object> stats)
                        {
                            if (playerIndex < stats.Count) stats.RemoveAt(playerIndex);
                        }
                    }
                }

                // Clean relic grab bags
                data.Remove("relic_grab_bag");
                data.Remove("shared_relic_grab_bag");

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                GD.Print($"[MultiplayerTools] RemovePlayer: index {playerIndex} removed");
                return OperationResult.Ok($"Player {playerIndex} removed");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] RemovePlayer failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

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

        private static Dictionary<string, object>? LoadSave(string path)
        {
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return JsonElementToDict(doc.RootElement);
        }

        private static bool SaveSave(string path, Dictionary<string, object> data)
        {
            try
            {
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(data, opts);
                json = json.Replace("\n", "\r\n");
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MultiplayerTools] SaveSave({path}) failed: " + ex.Message);
                return false;
            }
        }

        private static Dictionary<string, object> JsonElementToDict(System.Text.Json.JsonElement el)
        {
            var dict = new Dictionary<string, object>();
            if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in el.EnumerateObject())
                    dict[prop.Name] = JsonValueToObject(prop.Value);
            }
            return dict;
        }

        private static object JsonValueToObject(System.Text.Json.JsonElement el)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String: return el.GetString() ?? "";
                case System.Text.Json.JsonValueKind.Number: return el.TryGetInt64(out var l) ? l : el.GetDouble();
                case System.Text.Json.JsonValueKind.True: return true;
                case System.Text.Json.JsonValueKind.False: return false;
                case System.Text.Json.JsonValueKind.Array: return el.EnumerateArray().Select(JsonValueToObject).ToList();
                case System.Text.Json.JsonValueKind.Object: return JsonElementToDict(el);
                default: return "";
            }
        }

        private static Dictionary<string, object> MakeDefaultPlayerStats()
        {
            return new Dictionary<string, object>
            {
                ["gold"] = 0,
                ["purchased_map_nodes"] = new List<object>(),
                ["red_purchased_map_nodes"] = new List<object>()
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
