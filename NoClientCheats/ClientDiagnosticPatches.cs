using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Godot;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// 客机诊断模块。
///
/// 核心问题（日志验证）：
///   主机检测作弊 → 发送 NCC 回滚 SyncPlayerDataMessage{ player.NetId=ClientNetId }
///   → 网络层 senderId=ClientNetId（因为 NCC 通过 SendMessageToPeer 发送）
///   → 客机收到后 senderId=ClientNetId = msgPlayer.NetId → Patch B MISMATCH=False → 无效！
///
/// 新方案（在 OnSyncPlayerReceived Prefix 中直接处理）：
///   1. 检测 senderId == localPlayer.NetId（自身消息）
///   2. 对比当前本地卡组 vs msg 中的卡组
///   3. 若不同 → 主机回滚消息 → 直接同步后 return false 跳过原方法
///   4. 若相同 → 自身正常同步 → 继续执行原方法
/// </summary>
#if false // 客机回滚诊断链路已弃用：保留历史实现，仅供追溯。
[HarmonyPatch]
internal static class ClientDiagnosticPatches
{
    // ── 类型缓存 ─────────────────────────────────────────────────────────
    private static readonly Type _syncMsgType;
    private static readonly Type _serializablePlayerType;
    private static readonly Type _runManagerType;

    static ClientDiagnosticPatches()
    {
        _syncMsgType = AccessTools.TypeByName(
            "MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.SyncPlayerDataMessage")
            ?? AccessTools.TypeByName("SyncPlayerDataMessage");
        _serializablePlayerType = AccessTools.TypeByName(
            "MegaCrit.Sts2.Core.Entities.Players.SerializablePlayer")
            ?? AccessTools.TypeByName("SerializablePlayer");
        _runManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? AccessTools.TypeByName("RunManager");
    }

    // ── 日志前缀常量 ──────────────────────────────────────────────────────
    private const string TAG = "[NCC-DIAG]";

    private static void DIAG(string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        NoClientCheatsMod.ThreadSafeLog($"{TAG}[{ts}] {msg}");
    }

    // ── 状态缓存 ─────────────────────────────────────────────────────────
    // 上一次收到自身 SyncPlayerDataMessage 时的本地卡组状态（用于区分 NCC 回滚 vs 自身正常同步）
    private static readonly ThreadLocal<object> _lastLocalDeckSnapshot = new();
    private static readonly ThreadLocal<bool> _nccRollbackApplied = new();
    private static readonly ThreadLocal<bool> _skipOriginalMethod = new();

