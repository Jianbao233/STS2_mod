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

                // Check ID conflict
                foreach (var p in players)
                    if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) && nid?.ToString() == newSteamId)
                        return OperationResult.Fail($"Steam ID {newSteamId} already exists in this save");

                if (players[sourcePlayerIndex] is not Dictionary<string, object> source)
                    return OperationResult.Fail("Invalid source player data");

                // Deep copy the source player
                var newPlayer = DeepCopyDict(source);
                newPlayer["net_id"] = newSteamId;
                newPlayer["current_hp"] = newPlayer.TryGetValue("max_hp", out var mh) ? mh : (newPlayer.TryGetValue("current_hp", out var ch) ? ch : 0);
                newPlayer["potions"] = new List<object>();

                string charId = newPlayer.TryGetValue("character_id", out var cid) ? cid?.ToString() ?? "?" : "?";

                players.Add(newPlayer);
                data["players"] = players;

                // Inject into map_point_history
                InjectPlayerIntoMapHistory(data, newPlayer);

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                GD.Print($"[MultiplayerTools] AddPlayerCopy: source={sourcePlayerIndex}, new={newSteamId}, char={charId}");
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
                    if (p is Dictionary<string, object> pd && pd.TryGetValue("net_id", out var nid) && nid?.ToString() == newSteamId)
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
                    ["net_id"] = newSteamId,
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

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                GD.Print($"[MultiplayerTools] AddPlayerFresh: new={newSteamId}, char={characterId}");
                return OperationResult.Ok($"Player added (fresh): {newSteamId}, char={characterId}");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] AddPlayerFresh failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        /// <summary>Remove a player at index. Removes from players[], cleans map_history and grab bags.
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

                // 1. Clean relic_grab_bag for all remaining players
                foreach (var p in players)
                {
                    if (p is not Dictionary<string, object> pd) continue;
                    if (pd.TryGetValue("relic_grab_bag", out var bagObj) && bagObj is Dictionary<string, object> bag)
                    {
                        if (bag.TryGetValue("relic_id_lists", out var listsObj) && listsObj is Dictionary<string, object> lists)
                        {
                            foreach (var key in new[] { "common", "uncommon", "rare", "shop" })
                                if (lists.TryGetValue(key, out var lst) && lst is List<object> l)
                                    l.Clear();
                        }
                    }
                }

                // 2. Clean shared_relic_grab_bag
                if (data.TryGetValue("shared_relic_grab_bag", out var sharedObj) && sharedObj is Dictionary<string, object> shared)
                {
                    if (shared.TryGetValue("relic_id_lists", out var listsObj) && listsObj is Dictionary<string, object> lists)
                    {
                        foreach (var key in new[] { "common", "uncommon", "rare", "shop", "event", "ancient" })
                            if (lists.TryGetValue(key, out var lst) && lst is List<object> l)
                                l.Clear();
                    }
                }

                // 3. Clean map_point_history player_stats
                if (data.TryGetValue("map_point_history", out var mph) && mph is List<object> history)
                {
                    foreach (var point in history)
                    {
                        if (point is Dictionary<string, object> mp && mp.TryGetValue("player_stats", out var psObj) && psObj is List<object> stats)
                        {
                            mp["player_stats"] = stats.Where(s =>
                                !(s is Dictionary<string, object> sd && sd.TryGetValue("player_id", out var sid) && sid?.ToString() == removedId)
                            ).ToList();
                        }
                    }
                }

                if (!SaveSave(savePath, data))
                    return OperationResult.Fail("Failed to write save file");

                GD.Print($"[MultiplayerTools] RemovePlayerFull: index {playerIndex} ({removedId}) removed");
                return OperationResult.Ok($"Player {playerIndex} removed (ID: {removedId}, char: {removedChar})");
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] RemovePlayerFull failed: " + ex);
                return OperationResult.Fail(ex.Message);
            }
        }

        // ─── Internal helpers (mirrors Python core.py) ─────────────────────────

        private static void InjectPlayerIntoMapHistory(Dictionary<string, object> saveData, Dictionary<string, object> player)
        {
            string? newId = player.TryGetValue("net_id", out var nid) ? nid?.ToString() : null;
            if (string.IsNullOrEmpty(newId)) return;

            if (!saveData.TryGetValue("map_point_history", out var mphObj) || mphObj is not List<object> history)
                return;

            foreach (var floor in history)
            {
                if (floor is not List<object> entries) continue;
                foreach (var entry in entries)
                {
                    if (entry is not Dictionary<string, object> node) continue;
                    var stats = node.TryGetValue("player_stats", out var psObj) && psObj is List<object> s ? s : null;
                    if (stats == null) continue;
                    if (stats.Any(s => s is Dictionary<string, object> sd && sd.TryGetValue("player_id", out var pid) && pid?.ToString() == newId))
                        continue;
                    string mpt = node.TryGetValue("map_point_type", out var mptObj) ? mptObj?.ToString() ?? "" : "";
                    stats.Add(BuildPlayerStatEntry(player, mpt));
                }
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
                ["player_id"] = player.TryGetValue("net_id", out var nid) ? nid! : 0,
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
