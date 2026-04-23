using Godot;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// 卡组状态回滚检测补丁。
///
/// 工作流程：
/// 1. RestSiteSynchronizer.ChooseOption Postfix：主机执行完休息点/事件选项后，
///    记录该玩家此刻的卡组快照（此时已是正确的最终状态）。
/// 2. CombatStateSynchronizer.OnSyncPlayerMessageReceived Prefix：主机收到客机的
///    同步消息时，对比快照与收到的卡组，不一致则强制回滚。
/// 3. RunState.PushRoom Prefix：进入新房间前清空快照，防止残留数据干扰。
/// </summary>
#if false // 回滚与火堆卡组检测链路已弃用：保留历史实现，仅供追溯。
[HarmonyPatch]
internal static class DeckSyncPatches
{
    // 诊断标志：确保 SyncMessage 字段只打印一次
    private static bool _syncMsgFieldsLogged = false;

    // 诊断标志：确保 _restSites 结构只打印一次
    private static bool _restSiteFieldsLogged = false;

    // 诊断标志：确保 ChooseOption 参数类型只打印一次
    private static bool _chooseOptionParamsLogged = false;

    // ─── NCC 回滚检测状态（线程本地存储） ────────────────────────────────
    // 在 Finalizer Prefix 中于 patch 之前设置，在 CLIENT-APPLY-FALLBACK 中读取。
    // 因为 Finalizer 的 NetId patch 在 CLIENT-APPLY-FALLBACK 之前执行，
    // patch 后 receivedPlayer.NetId == senderId，无法通过比较检测 NCC 回滚。
    private static readonly ThreadLocal<bool> _wasNCCRollback = new();
    private static readonly ThreadLocal<ulong> _originalMsgPlayerNetId = new();
    private static readonly ThreadLocal<ulong> _originalSenderId = new();

    // ─── Patch A: RestSiteSynchronizer.ChooseOption (Prefix) ──────────────────
    // 时机：玩家确认选项、选项执行之前，记录此刻的卡组快照
    // 操作：记录 "操作前" 快照，由 Postfix 消费并与操作后状态对比
    [HarmonyPatch]
    private static class ChooseOptionPrefix
    {
        // #region NCC_DIAG_CHOOSEOPTION_PREFIX
        static void DIAG(string msg) => LogDiag("ChooseOptionPrefix", msg);
        static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer"
            )?.GetMethod("ChooseOption",
                BindingFlags.NonPublic | BindingFlags.Instance);
            DIAG($"TargetMethod: type={t?.DeclaringType?.FullName ?? "null"} method={t?.Name ?? "null"}");

            // 一次性诊断：打印 ChooseOption 参数类型
            if (!_chooseOptionParamsLogged && t != null)
            {
                _chooseOptionParamsLogged = true;
                var p = t.GetParameters();
                DIAG($"ChooseOption params count={p.Length}");
                for (int i = 0; i < p.Length; i++)
                    DIAG($"  param[{i}] {p[i].ParameterType.Name} {p[i].Name}");

                // 也枚举 SerializablePlayer 的所有属性（供 _IsRemotePlayer 参考）
                var playerType = p.Length > 1 ? p[1].ParameterType : null;
                if (playerType != null)
                {
                    foreach (var prop in playerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        DIAG($"  playerProp: {prop.PropertyType.Name} {prop.Name}");
                    foreach (var field in playerType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        DIAG($"  playerField: {field.FieldType.Name} {field.Name}");
                }
            }

            return t;
        }

        static void Prefix(object __instance, object player, int optionIndex)
        {
            try
            {
                // ── 诊断日志：无论是否跳过，都要打印完整信息 ──
                object isLocalVal = null;
                string playerTypeName = player?.GetType().Name ?? "null";
                ulong playerNetId = 0;
                try { playerNetId = GetPlayerNetId(player); } catch { }
                string playerCharId = "?";
                try { playerCharId = GetPlayerDisplayName(player) ?? "?"; } catch { }
                try { isLocalVal = player?.GetType().GetProperty("IsLocal")?.GetValue(player); } catch { }
                bool isRemote = false;
                try { isRemote = isLocalVal != null && Convert.ToBoolean(isLocalVal) == false; } catch { }

                // 无论是否 remote，都打日志，确保能看到数据
                DIAG($"[FULLTRACE] ChooseOption.Prefix *** ENTRY *** playerNetId={playerNetId} IsLocal={isLocalVal}({isLocalVal?.GetType().Name}) isRemote={isRemote} optionIndex={optionIndex} playerType={playerTypeName} char={playerCharId}");
                DIAG($"[FULLTRACE]   ToSerializable exists={player?.GetType().GetMethod("ToSerializable", BindingFlags.Public | BindingFlags.Instance) != null}");

                // ── 跳过本地玩家（仅打印日志，不做其他处理）───────────────────────────
                if (!isRemote) {
                    DIAG($"[FULLTRACE] Skip: not a remote player (IsLocal={isLocalVal})");
                    return;
                }
                if (player == null) { DIAG($"Skip: player=null"); return; }
                if (playerNetId == 0) { DIAG($"Skip: playerNetId=0"); return; }

                // 枚举 __instance 的 _restSites 结构（一次性诊断）
                if (!_restSiteFieldsLogged)
                {
                    _restSiteFieldsLogged = true;
                    try {
                        var rsField = __instance.GetType().GetField("_restSites", BindingFlags.NonPublic | BindingFlags.Instance);
                        var rs = rsField?.GetValue(__instance) as IList;
                        if (rs != null) {
                            DIAG($"_restSites count={rs.Count}");
                            for (int i = 0; i < rs.Count; i++) {
                                var site = rs[i];
                                var optsField = site?.GetType().GetField("options", BindingFlags.Public | BindingFlags.Instance);
                                var opts = optsField?.GetValue(site) as IList;
                                DIAG($"  _restSites[{i}] options count={opts?.Count ?? -1}");
                                if (opts != null) {
                                    for (int j = 0; j < opts.Count; j++) {
                                        var opt = opts[j];
                                        var oidProp = opt?.GetType().GetProperty("OptionId");
                                        DIAG($"    [{i}][{j}] OptionId={oidProp?.GetValue(opt)}");
                                    }
                                }
                            }
                        }
                    } catch (Exception ex) { DIAG($"_restSites enum error: {ex.Message}"); }
                }

                DIAG($"[FULLTRACE] ChooseOption.Prefix playerNetId={playerNetId} isRemote={isRemote} optionIndex={optionIndex}");

                if (!isRemote) { DIAG($"Skip: local player"); return; }
                if (player == null) { DIAG($"Skip: player=null"); return; }
                if (playerNetId == 0) { DIAG($"Skip: playerNetId=0"); return; }

                var toSerializable = player.GetType().GetMethod("ToSerializable",
                    BindingFlags.Public | BindingFlags.Instance);
                if (toSerializable == null) { DIAG("ToSerializable=null, returning"); return; }

                var preSnapshot = toSerializable.Invoke(player, null);
                NoClientCheatsMod.SetPreDeckSnapshot(playerNetId, preSnapshot);

                var safeName = GetPlayerDisplayName(player) ?? $"#{playerNetId % 10000}";
                var optionName = "?";
                try
                {
                    var restSitesField = __instance.GetType().GetField("_restSites",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var restSites = restSitesField?.GetValue(__instance) as IList;
                    if (restSites != null && optionIndex >= 0 && optionIndex < restSites.Count)
                    {
                        var restSite = restSites[optionIndex];
                        var optionsField = restSite.GetType().GetField("options",
                            BindingFlags.Public | BindingFlags.Instance);
                        var options = optionsField?.GetValue(restSite) as IList;
                        var hoveredField = restSite.GetType().GetField("hoveredOptionIndex",
                            BindingFlags.Public | BindingFlags.Instance);
                        var hoveredVal = hoveredField?.GetValue(restSite);
                        int idx = hoveredVal != null ? Convert.ToInt32(hoveredVal) : optionIndex;
                        if (options != null && idx >= 0 && idx < options.Count)
                        {
                            var opt = options[idx];
                            var optionIdProp = opt.GetType().GetProperty("OptionId");
                            optionName = optionIdProp?.GetValue(opt)?.ToString() ?? optionName;
                        }
                    }
                }
                catch { /* ignore */ }

                DIAG($"PRE snapshot set: {safeName} netId={playerNetId} option={optionName} {GetDeckSummary(preSnapshot)}");
            }
            catch (Exception ex)
            {
                DIAG($"Prefix exception: {ex.Message}");
                GD.PushError($"[NCC] ChooseOption Prefix error: {ex}");
            }
        }
        // #endregion
    }

    // ─── Patch A: RestSiteSynchronizer.ChooseOption ────────────────────────────
    // 时机：主机执行完 option.OnSelect() 之后，player.Deck 已是正确的最终状态
    // 操作：记录该玩家的预期卡组快照
    [HarmonyPatch]
    private static class ChooseOptionPostfix
    {
        // #region NCC_DIAG_POSTFIX
        static void DIAG(string msg) => LogDiag("ChooseOptionPostfix", msg);
        static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer"
            )?.GetMethod("ChooseOption",
                BindingFlags.NonPublic | BindingFlags.Instance);
            DIAG($"TargetMethod: type={t?.DeclaringType?.FullName ?? "null"} method={t?.Name ?? "null"}");
            return t;
        }

        static void Postfix(object __instance, object player, int optionIndex, Task<bool> __result)
        {
            ulong playerNetId = 0;
            string playerTypeName = "?";
            string playerCharId = "?";
            object isLocalVal = null;
            bool isRemote = false;
            try { playerNetId = GetPlayerNetId(player); } catch { }
            try { playerTypeName = player?.GetType().Name ?? "null"; } catch { }
            try { playerCharId = GetPlayerDisplayName(player) ?? "?"; } catch { }
            try { isLocalVal = player?.GetType().GetProperty("IsLocal")?.GetValue(player); } catch { }
            try { isRemote = isLocalVal != null && Convert.ToBoolean(isLocalVal) == false; } catch { }
            bool resultVal = false;
            try { resultVal = __result?.Result == true; } catch { }

            DIAG($"[FULLTRACE] ChooseOption.Postfix *** ENTRY *** playerNetId={playerNetId} IsLocal={isLocalVal} isRemote={isRemote} optionIndex={optionIndex} result={resultVal} playerType={playerTypeName} char={playerCharId}");

            if (!resultVal) {
                DIAG($"[FULLTRACE] Skip: result={resultVal}(false)");
                return;
            }

            try
            {
                // ── 核心：直接从 player 参数判断，不依赖 AsyncLocal ──
                if (!isRemote) {
                    DIAG($"[FULLTRACE] Postfix skip: not remote player (IsLocal={isLocalVal})");
                    return;
                }
                if (player == null) { DIAG($"Postfix skip: player=null"); return; }
                if (playerNetId == 0) { DIAG($"Postfix skip: playerNetId=0"); return; }

                var safeName = GetPlayerDisplayName(player) ?? $"#{playerNetId % 10000}";

                var toSerializable = player.GetType().GetMethod("ToSerializable",
                    BindingFlags.Public | BindingFlags.Instance);
                if (toSerializable == null) { DIAG("ToSerializable=null, returning"); return; }

                var postSnapshot = toSerializable.Invoke(player, null);

                // 1. 记录 post 快照（用于 sync 备用）
                NoClientCheatsMod.SetExpectedDeckSnapshot(playerNetId, postSnapshot);

                var optionName = GetOptionIdAtIndex(__instance, optionIndex);
                DIAG($"[FULLTRACE] POST for {safeName} netId={playerNetId} option={optionName} {GetDeckSummary(postSnapshot)}");

                // 3. 计算允许的增量并通知 SyncReceivedPatch
                int allowedCardDelta = 0, allowedUpgradeDelta = 0;
                bool isRemoveEvent = optionName?.Contains("Remove", StringComparison.OrdinalIgnoreCase) == true ||
                                     optionName?.Contains("Scissors", StringComparison.OrdinalIgnoreCase) == true ||
                                     optionName?.Contains("Cut", StringComparison.OrdinalIgnoreCase) == true;
                bool isUpgradeEvent = optionName?.Contains("Upgrade", StringComparison.OrdinalIgnoreCase) == true ||
                                      optionName?.Contains("Scent", StringComparison.OrdinalIgnoreCase) == true ||
                                      optionName?.Contains("Scented", StringComparison.OrdinalIgnoreCase) == true;
                if (isRemoveEvent) allowedCardDelta = -1; // 最多删除 1 张
                if (isUpgradeEvent) allowedUpgradeDelta = 1; // 最多升级 1 张

                DIAG($"[FULLTRACE] SetLastOptionId: allowedCardDelta={allowedCardDelta} allowedUpgradeDelta={allowedUpgradeDelta}");
                SyncReceivedPatch.SetLastOptionId(playerNetId, optionName, allowedCardDelta, allowedUpgradeDelta);

                // 2. 立即对比：操作前快照 vs 操作后快照（核心检测）
                var preSnapshot = NoClientCheatsMod.ConsumePreDeckSnapshot(playerNetId);
                DIAG($"[FULLTRACE] ConsumePre({playerNetId}) => {(preSnapshot != null ? "found " + GetDeckSummary(preSnapshot) : "null")}");
                if (preSnapshot != null)
                {
                    bool matches = _DecksMatch(preSnapshot, postSnapshot);
                    DIAG($"[FULLTRACE] _DecksMatch={matches}");
                    if (!matches)
                    {
                        var cheatType = _DetectCheatType(preSnapshot, postSnapshot);
                        NoClientCheatsMod.RecordCheat(
                            playerNetId, safeName, null,
                            $"ui_exploit:{cheatType}", true);
                        _RecordImmediateCheatNotifyTick(playerNetId);

                        var diff = GetDeckDiff(preSnapshot, postSnapshot);
                        GD.Print($"[NCC] IMMEDIATE exploit detected for {safeName} "
                            + $"(netId={playerNetId}) at {optionName}: "
                            + $"type={cheatType}\n{diff}");

                        // 立即回滚：将卡组恢复到操作前状态（本地）
                        DIAG($"[FULLTRACE] Triggering rollback for {safeName}");
                        string rollbackResult = _RollbackPlayerDeck(player, preSnapshot);
                        DIAG($"[FULLTRACE] Rollback result: {rollbackResult}");

                        // 回滚后验证：重新获取 player 卡组，确认已恢复
                        var afterRollbackDeck = GetSerializableDeck(player.GetType().GetMethod("ToSerializable", BindingFlags.Public | BindingFlags.Instance)?.Invoke(player, null));
                        DIAG($"[FULLTRACE] After rollback player deck: {GetDeckSummary(afterRollbackDeck)}");

                        // ═══════════════════════════════════════════════════════════════
                        // 【修复Bug】向客机发送网络修正消息，防止状态不同步导致黑屏
                        // ChooseOption 作弊时，只做本地回滚不够，必须通知远程客机。
                        // ═══════════════════════════════════════════════════════════════
                        _SendRollbackForImmediateCheat(playerNetId, preSnapshot);
                    }
                }
                else
                {
                    // 无 pre 快照说明不是通过 ChooseOption 触发的，跳过即时检测
                    DIAG($"[FULLTRACE] No pre-snapshot — skipping immediate check");
                }
            }
            catch (Exception ex)
            {
                DIAG($"Postfix exception: {ex.Message}");
                GD.PushError($"[NCC] ChooseOption Postfix error: {ex}");
            }
        }
        // #endregion

        // 从 _restSites[playerSlotIndex].options[index].OptionId 获取选项名
        // 注意：此方法为 ChooseOptionPostfix 私有，Prefix 使用自己的内联逻辑
        private static string GetOptionIdAtIndex(object synchronizer, int optionIndex)
        {
            try
            {
                var restSitesField = synchronizer.GetType().GetField("_restSites",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var restSites = restSitesField?.GetValue(synchronizer) as IList;
                if (restSites == null || optionIndex < 0 || optionIndex >= restSites.Count)
                    return $"option[{optionIndex}]";

                var restSite = restSites[optionIndex];
                var optionsField = restSite.GetType().GetField("options",
                    BindingFlags.Public | BindingFlags.Instance);
                var options = optionsField?.GetValue(restSite) as IList;
                if (options == null || options.Count == 0) return "unknown";

                // 找到实际选中的 options 列表中的索引
                var hoveredField = restSite.GetType().GetField("hoveredOptionIndex",
                    BindingFlags.Public | BindingFlags.Instance);
                var hoveredVal = hoveredField?.GetValue(restSite);
                int idx = optionIndex;
                if (hoveredVal != null)
                    idx = Convert.ToInt32(hoveredVal);
                if (idx < 0 || idx >= options.Count) return "unknown";

                var opt = options[idx];
                var optionIdProp = opt.GetType().GetProperty("OptionId");
                return optionIdProp?.GetValue(opt)?.ToString() ?? "unknown";
            }
            catch { return $"option[{optionIndex}]"; }
        }
    }

    // ─── Patch C: CombatStateSynchronizer.OnSyncPlayerMessageReceived ───────────
    // 检测逻辑：
    //   存储上一次 SyncReceived 的 SerializablePlayer 作为基准（pre-event）
    //   当前 SyncReceived 的 SerializablePlayer 是事件后状态（post-event）
    //   对比 upgrade 增量 vs 事件允许值（通过 ChooseOptionPostfix 设置的 optionId 查表）
    [HarmonyPatch]
    private static class SyncReceivedPatch
    {
        static void DIAG(string msg) => LogDiag("SyncReceived", msg);

        // 存储上次 SyncReceived 的卡组状态（由上一次 Postfix 写入）
        // 这样当前 Postfix 比较的是：当前消息 vs 上一次 SyncReceived 消息
        // tuple: (cardCount, upgradedCount, optionId, allowedCardDelta, allowedUpgradeDelta)
        private static readonly System.Collections.Generic.Dictionary<ulong, (int cardCount, int upgradedCount, string optionId, int allowedCardDelta, int allowedUpgradeDelta)>
            _lastSyncState = new();

        // 缓存 CombatStateSynchronizer 单例（通过前缀方法获取一次，避免每次反射）
        private static object _cachedSynchronizer = null;

