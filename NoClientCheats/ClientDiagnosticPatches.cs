using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
[HarmonyPatch]
internal static class ClientDiagnosticPatches
{
    // ── 类型缓存 ─────────────────────────────────────────────────────────
    private static readonly Type _syncMsgType;
    private static readonly Type _serializablePlayerType;
    private static readonly Type _runManagerType;
    private static readonly Type _localContextType;

    static ClientDiagnosticPatches()
    {
        _syncMsgType = AccessTools.TypeByName(
            "MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.SyncPlayerDataMessage")
            ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.Messages.Game.SyncPlayerDataMessage")
            ?? AccessTools.TypeByName("SyncPlayerDataMessage");
        _serializablePlayerType = AccessTools.TypeByName(
            "MegaCrit.Sts2.Core.Saves.Runs.SerializablePlayer")
            ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Players.SerializablePlayer")
            ?? AccessTools.TypeByName("SerializablePlayer");
        _runManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager")
            ?? AccessTools.TypeByName("RunManager");
        _localContextType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Context.LocalContext")
            ?? AccessTools.TypeByName("LocalContext");
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
    private static readonly ThreadLocal<bool> _redirectInProgress = new();
    private static readonly object _pendingRollbackLock = new();
    private static readonly ConditionalWeakTable<object, PendingRollbackInfo> _pendingRollbackSnapshots = new();

    private sealed class PendingRollbackInfo
    {
        public ulong SenderId;
        public ulong TargetNetId;
        public string SnapshotCharacterId;
        public long RegisteredAtMs;
    }

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
                bool explicitNccRollback = sp != null
                    && senderId != 0
                    && msgPlayerNetId != 0
                    && msgPlayerNetId != senderId;

                DIAG($"[SyncRecv] ★ RECEIVED senderId={senderId} msgPlayer.NetId={msgPlayerNetId}");

                if (explicitNccRollback)
                {
                    RegisterPendingRollbackSnapshot(sp, senderId, msgPlayerNetId);
                    DIAG($"[SyncRecv]   Registered NCC redirect snapshot: senderId={senderId} -> targetNetId={msgPlayerNetId}");
                }

                // ── 步骤1：获取本地 Player NetId ──────────────────────────
                ulong localPlayerNetId = GetLocalPlayerNetId();
                DIAG($"[SyncRecv]   localPlayer.NetId={localPlayerNetId}");

                // ── 步骤2：检测是否是自身消息 ───────────────────────────
                bool isOwnMessage = (localPlayerNetId != 0
                    && (senderId == localPlayerNetId || msgPlayerNetId == localPlayerNetId));
                DIAG($"[SyncRecv]   isOwnMessage={isOwnMessage}");

                if (explicitNccRollback && isOwnMessage)
                {
                    DIAG("[SyncRecv]   senderId != msgPlayer.NetId — explicit NCC rollback packet.");

                    bool applied = TryApplyNCCRollback(sp);
                    _nccRollbackApplied.Value = applied;
                    _skipOriginalMethod.Value = applied;

                    if (applied)
                    {
                        DIAG("[SyncRecv] ★ Explicit NCC rollback applied before original method.");
                        return false;
                    }

                    DIAG("[SyncRecv]   Explicit NCC rollback apply failed — falling through to original method as last resort.");
                }

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
    // 补丁 B：Player.SyncWithSerializedPlayer
    //
    // 当 NCC 回滚快照因为 senderId 路由而即将同步到错误玩家时：
    //   1. 识别该 SerializablePlayer 对象就是之前登记过的 NCC 回滚快照
    //   2. 用登记的原始 targetNetId 找到真正应被同步的本地玩家
    //   3. 临时把 snapshot.NetId 还原为 targetNetId
    //   4. 直接对正确玩家执行同步，并跳过当前这次错误调用
    // ═══════════════════════════════════════════════════════════════════════
    [HarmonyPatch]
    private static class SyncWithSerializedPlayer_Patch
    {
        static MethodBase TargetMethod()
        {
            var playerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Players.Player")
                ?? AccessTools.TypeByName("Player");
            if (playerType == null || _serializablePlayerType == null) return null;
            return AccessTools.Method(playerType, "SyncWithSerializedPlayer",
                new[] { _serializablePlayerType });
        }

        static bool Prefix(object __instance, object player)
        {
            if (__instance == null || player == null) return true;
            if (NoClientCheatsMod.IsMultiplayerHost()) return true;
            if (_redirectInProgress.Value) return true;

            try
            {
                if (!TryGetPendingRollbackSnapshot(player, out var pending))
                    return true;

                ulong instanceNetId = GetPlayerNetIdFromEntity(__instance);
                ulong snapshotNetId = GetPlayerNetIdFromSerializable(player);
                string instanceCharacterId = GetPlayerCharacterId(__instance);
                string snapshotCharacterId = GetSerializablePlayerCharacterId(player);

                DIAG($"[SyncPlayer] Pending NCC redirect detected: "
                    + $"instanceNetId={instanceNetId} snapshotNetId={snapshotNetId} "
                    + $"targetNetId={pending.TargetNetId} instanceChar={instanceCharacterId ?? "null"} "
                    + $"snapshotChar={snapshotCharacterId ?? "null"}");

                if (instanceNetId == pending.TargetNetId)
                {
                    DIAG("[SyncPlayer] Snapshot already reached the correct player, clearing pending redirect.");
                    ConsumePendingRollbackSnapshot(player);
                    return true;
                }

                object targetPlayer = ResolvePlayerFromContext(__instance, pending.TargetNetId) ?? GetLocalPlayer();
                ulong targetPlayerNetId = GetPlayerNetIdFromEntity(targetPlayer);
                string targetCharacterId = GetPlayerCharacterId(targetPlayer);

                DIAG($"[SyncPlayer] Resolved target player: exists={targetPlayer != null} "
                    + $"targetPlayerNetId={targetPlayerNetId} targetChar={targetCharacterId ?? "null"}");

                if (targetPlayer == null || targetPlayerNetId != pending.TargetNetId)
                {
                    DIAG("[SyncPlayer] Target player not found or NetId mismatch, leaving original sync path untouched.");
                    return true;
                }

                if (!string.IsNullOrEmpty(snapshotCharacterId)
                    && !string.IsNullOrEmpty(targetCharacterId)
                    && !string.Equals(snapshotCharacterId, targetCharacterId, StringComparison.Ordinal))
                {
                    DIAG("[SyncPlayer] Snapshot character does not match resolved target player, skipping redirect.");
                    return true;
                }

                SetSerializablePlayerNetId(player, pending.TargetNetId);
                DIAG($"[SyncPlayer] Redirecting NCC rollback to targetNetId={pending.TargetNetId}.");

                var syncMethod = AccessTools.Method(targetPlayer.GetType(), "SyncWithSerializedPlayer",
                    new[] { _serializablePlayerType });
                if (syncMethod == null)
                {
                    DIAG("[SyncPlayer] SyncWithSerializedPlayer method not found on resolved target player.");
                    return true;
                }

                _redirectInProgress.Value = true;
                try
                {
                    syncMethod.Invoke(targetPlayer, new[] { player });
                }
                finally
                {
                    _redirectInProgress.Value = false;
                }

                ConsumePendingRollbackSnapshot(player);
                DIAG("[SyncPlayer] ★ NCC redirect succeeded, skipping original SyncWithSerializedPlayer.");
                return false;
            }
            catch (Exception ex)
            {
                DIAG($"[SyncPlayer] Redirect failed: {ex.GetType().Name}: {ex.Message}");
                return true;
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
            ulong localContextNetId = GetLocalContextNetId();
            if (localContextNetId != 0) return localContextNetId;

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
            ulong localContextNetId = GetLocalContextNetId();
            var rmInst = _runManagerType?.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (rmInst == null) return null;

            object runState = rmInst.GetType()
                .GetProperty("State", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(rmInst);
            runState ??= rmInst.GetType()
                .GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(rmInst);
            runState ??= rmInst.GetType()
                .GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(rmInst);
            if (runState == null) return null;

            var playersProp = runState.GetType().GetProperty("Players",
                BindingFlags.Public | BindingFlags.Instance);
            var players = playersProp?.GetValue(runState) as IList;
            if (players == null || players.Count == 0) return null;

            if (localContextNetId != 0)
            {
                foreach (var player in players)
                {
                    if (GetPlayerNetIdFromEntity(player) == localContextNetId)
                        return player;
                }
            }

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

    private static ulong GetLocalContextNetId()
    {
        try
        {
            var raw = _localContextType?.GetProperty("NetId",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (raw == null) return 0;
            return Convert.ToUInt64(raw);
        }
        catch
        {
            return 0;
        }
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

    private static string GetSerializablePlayerCharacterId(object player)
        => GetMemberValue(player, "CharacterId")?.ToString();

    private static string GetPlayerCharacterId(object entity)
    {
        var character = GetMemberValue(entity, "Character");
        return GetMemberValue(character, "Id")?.ToString();
    }

    private static void SetSerializablePlayerNetId(object player, ulong netId)
    {
        if (player == null || netId == 0) return;
        foreach (var name in new[] { "NetId", "netId", "_netId", "NetworkId" })
            SetMemberValue(player, name, netId);
    }

    private static object ResolvePlayerFromContext(object playerEntity, ulong netId)
    {
        if (playerEntity == null || netId == 0) return null;

        try
        {
            var runState = GetMemberValue(playerEntity, "RunState");
            if (runState != null)
            {
                var getPlayer = AccessTools.Method(runState.GetType(), "GetPlayer", new[] { typeof(ulong) });
                var resolved = getPlayer?.Invoke(runState, new object[] { netId });
                if (resolved != null) return resolved;

                var players = GetMemberValue(runState, "Players") as IEnumerable;
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        if (GetPlayerNetIdFromEntity(p) == netId)
                            return p;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DIAG($"[Utils] ResolvePlayerFromContext failed: {ex.Message}");
        }

        return null;
    }

    private static void RegisterPendingRollbackSnapshot(object snapshot, ulong senderId, ulong targetNetId)
    {
        if (snapshot == null || senderId == 0 || targetNetId == 0) return;

        var info = new PendingRollbackInfo
        {
            SenderId = senderId,
            TargetNetId = targetNetId,
            SnapshotCharacterId = GetSerializablePlayerCharacterId(snapshot),
            RegisteredAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_pendingRollbackLock)
        {
            _pendingRollbackSnapshots.Remove(snapshot);
            _pendingRollbackSnapshots.Add(snapshot, info);
        }
    }

    private static bool TryGetPendingRollbackSnapshot(object snapshot, out PendingRollbackInfo info)
    {
        info = null;
        if (snapshot == null) return false;

        lock (_pendingRollbackLock)
        {
            if (!_pendingRollbackSnapshots.TryGetValue(snapshot, out info))
                return false;
        }

        long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - info.RegisteredAtMs;
        if (ageMs > 30000)
        {
            ConsumePendingRollbackSnapshot(snapshot);
            info = null;
            return false;
        }

        return true;
    }

    private static void ConsumePendingRollbackSnapshot(object snapshot)
    {
        if (snapshot == null) return;
        lock (_pendingRollbackLock)
            _pendingRollbackSnapshots.Remove(snapshot);
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

    private static bool SetMemberValue(object target, string memberName, object value)
    {
        if (target == null) return false;
        var t = target.GetType();
        var prop = t.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return true;
        }

        var field = t.GetField(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            return true;
        }

        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (f.Name == memberName || f.Name == "_" + memberName
                || f.Name == "<" + memberName + ">k__BackingField")
            {
                f.SetValue(target, value);
                return true;
            }
        }

        return false;
    }
}
