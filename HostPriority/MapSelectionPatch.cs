using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace HostPriority;

/// <summary>
/// Prefix MapSelectionSynchronizer.MoveToMapCoord：优先采用房主的投票。
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

        var runState = AccessTools.Field(__instance.GetType(), "_runState")?.GetValue(__instance);
        var votes = AccessTools.Field(__instance.GetType(), "_votes")?.GetValue(__instance) as IList;
        var actionQueue = AccessTools.Field(__instance.GetType(), "_actionQueueSynchronizer")?.GetValue(__instance);
        var acceptingVotesFromSource = AccessTools.Field(__instance.GetType(), "_acceptingVotesFromSource");
        var mapGenCount = AccessTools.Property(__instance.GetType(), "MapGenerationCount")?.GetValue(__instance);

        if (runState == null || votes == null || actionQueue == null) return true;

        var localContext = AccessTools.TypeByName("MegaCrit.Sts2.Core.Context.LocalContext")
            ?? AccessTools.TypeByName("LocalContext");
        if (localContext == null) return true;
        var getMe = AccessTools.Method(localContext, "GetMe", new[] { runState.GetType() });
        if (getMe == null)
        {
            var iplayer = runState.GetType().GetInterface("IPlayerCollection")
                ?? runState.GetType().GetInterfaces().FirstOrDefault(i => i.Name.Contains("IPlayerCollection"));
            getMe = iplayer != null ? AccessTools.Method(localContext, "GetMe", new[] { iplayer }) : null;
        }
        if (getMe == null) return true;

        var host = getMe.Invoke(null, new object[] { runState });
        if (host == null) return true;

        var getPlayerSlotIndex = runState.GetType().GetMethod("GetPlayerSlotIndex")
            ?? AccessTools.Method(runState.GetType(), "GetPlayerSlotIndex");
        if (getPlayerSlotIndex == null) return true;

        var hostSlot = (int)getPlayerSlotIndex.Invoke(runState, new object[] { host });
        var voteCount = (votes as ICollection)?.Count ?? 0;
        if (hostSlot < 0 || hostSlot >= voteCount) return true;

        var hostVote = votes[hostSlot];
        if (hostVote == null) return true;

        var requiredGen = mapGenCount != null ? Convert.ToInt32(mapGenCount) : 0;
        object mapVoteVal = hostVote;
        var voteType = hostVote.GetType();
        if (voteType.IsGenericType && voteType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var hasValue = (bool)voteType.GetField("hasValue")?.GetValue(hostVote);
            if (!hasValue) return true;
            mapVoteVal = voteType.GetField("value")?.GetValue(hostVote);
        }
        if (mapVoteVal == null) return true;

        var mapGenCountField = mapVoteVal.GetType().GetField("mapGenerationCount");
        var coordField = mapVoteVal.GetType().GetField("coord");
        if (mapGenCountField != null)
        {
            var gen = Convert.ToInt32(mapGenCountField.GetValue(mapVoteVal));
            if (gen != requiredGen) return true;
        }
        var coord = coordField?.GetValue(mapVoteVal);
        if (coord == null) return true;

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
                    }
                }
            }
        }
        catch { }

        var moveActionType = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.MoveToMapCoordAction")
            ?? AccessTools.TypeByName("MoveToMapCoordAction");
        var moveAction = moveActionType != null
            ? Activator.CreateInstance(moveActionType, host, coord)
            : null;
        if (moveAction != null)
        {
            var requestEnqueue = actionQueue.GetType().GetMethod("RequestEnqueue");
            requestEnqueue?.Invoke(actionQueue, new object[] { moveAction });
        }

        return false;
    }
}