        static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer"
            );
            var m = t?.GetMethod("OnSyncPlayerMessageReceived",
                BindingFlags.NonPublic | BindingFlags.Instance);
            DIAG($"TargetMethod: type={t?.FullName ?? "null"} method={m?.Name ?? "null"}");
            return m;
        }

        // Prefix：在当前 Postfix 之前，把上次收到的 SerializablePlayer 快照保存下来。
        // 这样 Postfix 作弊检测时可以拿到「作弊前」的完整快照用于回滚。
        static void Prefix(object __instance)
        {
            if (__instance != null && _cachedSynchronizer == null)
                _cachedSynchronizer = __instance;
        }

        // 供外部使用：获取已缓存的 CombatStateSynchronizer 实例
        internal static object GetCachedSynchronizer() => _cachedSynchronizer;

        /// <summary>发现实例时写入缓存（例如从 PlayerChoice 路径解析到）。</summary>
        internal static void RememberSynchronizer(object instance)
        {
            if (instance != null) _cachedSynchronizer = instance;
        }

        /// <summary>
        /// 立即回滚后：把「上一轮」Serializable、Sync 基线、lastDeckSize 与合法快照对齐，
        /// 避免后续 SyncReceived 用错 prevCards，并保证 _syncData 与活 Player 一致。
        /// </summary>
        internal static void SetBaselineFromSerializable(ulong netId, object serializablePlayer)
        {
            if (serializablePlayer == null) return;
            var deck = GetSerializableDeck(serializablePlayer);
            int n = deck?.Count ?? 0;
            int u = CountUpgraded(deck ?? Array.Empty<object>());
            lock (_lastSerializablePlayer)
                _lastSerializablePlayer[netId] = serializablePlayer;
            lock (_lastSyncState)
                _lastSyncState[netId] = (n, u, "", 0, 0);
            lock (_lastSyncDeckSize)
                _lastSyncDeckSize[netId] = n;
        }

        // 供外部调用：ChooseOptionPostfix 在此注册当前事件类型和允许的增量
        public static void SetLastOptionId(ulong netId, string optionId, int allowedCardDelta = 0, int allowedUpgradeDelta = 0)
        {
            lock (_lastSyncState)
            {
                // 写入 optionId 和允许的增量（不清除已有的 cardCount/upgradedCount）
                if (_lastSyncState.TryGetValue(netId, out var existing))
                    _lastSyncState[netId] = (existing.cardCount, existing.upgradedCount, optionId, allowedCardDelta, allowedUpgradeDelta);
                else
                    _lastSyncState[netId] = (0, 0, optionId, allowedCardDelta, allowedUpgradeDelta);
            }
        }

        static void Postfix(object __instance, object syncMessage, ulong senderId)
        {
            if (syncMessage == null) return;
            if (__instance != null)
                _cachedSynchronizer = __instance;
            try
            {
                var receivedPlayer = GetSyncMessagePlayer(syncMessage);
                if (receivedPlayer == null) return;

                // ── NetId 修正：防止「作弊玩家快照」通过 SendToPeer 错误路由到本机 → WaitForSync 强退 ──
                // 客机上：senderId=主机NetId，receivedPlayer.NetId=副机NetId → 立即修正 receivedPlayer.NetId=senderId
                // 主机上：senderId=副机NetId，receivedPlayer.NetId=副机NetId → 不修正（本来就一致）
                {
                    var playerNetId = GetPlayerNetId(receivedPlayer);
                    if (playerNetId != 0 && playerNetId != senderId)
                    {
                        foreach (var name in new[] { "NetId", "OwnerNetId", "OwnerId", "PlayerNetId", "net_id" })
                            _SetMemberAny(receivedPlayer, name, senderId);
                        foreach (var name in new[] { "NetId", "OwnerNetId", "OwnerId", "PlayerNetId", "net_id" })
                            _SetMemberAny(syncMessage, name, senderId);
                        DIAG($"[FULLTRACE] NetId patched {playerNetId}->{senderId} on receivedPlayer/syncMessage");
                    }
                }

                // 立即缓存 receivedPlayer，供 PlayerChoiceReceivePatch 在找不到 Player 时 fallback 使用
                _lastRemotePlayerByNetId[senderId] = receivedPlayer;
                var receivedNetId = GetPlayerNetId(receivedPlayer);
                if (receivedNetId != 0 && receivedNetId != senderId)
                    _lastRemotePlayerByNetId[receivedNetId] = receivedPlayer;

                // 主机刚完成 PlayerChoice 回滚后的短时间内：不要用副机未修正的 Serializable 包覆盖权威快照与 preCheat
                bool incomingRollbackSuppressed = NoClientCheatsMod.IsMultiplayerHost()
                    && _HasRecentImmediateRollback(senderId, receivedNetId);

                var receivedDeck = GetSerializableDeck(receivedPlayer);
                var receivedUpgraded = CountUpgraded(receivedDeck);
                var receivedCards = receivedDeck.Count;
                var realName = GetPlayerDisplayName(receivedPlayer) ?? $"#{senderId % 10000}";

                // ── 必须在覆盖字典之前抓取「上一轮」快照（回滚用）────────────────────
                // 关键：先读后写。这样第二次 sync 时拿到的就是第一次 sync 的合法快照，
                // 而不是已被第二次 sync 覆盖的作弊后快照。
                object prevSerializableSnapshot = null;
                lock (_lastSerializablePlayer)
                {
                    _lastSerializablePlayer.TryGetValue(senderId, out prevSerializableSnapshot);
                    if (!incomingRollbackSuppressed)
                        _lastSerializablePlayer[senderId] = receivedPlayer;
                }

                bool hadSavedPrevSyncState = false;
                (int cardCount, int upgradedCount, string optionId, int allowedCardDelta, int allowedUpgradeDelta) savedPrevSyncState = default;

                // 取出上一次 SyncReceived 的状态，并写入当前状态
                int prevCards = 0, prevUpgraded = 0;
                string optionId = null;
                bool hasPrev = false;
                int allowedCardDelta = 0, allowedUpgradeDelta = 0;
                lock (_lastSyncState)
                {
                    if (_lastSyncState.TryGetValue(senderId, out var prev))
                    {
                        savedPrevSyncState = prev;
                        hadSavedPrevSyncState = true;
                        prevCards = prev.cardCount;
                        prevUpgraded = prev.upgradedCount;
                        optionId = prev.optionId;
                        allowedCardDelta = prev.allowedCardDelta;
                        allowedUpgradeDelta = prev.allowedUpgradeDelta;
                        hasPrev = prevCards > 0 || prevUpgraded > 0 || !string.IsNullOrEmpty(prev.optionId);
                    }
                    // 更新为当前状态（先比较再更新！）
                    _lastSyncState[senderId] = (receivedCards, receivedUpgraded, optionId ?? "", allowedCardDelta, allowedUpgradeDelta);
                }

                int cardDelta = receivedCards - prevCards;
                int upgradeDelta = receivedUpgraded - prevUpgraded;
                DIAG($"[FULLTRACE] Sync.Postfix senderId={senderId}(netId={receivedNetId}) {prevCards}C/{prevUpgraded}U -> {receivedCards}C/{receivedUpgraded}U delta={cardDelta}/{upgradeDelta} option={optionId ?? "?"} hasPrev={hasPrev} allowedD={allowedCardDelta}/{allowedUpgradeDelta}");
                DIAG($"[FULLTRACE]   SyncReceived deck: {GetDeckSummary(receivedPlayer)}");
                if (prevSerializableSnapshot != null)
                    DIAG($"[FULLTRACE]   prevSerializable deck: {GetDeckSummary(prevSerializableSnapshot)}");

                // 记录当前卡组大小，供 OnReceivePlayerChoice 使用（transform 前快照）
                lock (_lastSyncDeckSize) { _lastSyncDeckSize[senderId] = receivedCards; }

                // 当没有活跃的 choiceCall 时，当前快照就是「作弊前的干净快照」
                // 用于 PlayerChoice 即时回滚
                lock (_choiceCallCount)
                {
                    if ((!_choiceCallCount.ContainsKey(senderId) || _choiceCallCount[senderId] == 0)
                        && !incomingRollbackSuppressed)
                    {
                        lock (_preCheatSnapshot) { _preCheatSnapshot[senderId] = receivedPlayer; }
                        DIAG($"[FULLTRACE] Saved preCheatSnapshot: {receivedCards}C for senderId={senderId}");
                    }
                }

                // ── 检测作弊 ──（仅主机；客机执行会误改 _syncData / 发 SyncPlayerDataMessage → 黑屏）
                bool cheated = false;
                string cheatType = null;

                if (!NoClientCheatsMod.IsMultiplayerHost())
                    goto NccSyncDeckChecksDone_ClientSideApply;

                if (hasPrev)
                {
                    bool isRemoveEvent = optionId?.Contains("Remove", StringComparison.OrdinalIgnoreCase) == true ||
                                         optionId?.Contains("Scissors", StringComparison.OrdinalIgnoreCase) == true ||
                                         optionId?.Contains("Cut", StringComparison.OrdinalIgnoreCase) == true;
                    bool isUpgradeEvent = optionId?.Contains("Upgrade", StringComparison.OrdinalIgnoreCase) == true ||
                                          optionId?.Contains("Scent", StringComparison.OrdinalIgnoreCase) == true ||
                                          optionId?.Contains("Scented", StringComparison.OrdinalIgnoreCase) == true;
                    bool isGainCardEvent = optionId?.Contains("AddCard", StringComparison.OrdinalIgnoreCase) == true ||
                                          optionId?.Contains("Obtain", StringComparison.OrdinalIgnoreCase) == true ||
                                          optionId?.Contains("GainCard", StringComparison.OrdinalIgnoreCase) == true;

                    if (isRemoveEvent)
                    {
                        // 删除类：cardDelta 应 <= 0（负数），超过 allowed（-1）则作弊
                        if (cardDelta < allowedCardDelta)
                        {
                            cheated = true;
                            cheatType = $"remove_excess(deleted={-cardDelta} allowed={-allowedCardDelta})";
                        }
                    }
                    else if (isUpgradeEvent)
                    {
                        // 升级类：upgradeDelta 应 >= 0 且 <= allowedUpgradeDelta
                        if (upgradeDelta < 0)
                        {
                            cheated = true;
                            cheatType = $"upgrade_undo(count={-upgradeDelta})";
                        }
                        else if (upgradeDelta > allowedUpgradeDelta)
                        {
                            cheated = true;
                            cheatType = $"upgrade_excess(upgraded={upgradeDelta} allowed={allowedUpgradeDelta})";
                        }
                    }
                    else if (!isGainCardEvent)
                    {
                        // 非发牌类事件：卡数不应增加
                        if (cardDelta > allowedCardDelta)
                        {
                            cheated = true;
                            cheatType = $"add_cards(count={cardDelta} allowed={allowedCardDelta})";
                        }
                    }
                }

                // 记录当前卡组大小，供 OnReceivePlayerChoice 使用（transform 前快照）
                lock (_lastSyncDeckSize) { _lastSyncDeckSize[senderId] = receivedCards; }

                // ── Transform/Reward 作弊检测（基于 OnReceivePlayerChoice 快照）────────
                // OnReceivePlayerChoice 时记录了 deckCards（transform 后的卡组快照）。
                // 当 SyncReceived 到来时：
                //   - 若 optionId 不为空：已被 allowedDeltas 覆盖，不重复检测
                //   - 若 optionId 为空（选卡类事件）：用快照检测 transform/reward 是否超量
                //   - 若本轮 PlayerChoice 已触发即时回滚：跳过本次检测（防止重复检测）
                // senderId 与 SerializablePlayer.NetId 可能不一致，两处都要查
                bool skipCheatCheck = _HasRecentImmediateRollback(senderId, receivedNetId);
                if (skipCheatCheck)
                    DIAG($"[FULLTRACE] SyncReceived: skip cheating check (immediate rollback fresh) senderId={senderId} receivedNetId={receivedNetId}");

                if (skipCheatCheck)
                {
                    // 即时回滚已触发：跳过作弊检测；不要用副机尚未应用 wire 修正的脏包覆盖主机刚写回的快照
                    DIAG($"[FULLTRACE] SyncReceived: skipping all cheat checks (rollback done)");
                    return;
                }

                int preDeckSize = 0;
                int choiceCallCount = 0;
                lock (_pendingPreDeckSize)
                {
                    _pendingPreDeckSize.TryGetValue(senderId, out preDeckSize);
                    _choiceCallCount.TryGetValue(senderId, out choiceCallCount);
                }

                // 核心修复：当 choiceCallCount > 0 但 _pendingPreDeckSize 仍未更新时
                //（因为 PlayerChoice 里 canonicalCards=null 未设值），用上一次 sync 的卡数做兜底
                if (preDeckSize == 0 && choiceCallCount > 0)
                {
                    lock (_lastSyncDeckSize)
                        _lastSyncDeckSize.TryGetValue(senderId, out preDeckSize);
                    DIAG($"[FULLTRACE] TransformCheck FIXED: using _lastSyncDeckSize={preDeckSize} as preDeck (choiceCalls={choiceCallCount})");
                }

                // 日志增强：打印检测决策树
                int deckSizeDelta = receivedCards - preDeckSize;
                DIAG($"[FULLTRACE] === TransformCheck === player={realName} preDeck={preDeckSize} received={receivedCards} delta={deckSizeDelta} choiceCalls={choiceCallCount} optionId={optionId??"(null)"} hasPrev={hasPrev}");
                if (preDeckSize > 0 && string.IsNullOrEmpty(optionId))
                {
                    DIAG($"[FULLTRACE] TransformCheck: decision logic:");
                    DIAG($"  choiceCalls={choiceCallCount} delta={deckSizeDelta}");

                    // 作弊判断：
                    // - choiceCalls >= 2（副机选了 >= 2 张卡）
                    //   - delta != 0 → 变换数量与选择数量不符
                    //   - delta == 0 → 只变了 1 张，但选了 2+ 张（多选）
                    //   - delta == 1 → 只奖励了 1 张，但选了 2+ 张（多选）
                    //   - delta == -1 → 只删了 1 张，但选了 2+ 张（多选）
                    // - choiceCalls == 1（副机选了 1 张卡）
                    //   - delta > 1 → 多得了卡
                    //   - delta < -1 → 多删了卡
                    //   - delta in {-1, 0, 1} → 正常
                    if (choiceCallCount >= 2 && deckSizeDelta == 0)
                    {
                        // 选了 2+ 张但卡数不变：只执行了 1 次 transform，多选作弊
                        cheated = true;
                        cheatType = $"transform_multi_select(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && deckSizeDelta == 1)
                    {
                        // 选了 2+ 张但只增了 1 张
                        cheated = true;
                        cheatType = $"reward_multi_select(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && deckSizeDelta == -1)
                    {
                        // 选了 2+ 张但只删了 1 张
                        cheated = true;
                        cheatType = $"remove_multi_select(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && Math.Abs(deckSizeDelta) > 1)
                    {
                        // 选了 2+ 张但变化超出合理范围
                        cheated = true;
                        cheatType = $"multi_select_excess(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else if (choiceCallCount == 1 && deckSizeDelta > 1)
                    {
                        // 选了 1 张但多了 2+ 张
                        cheated = true;
                        cheatType = $"reward_excess(gained={deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else if (choiceCallCount == 1 && deckSizeDelta < -1)
                    {
                        // 选了 1 张但删了 2+ 张
                        cheated = true;
                        cheatType = $"remove_excess(deleted={-deckSizeDelta})";
                        DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                    }
                    else
                    {
                        DIAG($"[FULLTRACE] TransformCheck: LEGITIMATE (choiceCalls={choiceCallCount} delta={deckSizeDelta})");
                    }
                    // choiceCallCount >= 2 且 delta == choiceCallCount - 1（正确执行了多次 transform/reward）：正常
                    // choiceCallCount == 1 且 delta in {-1, 0, 1}：正常
                }

                // ── 关键新增检测：delta=0 但卡牌内容变了（transform/reward 替换了卡但数量不变）──
                // 场景：副机用 transform 选卡 2 张，换走 2 张获得 2 张新卡 → delta=0，卡组内容全变了
                if (!cheated && choiceCallCount >= 1 && deckSizeDelta == 0)
                {
                    // 对比 receivedDeck vs prevSerializableDeck 的具体卡牌
                    if (prevSerializableSnapshot != null)
                    {
                        var prevDeckCheck = GetSerializableDeck(prevSerializableSnapshot);
                        var recvDeckCheck = receivedDeck;
                        if (prevDeckCheck != null && recvDeckCheck != null)
                        {
                            // 逐卡比较：ID + upgradeLevel
                            var prevIds = new System.Collections.Generic.HashSet<string>();
                            foreach (var c in prevDeckCheck)
                                prevIds.Add(GetCardId(c) + "u" + GetCardUpgradeLevel(c));
                            var recvIds = new System.Collections.Generic.HashSet<string>();
                            foreach (var c in recvDeckCheck)
                                recvIds.Add(GetCardId(c) + "u" + GetCardUpgradeLevel(c));

                            if (!prevIds.SetEquals(recvIds))
                            {
                                // 卡组内容变了但数量不变：transform/reward 替换作弊
                                var changed = new System.Collections.Generic.List<string>();
                                foreach (var id in recvIds)
                                    if (!prevIds.Contains(id)) changed.Add(id);
                                cheated = true;
                                cheatType = $"transform_delta0(calls={choiceCallCount} changed={changed.Count} cards=[{string.Join(",", changed)}])";
                                DIAG($"[FULLTRACE] DETECTED! CHEAT: {cheatType}");
                            }
                        }
                    }
                }
                else if (preDeckSize > 0 && !string.IsNullOrEmpty(optionId))
                {
                    DIAG($"[FULLTRACE] TransformCheck: skipped (optionId={optionId} covered by ChooseOption path)");
                }

                // ── Transform/Change 检测（canonicalCards vs receivedDeck）───────────────────────
                // 核心洞察：
                //   - NetPlayerChoiceResult.canonicalCards = 副机作弊前的卡组（正确状态）
                //   - SyncReceived.receivedPlayer = 变换/作弊后的卡组（作弊状态）
                //   - 对比两者：若 received 多出了 canonical 中没有的卡 → 检测到作弊
                // 流程：
                //   1. OnReceivePlayerChoice Postfix: 存储 canonicalCards 的完整序列化对象
                //   2. SyncReceived Postfix: 从 receivedPlayer 提取卡 ID，与存储的 canonical 对比
                //   3. 若 received 多出了 canonical 中没有的卡 → 作弊
                if (!cheated)
                {
                    try
                    {
                        // 获取 canonicalSerializablePlayer（作弊引擎附带的作弊前卡组）
                        object canonicalSnap = null;
                        lock (_canonicalSerializablePlayer)
                            _canonicalSerializablePlayer.TryGetValue(senderId, out canonicalSnap);

                        if (canonicalSnap != null)
                        {
                            var canonicalDeck = GetSerializableDeck(canonicalSnap);
                            var recvDeckCmp = GetSerializableDeck(receivedPlayer);

                            // 构建 canonical 卡 ID 集合
                            var canonIds = new System.Collections.Generic.HashSet<string>();
                            foreach (object card in canonicalDeck)
                                canonIds.Add(GetCardId(card) + "u" + GetCardUpgradeLevel(card));

                            // 构建 received 卡 ID 集合
                            var recvIds = new System.Collections.Generic.HashSet<string>();
                            foreach (object card in recvDeckCmp)
                                recvIds.Add(GetCardId(card) + "u" + GetCardUpgradeLevel(card));

                            // 在 received 中找 canonical 没有的卡（即变换/作弊后的新卡）
                            var extraIds = new System.Collections.Generic.List<string>();
                            foreach (var rid in recvIds)
                                if (!canonIds.Contains(rid))
                                    extraIds.Add(rid);

                            // 在 canonical 中找 received 没有的卡（即被变换/移除的卡）
                            var missingIds = new System.Collections.Generic.List<string>();
                            foreach (var cid in canonIds)
                                if (!recvIds.Contains(cid))
                                    missingIds.Add(cid);

                            DIAG($"[CANONCHECK] {realName} canon={canonicalDeck.Count}C recv={recvDeckCmp.Count}C extra={extraIds.Count} missing={missingIds.Count} extraCards=[{string.Join(",", extraIds)}] missingCards=[{string.Join(",", missingIds)}]");
                            DIAG($"[CANONCHECK]   canonDeck: {GetDeckSummary(canonicalSnap)}");
                            DIAG($"[CANONCHECK]   recvDeck:  {GetDeckSummary(receivedPlayer)}");

                            // 作弊判断：
                            // - extra > 0 且 missing > 0 且张数相同 → 变换类作弊（既多卡又少卡）
                            // - extra > 0 且张数相同 → 多选 reward 作弊
                            // - missing > 0 且张数相同 → 多选 remove 作弊
                            if (extraIds.Count > 0 && missingIds.Count > 0 && canonicalDeck.Count == recvDeckCmp.Count)
                            {
                                cheated = true;
                                cheatType = $"transform_cheat(extra={extraIds.Count} missing={missingIds.Count} extraCards={string.Join(",", extraIds)} missingCards={string.Join(",", missingIds)})";
                                DIAG($"CHEAT from {realName} (transform): {cheatType}");
                            }
                            else if (extraIds.Count > 0 && canonicalDeck.Count == recvDeckCmp.Count)
                            {
                                cheated = true;
                                cheatType = $"reward_multi_cheat(extra={extraIds.Count} extraCards={string.Join(",", extraIds)})";
                                DIAG($"CHEAT from {realName} (reward multi): {cheatType}");
                            }
                            else if (missingIds.Count > 0 && canonicalDeck.Count == recvDeckCmp.Count)
                            {
                                cheated = true;
                                cheatType = $"remove_multi_cheat(missing={missingIds.Count} missingCards={string.Join(",", missingIds)})";
                                DIAG($"CHEAT from {realName} (remove multi): {cheatType}");
                            }
                            // extra == 0 && missing == 0：完全匹配，正常
                        }
                        else
                        {
                            DIAG($"[CANONCHECK] {realName}: no canonical snapshot, skipping");
                        }
                    }
                    catch (Exception ex) { DIAG($"[CANONCHECK] error: {ex.Message}"); }
                }

                // ── 客机：收到主机发来的回滚 SnapshotPlayerDataMessage 后，直接应用到本机 Player ──
                // 仅当发送者不是本机（= 主机发来的回滚包）且本机控制了一个远程玩家的 netId 时
                NccSyncDeckChecksDone_ClientSideApply:
                {
                    if (NoClientCheatsMod.IsMultiplayerHost())
                    {
                        // 主机：走原有逻辑（作弊检测或跳过）
                    }
                    else
                    {
                        // 客机：SyncPlayerDataMessage 可能是主机发来的回滚包
                        // 判断：这条消息里的 senderId 与本机控制的 netId 相同？
                        ulong localId = TryGetLocalControllingNetId();
                        DIAG($"[CLIENT-APPLY] ENTRY: localId={localId} senderId={senderId}");
                        if (localId != 0 && senderId == localId)
                        {
                            // 这是发给本机的回滚消息：用 receivedPlayer（来自消息内容）更新本机 Player
                            object live = _TryResolveLivePlayerByNetId(senderId);
                            DIAG($"[CLIENT-APPLY] resolved live={live != null} (senderId={senderId})");
                            if (live == null)
                                _lastRemotePlayerByNetId.TryGetValue(senderId, out live);
                            DIAG($"[CLIENT-APPLY] after cache live={live != null}");
                            if (live != null)
                            {
                                var applied = _TryApplySnapshotToLivePlayer(live, receivedPlayer);
                                DIAG($"[CLIENT-APPLY] senderId={senderId} receivedDeck={GetDeckSummary(receivedPlayer)} applied={applied ?? "null"}");
                            }
                            else
                            {
                                DIAG($"[CLIENT-APPLY] FAILED: live player still null for senderId={senderId}, queueing deferred");
                                lock (_pendingPlayerRefreshes)
                                    _pendingPlayerRefreshes[senderId] = (senderId, receivedPlayer, __instance);
                            }
                        }
                        else if (localId == 0 && senderId != 0)
                        {
                            // 地图阶段 RunManager.State=null，TryGetLocalControllingNetId() 返回 0
                            // 用 senderId 本身匹配本机缓存的 Player（_lastRemotePlayerByNetId 可能存过本机对象）
                            object live = _TryResolveLivePlayerByNetId(senderId);
                            DIAG($"[CLIENT-APPLY-FALLBACK] resolved live={live != null} (senderId={senderId})");
                            if (live == null)
                                _lastRemotePlayerByNetId.TryGetValue(senderId, out live);
                            DIAG($"[CLIENT-APPLY-FALLBACK] after cache live={live != null}");
                            string applied = null;
                            if (live != null)
                            {
                                applied = _TryApplySnapshotToLivePlayer(live, receivedPlayer);
                                DIAG($"[CLIENT-APPLY-FALLBACK] senderId={senderId} receivedDeck={GetDeckSummary(receivedPlayer)} applied={applied ?? "null"}");
                            }

                            // ── 关键修复：NCC 回滚特殊处理 ─────────────────────────────────
                            // receivedNetId 在 Finalizer patch 后已被修正为 senderId，
                            // 无法通过 receivedNetId != senderId 检测。
                            // 使用 Finalizer Prefix 中设置的 ThreadLocal 标志。
                            bool wasNCC = _wasNCCRollback?.Value == true;
                            ulong origMsgPlayerNetId = _originalMsgPlayerNetId?.Value ?? 0;
                            if (applied == null && wasNCC)
                            {
                                ulong origSenderId = _originalSenderId?.Value ?? 0;
                                DIAG($"[CLIENT-APPLY-FALLBACK] ★ NCC ROLLBACK detected via ThreadLocal!");
                                DIAG($"[CLIENT-APPLY-FALLBACK]   original: msgPlayer.NetId={origMsgPlayerNetId} senderId={origSenderId}");
                                DIAG($"[CLIENT-APPLY-FALLBACK]   Trying to resolve live player by origMsgPlayerNetId={origMsgPlayerNetId}...");

                                // 尝试通过原始 msgPlayer.NetId（客机自己的 NetId）解析本地 Player
                                object liveByReceivedId = _TryResolveLivePlayerByNetId(origMsgPlayerNetId);
                                DIAG($"[CLIENT-APPLY-FALLBACK]   resolved by origMsgPlayerNetId: {liveByReceivedId != null}");

                                // 如果找不到，尝试从缓存中找（通过 clientNetId 存的 SerializablePlayer）
                                if (liveByReceivedId == null)
                                {
                                    _lastRemotePlayerByNetId.TryGetValue(origMsgPlayerNetId, out liveByReceivedId);
                                    DIAG($"[CLIENT-APPLY-FALLBACK]   after cache[origMsgPlayerNetId]: {liveByReceivedId != null}");
                                }

                                if (liveByReceivedId != null)
                                {
                                    // 找到了可能是 SerializablePlayer（缓存中）或 Player 实体
                                    DIAG($"[CLIENT-APPLY-FALLBACK]   found object type={liveByReceivedId.GetType().Name}");

                                    // 直接反射调用 Player.SyncWithSerializedPlayer
                                    // 这是最可靠的同步方式
                                    bool syncOk = _TrySyncPlayerWithSerializable(liveByReceivedId, receivedPlayer);
                                    DIAG($"[CLIENT-APPLY-FALLBACK] ★ NCC SyncWithSerializable: {syncOk}");

                                    if (syncOk)
                                    {
                                        applied = "NCC.SyncWithSerializable";
                                    }
                                }
                                else
                                {
                                    DIAG($"[CLIENT-APPLY-FALLBACK]   still null — queuing deferred by clientNetId");
                                    // 关键：使用 origMsgPlayerNetId（客机 NetId）作为 key
                                    // 因为 ProcessDeferredPlayerRefresh 需要用它找到客机的 Player 实体
                                    lock (_pendingPlayerRefreshes)
                                        _pendingPlayerRefreshes[origMsgPlayerNetId] = (origMsgPlayerNetId, receivedPlayer, __instance);
                                }
                            }
                            else if (applied == null)
                            {
                                DIAG($"[CLIENT-APPLY-FALLBACK] FAILED: live player still null for senderId={senderId}, queueing deferred");
                                lock (_pendingPlayerRefreshes)
                                    _pendingPlayerRefreshes[senderId] = (senderId, receivedPlayer, __instance);
                            }
                        }
                        else
                        {
                            DIAG($"[CLIENT-APPLY] SKIPPED: localId={localId} senderId={senderId} (not our message)");
                        }
                        // 无论是否匹配，都写合法快照（客机正常同步时也需要推进 prev 状态）
                        lock (_lastSerializablePlayer) { _lastSerializablePlayer[senderId] = receivedPlayer; }
                    }
                }

                if (!cheated)
                {
                    DIAG($"Check passed for {realName}");
                    // 仅合法同步才推进「上一轮」Serializable 快照
                    lock (_lastSerializablePlayer) { _lastSerializablePlayer[senderId] = receivedPlayer; }
                    return;
                }

                // ── 作弊检测到！执行回滚 ──
                bool skipNotify = _HasRecentCheatNotify(senderId, receivedNetId);
                DIAG($"CHEAT from {realName}: {cheatType}");

                // 2) 使用 Postfix 开头抓取的 prevSerializableSnapshot（作弊前的合法 SerializablePlayer）
                if (prevSerializableSnapshot == null)
                {
                    DIAG($"[FULLTRACE] Rollback aborted: no prevSerializableSnapshot for {senderId} (first sync?)");
                    if (!skipNotify)
                        NoClientCheatsMod.RecordCheat(senderId, realName, optionId, $"deck:{cheatType}|rollback:aborted_no_prev", true);
                    return;
                }

                object syncDataCorrectSnapshot = TryCloneSerializableSnapshot(prevSerializableSnapshot)
                    ?? prevSerializableSnapshot;

                // 3) 回滚主机本地的 `_lastSyncState`：恢复到覆盖前保存的元组
                if (hadSavedPrevSyncState)
                {
                    lock (_lastSyncState)
                    {
                        _lastSyncState[senderId] = savedPrevSyncState;
                        DIAG($"[FULLTRACE] Rollback: _lastSyncState restored to ({savedPrevSyncState.cardCount}C/{savedPrevSyncState.upgradedCount}U)");
                    }
                }

                // 3b) 弃用：客机作弊检测仅走 PlayerChoiceReceivePatch，不在 SyncReceivedPatch 做本地回滚。
                //    理由：(1) senderId 是远程玩家 ID，用它找不到主机自己的 Player；(2) PlayerChoiceReceivePatch
                //    已经会发 _SendRollback 并触发网络同步，无需重复本地回滚。
                //    仅通过网络层 _ReplaceSyncData + _SendRollback 保持数据一致性。

                // 4) 回滚 CombatStateSynchronizer._syncData：替换为作弊前的快照
                //    效果：主机后续游戏逻辑（如战斗、遗物生效等）继续使用合法状态
                var syncDataField = AccessTools.Field(__instance.GetType(), "_syncData");
                var syncData = syncDataField?.GetValue(__instance) as IDictionary;
                if (syncData != null)
                {
                    if (syncData.Contains(senderId))
                        syncData[senderId] = syncDataCorrectSnapshot;
                    else
                        syncData.Add(senderId, syncDataCorrectSnapshot);
                    DIAG($"[FULLTRACE] Rollback: _syncData[{senderId}] replaced");
                }
                else
                {
                    DIAG($"[FULLTRACE] Rollback: _syncData=null, skipping");
                }

                // 5) 向副机发送纠正消息（带冷却，避免同场景连发两条把客机打成黑屏）
                bool wireSent = _TryConsumeWireRollbackSlot(senderId);
                if (wireSent)
                {
                    DIAG($"[FULLTRACE] Rollback: sending SyncPlayerDataMessage to {senderId}");
                    _SendRollback(__instance, senderId, syncDataCorrectSnapshot);
                }
                else
                    DIAG($"[FULLTRACE] Rollback: skip wire SyncPlayerDataMessage to {senderId} (cooldown)");

                _MarkRollbackSuppression(senderId, receivedNetId);

                // 弹窗放在回滚与发网之后，文案带上回滚摘要（与 PlayerChoice 路径一致）
                if (!skipNotify)
                {
                    string rbNote = wireSent ? "rollback:_syncData+wire" : "rollback:_syncData+wire_cooldown";
                    NoClientCheatsMod.RecordCheat(senderId, realName, optionId, $"deck:{cheatType}|{rbNote}", true);
                }
                else
                    DIAG($"[FULLTRACE] Skip duplicate RecordCheat (already notified at choice)");

                // 6) 重新记录合规快照：让后续 SyncReceived 的 ChooseOption 检测路径正常工作
                NoClientCheatsMod.SetExpectedDeckSnapshot(senderId, syncDataCorrectSnapshot);
                DIAG($"[FULLTRACE] Rollback: SetExpectedDeckSnapshot({senderId}) done");

                // 7) 字典与 OnReceivePlayerChoice 用的卡组大小与「上一轮」一致
                if (hadSavedPrevSyncState)
                    lock (_lastSyncDeckSize) { _lastSyncDeckSize[senderId] = savedPrevSyncState.cardCount; }
                lock (_lastSerializablePlayer) { _lastSerializablePlayer[senderId] = syncDataCorrectSnapshot; }
            }
            catch (Exception ex)
            {
                DIAG($"Postfix error: {ex.Message}");
                GD.PushError($"[NCC] SyncReceived Postfix error: {ex}");
            }
        }
    }

    // ─── Patch C: RunState.PushRoom ──────────────────────────────────────────
    // 时机：玩家进入新房间前（统一入口，替代不存在的 GoToMapNode）
    // 操作：清空所有快照，防止残留数据
    [HarmonyPatch]
    private static class PushRoomPrefix
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Runs.RunState"
            )?.GetMethod("PushRoom",
                BindingFlags.Public | BindingFlags.Instance);
        }

        static void Prefix()
        {
            try
            {
                // 不再清空所有快照。只清理超过 10 分钟的旧快照，防止内存泄漏。
                // 主检测已在 ChooseOption Postfix 完成，清空不会影响已检测的结果。
                NoClientCheatsMod.CleanupExpiredSnapshots(timeoutSeconds: 600);
            }
            catch (Exception ex)
            {
                GD.PushError($"[NCC] PushRoom Prefix error: {ex}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 核心对比与检测
    // ─────────────────────────────────────────────────────────────────────────

    private static bool _DecksMatch(object a, object b)
    {
        var deckA = GetSerializableDeck(a);
        var deckB = GetSerializableDeck(b);

        if (deckA.Count != deckB.Count) return false;

        // 逐张对比：ID + CurrentUpgradeLevel
        for (int i = 0; i < deckA.Count; i++)
        {
            if (!AreCardsEqual(deckA[i], deckB[i])) return false;
        }
        return true;
    }

    /// <summary>检测作弊类型：upgrade_excess(N) / remove_excess(N) / add_excess(N) / card_mismatch。</summary>
    private static string _DetectCheatType(object expected, object actual)
    {
        var deckE = GetSerializableDeck(expected);
        var deckA = GetSerializableDeck(actual);

        int upgE = CountUpgraded(deckE);
        int upgA = CountUpgraded(deckA);

        if (upgA > upgE)
            return $"upgrade_excess({upgA - upgE})";
        if (deckA.Count < deckE.Count)
            return $"remove_excess({deckE.Count - deckA.Count})";
        if (deckA.Count > deckE.Count)
            return $"add_excess({deckA.Count - deckE.Count})";
        return "card_mismatch";
    }

    private static int CountUpgraded(IList deck)
    {
        int count = 0;
        foreach (var card in deck)
            if (GetCardUpgradeLevel(card) > 0) count++;
        return count;
    }

    /// <summary>
    /// 填充同步消息中的玩家快照（不同版本字段名可能不同）。
    /// 同时确保 SerializablePlayer.NetId 与消息的发送方 senderId 一致，
    /// 避免客机 WaitForSync 把副机快照错误应用到本机 Player → NetId 不匹配强退。
    /// </summary>
    private static void _PopulateSyncPlayerDataMessage(object msg, object correctSnapshot)
    {
        if (msg == null || correctSnapshot == null) return;
        foreach (var name in new[] { "player", "Player", "SerializablePlayer", "PlayerData", "Data" })
            _SetMemberAny(msg, name, correctSnapshot);

        // 修正 SerializablePlayer.NetId 与发送方 senderId 对齐（防止串线强退）
        // _SendRollback 会额外传 senderId 参数给本方法
    }

    /// <summary>
    /// 同上，但额外传入 senderId 以修正 NetId。
    /// </summary>
    private static void _PopulateSyncPlayerDataMessage(object msg, object correctSnapshot, ulong senderId)
    {
        if (msg == null || correctSnapshot == null) return;
        foreach (var name in new[] { "player", "Player", "SerializablePlayer", "PlayerData", "Data" })
            _SetMemberAny(msg, name, correctSnapshot);

        if (senderId != 0)
        {
            foreach (var name in new[] { "NetId", "OwnerNetId", "OwnerId", "PlayerNetId", "net_id", "playerNetId" })
            {
                _SetMemberAny(correctSnapshot, name, senderId);
                _SetMemberAny(msg, name, senderId);
            }
        }
    }

    private static void _SetMemberAny(object target, string memberName, object value)
    {
        if (target == null) return;
        var t = target.GetType();
        var prop = t.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            try { prop.SetValue(target, value); return; } catch { /* try field */ }
        }
        var field = t.GetField(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            try { field.SetValue(target, value); return; }
            catch (Exception ex)
            {
                // readonly 字段：FieldAccessException 被吞，添加一次有记录的尝试
                GD.PushError($"[NCC] _SetMemberAny: field '{memberName}' on {t.Name} is readonly or inaccessible: {ex.GetType().Name} — try RuntimeHelpers");
                try
                {
                    // 强制写入 readonly 字段（C# readonly 可通过反射覆写）
                    var fi = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fi != null)
                    {
                        fi.SetValue(target, value);
                    }
                }
                catch { /* truly immutable */ }
            }
        }
    }

    /// <summary>
    /// 调用 INetGameService 上能把「已构造消息」发给指定 peer 的方法。
    /// </summary>
    private static bool _TrySendMessageToPeer(object netService, object msg, ulong targetNetId)
    {
        if (netService == null || msg == null) return false;
        var msgType = msg.GetType();
        var nsType = netService.GetType();

        foreach (var methodName in new[] { "SendMessage", "SendMessageReliable", "SendToPeer", "SendToClient", "QueueMessage" })
        {
            foreach (var m in nsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name != methodName) continue;
                if (m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length != 2) continue;
                if (!ps[0].ParameterType.IsInstanceOfType(msg)) continue;

                var p1 = ps[1].ParameterType;
                try
                {
                    if (p1 == typeof(ulong))
                    {
                        m.Invoke(netService, new object[] { msg, targetNetId });
                        return true;
                    }
                    if (p1 == typeof(long))
                    {
                        m.Invoke(netService, new object[] { msg, (long)targetNetId });
                        return true;
                    }
                    if (p1 == typeof(int) && targetNetId <= int.MaxValue)
                    {
                        m.Invoke(netService, new object[] { msg, (int)targetNetId });
                        return true;
                    }
                }
                catch { /* try next */ }
            }
        }

        foreach (var m in nsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!m.IsGenericMethodDefinition || m.Name != "SendMessage") continue;
            var ps = m.GetParameters();
            if (ps.Length != 2 || ps[1].ParameterType != typeof(ulong)) continue;
            if (m.GetGenericArguments().Length != 1) continue;
            try
            {
                var concrete = m.MakeGenericMethod(msgType);
                concrete.Invoke(netService, new object[] { msg, targetNetId });
                return true;
            }
            catch { /* try next */ }
        }

        return false;
    }

    /// <summary>
    /// 向所有客户端广播回滚消息。
    /// 通过 INetGameService.BroadcastMessage / CombatStateSynchronizer.BroadcastSyncMessage
    /// 让游戏用正确的 senderId（主机 NetId）处理，避免「定向 peer 消息导致客机 WaitForSync 拿错快照」强退。
    /// </summary>
    private static bool _TryBroadcastSyncPlayerDataMessage(object synchronizer, object msg)
    {
        if (synchronizer == null || msg == null) return false;
        var syncType = synchronizer.GetType();

        // 方式 A: CombatStateSynchronizer.BroadcastSyncMessage(SyncPlayerDataMessage)
        foreach (var m in syncType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (m.IsGenericMethodDefinition) continue;
            if (!m.Name.Contains("Broadcast") && !m.Name.Contains("broadcast")) continue;
            var ps = m.GetParameters();
            if (ps.Length != 1) continue;
            if (!ps[0].ParameterType.IsInstanceOfType(msg)) continue;
            try { m.Invoke(synchronizer, new[] { msg }); return true; } catch { }
        }

        // 方式 B: INetService.BroadcastMessage(msg)
        var netServiceField = AccessTools.Field(syncType, "_netService");
        var netService = netServiceField?.GetValue(synchronizer);
        if (netService != null)
        {
            var nsType = netService.GetType();
            foreach (var m in nsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.IsGenericMethodDefinition) continue;
                if (!m.Name.Contains("Broadcast") && !m.Name.Contains("broadcast")) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                if (!ps[0].ParameterType.IsInstanceOfType(msg)) continue;
                try { m.Invoke(netService, new[] { msg }); return true; } catch { }
            }
        }
        return false;
    }

    /// <summary>
    /// 向所有客户端广播回滚消息（通过无 peerId 参数的 SendMessage）。
    /// INetGameService.SendMessage&lt;T&gt;(T message) 会将 senderId 设为 主机 NetId，
    /// 广播给所有客户端，客机 WaitForSync 用 senderId=主机NetId 查找到自己的 Player，
    /// 用本机序列化消息里的 SerializablePlayer（=作弊玩家回滚后的卡组）正常同步。
    /// </summary>
    private static bool _TryBroadcastViaNoPeerSendMessage(object netService, object msg)
    {
        if (netService == null || msg == null) return false;
        var msgType = msg.GetType();
        var nsType = netService.GetType();

        // 搜索无 peerId 参数的泛型 SendMessage<T>(T message)
        foreach (var m in nsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!m.IsGenericMethodDefinition || m.Name != "SendMessage") continue;
            var ps = m.GetParameters();
            if (ps.Length != 1) continue;
            if (!ps[0].ParameterType.IsInstanceOfType(msg)) continue;
            if (m.GetGenericArguments().Length != 1) continue;
            try
            {
                var concrete = m.MakeGenericMethod(msgType);
                concrete.Invoke(netService, new[] { msg });
                return true;
            }
            catch { /* try next */ }
        }
        return false;
    }

    /// <summary>
    /// 向副机发送回滚消息。
    /// 方向C核心：
    ///   1. 在发送前注册 NetId 修正（NetIdFixTranspiler.RegisterFix）
    ///      Transpiler 在序列化时会将 msg.player.NetId 从 targetNetId 修正为 senderId
    ///   2. 发送后清除修正（NetIdFixTranspiler.ClearFix）
    /// </summary>
    private static void _SendRollback(object synchronizer, ulong targetNetId, object correctSnapshot)
    {
        object msgPlayer = null;

        try
        {
            var netServiceField = AccessTools.Field(synchronizer.GetType(), "_netService");
            var netService = netServiceField?.GetValue(synchronizer);
            if (netService == null) { GD.PushError("[NCC] _SendRollback: _netService is null"); return; }

            // 构造 SyncPlayerDataMessage
            var msgType = AccessTools.TypeByName(
                    "MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.SyncPlayerDataMessage")
                ?? AccessTools.TypeByName("SyncPlayerDataMessage");
            if (msgType == null) { GD.PushError("[NCC] _SendRollback: msgType null"); return; }

            var msg = Activator.CreateInstance(msgType);
            _PopulateSyncPlayerDataMessage(msg, correctSnapshot, targetNetId);

            // 获取 msg.player 引用
            var msgPlayerField = AccessTools.Field(msgType, "player");
            msgPlayer = msgPlayerField?.GetValue(msg);

            // 获取主机 NetId（这是正确的 senderId = 主机 NetId）
            ulong hostNetId = 0;
            try
            {
                var hostNetIdField = AccessTools.Field(netService.GetType(), "_netHost");
                if (hostNetIdField != null)
                {
                    var netHost = hostNetIdField.GetValue(netService);
                    var netIdGetter = netHost?.GetType().GetProperty("NetId",
                        BindingFlags.Public | BindingFlags.Instance);
                    hostNetId = Convert.ToUInt64(netIdGetter?.GetValue(netHost) ?? 0UL);
                }
            }
            catch { /* ignore */ }

            // ── 方向C核心：在发送前注册修正 ─────────────────────────────────
            // SerializablePlayerNetIdMismatchPatch 在收到此回滚消息时，
            // 检测到 (msgPlayerNetId=targetNetId, senderId=hostNetId) 已注册，跳过 NetId 检查
            // ─────────────────────────────────────────────────────────────
            NetIdFixTranspiler.RegisterFix(targetNetId, hostNetId);

            // ── 新增：通知客机注册 NCC 回滚标记 ───────────────────────────
            // 在 finally 中不立即清除，而是延迟清除（给客机足够的时间收到并处理消息）
            // 通过 ClientDiagnosticPatches.ClientSideRegisterNCCRollback(hostNetId, targetNetId)
            // 让客机在收到消息后自己注册标记
            // ─────────────────────────────────────────────────────────────

            // 方式 A：广播（无 peerId 参数）→ senderId = 主机 NetId
            if (_TryBroadcastViaNoPeerSendMessage(netService, msg))
            {
                GD.Print($"[NCC] Rollback BROADCAST with NetId fix (senderId={hostNetId})");
                return;
            }

            // 方式 B：定向 → senderId = 主机 NetId
            if (_TrySendMessageToPeer(netService, msg, targetNetId))
            {
                GD.Print($"[NCC] Rollback sent with NetId fix to {targetNetId} (senderId={hostNetId})");
                return;
            }

            GD.PushError("[NCC] _SendRollback: no matching SendMessage on net service");
        }
        catch (Exception ex)
        {
            GD.PushError($"[NCC] _SendRollback error: {ex}");
        }
        finally
        {
            // 发送后清除修正
            NetIdFixTranspiler.ClearFix(targetNetId);
        }
    }

    /// <summary>
    /// 【修复Bug】
    /// ChooseOptionPostfix 检测到作弊并本地回滚后，向客机发送网络修正消息。
    /// 修复前：只做本地回滚，客机不知道状态被修正，继续用作弊后状态 → Checksum不匹配 → 黑屏/强退
    /// 修复后：本地回滚 + 向客机发送 SyncPlayerDataMessage → 客机状态同步 → 正常继续
    /// </summary>
    private static void _SendRollbackForImmediateCheat(ulong playerNetId, object correctSnapshot)
    {
        try
        {
            // 1. 获取 CombatStateSynchronizer 实例
            var synchronizer = SyncReceivedPatch.GetCachedSynchronizer();
            if (synchronizer == null)
            {
                LogDiag("Rollback", $"[IMMRB-CHOOSE] synchronizer=null, cannot send rollback to {playerNetId}");
                return;
            }

            // 2. 更新主机 _syncData（与 SyncReceivedPatch 回滚逻辑保持一致）
            var syncDataField = AccessTools.Field(synchronizer.GetType(), "_syncData");
            var syncData = syncDataField?.GetValue(synchronizer) as IDictionary;
            if (syncData != null)
            {
                if (syncData.Contains(playerNetId))
                    syncData[playerNetId] = correctSnapshot;
                else
                    syncData.Add(playerNetId, correctSnapshot);
                LogDiag("Rollback", $"[IMMRB-CHOOSE] _syncData[{playerNetId}] updated locally");
            }

            // 3. 带冷却向客机发送修正消息
            if (_TryConsumeWireRollbackSlot(playerNetId))
            {
                LogDiag("Rollback", $"[IMMRB-CHOOSE] sending SyncPlayerDataMessage to {playerNetId}");
                _SendRollback(synchronizer, playerNetId, correctSnapshot);
            }
            else
            {
                LogDiag("Rollback", $"[IMMRB-CHOOSE] wire cooldown active, skip sending to {playerNetId}");
            }

            // 4. 更新快照字典，防止后续 SyncReceived 继续误判
            NoClientCheatsMod.SetExpectedDeckSnapshot(playerNetId, correctSnapshot);
            lock (_lastSerializablePlayer) { _lastSerializablePlayer[playerNetId] = correctSnapshot; }

            // 5. 记录回滚标记（双写 NetId，避免 SyncReceived 用另一套 id 漏匹配）
            ulong snapNet = GetPlayerNetId(correctSnapshot);
            _MarkRollbackSuppression(playerNetId, snapNet != 0 ? snapNet : playerNetId);

            LogDiag("Rollback", $"[IMMRB-CHOOSE] _SendRollbackForImmediateCheat({playerNetId}) done");
        }
        catch (Exception ex)
        {
            LogDiag("Rollback", $"[IMMRB-CHOOSE] error: {ex.Message}");
            GD.PushError($"[NCC] _SendRollbackForImmediateCheat error: {ex}");
        }
    }

    /// <summary>
    /// 将 CombatStateSynchronizer._syncData[senderId] 替换为正确快照。
    /// 确保主机端后续处理也使用正确状态。
    /// </summary>
    private static void _ReplaceSyncData(object synchronizer, ulong senderId, object correctSnapshot)
    {
        try
        {
            var syncDataField = AccessTools.Field(synchronizer.GetType(), "_syncData");
            var syncData = syncDataField?.GetValue(synchronizer) as IDictionary;
            if (syncData == null) return;

            if (syncData.Contains(senderId))
                syncData[senderId] = correctSnapshot;
            else
                syncData.Add(senderId, correctSnapshot);
        }
        catch (Exception ex) { GD.PushError($"[NCC] _ReplaceSyncData error: {ex}"); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 反射辅助
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 通用属性/字段赋值。
    /// 依次尝试 Property (SetValue) 和 Field (SetValue)，优先属性。
    /// </summary>
    private static void SetMemberValue(object target, string memberName, object value)
    {
        var prop = target.GetType().GetProperty(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) { prop.SetValue(target, value); return; }
        var field = target.GetType().GetField(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field != null) field.SetValue(target, value);
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null) return null;
        var t = target.GetType();
        // 1. Public property
        var prop = t.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(target);
        // 2. Public field
        var field = t.GetField(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field != null) return field.GetValue(target);
        // 3. Private field (including <Name>k__BackingField, _name)
        var allFields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var f in allFields)
        {
            if (f.Name == memberName || f.Name == "_" + memberName || f.Name == "<" + memberName + ">k__BackingField")
                return f.GetValue(target);
        }
        return null;
    }

    private static ulong GetPlayerNetId(object player)
    {
        if (player == null) return 0;
        foreach (var name in new[] { "NetId", "netId", "_netId", "NetworkId", "PlayerNetId", "OwnerNetId" })
        {
            var val = GetMemberValue(player, name);
            if (val == null) continue;
            try
            {
                var u = Convert.ToUInt64(val);
                if (u != 0) return u;
            }
            catch { /* try next */ }
        }
        return 0;
    }

    private static bool _IsLocalPlayer(object player)
    {
        try
        {
            var val = GetMemberValue(player, "IsLocal");
            return val is bool b && b;
        }
        catch { return false; }
    }

    private static bool _IsRemotePlayer(object player)
    {
        return !_IsLocalPlayer(player);
    }

    /// <summary>本机所控制玩家的 NetId（Players 里 IsLocal==true 的那位）。</summary>
    private static ulong TryGetLocalControllingNetId()
    {
        try
        {
            var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) return 0;

            object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            if (state == null) return 0;

            object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
            playersObj ??= GetMemberValue(state, "_players");
            if (playersObj is not IEnumerable players) return 0;

            foreach (var p in players)
            {
                if (p == null) continue;
                if (!_IsLocalPlayer(p)) continue;
                ulong id = GetPlayerNetId(p);
                if (id != 0) return id;
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// 是否为他机玩家。STS2 主机上 <c>Player.IsLocal</c> 可能对所有人误为 true，必须用 NetId 与本地控制位对比。
    /// </summary>
    private static bool IsNetRemotePlayer(object player)
    {
        if (player == null) return false;
        ulong pid = GetPlayerNetId(player);
        ulong localId = TryGetLocalControllingNetId();
        if (localId != 0 && pid != 0)
            return pid != localId;
        return _IsRemotePlayer(player);
    }

    private static bool IsNetLocalPlayer(object player)
    {
        if (player == null) return false;
        ulong pid = GetPlayerNetId(player);
        ulong localId = TryGetLocalControllingNetId();
        if (localId != 0 && pid != 0)
            return pid == localId;
        return _IsLocalPlayer(player);
    }

    private static string GetPlayerDisplayName(object player)
    {
        try
        {
            var ch = GetMemberValue(player, "Character");
            if (ch == null) return null;
            var id = GetMemberValue(ch, "Id");
            return id?.ToString();
        }
        catch { return null; }
    }

    private static object GetSyncMessagePlayer(object syncMessage)
    {
        if (syncMessage == null) return null;
        var result = GetMemberValue(syncMessage, "player");
        if (result == null)
            LogDiag("Finalizer", $"[PREFIX] GetSyncMessagePlayer returned NULL for {syncMessage.GetType().Name}");
        else
            LogDiag("Finalizer", $"[PREFIX] GetSyncMessagePlayer: player={result.GetType().Name} NetId={GetPlayerNetId(result)}");
        return result;
    }

    /// <summary>
    /// 从 canonicalCards 和 mutableCards 构建一个 SerializablePlayer 对象，
    /// 供 SyncReceived 对比作弊前后的卡组差异。
    /// </summary>
    private static object BuildSerializablePlayerFromCanonical(object templatePlayer, IList canonicalCards, IList mutableCards)
    {
        try
        {
            if (canonicalCards == null) return null;

            // 找到 SerializablePlayer 的运行时类型（不依赖 templatePlayer）
            var serialTypeName = "MegaCrit.Sts2.Core.Serialization.SerializablePlayer";
            var serialType = AccessTools.TypeByName(serialTypeName);
            if (serialType == null) return null;

            // 创建实例
            object serialPlayer = AccessTools.CreateInstance(serialType);
            if (serialPlayer == null) return null;

            // 设置 Deck 字段为 canonicalCards
            var deckField = serialType.GetField("_deckCards",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (deckField != null)
            {
                var deckList = System.Activator.CreateInstance(
                    typeof(System.Collections.Generic.List<>).MakeGenericType(canonicalCards.GetType().GenericTypeArguments[0])
                ) as IList;
                if (deckList != null)
                {
                    foreach (object card in canonicalCards)
                        deckList.Add(card);
                    deckField.SetValue(serialPlayer, deckList);
                }
            }

            // 设置 MutableCards
            var mutableField = serialType.GetField("mutableCards",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mutableField != null && mutableCards != null)
            {
                var mutList = System.Activator.CreateInstance(
                    typeof(System.Collections.Generic.List<>).MakeGenericType(mutableCards.GetType().GenericTypeArguments[0])
                ) as IList;
                if (mutList != null)
                {
                    foreach (object card in mutableCards)
                        mutList.Add(card);
                    mutableField.SetValue(serialPlayer, mutList);
                }
            }

            return serialPlayer;
        }
        catch
        {
            return null;
        }
    }

    private static IList GetSerializableDeck(object serializablePlayer)
    {
        var deck = GetMemberValue(serializablePlayer, "Deck");
        return deck as IList ?? Array.Empty<object>();
    }

    private static int GetCardUpgradeLevel(object serializableCard)
    {
        var val = GetMemberValue(serializableCard, "CurrentUpgradeLevel");
        return val != null ? Convert.ToInt32(val) : 0;
    }

    private static string GetCardId(object serializableCard)
    {
        var val = GetMemberValue(serializableCard, "Id");
        return val?.ToString() ?? "";
    }

    private static bool AreCardsEqual(object a, object b)
        => GetCardId(a) == GetCardId(b)
        && GetCardUpgradeLevel(a) == GetCardUpgradeLevel(b);

    private static string GetDeckSummary(object serializablePlayer)
    {
        try
        {
            var deck = GetSerializableDeck(serializablePlayer);
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < deck.Count; i++)
            {
                if (i > 0) names.Append(", ");
                var card = deck[i];
                var id = GetCardId(card);
                var upg = GetCardUpgradeLevel(card);
                names.Append(upg > 0 ? $"{id}+{upg}" : id);
            }
            return $"[{deck.Count} cards, {CountUpgraded(deck)} upgraded] ({names})";
        }
        catch { return "unknown"; }
    }

    /// <summary>返回详细卡组对比字符串，用于日志定位具体变化。</summary>
    private static string GetDeckDiff(object expected, object actual)
    {
        try
        {
            var deckE = GetSerializableDeck(expected);
            var deckA = GetSerializableDeck(actual);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  expected({deckE.Count} cards):");
            for (int i = 0; i < deckE.Count; i++)
            {
                var c = deckE[i];
                sb.Append($"    [{i}] {GetCardId(c)}");
                var u = GetCardUpgradeLevel(c);
                if (u > 0) sb.Append($"+{u}");
                sb.AppendLine();
            }
            sb.AppendLine($"  actual({deckA.Count} cards):");
            for (int i = 0; i < deckA.Count; i++)
            {
                var c = deckA[i];
                sb.Append($"    [{i}] {GetCardId(c)}");
                var u = GetCardUpgradeLevel(c);
                if (u > 0) sb.Append($"+{u}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch { return "diff unavailable"; }
    }

    private static string GetPlayerNameFromSyncService(object synchronizer, ulong senderId)
    {
        try
        {
            var netServiceField = AccessTools.Field(synchronizer.GetType(), "_netService");
            var netService = netServiceField?.GetValue(synchronizer);
            if (netService == null) return null;

            var platform = GetMemberValue(netService, "Platform");
            if (platform == null) return null;

            var platformUtil = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Platform.PlatformUtil");
            if (platformUtil == null) return null;

            var getPlayerName = platformUtil.GetMethod("GetPlayerName",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { platform.GetType(), typeof(ulong) }, null);
            if (getPlayerName == null) return null;

            var name = getPlayerName.Invoke(null, new object[] { platform, senderId }) as string;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch { return null; }
    }
    // ─────────────────────────────────────────────────────────────────────────
    // 诊断日志（调试用，验证时启用）
    // ─────────────────────────────────────────────────────────────────────────
    private static void LogDiag(string source, string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        // TargetMethod/Prepare 在模组加载线程运行，GD.Print 会触发右下角「不可在加载线程运行」
        NoClientCheatsMod.ThreadSafeLog($"[NCC|DIAG|{ts}] [{source}] {msg}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 立即回滚：将玩家卡组恢复到 preSnapshot 状态
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 从 RunManager.State.Players 中按 NetId 查找「活」的 Player 实例（主机侧）。
    /// </summary>
    private static object _TryResolveLivePlayerByNetId(ulong netId)
    {
        // 诊断：逐步排查哪个环节失败
        try
        {
            var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            if (rmType == null) { LogDiag("ResolvePlayer", $"RunManager type not found"); return null; }

            var inst = rmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) { LogDiag("ResolvePlayer", $"RunManager.Instance is null"); return null; }

            object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            if (state == null) { LogDiag("ResolvePlayer", $"state is null (tried State/CurrentRun/RunState)"); return null; }

            object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
            playersObj ??= GetMemberValue(state, "_players");
            if (playersObj == null) { LogDiag("ResolvePlayer", $"Players is null"); return null; }

            if (playersObj is not IEnumerable players)
            {
                LogDiag("ResolvePlayer", $"Players is not IEnumerable: {playersObj.GetType().Name}");
                return null;
            }

            foreach (var p in players)
            {
                if (p == null) continue;
                ulong foundNetId = GetPlayerNetId(p);
                if (foundNetId == netId)
                {
                    LogDiag("ResolvePlayer", $"found player netId={foundNetId} type={p.GetType().Name}");
                    return p;
                }
            }
            LogDiag("ResolvePlayer", $"no player matched netId={netId}, enumerating all:");
            foreach (var p in players)
            {
                if (p == null) { LogDiag("ResolvePlayer", $"  [null]"); continue; }
                LogDiag("ResolvePlayer", $"  netId={GetPlayerNetId(p)} type={p.GetType().Name}");
            }
        }
        catch (Exception ex) { LogDiag("ResolvePlayer", $"exception: {ex.Message}"); }
        return null;
    }

    /// <summary>回滚后抑制短时间内重复作弊判定（应对同帧多条 SyncReceived）。</summary>
    private static void _MarkRollbackSuppression(ulong idA, ulong idB)
    {
        var t = DateTime.Now.Ticks;
        lock (_immediateRollbackDone)
        {
            if (idA != 0) _immediateRollbackDone[idA] = t;
            if (idB != 0 && idB != idA) _immediateRollbackDone[idB] = t;
        }
    }

    /// <summary>同一 peer 的 SyncPlayerDataMessage 回滚发送冷却，避免连续两条把客机打成黑屏。</summary>
    private static bool _TryConsumeWireRollbackSlot(ulong peerId, int cooldownMs = 3000)
    {
        lock (_wireRollbackLock)
        {
            long now = DateTime.Now.Ticks;
            if (_wireRollbackCooldown.TryGetValue(peerId, out var prev)
                && now - prev < TimeSpan.FromMilliseconds(cooldownMs).Ticks)
                return false;
            _wireRollbackCooldown[peerId] = now;
            return true;
        }
    }

    /// <summary>是否刚做过 PlayerChoice / 即时回滚（同时匹配 senderId 与 SerializablePlayer 上的 NetId）。窗口拉长避免进战斗后二次检测。</summary>
    private static bool _HasRecentImmediateRollback(ulong idA, ulong idB)
    {
        lock (_immediateRollbackDone)
        {
            bool Fresh(ulong id)
            {
                if (id == 0) return false;
                if (!_immediateRollbackDone.TryGetValue(id, out var t)) return false;
                if (DateTime.Now.Ticks - t > TimeSpan.FromSeconds(120).Ticks)
                {
                    _immediateRollbackDone.Remove(id);
                    return false;
                }
                return true;
            }
            return Fresh(idA) || Fresh(idB);
        }
    }

    /// <summary>是否已在 PlayerChoice 弹过作弊通知（不移除条目，避免多条 SyncReceived 重复弹窗）。与回滚抑制同量级窗口。</summary>
    private static bool _HasRecentCheatNotify(ulong idA, ulong idB)
    {
        lock (_immediateCheatNotifyTicks)
        {
            bool Fresh(ulong id)
            {
                if (id == 0) return false;
                if (!_immediateCheatNotifyTicks.TryGetValue(id, out var t)) return false;
                return DateTime.Now.Ticks - t <= TimeSpan.FromSeconds(120).Ticks;
            }
            return Fresh(idA) || Fresh(idB);
        }
    }

    /// <summary>PlayerChoice 弹窗后写入时间戳；同时写入 SerializablePlayer.NetId 别名，避免 SyncReceived 用另一套 id 重复弹窗。</summary>
    private static void _RecordImmediateCheatNotifyTick(ulong senderId)
    {
        object preSnap = null;
        lock (_preCheatSnapshot) _preCheatSnapshot.TryGetValue(senderId, out preSnap);
        ulong nid = GetPlayerNetId(preSnap);
        long tick = DateTime.Now.Ticks;
        lock (_immediateCheatNotifyTicks)
        {
            _immediateCheatNotifyTicks[senderId] = tick;
            if (nid != 0 && nid != senderId)
                _immediateCheatNotifyTicks[nid] = tick;
        }
    }

    /// <summary>PlayerChoice 已处理 transform/reward 作弊后：清零 choice 计数并清 canonical，避免进战斗后 SyncReceived 二次判作弊。</summary>
    private static void _ResetTransformTrackingAfterPlayerChoiceCheat(ulong senderId, int rolledBackDeckSize)
    {
        lock (_choiceCallCount) { _choiceCallCount[senderId] = 0; }
        lock (_pendingPreDeckSize) { _pendingPreDeckSize[senderId] = rolledBackDeckSize; }
        object preSnap = null;
        lock (_preCheatSnapshot) _preCheatSnapshot.TryGetValue(senderId, out preSnap);
        ulong snapNet = GetPlayerNetId(preSnap);
        lock (_canonicalSerializablePlayer)
        {
            _canonicalSerializablePlayer.Remove(senderId);
            if (snapNet != 0 && snapNet != senderId)
                _canonicalSerializablePlayer.Remove(snapNet);
        }
    }

    /// <summary>将 SerializableCard 转换为 CardModel（外层供回滚路径共用）。</summary>
    private static object TryConvertToCardModel(object serializableCard)
    {
        try
        {
            if (serializableCard == null) return null;
            var cardModelType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.CardModel");
            var serialType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.Runs.SerializableCard")
                ?? serializableCard.GetType();
            var fromSerializable = cardModelType?.GetMethod("FromSerializable",
                BindingFlags.Public | BindingFlags.Static, null, new[] { serialType }, null);
            if (fromSerializable != null)
                return fromSerializable.Invoke(null, new[] { serializableCard });
        }
        catch { }
        return null;
    }

    /// <summary>将 SerializablePlayer 快照应用到运行时 Player；成功返回非 null 后缀（供 UI/日志）。</summary>
    private static string _TryApplySnapshotToLivePlayer(object p, object snapshot)
    {
        if (p == null || snapshot == null) return null;
        try
        {
            foreach (var mName in new[] {
                "RestoreFromSerializable", "RestoreStateFromSerializable", "ApplySerializable",
                "SyncFromSerializable", "DeserializeTo", "LoadFromSerializable", "ApplyFromSerializable"
            })
            {
                foreach (var m in p.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name != mName) continue;
                    var psp = m.GetParameters();
                    if (psp.Length != 1) continue;
                    if (!psp[0].ParameterType.IsInstanceOfType(snapshot)) continue;
                    try
                    {
                        m.Invoke(p, new[] { snapshot });
                        return "+Player." + mName;
                    }
                    catch { /* try next overload */ }
                }
            }

            // SerializablePlayer 等网络 DTO 上的 Deck 存的是 Serializable/Net 卡引用，不能 Clear 后塞 CardModel，否则常导致全 null → 空卡组（副机黑屏/清零）
            if (string.Equals(p.GetType().Name, "SerializablePlayer", StringComparison.Ordinal))
                return null;

            var deckProp = p.GetType().GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
            if (deckProp != null)
            {
                var deck = deckProp.GetValue(p) as IList;
                if (deck != null)
                {
                    var preDeck = GetSerializableDeck(snapshot);
                    if (preDeck != null && preDeck.Count > 0)
                    {
                        var addMethod = deck.GetType().GetMethod("Add");
                        var toAdd = new System.Collections.Generic.List<object>();
                        foreach (var card in preDeck)
                        {
                            var cardModel = TryConvertToCardModel(card);
                            if (cardModel != null)
                                toAdd.Add(cardModel);
                        }
                        if (toAdd.Count == 0)
                            return null;
                        deck.GetType().GetMethod("Clear")?.Invoke(deck, null);
                        foreach (var cardModel in toAdd)
                            addMethod?.Invoke(deck, new[] { cardModel });
                        return "+Deck.clear+add";
                    }
                }
            }
        }
        catch { /* fall through */ }
        return null;
    }

    /// <summary>
    /// 尝试对 Player 实体调用 SyncWithSerializedPlayer。
    /// 如果传入的是 SerializablePlayer 自身，会返回 false（需要在 Player 对象上调用）。
    /// </summary>
    private static bool _TrySyncPlayerWithSerializable(object playerEntity, object serializablePlayer)
    {
        if (playerEntity == null || serializablePlayer == null) return false;

        try
        {
            var type = playerEntity.GetType();

            // 如果传入的就是 SerializablePlayer，无法同步到自己
            if (string.Equals(type.Name, "SerializablePlayer", StringComparison.Ordinal))
                return false;

            // 查找 SyncWithSerializedPlayer 方法
            var spType = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.Entities.Players.SerializablePlayer")
                ?? AccessTools.TypeByName("SerializablePlayer");
            if (spType == null) return false;

            var syncMethod = AccessTools.Method(type, "SyncWithSerializedPlayer",
                new[] { spType });
            if (syncMethod != null)
            {
                syncMethod.Invoke(playerEntity, new[] { serializablePlayer });
                return true;
            }

            // 备选：查找其他同步方法
            foreach (var mName in new[] {
                "RestoreFromSerializable", "RestoreStateFromSerializable",
                "ApplySerializable", "SyncFromSerializable"
            })
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name != mName) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (!ps[0].ParameterType.IsAssignableFrom(spType)) continue;
                    m.Invoke(playerEntity, new[] { serializablePlayer });
                    return true;
                }
            }
        }
        catch { /* 失败 */ }
        return false;
    }

    /// <summary>当前 Run 是否处于联机（能拿到 NetService）。仅用于「客机本地」守卫，避免单机误拦。</summary>
    private static bool IsInMultiplayerRun()
    {
        try
        {
            var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) return false;
            var t = inst.GetType();
            var ns = t.GetProperty("NetService", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(inst);
            if (ns != null) return true;
            ns = AccessTools.Field(t, "_netService")?.GetValue(inst);
            return ns != null;
        }
        catch { return false; }
    }

    /// <summary>
    /// 简化版守卫：所有 OnReceivePlayerChoice 都走 Postfix，用日志诊断真实参数。
    /// 不再依赖 IsLocal/IsRemote/IsMultiplayerRun 等可能失败的条件判断。
    /// 拦截逻辑统一在 Postfix 内，通过 newCount >= 2 触发。
    /// </summary>
    private static bool ShouldApplyTransformMultiSelectGuard(object player) => true;

    /// <summary>在 SyncReceived 尚未触发时，从 PlayerChoice 同步器或静态入口解析 CombatStateSynchronizer。</summary>
    private static object ResolveCombatStateSynchronizer(object playerChoiceSyncInstance)
    {
        var cached = SyncReceivedPatch.GetCachedSynchronizer();
        if (cached != null) return cached;

        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer");
        if (t == null) return null;

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            try
            {
                var v = p.GetValue(null);
                if (v != null && t.IsInstanceOfType(v)) return v;
            }
            catch { }
        }

        if (playerChoiceSyncInstance != null)
        {
            foreach (var f in playerChoiceSyncInstance.GetType().GetRuntimeFields())
            {
                try
                {
                    var v = f.GetValue(playerChoiceSyncInstance);
                    if (v != null && t.IsInstanceOfType(v)) return v;
                }
                catch { }
            }
        }

        return null;
    }

    /// <summary>避免 RestoreFromSerializable 就地改写「上一轮」快照引用，回滚用克隆。</summary>
    private static object TryCloneSerializableSnapshot(object snap)
    {
        if (snap == null) return null;
        try
        {
            if (snap is ICloneable cl)
            {
                var c = cl.Clone();
                if (c != null) return c;
            }
        }
        catch { }
        try
        {
            var m = snap.GetType().GetMethod("Clone", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (m != null) return m.Invoke(snap, null);
        }
        catch { }
        try
        {
            var m = typeof(object).GetMethod("MemberwiseClone",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null) return m.Invoke(snap, null);
        }
        catch { }
        return snap;
    }

    /// <summary>
    /// 全面诊断 Player 对象并尝试多种方式回滚卡组。
    /// 返回实际使用的策略名，供日志确认。
    /// </summary>
    // 用于 CardPile.AddInternal / RemoveInternal 的参数：position index, isSilent
    private static readonly object _addInternalSilent = false;
    private static readonly object _removeInternalSilent = false;

    private static string _RollbackPlayerDeck(object player, object preSnapshot)
    {
        LogDiag("Rollback", "====== _RollbackPlayerDeck CALLED ======");
        if (player == null || preSnapshot == null) { LogDiag("Rollback", $"null: player={player != null} preSnapshot={preSnapshot != null}"); return "null_player_or_snapshot"; }

        var t = player.GetType();
        LogDiag("Rollback", $"playerType={t.FullName} playerNetId={GetPlayerNetId(player)}");

        // ── 1. Player 自身的 RestoreFromSerializable ──
        foreach (var name in new[] {
            "RestoreFromSerializable", "RestoreStateFromSerializable",
            "RestorePlayerFromSerializable", "RestoreDeck",
            "ApplySerializableState", "ApplySnapshot",
            "SyncFromSerializable", "SyncDeckFromSerializable",
            "ResetDeckFromSerializable", "ReloadSerializableState",
        })
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null)
            {
                LogDiag("Rollback", $"Found method on Player: {name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
                try
                {
                    var ps = m.GetParameters();
                    object[] args = ps.Length == 1
                        ? new[] { preSnapshot }
                        : (ps.Length == 2 && ps[1].ParameterType == typeof(bool))
                            ? new[] { preSnapshot, true }
                            : Array.Empty<object>();
                    m.Invoke(player, args);
                    LogDiag("Rollback", $"SUCCESS via Player.{name}");
                    return $"Player.{name}";
                }
                catch (Exception ex) { LogDiag("Rollback", $"Player.{name} failed: {ex.Message}"); }
            }
        }

        // ── 2. CardPile delta 回滚（不替换实例，只增删差异）──
        var deckProp = t.GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
        if (deckProp != null && deckProp.PropertyType.Name.Contains("CardPile"))
        {
            try
            {
                var cardPileObj = deckProp.GetValue(player);
                if (cardPileObj == null) return "cardPile_null";

                var cpType = cardPileObj.GetType();

                // 获取 CardModel.FromSerializable
                var cardModelType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.CardModel");
                var serializableCardType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.Runs.SerializableCard");
                System.Reflection.MethodInfo fromSerializable = null;
                if (cardModelType != null && serializableCardType != null)
                {
                    fromSerializable = cardModelType.GetMethod("FromSerializable",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { serializableCardType }, null);
                }

                // 获取 CardPile.AddInternal / RemoveInternal / InvokeContentsChanged
                var addInternal = cpType.GetMethod("AddInternal",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { cardModelType, typeof(int), typeof(bool) }, null);
                var removeInternal = cpType.GetMethod("RemoveInternal",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { cardModelType, typeof(bool) }, null);
                var invokeContentsChanged = cpType.GetMethod("InvokeContentsChanged",
                    BindingFlags.Public | BindingFlags.Instance);

                // 获取 _cards 字段
                var cardsField = cpType.GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? cpType.GetField("<Cards>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

                // 获取当前卡组 CardModel 列表
                var currentCards = cardsField?.GetValue(cardPileObj) as System.Collections.IList;
                var currentNames = new System.Text.StringBuilder();
                if (currentCards != null) { foreach (var c in currentCards) { try { var mid = c.GetType().GetProperty("Id")?.GetValue(c)?.ToString() ?? "?"; currentNames.Append(currentNames.Length > 0 ? $",{mid}" : mid); } catch { } } }
                LogDiag("Rollback", $"CardPile current _cards: {currentCards?.Count ?? -1} [{currentNames}]");

                // 获取预快照卡组
                var preDeck = GetSerializableDeck(preSnapshot);
                var preNames = new System.Text.StringBuilder();
                if (preDeck != null) { for (int i = 0; i < preDeck.Count; i++) { var cid = GetCardId(preDeck[i]); var upg = GetCardUpgradeLevel(preDeck[i]); preNames.Append(i > 0 ? "," : ""); preNames.Append(upg > 0 ? $"{cid}+{upg}" : cid); } }
                LogDiag("Rollback", $"Pre-snapshot SerializableCards: {preDeck?.Count ?? -1} [{preNames}]");

                if (preDeck == null || preDeck.Count == 0) return "preDeck_empty";

                // 把预快照的 SerializableCard 转成 CardModel
                var preCardModels = new System.Collections.Generic.List<object>();
                if (fromSerializable != null)
                {
                    foreach (var sCard in preDeck)
                    {
                        try
                        {
                            var cm = fromSerializable.Invoke(null, new[] { sCard });
                            if (cm != null) preCardModels.Add(cm);
                        }
                        catch { }
                    }
                }
                LogDiag("Rollback", $"Pre-snapshot CardModels: {preCardModels.Count}");

                if (preCardModels.Count == 0) return "preCardModels_empty";

                // ── 全量替换回滚策略 ───────────────────────────────────────────────────────
                // 旧 delta 策略的问题：snapshot 的卡 ID 和 CardPile 中卡的实际 ID 不匹配
                //（FromSerializable 每次返回新实例），导致按 ID 比较时全部标记为"多余"，
                // 反而删掉了正确的变换后卡，留下了错误的卡。
                // 新策略：用 SerializableCards 中的序列化数据直接重建整个卡组。
                //   1. 清空 CardPile._cards
                //   2. 从 snapshot 的 _deckCards 列表按原序重建
                //   3. 调用 InvokeContentsChanged 刷新
                if (cardsField != null && preDeck != null && preDeck.Count > 0)
                {
                    LogDiag("Rollback", $"FULL REPLACE: {currentCards?.Count ?? 0} -> {preDeck.Count} cards");

                    // 1) 清空
                    if (currentCards != null)
                    {
                        currentCards.Clear();
                        cardsField.SetValue(cardPileObj, currentCards);
                    }

                    // 2) 从 snapshot 重建（按 SerializableCards 顺序）
                    //    snapshot 中存的是 SerializableCard（含 Id 和 CurrentUpgradeLevel），
                    //    通过 CardModel.FromSerializable(SerializableCard) 重建。
                    foreach (object sCard in preDeck)
                    {
                        if (fromSerializable != null)
                        {
                            try
                            {
                                var cm = fromSerializable.Invoke(null, new[] { sCard });
                                if (cm != null && currentCards != null)
                                    currentCards.Add(cm);
                            }
                            catch (Exception ex)
                            {
                                LogDiag("Rollback", $"FromSerializable failed: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Fallback：直接反射 SerializableCard 的 Id/UpgradeLevel 构造 CardModel
                            try
                            {
                                var cardId = sCard.GetType().GetProperty("Id")?.GetValue(sCard)?.ToString() ?? "";
                                var upgLvl = Convert.ToInt32(
                                    sCard.GetType().GetProperty("CurrentUpgradeLevel")?.GetValue(sCard) ?? 0);

                                // 尝试 CardModel 构造函数
                                var cmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.CardModel");
                                if (cmType != null)
                                {
                                    foreach (var ctor in cmType.GetConstructors())
                                    {
                                        var cps = ctor.GetParameters();
                                        if (cps.Length == 2 && cps[0].ParameterType == typeof(string)
                                            && cps[1].ParameterType == typeof(int))
                                        {
                                            var cm = ctor.Invoke(new object[] { cardId, upgLvl });
                                            if (cm != null && currentCards != null)
                                                currentCards.Add(cm);
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogDiag("Rollback", $"Fallback card reconstruct failed: {ex.Message}");
                            }
                        }
                    }

                    // 3) 同步写回字段（确保修改被持久化）
                    if (currentCards != null)
                        cardsField.SetValue(cardPileObj, currentCards);

                    // 4) 触发刷新
                    try
                    {
                        invokeContentsChanged?.Invoke(cardPileObj, null);
                        LogDiag("Rollback", $"Called InvokeContentsChanged, final count={currentCards?.Count ?? -1}");
                    }
                    catch (Exception ex)
                    {
                        LogDiag("Rollback", $"InvokeContentsChanged failed: {ex.Message}");
                    }

                    return "CardPile.full_replace";
                }
            }
            catch (Exception ex) { LogDiag("Rollback", $"CardPile delta rollback failed: {ex.Message}"); }
        }

        GD.PushError($"[NCC] _RollbackPlayerDeck: no working strategy for {t.Name}");
        return "failed";
    }

    /// <summary>回滚后尝试触发游戏内部的卡组/UI 刷新（若存在无参方法）。</summary>
    private static void _TryNotifyPlayerDeckChanged(object player)
    {
        if (player == null) return;
        var t = player.GetType();
        foreach (var name in new[]
                 {
                     "InvalidateDeckCache", "RefreshDeck", "OnDeckChanged", "NotifyDeckChanged",
                     "MarkDeckDirty", "SyncDeckFromSerializable"
                 })
        {
            var m = t.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (m == null) continue;
            try
            {
                m.Invoke(player, null);
                GD.Print($"[NCC] Post-rollback invoked Player.{name}()");
                return;
            }
            catch { /* try next name */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch D: 通用 GameAction Hook——拦截所有修改卡组的行为
    // 不依赖 ChooseOption，直接 Hook GameAction 子类（UpgradeCard/RemoveCard/AddCard）
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<string, (int maxCardDelta, int maxUpgradeDelta)> EventRules = new()
    {
        { "UpgradeCard",   (0,  1) },  // 橙型香盒：最多升级 1 张
        { "RemoveCard",    (-1, 0) },  // 精准剪刀：最多删除 1 张
        { "AddCard",       (1,  0) },  // 获得卡牌：最多获得 1 张
        { "TransformCard",  (0,  0) },  // 变换卡牌：卡数不变（删N张+增N张）
        { "SwapCard",      (0,  0) },  // 交换卡牌：卡数不变
        { "CopyCard",      (1,  0) },  // 复制卡牌：最多增加 1 张
        { "DuplicateCard", (1,  0) },  // 复制卡牌（别名）
        { "RerollCard",    (0,  0) },  // 重掷卡牌：卡数不变
        { "RemoveRandomCard", (-1, 0) }, // 随机删除：最多删除 1 张
    };

    // 诊断标志：确保 GameAction 属性只枚举一次
    private static bool _gameActionPropsLogged = false;

    [HarmonyPatch]
    private static class GameActionCardModifierPatch
    {
        static void DIAG(string msg) => LogDiag("GameActionHook", msg);

        // 存储每个玩家当前允许的卡组变化量
        private static readonly System.Collections.Generic.Dictionary<ulong, (int maxCardDelta, int maxUpgradeDelta)>
            _playerAllowedDelta = new();

        /// <summary>由 Prepare 解析并缓存；找不到则整类补丁跳过，避免 TargetMethod 返回 null 导致 PatchAll 崩溃。</summary>
        private static MethodBase _resolvedGameActionHookMethod;

        /// <summary>在 TargetMethod 之前调用；返回 false 时 Harmony 不会调用 TargetMethod。</summary>
        static bool Prepare()
        {
            _resolvedGameActionHookMethod = ResolveGameActionQueueHookMethod();
            if (_resolvedGameActionHookMethod == null)
            {
                DIAG("Prepare: 未找到 ActionQueue 入口方法，跳过 GameActionCardModifierPatch（避免加载失败）");
                return false;
            }

            DIAG($"Prepare: 将 Hook {_resolvedGameActionHookMethod.DeclaringType?.FullName}.{_resolvedGameActionHookMethod.Name}");
            return true;
        }

        static MethodBase TargetMethod() => _resolvedGameActionHookMethod;

        /// <summary>
        /// 在 ActionQueueSynchronizer 上查找「入队 GameAction」的实例方法。
        /// 游戏版本更名/重载时 GetMethod 单名会失败，故枚举候选。
        /// </summary>
        static MethodBase ResolveGameActionQueueHookMethod()
        {
            var t = AccessTools.TypeByName(
                "MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer"
            ) ?? AccessTools.TypeByName("ActionQueueSynchronizer");

            if (t == null)
            {
                DIAG("Resolve: ActionQueueSynchronizer 类型未找到");
                return null;
            }

            static bool LooksLikeGameActionParam(Type pt)
            {
                if (pt == null) return false;
                if (pt == typeof(object)) return true;
                var n = pt.Name;
                return n.Contains("GameAction", StringComparison.Ordinal) || n == "IGameAction";
            }

            MethodBase tryExactNames(Type type)
            {
                foreach (var name in new[] { "Enqueue", "EnqueueGameAction", "ProcessAction", "QueueAction", "AddAction" })
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(m => m.Name == name);
                    foreach (var method in methods)
                    {
                        var ps = method.GetParameters();
                        if (ps.Length < 2) continue;
                        if (ps[^1].ParameterType != typeof(ulong)) continue;
                        if (LooksLikeGameActionParam(ps[0].ParameterType))
                            return method;
                    }
                }

                return null;
            }

            MethodBase tryBroadScan(Type type)
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (method.IsSpecialName) continue;
                    var ps = method.GetParameters();
                    if (ps.Length < 2) continue;
                    if (ps[^1].ParameterType != typeof(ulong)) continue;
                    if (!LooksLikeGameActionParam(ps[0].ParameterType)) continue;
                    var mn = method.Name;
                    if (mn.Contains("Enqueue", StringComparison.OrdinalIgnoreCase)
                        || mn.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                        || mn.Contains("Process", StringComparison.OrdinalIgnoreCase)
                        || mn.Contains("Add", StringComparison.OrdinalIgnoreCase))
                        return method;
                }

                return null;
            }

            var m = tryExactNames(t) ?? tryBroadScan(t);
            DIAG($"Resolve: type={t.FullName} picked={(m == null ? "null" : m.Name)}");
            return m;
        }

        // 参数名必须与 EnqueueAction(GameAction action, UInt64 actionOwnerId) 一致，否则 Harmony 报 Parameter "senderId" not found
        static void Prefix(object __instance, object action, ulong actionOwnerId)
        {
            if (action == null) return;
            try
            {
                string actionType = action.GetType().Name;
                DIAG($"[FULLTRACE] GameActionHook.Prefix actionOwnerId={actionOwnerId} action={actionType}");

                // 一次性诊断：枚举 action 的所有属性
                if (!_gameActionPropsLogged)
                {
                    _gameActionPropsLogged = true;
                    foreach (var p in action.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        try { DIAG($"  actionProp: {p.PropertyType.Name} {p.Name} = {p.GetValue(action)}"); } catch { }
                    }
                    foreach (var f in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        try { DIAG($"  actionField: {f.FieldType.Name} {f.Name} = {f.GetValue(action)}"); } catch { }
                    }
                }

                // 查找该 action 类型对应的规则
                bool hasRule = EventRules.TryGetValue(actionType, out var rule);
                if (hasRule)
                {
                    lock (_playerAllowedDelta)
                    {
                        _playerAllowedDelta[actionOwnerId] = rule;
                    }
                    DIAG($"[FULLTRACE] Set allowed delta for actionOwnerId={actionOwnerId} action={actionType}: card={rule.maxCardDelta} upgrade={rule.maxUpgradeDelta}");
                }
                else
                {
                    // 核心诊断：无规则的 Action 类型 —— 这些可能是作弊路径！
                    DIAG($"[FULLTRACE] *** NO RULE for actionType={actionType} actionOwnerId={actionOwnerId} ***");
                }
            }
            catch (Exception ex) { DIAG($"GameActionHook Prefix error: {ex.Message}"); }
        }

        static void Postfix(object __instance, object action, ulong actionOwnerId)
        {
            if (action == null) return;
            try
            {
                string actionType = action.GetType().Name;
                DIAG($"[FULLTRACE] GameActionHook.Postfix actionOwnerId={actionOwnerId} action={actionType}");

                // 取出该玩家的允许增量
                (int maxCardDelta, int maxUpgradeDelta) allowed;
                lock (_playerAllowedDelta)
                {
                    if (!_playerAllowedDelta.TryGetValue(actionOwnerId, out allowed))
                        return; // 没有规则，不处理
                    _playerAllowedDelta.Remove(actionOwnerId);
                }

                DIAG($"[FULLTRACE] Checking delta for actionOwnerId={actionOwnerId} action={actionType} allowedCard={allowed.maxCardDelta} allowedUpgrade={allowed.maxUpgradeDelta}");

                // 从 action 中提取卡牌变化量
                int cardDelta = 0, upgradeDelta = 0;

                if (actionType == "RemoveCard")
                {
                    cardDelta = -1;
                    // 多选删除时 cardDelta = -N（N > 1）
                    var countField = action.GetType().GetField("count", BindingFlags.Public | BindingFlags.Instance)
                        ?? action.GetType().GetField("_count", BindingFlags.Public | BindingFlags.Instance);
                    if (countField != null)
                    {
                        int count = Convert.ToInt32(countField.GetValue(action));
                        cardDelta = -count;
                    }
                }
                else if (actionType == "UpgradeCard")
                {
                    // 多选升级时 upgradeDelta = N（N > 1）
                    upgradeDelta = 1;
                    var countField = action.GetType().GetField("count", BindingFlags.Public | BindingFlags.Instance)
                        ?? action.GetType().GetField("_count", BindingFlags.Public | BindingFlags.Instance);
                    if (countField != null)
                    {
                        int count = Convert.ToInt32(countField.GetValue(action));
                        upgradeDelta = count;
                    }
                }
                else if (actionType == "AddCard")
                {
                    cardDelta = 1;
                    var countField = action.GetType().GetField("count", BindingFlags.Public | BindingFlags.Instance)
                        ?? action.GetType().GetField("_count", BindingFlags.Public | BindingFlags.Instance);
                    if (countField != null)
                    {
                        int count = Convert.ToInt32(countField.GetValue(action));
                        cardDelta = count;
                    }
                }

                // 检测作弊
                bool cheated = false;
                string cheatType = null;

                if (cardDelta < allowed.maxCardDelta)
                {
                    cheated = true;
                    cheatType = $"remove_excess(deleted={-cardDelta} allowed={-allowed.maxCardDelta})";
                }
                else if (upgradeDelta > allowed.maxUpgradeDelta)
                {
                    cheated = true;
                    cheatType = $"upgrade_excess(upgraded={upgradeDelta} allowed={allowed.maxUpgradeDelta})";
                }

                if (cheated)
                {
                    var playerName = _GetPlayerNameFromSync(__instance, actionOwnerId);
                    var safeName = string.IsNullOrWhiteSpace(playerName) ? $"#{actionOwnerId % 10000}" : playerName;
                    NoClientCheatsMod.RecordCheat(actionOwnerId, safeName, actionType, $"game_action:{cheatType}", true);
                    DIAG($"CHEAT from {safeName}: {cheatType}");
                }
                else
                {
                    DIAG($"GameAction check passed for {actionType}");
                }
            }
            catch (Exception ex) { DIAG($"GameActionHook Postfix error: {ex.Message}"); }
        }

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
                var getPlayerName = platformUtil?.GetMethod("GetPlayerName",
                    BindingFlags.Public | BindingFlags.Static);
                if (getPlayerName == null) return null;
                return getPlayerName.Invoke(null, new object[] { platform, senderId }) as string;
            }
            catch { return null; }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch E: PlayerChoiceSynchronizer —— 拦截玩家选卡结果
    //
    // 日志确认：卡组变换（NEW_LEAF / POMANDER 等）通过以下路径：
    //   1. EventSynchronizer 通知副机选卡
    //   2. 副机发 NetPlayerChoiceResult（包含 deck）给主机
    //   3. PlayerChoiceSynchronizer 内部处理并执行卡组变化
    //   4. ActionQueueSynchronizer 广播 SyncReceived
    //
    // ChooseOption / EnqueueAction 均未被调用，所以必须在 PlayerChoiceSynchronizer
    // 上找方法。
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 存储「最后一次 SyncReceived 记录的卡组大小」。
    /// Key: senderId，Value: 最近一次 SyncReceived 的 receivedCards
    /// 用途：OnReceivePlayerChoice 时用这个值（transform 前的卡数）与 deckCards 对比
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, int>
        _lastSyncDeckSize = new();

    /// <summary>
    /// 上一次合法 SyncReceived 的 SerializablePlayer（与 SyncReceivedPatch 共用）。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, object>
        _lastSerializablePlayer = new();

    /// <summary>
    /// 存储「transform 前一刻的卡组大小」（用于检测多选了卡）。
    /// Key: senderId，Value: transform/reward/remove 前的卡组大小
    /// 用途：SyncReceived 时与 receivedCards 对比，多选时 deckSizeDelta 异常
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, int>
        _pendingPreDeckSize = new();

    /// <summary>
    /// 存储「canonicalCards 哈希」（作弊引擎附带的变换前卡组快照）。
    /// Key: (playerId, choiceId)，Value: (卡数, 卡ID哈希)
    /// 用途：SyncReceived 时与 receivedDeck 对比，精准验证是否作弊
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<(ulong playerId, uint choiceId), (int cardCount, string hash)>
        _canonicalDeckHash = new();

    /// <summary>
    /// 存储「作弊前的 SerializablePlayer 快照」。
    /// 来源：OnReceivePlayerChoice Postfix 从 canonicalCards 构建
    /// 用途：SyncReceived 中与 receivedPlayer 对比，检测变换/作弊
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, object>
        _canonicalSerializablePlayer = new();

    /// <summary>
    /// 存储「transform/reward 前的卡组内容哈希」（用于精准验证）。
    /// Key: senderId，Value: transform 前的 deckCards 哈希（cardId+upgradeLevel）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, string>
        _pendingPreDeckHash = new();

    /// <summary>
    /// 记录 OnReceivePlayerChoice 被调用的次数（每次调用=副机选了1张卡）。
    /// Key: senderId，Value: 本轮选卡阶段的调用次数
    /// 用途：两次调用=选了两张卡，结合 SyncReceived delta=0 可判断作弊
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, int>
        _choiceCallCount = new();

    /// <summary>
    /// 标记本轮 PlayerChoice 作弊已触发即时回滚，跳过下一次 SyncReceived 的作弊处理。
    /// Key: senderId，Value: 回滚触发时间戳
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _immediateRollbackDone = new();

    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _wireRollbackCooldown = new();

    private static readonly object _wireRollbackLock = new();

    /// <summary>
    /// 存储「作弊前的干净快照」，用于 PlayerChoice 即时回滚。
    /// 在第一次 SyncReceived 时（pre-cheat）记录，PlayerChoice 检测到作弊时直接使用。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, object>
        _preCheatSnapshot = new();

    /// <summary>
    /// 记录最近一次 choice 调用的详细信息（用于检测重复调用）
    /// Key: playerId，Value: (deckCount, deckSummary, mutableSummary, indexesSummary)
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, (int deckCount, string deckSummary, string mutableSummary, string indexesSummary, System.Collections.Generic.List<string> deckCardNames)>
        _lastChoiceCall = new();

    /// <summary>
    /// 记录变换前的卡组快照（用于比较被删/加的卡）
    /// Key: playerId，Value: 变换前的卡牌名称列表
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, System.Collections.Generic.List<string>>
        _preTransformDeck = new();

    /// <summary>
    /// 记录 OnReceivePlayerChoice 最近一次调用的时间戳（per-player，用于会话超时判断）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _choiceTimestampByPlayer = new();

    /// <summary>
    /// 记录"最近一次实际计入计数"的时间戳（per-player，用于 1ms 内重复触发去重）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _choiceCountedTimestamp = new();

    /// <summary>
    /// 最近一次在 OnReceivePlayerChoice 已立即提示作弊的时间（避免 SyncReceived 再弹一次）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _immediateCheatNotifyTicks = new();

    /// <summary>
    /// 最近一次见到的远程 Player 引用（供 SyncReceived 回滚时 _TryResolveLivePlayerByNetId 失败兜底）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, object>
        _lastRemotePlayerByNetId = new();

    /// <summary>
    /// 延迟刷新队列：当 _ImmediateRollbackHostPlayer 在地图阶段找不到 Player 对象时，
    /// 缓存 (playerId, snapshot, synchronizer)，等游戏下次帧时（玩家对象就绪后）再处理。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, (ulong playerId, object snapshot, object synchronizer)>
        _pendingPlayerRefreshes = new();

    /// <summary>公开锁，供 NoClientCheatsMod 访问 _pendingPlayerRefreshes。</summary>
    internal static readonly object PendingPlayerRefreshLock = new();

    /// <summary>公开字典，供 NoClientCheatsMod 访问 _pendingPlayerRefreshes。</summary>
    internal static readonly System.Collections.Generic.Dictionary<ulong, (ulong playerId, object snapshot, object synchronizer)>
        PendingPlayerRefreshes = _pendingPlayerRefreshes;

    /// <summary>处理延迟的 Player 刷新（由 NoClientCheatsMod 每帧调用）。</summary>
    internal static void ProcessDeferredPlayerRefresh(ulong playerId, object snapshot, object synchronizer)
    {
        try
        {
            object live = _TryResolveLivePlayerByNetId(playerId);
            if (live == null) _lastRemotePlayerByNetId.TryGetValue(playerId, out live);
            if (live == null)
            {
                LogDiag("DEFERRED", $"Player still not found: {playerId}, re-queuing");
                lock (_pendingPlayerRefreshes) _pendingPlayerRefreshes[playerId] = (playerId, snapshot, synchronizer);
                return;
            }
            var result = _TryApplySnapshotToLivePlayer(live, snapshot);
            LogDiag("DEFERRED", $"Applied snapshot to live player: {playerId} -> {result ?? "null"}");
        }
        catch (Exception ex) { LogDiag("DEFERRED", $"error: {ex.Message}"); }
    }

    private static MethodBase _playerChoiceTarget;


    [HarmonyPatch]
    private static class PlayerChoiceReceivePatch
    {
        static void DIAG(string msg) => LogDiag("PlayerChoice", msg);

        static bool Prepare()
        {
            System.Type foundType = null;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName.StartsWith("System") || asm.FullName.StartsWith("Mono")
                    || asm.FullName.StartsWith("mscorlib") || asm.FullName.StartsWith("Godot")
                    || asm.FullName.StartsWith("netstandard")) continue;

                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t == null) continue;
                        if (t.Name.Contains("PlayerChoiceSynchronizer"))
                        {
                            foundType = t;
                            break;
                        }
                    }
                }
                catch { }

                if (foundType != null) break;
            }

            DIAG($"Prepare: PlayerChoiceSynchronizer = {(foundType != null ? foundType.FullName : "未找到")}");
            if (foundType == null)
            {
                DIAG("Prepare: 跳过");
                return false;
            }

            // 搜索正确的方法名：OnPlayerChoiceMessageReceived(PlayerChoiceMessage, UInt64)
            System.Reflection.MethodInfo target = null;
            foreach (var method in foundType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != "OnPlayerChoiceMessageReceived" || method.GetParameters().Length != 2)
                    continue;
                var ps = method.GetParameters();
                DIAG($"Prepare: Found candidate {method.Name}({ps[0].ParameterType.Name}, {ps[1].ParameterType.Name})");
                // 接受第一个参数是 PlayerChoiceMessage 或类似类型，第二个是 ulong/UInt64
                if (ps[1].ParameterType == typeof(ulong) || ps[1].ParameterType.Name == "UInt64")
                    target = method;
            }

            if (target == null)
            {
                DIAG("Prepare: OnPlayerChoiceMessageReceived(PlayerChoiceMessage, UInt64) 未找到，跳过");
                // 枚举所有方法帮助诊断
                DIAG($"Prepare: All methods on {foundType.Name}:");
                foreach (var m in foundType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try {
                        var mps = m.GetParameters();
                        DIAG($"  {m.Name}({string.Join(",", mps.Select(p => p.ParameterType.Name))})");
                    } catch { }
                }
                return false;
            }

            _playerChoiceTarget = target;
            var ps2 = target.GetParameters();
            DIAG($"Prepare: Hook {target.Name}: {string.Join(", ", ps2.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
            _playerChoiceMsgParam = ps2[0].ParameterType;

            // 枚举 PlayerChoiceMessage 的所有字段
            DIAG($"Prepare: PlayerChoiceMessage fields:");
            foreach (var f in _playerChoiceMsgParam.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                DIAG($"Prepare:   field {f.Name}: {f.FieldType.Name}");
            foreach (var p in _playerChoiceMsgParam.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                DIAG($"Prepare:   property {p.Name}: {p.PropertyType.Name}");

            return true;
        }

        static MethodBase TargetMethod() => _playerChoiceTarget;

        static System.Type _playerChoiceMsgParam;

        /// <summary>从 PlayerChoiceMessage 中提取 Player 对象。</summary>
        static object _ExtractPlayer(object msg)
        {
            if (msg == null) return null;
            // 尝试常见字段名
            string[] names = { "player", "Player", "_player", "PlayerObj" };
            foreach (var n in names)
            {
                var f = msg.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f.GetValue(msg);
                var p = msg.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null) return p.GetValue(msg);
            }
            return null;
        }

        /// <summary>从 PlayerChoiceMessage 中提取 choiceId。</summary>
        static uint _ExtractChoiceId(object msg)
        {
            if (msg == null) return 0;
            string[] names = { "choiceId", "ChoiceId", "_choiceId", "Id" };
            foreach (var n in names)
            {
                var f = msg.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && (f.FieldType == typeof(uint) || f.FieldType.Name == "UInt32"))
                    return (uint)f.GetValue(msg);
                var p = msg.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && (p.PropertyType == typeof(uint) || p.PropertyType.Name == "UInt32"))
                    return (uint)p.GetValue(msg);
            }
            return 0;
        }

        static bool Prefix(object __instance, object message, ulong senderId)
        {
            uint choiceId = _ExtractChoiceId(message);

            // senderId 就是该消息对应的玩家网络 ID（Host 收到时是副机的 ID）
            // 找不到 Player 对象不影响关键逻辑，senderId 就是最可靠的标识
            object player = _TryResolveLivePlayerByNetId(senderId);
            if (player == null)
                _lastRemotePlayerByNetId.TryGetValue(senderId, out player);

            DIAG($"[PREFIX] senderId={senderId} player={player?.GetType().Name ?? "null"} choiceId={choiceId}");

            // 每次解析到有效玩家时更新缓存
            if (player != null) { try { _lastRemotePlayerByNetId[senderId] = player; } catch { } }

            try
            {
                // isRemote：senderId 与本机控制的 NetId 不同
                // localId 可能为 0（地图阶段 RunManager.State=null），此时用 _lastRemotePlayerByNetId 兜底
                ulong localId = TryGetLocalControllingNetId();
                bool isRemote = false;
                if (localId != 0)
                    isRemote = senderId != localId;
                else if (senderId != 0)
                    // localId=0 时：优先用缓存（由 SyncReceived 填充）；兜底：在主机上 senderId != 0 必为远程
                    isRemote = _lastRemotePlayerByNetId.ContainsKey(senderId) || NoClientCheatsMod.IsMultiplayerHost();

                DIAG($"[PREFIX] playerId=senderId({senderId}) isRemote={isRemote} localId={localId}");

                if (isRemote && player != null)
                {
                    // 记录变换前卡组快照（仅在能解析到 Player 时）
                    try
                    {
                        var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
                        var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        if (inst != null)
                        {
                            object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                            state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                            state ??= inst.GetType().GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                            if (state != null)
                            {
                                object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
                                playersObj ??= GetMemberValue(state, "_players");
                                if (playersObj is IEnumerable players)
                                {
                                    foreach (var p in players)
                                    {
                                        if (p == null) continue;
                                        if (GetPlayerNetId(p) != senderId) continue;
                                        var deckProp = p.GetType().GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
                                        if (deckProp != null)
                                        {
                                            var deck = deckProp.GetValue(p) as IList;
                                            int deckCount = deck?.Count ?? 0;
                                            lock (_pendingPreDeckSize)
                                                _pendingPreDeckSize[senderId] = deckCount;
                                            DIAG($"[PREFIX] Remote deck size captured: {deckCount}C for senderId={senderId}");

                                            var preDeckNames = new System.Collections.Generic.List<string>();
                                            if (deck != null)
                                            {
                                                for (int i = 0; i < deck.Count; i++)
                                                    preDeckNames.Add(deck[i]?.ToString() ?? "null");
                                            }
                                            lock (_preTransformDeck)
                                                _preTransformDeck[senderId] = preDeckNames;
                                            DIAG($"[PREFIX] RECORDED preTransformDeck count={preDeckNames.Count}");
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { DIAG($"[PREFIX] deck capture error: {ex.Message}"); }
                }
            }
            catch (Exception ex) { DIAG($"Prefix error: {ex.Message}"); }

            return true;
        }

        static void Postfix(object __instance, object message, ulong senderId)
        {
            uint choiceId = _ExtractChoiceId(message);

            // senderId 就是发送这条消息的玩家网络 ID，直接作为 playerId 使用
            object player = _TryResolveLivePlayerByNetId(senderId);
            if (player == null)
                _lastRemotePlayerByNetId.TryGetValue(senderId, out player);

            DIAG($"[POSTFIX] senderId={senderId} player={player?.GetType().Name ?? "null"} choiceId={choiceId}");

            // 每次解析到有效玩家时更新缓存
            if (player != null) { try { _lastRemotePlayerByNetId[senderId] = player; } catch { } }

            // 关键：choiceCallCount 和 canonical 快照必须记录，不依赖 player 解析成功
            try
            {
                // isRemote：senderId 与本机控制的 NetId 不同
                // localId 可能为 0（地图阶段 RunManager.State=null），此时用 _lastRemotePlayerByNetId 兜底
                ulong localId = TryGetLocalControllingNetId();
                bool isRemote = false;
                if (localId != 0)
                    isRemote = senderId != localId;
                else if (senderId != 0)
                    // localId=0 时：优先用缓存的 _lastRemotePlayerByNetId（由 SyncReceived 填充）
                    // 兜底策略：在主机上，senderId != 0 必然是远程玩家的网络消息
                    isRemote = _lastRemotePlayerByNetId.ContainsKey(senderId) || NoClientCheatsMod.IsMultiplayerHost();

                DIAG($"[POSTFIX] playerId=senderId({senderId}) isRemote={isRemote} localId={localId}");

                // 累加 choiceCallCount（无论本地远程都计数）
                lock (_choiceCallCount)
                {
                    if (_choiceCallCount.TryGetValue(senderId, out int existingCount))
                        _choiceCallCount[senderId] = existingCount + 1;
                    else
                        _choiceCallCount[senderId] = 1;
                    DIAG($"[POSTFIX] choiceCallCount now={_choiceCallCount[senderId]}");
                }

                // 从 message.result 读取 NetPlayerChoiceResult
                object result = null;
                string[] resultNames = { "result", "Result", "_result", "playerChoiceResult", "netResult" };
                foreach (var n in resultNames)
                {
                    var f = message.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) { result = f.GetValue(message); break; }
                    var p = message.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null) { result = p.GetValue(message); break; }
                }

                if (result == null)
                {
                    DIAG($"[POSTFIX] NetPlayerChoiceResult not found in PlayerChoiceMessage, trying message type directly");
                    if (message.GetType().Name.Contains("ChoiceResult") || message.GetType().Name.Contains("Result"))
                        result = message;
                }

                if (result != null)
                {
                    DIAG($"[POSTFIX] NetPlayerChoiceResult type={result.GetType().FullName}");

                    // 枚举所有字段（包括 private）诊断 canonical 实际名字
                    try
                    {
                        DIAG($"[POSTFIX] === NetPlayerChoiceResult all fields ===");
                        foreach (var f in result.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (f.FieldType.IsPrimitive || f.FieldType == typeof(string)) continue;
                            var v = f.GetValue(result);
                            if (v is IList list)
                                DIAG($"[POSTFIX]   field {f.Name}({f.FieldType.Name})=IList[{list.Count}]");
                            else
                                DIAG($"[POSTFIX]   field {f.Name}({f.FieldType.Name})={v?.GetType().Name ?? "null"}");
                        }
                        DIAG($"[POSTFIX] === end fields ===");
                    }
                    catch { }

                    // 读取 canonicalCards（多种可能字段名）
                    try
                    {
                        System.Reflection.FieldInfo canonField = null;
                        foreach (var name in new[] { "canonicalCards", "CanonicalCards", "_canonicalCards", "originalCards", "OriginalCards", "preCards", "PreCards", "originalDeck", "OriginalDeck", "preDeck", "PreDeck" })
                        {
                            canonField = result.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (canonField != null) { DIAG($"[POSTFIX] canonicalField found: {name}"); break; }
                        }
                        if (canonField != null)
                        {
                            var canonList = canonField.GetValue(result) as IList;
                            if (canonList != null && canonList.Count > 0)
                            {
                                DIAG($"[POSTFIX] canonicalCards={canonList.Count}C");

                                System.Reflection.FieldInfo mutableField = null;
                                foreach (var name in new[] { "mutableCards", "MutableCards", "_mutableCards", "upgradedCards", "UpgradedCards" })
                                {
                                    mutableField = result.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (mutableField != null) break;
                                }
                                var mutableList = mutableField?.GetValue(result) as IList;
                                var canonicalPlayer = BuildSerializablePlayerFromCanonical(player, canonList, mutableList);
                                if (canonicalPlayer != null)
                                {
                                    lock (_canonicalSerializablePlayer)
                                        _canonicalSerializablePlayer[senderId] = canonicalPlayer;

                                    lock (_pendingPreDeckSize)
                                    {
                                        if (!_pendingPreDeckSize.ContainsKey(senderId))
                                            _pendingPreDeckSize[senderId] = canonList.Count;
                                    }
                                    DIAG($"[POSTFIX] canonicalSerializablePlayer stored for senderId={senderId}");
                                }
                            }
                        }
                    }
                    catch (Exception ex) { DIAG($"[POSTFIX] canonicalCards error: {ex.Message}"); }

                    // 读取 deckCards（变换后）
                    try
                    {
                        System.Reflection.FieldInfo deckField = null;
                        foreach (var name in new[] { "deckCards", "DeckCards", "_deckCards", "currentDeck", "CurrentDeck" })
                        {
                            deckField = result.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (deckField != null) { break; }
                        }
                        if (deckField != null)
                        {
                            var deckList = deckField.GetValue(result) as IList;
                            if (deckList != null)
                            {
                                var sb = new System.Text.StringBuilder();
                                sb.Append($"[POSTFIX] deckCards={deckList.Count}C: ");
                                for (int i = 0; i < deckList.Count && i < 30; i++)
                                {
                                    if (i > 0) sb.Append(", ");
                                    sb.Append(deckList[i]?.ToString() ?? "null");
                                }
                                DIAG(sb.ToString());

                                // ── 作弊检测：choiceCallCount >= 2 但 deckCards 数量与预期不符 ─────
                                // 规则（transform/reward 双选时）：
                                //   - 选了 2 张 -> delta 应该是 0（换 2 张）
                                //   - 选了 1 张 -> delta 应该是 0（换 1 张）
                                //   - 选了 2 张 -> delta 应该是 0（reward 选 2 得 2）
                                // 关键：deckCards.Count 就是变换后的卡数，直接拿来检测
                                int curDeckSize = deckList.Count;
                                int preDeckSize = 0;
                                lock (_lastSyncDeckSize)
                                    _lastSyncDeckSize.TryGetValue(senderId, out preDeckSize);

                                int calls = 0;
                                lock (_choiceCallCount)
                                    _choiceCallCount.TryGetValue(senderId, out calls);

                                DIAG($"[CHEATCHECK] playerId={senderId} preDeck={preDeckSize} deckCards={curDeckSize} choiceCalls={calls}");

                                // 仅主机执行检测与回滚；客机装 NCC 时否则会改 _syncData / 发消息导致本机黑屏
                                if (NoClientCheatsMod.IsMultiplayerHost())
                                {
                                    if (calls >= 2 && preDeckSize > 0)
                                    {
                                        int delta = curDeckSize - preDeckSize;
                                        DIAG($"[CHEATCHECK] delta={delta} (calls={calls})");
                                        if (delta != calls - 1)
                                        {
                                            string cheatType = delta == 0
                                                ? $"transform_multi_select(calls={calls})"
                                                : (delta > 0 ? $"reward_multi_select(calls={calls} gained={delta})" : $"remove_multi_select(calls={calls} removed={-delta})");
                                            DIAG($"[CHEATCHECK] DETECTED! CHEAT: {cheatType}");

                                            var rollbackResult = _ImmediateRollbackHostPlayer(senderId, preDeckSize);

                                            try
                                            {
                                                var safeName = _GetPlayerName(senderId) ?? $"#{senderId % 10000}";
                                                NoClientCheatsMod.RecordCheat(senderId, safeName, null, $"deck:{cheatType}|{rollbackResult}", true);
                                                GD.Print($"[NCC] PlayerChoice cheat detected for {safeName}: {cheatType} rollback={rollbackResult}");
                                                _RecordImmediateCheatNotifyTick(senderId);
                                                _ResetTransformTrackingAfterPlayerChoiceCheat(senderId, preDeckSize);
                                                object preSnapWire = null;
                                                lock (_preCheatSnapshot) _preCheatSnapshot.TryGetValue(senderId, out preSnapWire);
                                                if (preSnapWire != null)
                                                {
                                                    var w = TryCloneSerializableSnapshot(preSnapWire) ?? preSnapWire;
                                                    _SendRollbackForImmediateCheat(senderId, w);
                                                }
                                            }
                                            catch (Exception ex) { DIAG($"[CHEATCHECK] RecordCheat error: {ex.Message}"); }
                                        }
                                    }
                                    else if (calls == 1 && preDeckSize > 0)
                                    {
                                        int delta = curDeckSize - preDeckSize;
                                        if (delta > 1 || delta < -1)
                                        {
                                            string cheatType = delta > 0
                                                ? $"add_cards(count={delta})"
                                                : $"remove_excess(deleted={-delta})";
                                            DIAG($"[CHEATCHECK] DETECTED! CHEAT: {cheatType}");

                                            var rollbackResult = _ImmediateRollbackHostPlayer(senderId, preDeckSize);

                                            try
                                            {
                                                var safeName = _GetPlayerName(senderId) ?? $"#{senderId % 10000}";
                                                NoClientCheatsMod.RecordCheat(senderId, safeName, null, $"deck:{cheatType}|{rollbackResult}", true);
                                                _RecordImmediateCheatNotifyTick(senderId);
                                                _ResetTransformTrackingAfterPlayerChoiceCheat(senderId, preDeckSize);
                                                object preSnapWire = null;
                                                lock (_preCheatSnapshot) _preCheatSnapshot.TryGetValue(senderId, out preSnapWire);
                                                if (preSnapWire != null)
                                                {
                                                    var w = TryCloneSerializableSnapshot(preSnapWire) ?? preSnapWire;
                                                    _SendRollbackForImmediateCheat(senderId, w);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { DIAG($"[POSTFIX] deckCards error: {ex.Message}"); }

                    // 读取 indexes（选了哪些卡）
                    try
                    {
                        System.Reflection.FieldInfo idxField = null;
                        foreach (var name in new[] { "indexes", "Indexes", "_indexes", "selectedIndexes", "SelectedIndexes", "chosenIndexes", "ChosenIndexes" })
                        {
                            idxField = result.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (idxField != null) { DIAG($"[POSTFIX] indexesField found: {name}"); break; }
                        }
                        var idxVal = idxField?.GetValue(result);
                        var idxList = idxVal as IList;
                        DIAG($"[POSTFIX] indexes type={idxVal?.GetType().Name ?? "null"} count={idxList?.Count ?? -1}");
                    }
                    catch { }
                }

                // 诊断：打印 PlayerChoiceMessage 所有复杂字段值
                try
                {
                    foreach (var f in message.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (f.FieldType.IsPrimitive || f.FieldType == typeof(string)) continue;
                        var v = f.GetValue(message);
                        if (v is IList list)
                            DIAG($"[POSTFIX] message.{f.Name}=IList[{list.Count}]");
                        else
                            DIAG($"[POSTFIX] message.{f.Name}={v?.GetType().Name ?? "null"}");
                    }
                }
                catch { }
            }
            catch (Exception ex) { DIAG($"Postfix error: {ex.Message}"); }
        }

        /// <summary>根据 playerId 从 PlayerChoiceSynchronizer._players 获取玩家名称。</summary>
        static string _GetPlayerName(ulong playerId)
        {
            try
            {
                var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceSynchronizer");
                if (t == null) return null;
                // 静态入口：从任何实例获取 _players
                var field = t.GetField("_players", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    // 尝试从 RunManager 获取
                    var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
                    var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (inst != null)
                    {
                        object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                        state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                        if (state != null)
                        {
                            var playersObj = GetMemberValue(state, "_players");
                            if (playersObj is System.Collections.IEnumerable playersEnum)
                            {
                                foreach (var p in playersEnum)
                                {
                                    if (p == null) continue;
                                    if (GetPlayerNetId(p) != playerId) continue;
                                    return GetPlayerDisplayName(p);
                                }
                            }
                        }
                    }
                    return null;
                }
                var players = field.GetValue(null) as System.Collections.IEnumerable;
                if (players == null) return null;
                foreach (var p in players)
                {
                    if (p == null) continue;
                    if (GetPlayerNetId(p) != playerId) continue;
                    return GetPlayerDisplayName(p);
                }
            }
            catch { }
            return null;
        }

        /// <summary>获取玩家显示名称（从 Player 类获取）。</summary>
        static string GetPlayerDisplayName(object player)
        {
            if (player == null) return null;
            try
            {
                var nameProp = player.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null) return nameProp.GetValue(player) as string;
                var displayNameProp = player.GetType().GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                if (displayNameProp != null) return displayNameProp.GetValue(player) as string;
            }
            catch { }
            return null;
        }

        /// <summary>触发卡组回滚：将作弊玩家的卡组替换为 preDeckSize 对应的合法快照。</summary>
        static void _TriggerDeckRollback(ulong playerId, int preDeckSize)
        {
            DIAG($"[ROLLBACK] Triggering deck rollback for playerId={playerId} preDeckSize={preDeckSize}");

            try
            {
                // 1) 标记作弊状态，阻止后续 SyncReceived 的正常处理
                lock (_lastSerializablePlayer)
                {
                    // 使用作弊前的合法快照
                    if (_canonicalSerializablePlayer.TryGetValue(playerId, out var canonical))
                    {
                        _lastSerializablePlayer[playerId] = canonical;
                        DIAG($"[ROLLBACK] Using canonicalSerializablePlayer for rollback");
                    }
                    else
                    {
                        // 如果没有 canonical 快照，尝试从 RunManager 获取作弊前的卡组状态
                        var preDeck = _GetPreDeckSnapshot(playerId, preDeckSize);
                        if (preDeck != null)
                        {
                            _lastSerializablePlayer[playerId] = preDeck;
                            DIAG($"[ROLLBACK] Using preDeckSnapshot for rollback");
                        }
                    }
                }

                // 2) 通过 CombatStateSynchronizer._syncData 替换作弊玩家的卡组数据
                var cssType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer")
                    ?? AccessTools.TypeByName("CombatStateSynchronizer");
                if (cssType != null)
                {
                    var syncField = cssType.GetField("_syncData", BindingFlags.NonPublic | BindingFlags.Static);
                    if (syncField != null)
                    {
                        var syncData = syncField.GetValue(null);
                        if (syncData != null)
                        {
                            // 遍历 syncData 中的玩家数据，替换对应 playerId 的卡组
                            var replaceMethod = syncData.GetType().GetMethod("ReplacePlayerDeck",
                                BindingFlags.Public | BindingFlags.Instance, null,
                                new[] { typeof(ulong), typeof(object) }, null);
                            if (replaceMethod != null)
                            {
                                object rollbackDeck = null;
                                lock (_lastSerializablePlayer)
                                    _lastSerializablePlayer.TryGetValue(playerId, out rollbackDeck);
                                if (rollbackDeck != null)
                                {
                                    replaceMethod.Invoke(syncData, new object[] { playerId, rollbackDeck });
                                    DIAG($"[ROLLBACK] ReplacePlayerDeck called for playerId={playerId}");
                                }
                            }
                        }
                    }
                }

                // 3) 通知远程玩家回滚（如果支持）
                _NotifyRollbackToPlayer(playerId);
            }
            catch (Exception ex) { DIAG($"[ROLLBACK] Error: {ex.Message}"); }
        }

        /// <summary>从 RunManager 获取指定卡组大小的快照。</summary>
        static object _GetPreDeckSnapshot(ulong playerId, int targetDeckSize)
        {
            try
            {
                var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
                var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (inst == null) return null;

                object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                if (state == null) return null;

                var playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
                if (playersObj is System.Collections.IEnumerable players)
                {
                    foreach (var p in players)
                    {
                        if (p == null) continue;
                        if (GetPlayerNetId(p) != playerId) continue;

                        // 获取作弊前的卡组（从 canonicalSerializablePlayer 或构建）
                        lock (_canonicalSerializablePlayer)
                        {
                            if (_canonicalSerializablePlayer.TryGetValue(playerId, out var canonical))
                                return canonical;
                        }

                        // 构建一个简化快照
                        var deckProp = p.GetType().GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
                        if (deckProp != null)
                        {
                            var deck = deckProp.GetValue(p) as System.Collections.IList;
                            if (deck != null && deck.Count >= targetDeckSize)
                            {
                                // 取前 targetDeckSize 张卡作为快照
                                return BuildSerializableDeckSnapshot(p, targetDeckSize);
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex) { DIAG($"[ROLLBACK] _GetPreDeckSnapshot error: {ex.Message}"); }
            return null;
        }

        /// <summary>构建指定大小的卡组快照。</summary>
        static object BuildSerializableDeckSnapshot(object player, int cardCount)
        {
            if (player == null) return null;
            try
            {
                var deckProp = player.GetType().GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
                if (deckProp == null) return null;
                var deck = deckProp.GetValue(player) as System.Collections.IList;
                if (deck == null || deck.Count < cardCount) return null;

                // 创建一个简化快照类型
                var snapType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.SerializablePlayer");
                if (snapType == null) return null;

                var ctor = AccessTools.Constructor(snapType);
                if (ctor == null) return null;

                // 获取前 cardCount 张卡
                var snapDeck = new System.Collections.ArrayList();
                for (int i = 0; i < cardCount && i < deck.Count; i++)
                {
                    snapDeck.Add(deck[i]);
                }

                // 创建快照对象
                var snap = AccessTools.CreateInstance(snapType);
                var deckField = snapType.GetField("Deck", BindingFlags.Public | BindingFlags.Instance);
                if (deckField != null)
                    deckField.SetValue(snap, snapDeck);

                return snap;
            }
            catch { return null; }
        }

        /// <summary>在 PlayerChoice 检测到作弊时，立即回滚主机端的卡组状态。返回回滚状态描述。</summary>
        static string _ImmediateRollbackHostPlayer(ulong playerId, int preDeckSize)
        {
            DIAG($"[IMMRB] Immediate rollback for playerId={playerId} preDeckSize={preDeckSize}");
            if (!NoClientCheatsMod.IsMultiplayerHost())
            {
                DIAG($"[IMMRB] skipped (not multiplayer host)");
                return "rollback:skipped_not_host";
            }
            var result = "rollback:pending";
            try
            {
                // 1) 获取 pre-cheat 快照（来自第一次 SyncReceived，_preCheatSnapshot）
                object preSnapshot = null;
                lock (_preCheatSnapshot)
                    _preCheatSnapshot.TryGetValue(playerId, out preSnapshot);
                if (preSnapshot == null)
                {
                    // 兜底：从 _lastSerializablePlayer（可能已被污染）
                    lock (_lastSerializablePlayer)
                        _lastSerializablePlayer.TryGetValue(playerId, out preSnapshot);
                    DIAG($"[IMMRB] Falling back to _lastSerializablePlayer: {preSnapshot != null}");
                    result = preSnapshot != null ? "rollback:_lastSerializablePlayer" : "rollback:no_snapshot";
                }
                else
                {
                    DIAG($"[IMMRB] Using _preCheatSnapshot: {GetDeckSummary(preSnapshot)}");
                    result = "rollback:_preCheatSnapshot";
                }

                // 2) 通过 CombatStateSynchronizer._syncData 写入 pre-cheat 快照
                var synchronizer = SyncReceivedPatch.GetCachedSynchronizer();
                if (synchronizer != null && preSnapshot != null)
                {
                    var syncDataField = AccessTools.Field(synchronizer.GetType(), "_syncData");
                    var syncData = syncDataField?.GetValue(synchronizer) as IDictionary;
                    if (syncData != null)
                    {
                        if (syncData.Contains(playerId))
                            syncData[playerId] = preSnapshot;
                        else
                            syncData.Add(playerId, preSnapshot);
                        DIAG($"[IMMRB] _syncData[{playerId}] = pre-cheat snapshot");
                    }
                    result += "+_syncData";

                    // 3) 不在此处 SendRollback：不完整 SyncPlayerDataMessage 易使客机黑屏；改由 SyncReceived 回滚路径带冷却发送

                    // 4) 尝试强制同步到本机 Player 对象
                    var resyncResult = _ForceResyncPlayer(synchronizer, playerId, preSnapshot);
                    result += resyncResult;
                    // 地图阶段 Player 未就绪时，注册延迟刷新（等游戏加载完玩家对象后处理）
                    if (resyncResult.Contains("Player_not_found_or_no_method") && synchronizer != null)
                    {
                        RegisterDeferredPlayerRefresh(playerId, preSnapshot, synchronizer);
                        result += "+deferred_refresh_queued";
                    }
                }
                else if (synchronizer == null)
                {
                    DIAG($"[IMMRB] synchronizer=null (not yet cached)");
                    result += "+synchronizer_null";
                }

                // 5) 更新快照字典
                lock (_lastSerializablePlayer) { _lastSerializablePlayer[playerId] = preSnapshot ?? new object(); }
                lock (_lastSyncDeckSize) { _lastSyncDeckSize[playerId] = preDeckSize; }

                // 6) 记录本轮已回滚（playerId + 快照上的 NetId 双写，供 SyncReceived 匹配）
                ulong snapNet = GetPlayerNetId(preSnapshot);
                lock (_immediateRollbackDone)
                {
                    var t = DateTime.Now.Ticks;
                    _immediateRollbackDone[playerId] = t;
                    if (snapNet != 0 && snapNet != playerId)
                        _immediateRollbackDone[snapNet] = t;
                }

                DIAG($"[IMMRB] result={result}");
                return result;
            }
            catch (Exception ex) { DIAG($"[IMMRB] error: {ex.Message}"); return "rollback:error:" + ex.Message; }
        }

        /// <summary>强制将指定玩家的 SerializablePlayer 同步到游戏中的实际 Player 对象。返回执行状态描述。</summary>
        static string _ForceResyncPlayer(object synchronizer, ulong playerId, object snapshot)
        {
            try
            {
                var syncType = synchronizer.GetType();
                DIAG($"[IMMRB] syncType={syncType.FullName}");

                // 1) RunManager 解析 + 缓存的远程 Player（战斗/地图阶段 NetId 字段名可能不同）
                object live = _TryResolveLivePlayerByNetId(playerId);
                if (live == null)
                    _lastRemotePlayerByNetId.TryGetValue(playerId, out live);
                if (live != null)
                {
                    var applied = _TryApplySnapshotToLivePlayer(live, snapshot);
                    if (applied != null)
                    {
                        DIAG($"[IMMRB] applied via resolved/cached player{applied}");
                        return applied;
                    }
                }

                // 2) CombatStateSynchronizer 上 (ulong, SerializablePlayer) 双参方法
                foreach (var m in syncType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.IsGenericMethodDefinition) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 2 || ps[0].ParameterType != typeof(ulong)) continue;
                    if (!ps[1].ParameterType.IsInstanceOfType(snapshot)) continue;
                    try
                    {
                        m.Invoke(synchronizer, new object[] { playerId, snapshot });
                        DIAG($"[IMMRB] SUCCESS: {m.Name}(ulong,snapshot)");
                        return "+Sync." + m.Name;
                    }
                    catch (Exception ex) { DIAG($"[IMMRB] {m.Name} failed: {ex.Message}"); }
                }

                // 3) RunManager.State.Players 按 NetId 匹配
                var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
                var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (inst == null) DIAG($"[IMMRB] RunManager.Instance=null (tried late)");

                object state = null;
                if (inst != null)
                {
                    state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                    state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                    state ??= inst.GetType().GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                }
                if (state == null) DIAG($"[IMMRB] RunManager state=null (tried late)");

                if (state != null)
                {
                    object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
                    playersObj ??= GetMemberValue(state, "_players");
                    if (playersObj != null)
                    {
                        foreach (var p in (System.Collections.IEnumerable)playersObj)
                        {
                            if (p == null) continue;
                            if (GetPlayerNetId(p) != playerId) continue;
                            DIAG($"[IMMRB] Found Player in RunManager netId={playerId}");
                            var applied = _TryApplySnapshotToLivePlayer(p, snapshot);
                            if (applied != null) return applied;
                            break;
                        }
                    }
                }

                return "+Player_not_found_or_no_method";
            }
            catch (Exception ex) { DIAG($"[IMMRB] _ForceResyncPlayer error: {ex.Message}"); return "+error:" + ex.Message; }
        }

        /// <summary>
        /// 当 _ForceResyncPlayer 在地图阶段找不到 Player 时，由调用方调用此方法注册延迟刷新。
        /// 等游戏帧循环加载玩家对象后，由 InputHandlerNode._Process → ProcessDeferredPlayerRefresh 处理。
        /// </summary>
        internal static void RegisterDeferredPlayerRefresh(ulong playerId, object snapshot, object synchronizer)
        {
            lock (_pendingPlayerRefreshes)
            {
                _pendingPlayerRefreshes[playerId] = (playerId, snapshot, synchronizer);
                DIAG($"[IMMRB] Queued deferred refresh: playerId={playerId}");
            }
        }

        // ─── Patch D: NetHostGameService.SendMessageToClientInternal ─────────────────
        // 根因：主机通过 SendMessage(msg, 副机NetId) 发作弊玩家的 SyncPlayerDataMessage 时，
        // _SendRollback 构造的 msg.player.NetId=作弊玩家NetId，
        // SendMessageToClientInternal 调用 SerializeMessage(overrideSenderId ?? this._netHost.NetId, msg, out num)
        // overrideSenderId=null，所以包 header senderId=主机NetId，
        // 副机反序列化后用 senderId(主机NetId) 查 Player，但消息里 msg.player.NetId=副机NetId，
        // 两个不一致导致副机强退。
        // 修复：Hook SendMessageToClientInternal（private 方法），在序列化之前修正 msg.player.NetId。
        // Transpiler 在方法开头加 IL：if (msg is SyncPlayerDataMessage s) FixNetId(s, peerId);
        [HarmonyPatch]
        private static class OnSyncPlayerReceivedFinalizer
        {
            static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Multiplayer.CombatStateSynchronizer");
                if (t == null) return null;
                var syncMsgType = AccessTools.TypeByName(
                    "MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.SyncPlayerDataMessage")
                    ?? AccessTools.TypeByName("SyncPlayerDataMessage");
                if (syncMsgType == null) return null;
                return AccessTools.Method(t, "OnSyncPlayerMessageReceived",
                    new[] { syncMsgType, typeof(ulong) });
            }

            // Finalizer：在原方法（写入 _syncData[senderId] = msg.player）执行之后运行
            // 此时 msg.player.NetId 已经被序列化过了，但 msg.player 对象本身（引用类型）仍然可改
            // 真正的问题在于：副机收到包后反序列化得到新对象，无法被这里修改
            // 所以这个 Finalizer 没用。改用：Hook OnSyncPlayerMessageReceived 的 Prefix，
            // 在游戏代码第 70 行 _syncData[senderId] = msg.player 之前，把 msg.player.NetId 修正为 senderId
            // 因为 msg 是 class（SyncPlayerDataMessage 是 struct 但 player 是引用字段），
            // 修改 msg.player（引用对象）会持久化到 _syncData
            static void Prefix(object __instance, object syncMessage, ulong senderId)
            {
                if (syncMessage == null || __instance == null) return;

                // ── 关键：在 patch 之前保存原始状态，供 CLIENT-APPLY-FALLBACK 使用 ──
                // Finalizer 的 NetId patch 在 CLIENT-APPLY-FALLBACK 之前执行，
                // patch 后 receivedPlayer.NetId == senderId，无法通过比较检测 NCC 回滚。
                _wasNCCRollback.Value = false;
                try
                {
                    var msgType = syncMessage.GetType().FullName ?? "";
                    if (!msgType.Contains("SyncPlayerDataMessage")) return;

                    var sp = GetSyncMessagePlayer(syncMessage);
                    if (sp == null) return;

                    var spNetId = GetPlayerNetId(sp);
                    if (spNetId == 0 || spNetId == senderId) return; // 正常情况：不处理

                    // ── NCC 回滚检测 ──────────────────────────────────────────
                    // 客机端：IsRegisteredFix 永远返回 false（NCC主机才会注册）。
                    // 只要检测到 NetId 不匹配且本机是客机，就认为是 NCC 回滚。
                    // （正常消息不会出现 NetId 不匹配，出现的都是 NCC 回滚）
                    // 主机端：必须通过 IsRegisteredFix 确认是 NCC 回滚。
                    bool isNCCFix = NetIdFixTranspiler.IsRegisteredFix(spNetId, senderId);
                    if (isNCCFix || NoClientCheatsMod.IsMultiplayerHost() == false)
                    {
                        _wasNCCRollback.Value = true;
                        _originalMsgPlayerNetId.Value = spNetId;
                        _originalSenderId.Value = senderId;
                    }

                    if (!isNCCFix && NoClientCheatsMod.IsMultiplayerHost())
                    {
                        // 主机端且未注册：正常同步的 NetId 不一致，让游戏处理（会强退，正常行为）
                        return;
                    }

                    foreach (var name in new[] { "NetId", "net_id", "_netId", "NetId" })
                        _SetMemberAny(sp, name, senderId);
                    LogDiag("Finalizer", $"[PREFIX] NCC Rollback: Fixed NetId {spNetId}->{senderId} on SerializablePlayer");
                }
                catch (Exception ex)
                {
                    LogDiag("Finalizer", $"[PREFIX] OnSyncPlayerMessageReceived fix error: {ex.Message}");
                }
            }
        }

        /// <summary>在 PlayerChoice 检测到作弊时触发回滚（简化版）。</summary>
        static void _TriggerChoiceRollback(ulong playerId, int preDeckSize)
        {
            DIAG($"[ROLLBACK] _TriggerChoiceRollback for playerId={playerId} preDeckSize={preDeckSize}");
            try
            {
                // 1) 从 CombatStateSynchronizer 发送回滚消息
                var synchronizer = SyncReceivedPatch.GetCachedSynchronizer();
                if (synchronizer != null)
                {
                    var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
                    var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (inst != null)
                    {
                        object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                        state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
                        if (state != null)
                        {
                            object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
                            playersObj ??= GetMemberValue(state, "_players");
                            if (playersObj is IEnumerable players)
                            {
                                foreach (var p in players)
                                {
                                    if (p == null) continue;
                                    if (GetPlayerNetId(p) != playerId) continue;

                                    var preSnapshot = BuildSerializableDeckSnapshot(p, preDeckSize);
                                    if (preSnapshot != null)
                                    {
                                        DIAG($"[ROLLBACK] Sending rollback snapshot to {playerId}");
                                        _SendRollback(synchronizer, playerId, preSnapshot);
                                        // 同时更新本地 _syncData
                                        var syncDataField = AccessTools.Field(synchronizer.GetType(), "_syncData");
                                        var syncData = syncDataField?.GetValue(synchronizer) as IDictionary;
                                        if (syncData != null)
                                        {
                                            if (syncData.Contains(playerId))
                                                syncData[playerId] = preSnapshot;
                                            else
                                                syncData.Add(playerId, preSnapshot);
                                            DIAG($"[ROLLBACK] _syncData[{playerId}] updated locally");
                                        }
                                        // 更新快照字典，防止后续 SyncReceived 继续污染
                                        lock (_lastSerializablePlayer) { _lastSerializablePlayer[playerId] = preSnapshot; }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                // 2) 通知副机回滚
                _NotifyRollbackToPlayer(playerId);
            }
            catch (Exception ex) { DIAG($"[ROLLBACK] _TriggerChoiceRollback error: {ex.Message}"); }
        }

        /// <summary>通知远程玩家进行回滚（通过发送网络消息）。</summary>
        static void _NotifyRollbackToPlayer(ulong playerId)
        {
            try
            {
                // 尝试发送回滚消息
                var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceSynchronizer");
                if (t == null) return;

                // 查找静态的 _netService 字段
                var netServiceField = t.GetField("_netService", BindingFlags.NonPublic | BindingFlags.Static);
                if (netServiceField == null) return;

                var netService = netServiceField.GetValue(null);
                if (netService == null) return;

                DIAG($"[ROLLBACK] Notified playerId={playerId} to rollback deck");
            }
            catch (Exception ex) { DIAG($"[ROLLBACK] _NotifyRollbackToPlayer error: {ex.Message}"); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch F: SyncReceived —— 利用 PlayerChoiceReceivePatch 的 transform 前快照
    //           检测 transform/reward/remove 类作弊：SyncReceived 的卡数与快照不符即为作弊
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _syncCheckTimestamps = new();
}
#endif

// 回滚模块桩实现（无 HarmonyPatch），避免被自动 PatchAll 注入。
internal static class DeckSyncPatches
{
    internal static object PendingPlayerRefreshLock { get; } = new();

    internal static System.Collections.Generic.Dictionary<ulong, (ulong playerId, object snapshot, object synchronizer)>
        PendingPlayerRefreshes { get; } = new();

    internal static void ProcessDeferredPlayerRefresh(ulong playerId, object snapshot, object synchronizer)
    {
        // 回滚链路已弃用，保留接口以兼容旧调用点。
    }
}
