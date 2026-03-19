using Godot;
using System;
using System.Reflection;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// Patch ActionQueueSynchronizer.HandleRequestEnqueueActionMessage。
/// 当房主收到客机发来的 NetConsoleCmdGameAction 且 cmd 为作弊指令时：
/// - BlockEnabled=true：静默丢弃（不入队、不广播），弹通知
/// - BlockEnabled=false：仍然记录历史，方便查作弊习惯
/// </summary>
[HarmonyPatch]
internal static class ClientCheatBlockPrefix
{
    static readonly string[] CheatCommands =
    {
        "gold", "relic", "card", "potion", "damage", "block", "heal", "power",
        "kill", "win", "godmode", "stars", "room", "event", "fight", "act",
        "travel", "ancient", "afflict", "enchant", "upgrade", "draw",
        "energy", "remove_card"
    };

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer")
            ?? AccessTools.TypeByName("ActionQueueSynchronizer");
        return t?.GetMethod("HandleRequestEnqueueActionMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    static bool Prefix(object __instance, object message, ulong senderId)
    {
        if (message == null) return true;

        object action = null;
        var t = message.GetType();
        action ??= t.GetProperty("action", BindingFlags.Public | BindingFlags.Instance)?.GetValue(message);
        action ??= t.GetField("action", BindingFlags.Public | BindingFlags.Instance)?.GetValue(message);

        if (action == null) return true;
        if (action.GetType().Name != "NetConsoleCmdGameAction") return true;

        var cmdField = action.GetType().GetField("cmd", BindingFlags.Public | BindingFlags.Instance);
        var cmd = cmdField?.GetValue(action) as string;
        if (string.IsNullOrWhiteSpace(cmd)) return true;

        var cmdName = cmd.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        bool isCheat = false;
        foreach (var c in CheatCommands)
        {
            if (string.Equals(c, cmdName, StringComparison.OrdinalIgnoreCase))
            { isCheat = true; break; }
        }
        if (!isCheat) return true;

        // 从当前 ActionQueueSynchronizer 取 _netService / _playerCollection，避免跨程序集反射失败
        var playerName = _GetPlayerNameFromSync(__instance, senderId);
        var characterName = _GetPlayerCharacterFromSync(__instance, senderId);
        var safeName = string.IsNullOrWhiteSpace(playerName) ? $"#{senderId % 10000}" : playerName;

        var wasBlocked = NoClientCheatsMod.BlockEnabled;
        NoClientCheatsMod.RecordCheat(senderId, safeName, characterName, cmd, wasBlocked);

        if (wasBlocked)
        {
            GD.Print($"[NoClientCheats] Blocked client cheat: '{cmd}' from {safeName} ({senderId})");
            return false; // 丢弃，不入队
        }

        return true; // BlockDisabled，放行
    }

    /// <summary>
    /// 从 ActionQueueSynchronizer._netService 取 Platform，再调 PlatformUtil.GetPlayerName，与游戏内显示一致。
    /// </summary>
    static string _GetPlayerNameFromSync(object sync, ulong senderId)
    {
        if (sync == null) return null;
        try
        {
            var netServiceField = AccessTools.Field(sync.GetType(), "_netService");
            var netService = netServiceField?.GetValue(sync);
            if (netService == null) return null;
            var platform = netService.GetType().GetProperty("Platform")?.GetValue(netService);
            if (platform == null) return null;

            var platformUtil = AccessTools.TypeByName("MegaCrit.Sts2.Core.Platform.PlatformUtil");
            if (platformUtil == null) return null;
            var getPlayerName = platformUtil.GetMethod("GetPlayerName", BindingFlags.Public | BindingFlags.Static, null, new[] { platform.GetType(), typeof(ulong) }, null);
            if (getPlayerName == null) return null;
            var name = getPlayerName.Invoke(null, new object[] { platform, senderId }) as string;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch { return null; }
    }

    /// <summary>从 ActionQueueSynchronizer._playerCollection.GetPlayer(senderId) 取 Character。</summary>
    static string _GetPlayerCharacterFromSync(object sync, ulong senderId)
    {
        if (sync == null) return "";
        try
        {
            var playerCollectionField = AccessTools.Field(sync.GetType(), "_playerCollection");
            var playerCollection = playerCollectionField?.GetValue(sync);
            if (playerCollection == null) return "";
            var getPlayer = playerCollection.GetType().GetMethod("GetPlayer", new[] { typeof(ulong) });
            if (getPlayer == null) return "";
            var player = getPlayer.Invoke(playerCollection, new object[] { senderId });
            if (player == null) return "";
            var charProp = player.GetType().GetProperty("Character");
            if (charProp == null) return "";
            var ch = charProp.GetValue(player);
            if (ch == null) return "";
            var idProp = ch.GetType().GetProperty("Id");
            if (idProp != null)
            {
                var id = idProp.GetValue(ch);
                if (id != null) return id.ToString();
            }
            return ch.GetType().Name;
        }
        catch { return ""; }
    }
}
