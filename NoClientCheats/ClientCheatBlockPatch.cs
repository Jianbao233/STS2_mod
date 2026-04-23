using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

    // 诊断标志：确保 action 字段只枚举一次
    private static bool _actionFieldsLogged = false;

    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer")
            ?? AccessTools.TypeByName("ActionQueueSynchronizer");

        // 诊断：枚举该类所有方法
        if (t != null)
        {
            foreach (var m in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name.Contains("Enqueue") || m.Name.Contains("Handle"))
                    Godot.GD.Print($"[NCC|DIAG] ActionQueueSynchronizer method: {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}");
            }
        }

        MethodInfo method = null;
        if (t != null)
        {
            // 首选：严格匹配 HandleRequestEnqueueActionMessage(?, ulong)
            method = t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "HandleRequestEnqueueActionMessage", StringComparison.Ordinal))
                        return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[1].ParameterType == typeof(ulong);
                });

            // 兜底：方法名可能微调，按形状匹配 Handle*Enqueue*Action*(?, ulong)
            method ??= t.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    if (!m.Name.Contains("Handle", StringComparison.Ordinal)
                        || !m.Name.Contains("Enqueue", StringComparison.Ordinal)
                        || !m.Name.Contains("Action", StringComparison.Ordinal))
                        return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[1].ParameterType == typeof(ulong);
                });
        }

        Godot.GD.Print($"[NCC|DIAG] HandleRequestEnqueueActionMessage target: type={t?.FullName ?? "null"} method={method?.Name ?? "null"} sig={method?.ToString() ?? "null"}");

        return method;
    }

    static bool Prefix(object __instance, object message, ulong senderId)
    {
        // 全面日志：追踪每一个进来的 action
        static void DIAG(string msg) => Godot.GD.Print($"[NCC|FULLTRACE] ClientCheatBlockPrefix: {msg}");
        DIAG($"HandleRequestEnqueueActionMessage senderId={senderId}");

        // 第三重保险：若 ModManagerInitPostfix 的两条路径都因 GetMainLoop()==null 而未执行，
        // 作弊拦截触发时兜底尝试初始化（确保终局也能创建 UI 节点）
        ModManagerInitPostfix.TryScheduleInit();

        if (message == null) return true;

        object action = null;
        var t = message.GetType();
        const BindingFlags inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        action ??= t.GetProperty("action", inst)?.GetValue(message);
        action ??= t.GetProperty("Action", inst)?.GetValue(message);
        action ??= t.GetField("action", inst)?.GetValue(message);
        action ??= t.GetField("Action", inst)?.GetValue(message);
        DIAG($"action type={action?.GetType().Name ?? "null"}");

        if (action == null) return true;

        // 枚举 action 的所有字段（一次性诊断）
        if (!_actionFieldsLogged && action != null)
        {
            _actionFieldsLogged = true;
            foreach (var f in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try { DIAG($"  action field: {f.FieldType.Name} {f.Name} = {f.GetValue(action)}"); } catch { }
            }
        }

        if (action.GetType().Name != "NetConsoleCmdGameAction") return true;

        var cmdField = action.GetType().GetField("cmd", inst) ?? action.GetType().GetField("Cmd", inst);
        var cmdProp = action.GetType().GetProperty("cmd", inst) ?? action.GetType().GetProperty("Cmd", inst);
        var cmd = cmdField?.GetValue(action) as string ?? cmdProp?.GetValue(action) as string;
        DIAG($"cmd={cmd ?? "null"}");
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
    /// Transpiler：在 HandleRequestEnqueueActionMessage 入口处插入 SetCurrentRemotePlayer，
    /// 并在所有返回指令前插入 ClearCurrentRemotePlayer。
    /// 这样 ChooseOption 在同一调用链中可以通过 AsyncLocal 读到有效的远程玩家 NetId。
    /// </summary>
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
    {
        var list = instructions.ToList();
        var clearMethod = typeof(NoClientCheatsMod).GetMethod(
            nameof(NoClientCheatsMod.ClearCurrentRemotePlayer),
            BindingFlags.NonPublic | BindingFlags.Static);
        var setMethod = typeof(NoClientCheatsMod).GetMethod(
            nameof(NoClientCheatsMod.SetCurrentRemotePlayer),
            BindingFlags.NonPublic | BindingFlags.Static);

        // 1. 在方法最开头插入：SetCurrentRemotePlayer(senderId)
        // senderId 是第 3 个参数（arg 0 = __instance, arg 1 = message, arg 2 = senderId）
        var first = list[0];
        var startLabel = gen.DefineLabel();
        first.labels.Add(startLabel);
        list.Insert(0, new CodeInstruction(OpCodes.Ldarg_2) { labels = { startLabel } }); // 加载 senderId
        list.Insert(1, new CodeInstruction(OpCodes.Call, setMethod));                      // 调用 SetCurrentRemotePlayer

        // 诊断：在每个 ret 前的清除调用旁打印日志
        // Transpiler 方法内不能用 static lambda，但 Transpiler 本身只调用一次所以不影响
        var result = new List<CodeInstruction>();
        foreach (var inst in list)
        {
            result.Add(inst);
            if (inst.opcode == OpCodes.Ret)
            {
                // 在 ret 前插入清除调用
                result.Add(new CodeInstruction(OpCodes.Call, clearMethod));
            }
        }
        return result;
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