    /// <summary>
    /// 诊断补丁初始化入口。仅客机端运行。
    /// </summary>
    internal static void EnsureInitialized()
    {
        if (NoClientCheatsMod.IsMultiplayerHost())
        {
            DIAG("[Init] Host mode: skipping client-side NCC rollback handler.");
            return;
        }
        DIAG("[Init] Client mode: NCC rollback handler ready.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 补丁 A（核心）：CombatStateSynchronizer.OnSyncPlayerMessageReceived
    //
    // 策略：
    //   - 在 Prefix 中检测 NCC 回滚（senderId == 本地NetId 且消息内容与本地不同）
    //   - 若检测到 NCC 回滚 → 直接同步 → return false 跳过原方法
    //   - 若不是 NCC 回滚 → 记录状态 → 继续执行原方法
    // ═══════════════════════════════════════════════════════════════════════
    [HarmonyPatch]
    private static class OnSyncPlayerReceived_Patch
    {
        static MethodBase TargetMethod()
        {
            if (_syncMsgType == null) return null;
            var cssType = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer");
            if (cssType == null) return null;
            return AccessTools.Method(cssType, "OnSyncPlayerMessageReceived",
                new[] { _syncMsgType, typeof(ulong) });
        }

        static bool Prefix(object __instance, object syncMessage, ulong senderId)
        {
            _skipOriginalMethod.Value = false;
            _nccRollbackApplied.Value = false;

            if (syncMessage == null) return true;

            try
            {
                var msgType = syncMessage.GetType().FullName ?? "";
                if (!msgType.Contains("SyncPlayerDataMessage")) return true;

                ulong msgPlayerNetId = GetPlayerNetIdFromMessage(syncMessage);
                object sp = GetSerializablePlayerFromMessage(syncMessage);

                DIAG($"[SyncRecv] ★ RECEIVED senderId={senderId} msgPlayer.NetId={msgPlayerNetId}");

                // ── 步骤1：获取本地 Player NetId ──────────────────────────
                ulong localPlayerNetId = GetLocalPlayerNetId();
                DIAG($"[SyncRecv]   localPlayer.NetId={localPlayerNetId}");

                // ── 步骤2：检测是否是自身消息 ───────────────────────────
                bool isOwnMessage = (localPlayerNetId != 0 && senderId == localPlayerNetId);
                DIAG($"[SyncRecv]   isOwnMessage={isOwnMessage}");

                if (!isOwnMessage)
                {
                    DIAG($"[SyncRecv]   Not own message — allowing original method.");
                    return true;  // 不是自身消息，继续正常处理
                }

                // ── 步骤3：对比本地卡组 vs 消息中的卡组 ──────────────
                var localDeck = GetLocalPlayerDeck();
                var msgDeck = GetSerializablePlayerDeck(sp);
                DIAG($"[SyncRecv]   localDeck={localDeck} msgDeck={msgDeck}");

                bool decksMatch = (localDeck == msgDeck);
                DIAG($"[SyncRecv]   decksMatch={decksMatch}");

                // ── 步骤4：根据对比结果决定行为 ──────────────────────────
                if (!decksMatch)
                {
                    // 卡组不匹配 → 这是主机发来的 NCC 回滚！
                    DIAG($"[SyncRecv] ★★★ DECK MISMATCH → NCC ROLLBACK DETECTED!");
                    DIAG($"[SyncRecv]   Applying rollback: msgDeck={msgDeck}");

                    bool applied = TryApplyNCCRollback(sp);
                    _nccRollbackApplied.Value = applied;
                    _skipOriginalMethod.Value = applied;

                    if (applied)
                    {
                        DIAG($"[SyncRecv] ★ NCC ROLLBACK APPLIED! Skipping original OnSyncPlayerMessageReceived.");
                        return false;  // 跳过原方法，避免后续 NetId 问题
                    }
                    else
                    {
                        DIAG($"[SyncRecv]   NCC rollback apply FAILED — falling through to original method.");
                    }
                }
                else
                {
                    // 卡组匹配 → 这是自身正常同步 → 记录状态后正常处理
                    DIAG($"[SyncRecv]   Deck matches — own normal sync, recording state.");
                    _lastLocalDeckSnapshot.Value = sp;
                }

                // 记录当前状态
                _lastLocalDeckSnapshot.Value = sp;
            }
            catch (Exception ex)
            {
                DIAG($"[SyncRecv] Prefix error: {ex.Message}");
            }

            return true;
        }

        static void Postfix(object __instance, object syncMessage, ulong senderId)
        {
            if (_skipOriginalMethod?.Value == true)
            {
                DIAG($"[SyncRecv] Postfix: original method was skipped by NCC.");
                return;
            }

            if (syncMessage == null) return;
            try
            {
                var msgType = syncMessage.GetType().FullName ?? "";
                if (!msgType.Contains("SyncPlayerDataMessage")) return;

                ulong msgPlayerNetId = GetPlayerNetIdFromMessage(syncMessage);

                bool alreadyExisted = false;
                try
                {
                    var sdField = AccessTools.Field(__instance.GetType(), "_syncData");
                    var sd = sdField?.GetValue(__instance) as IDictionary;
                    alreadyExisted = sd?.Contains(senderId) ?? false;
                }
                catch { }

                DIAG($"[SyncRecv] Postfix _syncData[{senderId}] alreadyExisted={alreadyExisted} "
                    + $"msgPlayer.NetId={msgPlayerNetId}");

                if (alreadyExisted)
                {
                    DIAG($"[SyncRecv] ★★★ WARNING: Duplicate SyncPlayerDataMessage from senderId={senderId}!");
                }
            }
            catch (Exception ex)
            {
                DIAG($"[SyncRecv] Postfix error: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 尝试应用 NCC 回滚：获取本地 Player 并调用 SyncWithSerializedPlayer。
    /// </summary>
    private static bool TryApplyNCCRollback(object serializablePlayer)
    {
        if (serializablePlayer == null) return false;

        try
        {
            // 获取本地 Player
            object localPlayer = GetLocalPlayer();
            if (localPlayer == null)
            {
                DIAG("[Rollback] Could not get local Player (state null or not found).");
                return false;
            }

            ulong localNetId = GetPlayerNetIdFromEntity(localPlayer);
            DIAG($"[Rollback] Got local Player: NetId={localNetId}");

            // 直接调用 SyncWithSerializedPlayer
            // 注意：这里直接用传入的 player（msg.player），不经过 _syncData
            // 所以不会有 NetId 错配问题
            var syncMethod = AccessTools.Method(
                localPlayer.GetType(),
                "SyncWithSerializedPlayer",
                new[] { _serializablePlayerType });

            if (syncMethod == null)
            {
                DIAG("[Rollback] SyncWithSerializedPlayer method not found on Player.");
                return false;
            }

            DIAG($"[Rollback] Calling SyncWithSerializedPlayer on local Player...");
            syncMethod.Invoke(localPlayer, new[] { serializablePlayer });
            DIAG("[Rollback] ★ NCC Rollback SUCCEEDED!");

            return true;
        }
        catch (Exception ex)
        {
            DIAG($"[Rollback] Apply failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取客机本地的 Player NetId（通过 RunManager 获取）。
    /// </summary>
    private static ulong GetLocalPlayerNetId()
    {
        try
        {
            var rmInst = _runManagerType?.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (rmInst == null) return 0;

            var runState = rmInst.GetType()
                .GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(rmInst) as Godot.Node;
            if (runState == null) return 0;

            // 获取当前玩家数量
            var playersProp = runState.GetType().GetProperty("Players",
                BindingFlags.Public | BindingFlags.Instance);
            var players = playersProp?.GetValue(runState) as IList;
            if (players == null || players.Count == 0) return 0;

            // 客机通常只有一个玩家（自己的）
            var player = players[0];
            return GetPlayerNetIdFromEntity(player);
        }
        catch (Exception ex)
        {
            DIAG($"[Utils] GetLocalPlayerNetId failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 获取本地 Player 对象。
    /// </summary>
    private static object GetLocalPlayer()
    {
        try
        {
            var rmInst = _runManagerType?.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (rmInst == null) return null;

            var runState = rmInst.GetType()
                .GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(rmInst) as Godot.Node;
            if (runState == null) return null;

            // 客机本地只有一个 Player
            var playersProp = runState.GetType().GetProperty("Players",
                BindingFlags.Public | BindingFlags.Instance);
            var players = playersProp?.GetValue(runState) as IList;
            if (players == null || players.Count == 0) return null;

            return players[0];
        }
        catch (Exception ex)
        {
            DIAG($"[Utils] GetLocalPlayer failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取本地 Player 当前持有的卡组信息（用于对比）。
    /// 返回格式："10C/0U" 或 "N/A"。
    /// </summary>
    private static string GetLocalPlayerDeck()
    {
        try
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer == null) return "null";

            var deckProp = localPlayer.GetType().GetProperty("Deck");
            var deck = deckProp?.GetValue(localPlayer);
            if (deck == null) return "null";

            // 获取卡数
            var countProp = deck.GetType().GetProperty("Count");
            int cardCount = Convert.ToInt32(countProp?.GetValue(deck) ?? -1);

            // 获取升级卡数（如果有）
            int upgradedCount = 0;
            try
            {
                var cardsField = AccessTools.Field(deck.GetType(), "_cards");
                var cards = cardsField?.GetValue(deck) as IList;
                if (cards != null)
                {
                    foreach (var card in cards)
                    {
                        var upgradedProp = card?.GetType().GetProperty("Upgraded");
                        if (upgradedProp != null && (bool)(upgradedProp.GetValue(card) ?? false))
                            upgradedCount++;
                    }
                }
            }
            catch { }

            return $"{cardCount}C/{upgradedCount}U";
        }
        catch
        {
            return "N/A";
        }
    }

    /// <summary>
    /// 从 SerializablePlayer 获取卡组信息。
    /// </summary>
    private static string GetSerializablePlayerDeck(object sp)
    {
        if (sp == null) return "null";

        try
        {
            var deckField = AccessTools.Field(sp.GetType(), "Deck");
            var deck = deckField?.GetValue(sp);
            if (deck == null) return "null";

            var countProp = deck.GetType().GetProperty("Count");
            int cardCount = Convert.ToInt32(countProp?.GetValue(deck) ?? -1);

            return $"{cardCount}C";
        }
        catch
        {
            return "N/A";
        }
    }

    private static ulong GetPlayerNetIdFromMessage(object msg)
    {
        if (msg == null) return 0;
        foreach (var name in new[] { "player", "Player" })
        {
            var field = AccessTools.Field(msg.GetType(), name);
            var player = field?.GetValue(msg);
            if (player != null)
            {
                ulong id = GetPlayerNetIdFromSerializable(player);
                if (id != 0) return id;
            }
        }
        return 0;
    }

    private static object GetSerializablePlayerFromMessage(object msg)
    {
        if (msg == null) return null;
        foreach (var name in new[] { "player", "Player" })
        {
            var field = AccessTools.Field(msg.GetType(), name);
            var player = field?.GetValue(msg);
            if (player != null) return player;
        }
        return null;
    }

    private static ulong GetPlayerNetIdFromSerializable(object player)
    {
        if (player == null) return 0;

        if (player is IDictionary idict)
        {
            foreach (var key in new[] { "player", "Player" })
            {
                if (idict.Contains(key))
                {
                    var inner = idict[key];
                    if (inner != null) return GetPlayerNetIdFromSerializable(inner);
                }
            }
        }

        foreach (var name in new[] { "NetId", "netId", "_netId", "NetworkId" })
        {
            var val = GetMemberValue(player, name);
            if (val == null) continue;
            try { return Convert.ToUInt64(val); } catch { }
        }
        return 0;
    }

    private static ulong GetPlayerNetIdFromEntity(object entity)
    {
        if (entity == null) return 0;
        foreach (var name in new[] { "NetId", "netId", "_netId", "NetworkId" })
        {
            var val = GetMemberValue(entity, name);
            if (val == null) continue;
            try { return Convert.ToUInt64(val); } catch { }
        }
        return 0;
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null) return null;
        var t = target.GetType();
        var prop = t.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(target);
        var field = t.GetField(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field != null) return field.GetValue(target);
        var allFields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var f in allFields)
        {
            if (f.Name == memberName || f.Name == "_" + memberName
                || f.Name == "<" + memberName + ">k__BackingField")
                return f.GetValue(target);
        }
        return null;
    }
}
#endif

// 客机回滚诊断桩实现（无 HarmonyPatch），避免被自动 PatchAll 注入。
internal static class ClientDiagnosticPatches
{
    internal static void EnsureInitialized()
    {
        // 回滚链路已弃用，保留接口以兼容旧调用点。
    }
}
