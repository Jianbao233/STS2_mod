using Godot;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace HostPriority;

/// <summary>
/// 仅干预「集齐所有票后的随机决定」：Prefix MapSelectionSynchronizer.MoveToMapCoord，
/// 用房主所选道路覆盖随机结果，不改变投票收集与显示。
/// </summary>
[HarmonyPatch]
internal static class MapSelectionPrefix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Game.MapSelectionSynchronizer")
            ?? AccessTools.TypeByName("MapSelectionSynchronizer");
        return t?.GetMethod("MoveToMapCoord", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static bool Prefix(object __instance)
    {
        if (!HostPriorityMod.Enabled) return true;
        try
        {
            var runState = AccessTools.Field(__instance.GetType(), "_runState")?.GetValue(__instance);
            var votes = AccessTools.Field(__instance.GetType(), "_votes")?.GetValue(__instance) as IList;
            var actionQueue = AccessTools.Field(__instance.GetType(), "_actionQueueSynchronizer")?.GetValue(__instance);
            var acceptingVotesFromSource = AccessTools.Field(__instance.GetType(), "_acceptingVotesFromSource");
            var mapGenCount = AccessTools.Property(__instance.GetType(), "MapGenerationCount")?.GetValue(__instance);

            if (runState == null || votes == null || actionQueue == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: runState/votes/actionQueue is null");
                return true;
            }

            var localContext = AccessTools.TypeByName("MegaCrit.Sts2.Core.Context.LocalContext")
                ?? AccessTools.TypeByName("LocalContext");
            if (localContext == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: LocalContext type not found");
                return true;
            }
            var getMe = AccessTools.Method(localContext, "GetMe", new[] { runState.GetType() });
            if (getMe == null)
            {
                var iplayer = runState.GetType().GetInterface("IPlayerCollection")
                    ?? runState.GetType().GetInterfaces().FirstOrDefault(i => i.Name.Contains("IPlayerCollection"));
                getMe = iplayer != null ? AccessTools.Method(localContext, "GetMe", new[] { iplayer }) : null;
            }
            if (getMe == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: GetMe method not found");
                return true;
            }

            var host = getMe.Invoke(null, new object[] { runState });
            if (host == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: GetMe returned null");
                return true;
            }
            GD.Print($"[HostPriority-MapDebug] host={host}, hostType={host.GetType().FullName}");

            int hostSlot = -1;
            var playerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Players.Player");
            var hostNetId = playerType?.GetProperty("NetId")?.GetValue(host);
            var players = runState.GetType().GetProperty("Players")?.GetValue(runState) as IList;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    bool match = players[i] == host;
                    if (!match && hostNetId != null)
                    {
                        var pNetId = playerType?.GetProperty("NetId")?.GetValue(players[i]);
                        match = hostNetId.Equals(pNetId);
                    }
                    if (match)
                    {
                        hostSlot = i;
                        break;
                    }
                }
            }
            else
            {
                var getPlayerSlotIndex = playerType != null
                    ? runState.GetType().GetMethod("GetPlayerSlotIndex", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { playerType }, null)
                    : null;
                if (getPlayerSlotIndex != null)
                {
                    hostSlot = (int)getPlayerSlotIndex.Invoke(runState, new[] { host });
                }
            }
            GD.Print($"[HostPriority-MapDebug] hostSlot={hostSlot}");
            var voteCount = (votes as ICollection)?.Count ?? 0;
            if (hostSlot < 0 || hostSlot >= voteCount)
            {
                GD.Print($"[HostPriority-MapDebug] FAIL: hostSlot={hostSlot} out of range (voteCount={voteCount})");
                return true;
            }

            var hostVote = votes[hostSlot];
            if (hostVote == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: hostVote is null");
                return true;
            }
            GD.Print($"[HostPriority-MapDebug] hostVote={hostVote}, hostVoteType={hostVote.GetType().FullName}");

            var requiredGen = mapGenCount != null ? Convert.ToInt32(mapGenCount) : 0;
            object mapVoteVal = hostVote;
            var voteType = hostVote.GetType();
            if (voteType.IsGenericType && voteType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var hasValue = (bool)voteType.GetField("hasValue")?.GetValue(hostVote);
                if (!hasValue)
                {
                    GD.Print("[HostPriority-MapDebug] FAIL: Nullable hostVote has no value");
                    return true;
                }
                mapVoteVal = voteType.GetField("value")?.GetValue(hostVote);
            }
            if (mapVoteVal == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: mapVoteVal is null after unwrap");
                return true;
            }
            GD.Print($"[HostPriority-MapDebug] mapVoteVal={mapVoteVal}, mapVoteValType={mapVoteVal.GetType().FullName}");

            var mapGenCountField = mapVoteVal.GetType().GetField("mapGenerationCount");
            var coordField = mapVoteVal.GetType().GetField("coord");
            if (mapGenCountField != null)
            {
                var gen = Convert.ToInt32(mapGenCountField.GetValue(mapVoteVal));
                if (gen != requiredGen)
                {
                    GD.Print($"[HostPriority-MapDebug] FAIL: gen mismatch (vote={gen}, required={requiredGen})");
                    return true;
                }
            }
            var coord = coordField?.GetValue(mapVoteVal);
            if (coord == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: coord is null");
                return true;
            }
            GD.Print($"[HostPriority-MapDebug] coord={coord}, coordType={coord.GetType().FullName}");

            try
            {
                var loc = acceptingVotesFromSource?.GetValue(__instance);
                if (loc != null && coord != null)
                {
                    var locCoordField = loc.GetType().GetField("coord");
                    if (locCoordField != null)
                    {
                        var nullableType = typeof(Nullable<>).MakeGenericType(coord.GetType());
                        var nullableCtor = nullableType.GetConstructor(new[] { coord.GetType() });
                        if (nullableCtor != null)
                        {
                            var nullableCoord = nullableCtor.Invoke(new[] { coord });
                            locCoordField.SetValue(loc, nullableCoord);
                            acceptingVotesFromSource?.SetValue(__instance, loc);
                            GD.Print("[HostPriority-MapDebug] _acceptingVotesFromSource updated OK");
                        }
                        else
                        {
                            GD.Print("[HostPriority-MapDebug] nullableCtor is null");
                        }
                    }
                    else
                    {
                        GD.Print("[HostPriority-MapDebug] locCoordField is null");
                    }
                }
                else
                {
                    GD.Print("[HostPriority-MapDebug] loc is null or coord is null");
                }
            }
            catch (Exception ex)
            {
                GD.Print($"[HostPriority-MapDebug] _acceptingVotesFromSource update failed: {ex.Message}");
            }

            var moveActionType = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.MoveToMapCoordAction")
                ?? AccessTools.TypeByName("MoveToMapCoordAction");
            if (moveActionType == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: MoveToMapCoordAction type not found");
                return true;
            }
            GD.Print($"[HostPriority-MapDebug] MoveToMapCoordAction ctor args: {string.Join(",", moveActionType.GetConstructors()[0].GetParameters().Select(p => p.ParameterType.Name))}");

            var moveAction = Activator.CreateInstance(moveActionType, host, coord);
            if (moveAction == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: Activator.CreateInstance returned null");
                return true;
            }

            var requestEnqueue = actionQueue.GetType().GetMethod("RequestEnqueue");
            if (requestEnqueue == null)
            {
                GD.Print("[HostPriority-MapDebug] FAIL: RequestEnqueue not found");
                return true;
            }
            requestEnqueue.Invoke(actionQueue, new object[] { moveAction });
            GD.Print("[HostPriority-MapDebug] SUCCESS: host vote enforced, returning false");
            return false;
        }
        catch (Exception ex)
        {
            GD.Print($"[HostPriority-MapDebug] UNHANDLED EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        return true;
    }
}
