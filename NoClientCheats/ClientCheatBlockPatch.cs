using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// Patch ActionQueueSynchronizer.HandleRequestEnqueueActionMessage。
/// 当房主收到客机发来的 NetConsoleCmdGameAction 且 cmd 为作弊指令时，静默丢弃，不入队、不广播。
/// </summary>
[HarmonyPatch]
internal static class ClientCheatBlockPrefix
{
    private static readonly HashSet<string> CheatCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "gold", "relic", "card", "potion", "damage", "block", "heal", "power", "kill", "win",
        "godmode", "stars", "room", "event", "fight", "act", "travel", "ancient",
        "afflict", "enchant", "upgrade", "draw", "energy", "remove_card"
    };

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer")
            ?? AccessTools.TypeByName("ActionQueueSynchronizer");
        return t?.GetMethod("HandleRequestEnqueueActionMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>Prefix：若启用了禁止且 action 为 NetConsoleCmdGameAction 且 cmd 在作弊列表中，跳过原方法。</summary>
    static bool Prefix(object __instance, object message, ulong senderId)
    {
        if (!NoClientCheatsMod.BlockEnabled) return true;

        if (message == null) return true;

        var action = GetActionFromMessage(message);
        if (action == null) return true;

        var type = action.GetType();
        if (type.Name != "NetConsoleCmdGameAction") return true;

        var cmdField = type.GetField("cmd", BindingFlags.Public | BindingFlags.Instance);
        var cmd = cmdField?.GetValue(action) as string;
        if (string.IsNullOrWhiteSpace(cmd)) return true;

        var cmdName = cmd.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!CheatCommands.Contains(cmdName)) return true;

        // 作弊指令，来自客机（HandleRequestEnqueueActionMessage 仅在房主收到客机请求时调用）
        GD.Print($"[NoClientCheats] Blocked client cheat: '{cmd}' from sender {senderId}");
        return false; // 跳过原方法
    }

    private static object GetActionFromMessage(object message)
    {
        if (message == null) return null;
        var t = message.GetType();
        var prop = t.GetProperty("action", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(message);
        var field = t.GetField("action", BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(message);
    }
}
