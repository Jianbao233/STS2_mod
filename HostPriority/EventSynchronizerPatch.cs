using System;
using System.Reflection;
using HarmonyLib;

namespace HostPriority;

/// <summary>
/// Prefix EventSynchronizer.ChooseSharedEventOption：优先采用房主的投票。
/// </summary>
[HarmonyPatch]
internal static class EventSynchronizerPrefix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer")
            ?? AccessTools.TypeByName("EventSynchronizer");
        return t?.GetMethod("ChooseSharedEventOption", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static bool Prefix(object __instance)
    {
        if (!HostPriorityMod.Enabled) return true;

        var playerCollection = AccessTools.Field(__instance.GetType(), "_playerCollection")?.GetValue(__instance);
        var localPlayerId = AccessTools.Field(__instance.GetType(), "_localPlayerId");
        var playerVotes = AccessTools.Field(__instance.GetType(), "_playerVotes")?.GetValue(__instance) as System.Collections.IList;
        var netService = AccessTools.Field(__instance.GetType(), "_netService")?.GetValue(__instance);
        var messageBuffer = AccessTools.Field(__instance.GetType(), "_messageBuffer")?.GetValue(__instance);
        var pageIndexField = AccessTools.Field(__instance.GetType(), "_pageIndex");

        if (playerCollection == null || playerVotes == null || netService == null || messageBuffer == null) return true;

        var netTypeProp = netService.GetType().GetProperty("Type");
        var netType = netTypeProp?.GetValue(netService);
        if (netType != null && netType.ToString() == "Client") return true;

        var getPlayer = playerCollection.GetType().GetMethod("GetPlayer", new[] { typeof(ulong) });
        if (getPlayer == null) return true;

        var localId = localPlayerId != null ? Convert.ToUInt64(localPlayerId.GetValue(__instance)) : 0UL;
        var host = getPlayer.Invoke(playerCollection, new object[] { localId });
        if (host == null) return true;

        var getPlayerSlotIndex = playerCollection.GetType().GetMethod("GetPlayerSlotIndex");
        if (getPlayerSlotIndex == null) return true;

        var hostSlot = (int)getPlayerSlotIndex.Invoke(playerCollection, new object[] { host });
        if (hostSlot < 0 || hostSlot >= playerVotes.Count) return true;

        var hostVote = playerVotes[hostSlot];
        if (hostVote == null) return true;

        var optionIndex = Convert.ToUInt32(hostVote);

        var chooseOptionForSharedEvent = __instance.GetType().GetMethod("ChooseOptionForSharedEvent",
            BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(uint) }, null);
        if (chooseOptionForSharedEvent == null) return true;

        var sendMessage = netService.GetType().GetMethod("SendMessage");
        if (sendMessage == null) return true;

        var msgType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.SharedEventOptionChosenMessage")
            ?? AccessTools.TypeByName("SharedEventOptionChosenMessage");
        if (msgType == null) return true;

        var currentLocation = messageBuffer.GetType().GetProperty("CurrentLocation")?.GetValue(messageBuffer);
        var pageIndex = pageIndexField?.GetValue(__instance);

        var msg = Activator.CreateInstance(msgType);
        msgType.GetField("optionIndex")?.SetValue(msg, optionIndex);
        msgType.GetField("pageIndex")?.SetValue(msg, pageIndex != null ? Convert.ToUInt32(pageIndex) : 0u);
        if (currentLocation != null)
            msgType.GetField("location")?.SetValue(msg, currentLocation);

        var sendMethod = sendMessage.MakeGenericMethod(msgType);
        sendMethod.Invoke(netService, new object[] { msg });
        chooseOptionForSharedEvent.Invoke(__instance, new object[] { optionIndex });

        return false;
    }
}
