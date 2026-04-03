using Godot;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
[HarmonyPatch]
internal static class DeckSyncPatches
{
    // 诊断标志：确保 SyncMessage 字段只打印一次
    private static bool _syncMsgFieldsLogged = false;

    // 诊断标志：确保 _restSites 结构只打印一次
    private static bool _restSiteFieldsLogged = false;

    // 诊断标志：确保 ChooseOption 参数类型只打印一次
    private static bool _chooseOptionParamsLogged = false;

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
                // ── 核心：直接从 player 参数取 NetId，不再依赖 AsyncLocal ──
                // AsyncLocal 只在 HandleRequestEnqueueActionMessage 中设置，但卡牌升级走其他消息路径
                ulong playerNetId = GetPlayerNetId(player);

                // 诊断：打印 player 的 IsLocal 属性值
                object isLocalVal = null;
                try { isLocalVal = player?.GetType().GetProperty("IsLocal")?.GetValue(player); } catch { }
                bool isRemote = !Convert.ToBoolean(isLocalVal); // null → false（本地）
                DIAG($"[FULLTRACE] ChooseOption.Prefix playerNetId={playerNetId} IsLocal={isLocalVal} isRemote={isRemote} optionIndex={optionIndex} playerType={player?.GetType().Name ?? "null"}");

                if (!isRemote) {
                    DIAG($"[FULLTRACE] Skip: local player");
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

                DIAG($"PRE snapshot set: {safeName} netId={playerNetId} option={optionName} deck={GetDeckSummary(preSnapshot)}");
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
            ulong playerNetId = GetPlayerNetId(player);
            bool isRemote = _IsRemotePlayer(player);
            bool resultVal = __result?.Result == true;
            DIAG($"[FULLTRACE] ChooseOption.Postfix playerNetId={playerNetId} isRemote={isRemote} optionIndex={optionIndex} result={resultVal}");

            if (!resultVal) {
                DIAG($"[FULLTRACE] Skip: result=false");
                return;
            }

            try
            {
                // ── 核心：直接从 player 参数判断，不依赖 AsyncLocal ──
                if (!isRemote) {
                    DIAG($"[FULLTRACE] Postfix skip: local player");
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
                DIAG($"[FULLTRACE] POST for {safeName} netId={playerNetId} option={optionName} deck={GetDeckSummary(postSnapshot)}");

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

                        GD.Print($"[NCC] IMMEDIATE exploit detected for {safeName} "
                            + $"(netId={playerNetId}) at {optionName}: "
                            + $"expected {GetDeckSummary(preSnapshot)}, "
                            + $"got {GetDeckSummary(postSnapshot)}, "
                            + $"type: {cheatType}");

                        // 立即回滚：将卡组恢复到操作前状态
                        DIAG($"[FULLTRACE] Triggering rollback for {safeName}");
                        _RollbackPlayerDeck(player, preSnapshot);
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

                var receivedDeck = GetSerializableDeck(receivedPlayer);
                var receivedUpgraded = CountUpgraded(receivedDeck);
                var receivedCards = receivedDeck.Count;
                var receivedNetId = GetPlayerNetId(receivedPlayer);
                var realName = GetPlayerDisplayName(receivedPlayer) ?? $"#{senderId % 10000}";

                // ── 必须在覆盖字典之前抓取「上一轮」快照（回滚用）────────────────────
                object prevSerializableSnapshot = null;
                lock (_lastSerializablePlayer)
                    _lastSerializablePlayer.TryGetValue(senderId, out prevSerializableSnapshot);

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

                // 记录当前卡组大小，供 OnReceivePlayerChoice 使用（transform 前快照）
                lock (_lastSyncDeckSize) { _lastSyncDeckSize[senderId] = receivedCards; }

                // ── 检测作弊 ──
                // 逻辑：
                //   如果 ChooseOption 设置了 allowedDeltas → 本轮 sync 变化必须 <= allowedDeltas
                //   如果没有 optionId（ChooseOption 未触发）→ 任何变化都是可疑的
                bool cheated = false;
                string cheatType = null;

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

                // ── Transform/Reward 作弊检测（基于 OnReceivePlayerChoice 快照）────────
                // OnReceivePlayerChoice 时记录了 deckCards（transform 后的卡组快照）。
                // 当 SyncReceived 到来时：
                //   - 若 optionId 不为空：已被 allowedDeltas 覆盖，不重复检测
                //   - 若 optionId 为空（选卡类事件）：用快照检测 transform/reward 是否超量
                int preDeckSize = 0;
                int choiceCallCount = 0;
                lock (_pendingPreDeckSize)
                {
                    _pendingPreDeckSize.TryGetValue(senderId, out preDeckSize);
                    _choiceCallCount.TryGetValue(senderId, out choiceCallCount);
                    // 清理
                    _pendingPreDeckSize.Remove(senderId);
                    _choiceCallCount.Remove(senderId);
                }

                if (preDeckSize > 0 && string.IsNullOrEmpty(optionId))
                {
                    int deckSizeDelta = receivedCards - preDeckSize;
                    DIAG($"[FULLTRACE] TransformCheck preDeck={preDeckSize} received={receivedCards} delta={deckSizeDelta} choiceCalls={choiceCallCount}");

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
                        DIAG($"CHEAT from {realName} (calls={choiceCallCount} preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && deckSizeDelta == 1)
                    {
                        // 选了 2+ 张但只增了 1 张
                        cheated = true;
                        cheatType = $"reward_multi_select(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"CHEAT from {realName} (calls={choiceCallCount} preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && deckSizeDelta == -1)
                    {
                        // 选了 2+ 张但只删了 1 张
                        cheated = true;
                        cheatType = $"remove_multi_select(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"CHEAT from {realName} (calls={choiceCallCount} preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    else if (choiceCallCount >= 2 && Math.Abs(deckSizeDelta) > 1)
                    {
                        // 选了 2+ 张但变化超出合理范围
                        cheated = true;
                        cheatType = $"multi_select_excess(calls={choiceCallCount} delta={deckSizeDelta})";
                        DIAG($"CHEAT from {realName} (calls={choiceCallCount} preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    else if (choiceCallCount == 1 && deckSizeDelta > 1)
                    {
                        // 选了 1 张但多了 2+ 张
                        cheated = true;
                        cheatType = $"reward_excess(gained={deckSizeDelta})";
                        DIAG($"CHEAT from {realName} (preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    else if (choiceCallCount == 1 && deckSizeDelta < -1)
                    {
                        // 选了 1 张但删了 2+ 张
                        cheated = true;
                        cheatType = $"remove_excess(deleted={-deckSizeDelta})";
                        DIAG($"CHEAT from {realName} (preDeck={preDeckSize}→{receivedCards}): {cheatType}");
                    }
                    // choiceCallCount >= 2 且 delta == choiceCallCount - 1（正确执行了多次 transform/reward）：正常
                    // choiceCallCount == 1 且 delta in {-1, 0, 1}：正常
                }

                if (!cheated)
                {
                    DIAG($"Check passed for {realName}");
                    // 仅合法同步才推进「上一轮」Serializable 快照
                    lock (_lastSerializablePlayer) { _lastSerializablePlayer[senderId] = receivedPlayer; }
                    return;
                }

                // ── 作弊检测到！执行回滚 ──
                // 1) 记录（若已在 OnReceivePlayerChoice 瞬间弹过窗，则不再重复弹）
                bool skipNotify = false;
                lock (_immediateCheatNotifyTicks)
                {
                    if (_immediateCheatNotifyTicks.TryGetValue(senderId, out var notifyT)
                        && DateTime.Now.Ticks - notifyT <= TimeSpan.FromSeconds(10).Ticks)
                    {
                        skipNotify = true;
                        _immediateCheatNotifyTicks.Remove(senderId);
                    }
                }
                if (!skipNotify)
                    NoClientCheatsMod.RecordCheat(senderId, realName, optionId, $"deck:{cheatType}", true);
                else
                    DIAG($"[FULLTRACE] Skip duplicate RecordCheat (already notified at choice)");
                DIAG($"CHEAT from {realName}: {cheatType}");

                // 2) 使用 Postfix 开头抓取的 prevSerializableSnapshot（作弊前的合法 SerializablePlayer）
                if (prevSerializableSnapshot == null)
                {
                    DIAG($"[FULLTRACE] Rollback aborted: no prevSerializableSnapshot for {senderId} (first sync?)");
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

                // 3b) 主机上该玩家的「活」Player 实体也要 RestoreFromSerializable
                object livePlayer = _TryResolveLivePlayerByNetId(senderId);
                if (livePlayer == null)
                {
                    lock (_lastRemotePlayerByNetId)
                        _lastRemotePlayerByNetId.TryGetValue(senderId, out livePlayer);
                }
                if (livePlayer != null)
                {
                    DIAG($"[FULLTRACE] Rollback: RestoreFromSerializable on live Player netId={senderId}");
                    _RollbackPlayerDeck(livePlayer, syncDataCorrectSnapshot);
                }
                else
                    DIAG($"[FULLTRACE] Rollback: live Player not found for netId={senderId} (sync-only rollback)");

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

                // 5) 向副机发送纠正消息：强制副机用合法快照覆盖自己的状态
                //    副机收到后会更新本地数据并广播 SyncReceived，主机收到后进入合法分支
                DIAG($"[FULLTRACE] Rollback: sending SyncPlayerDataMessage to {senderId}");
                _SendRollback(__instance, senderId, syncDataCorrectSnapshot);

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
    /// </summary>
    private static void _PopulateSyncPlayerDataMessage(object msg, object correctSnapshot)
    {
        if (msg == null || correctSnapshot == null) return;
        foreach (var name in new[] { "player", "Player", "SerializablePlayer", "PlayerData", "Data" })
            _SetMemberAny(msg, name, correctSnapshot);
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
            try { field.SetValue(target, value); } catch { }
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
    /// 向指定客机发送回滚消息。
    /// 通过 INetGameService.SendMessage&lt;SyncPlayerDataMessage&gt;(msg, peerId) 实现定向发送。
    /// </summary>
    private static void _SendRollback(object synchronizer, ulong targetNetId, object correctSnapshot)
    {
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
            _PopulateSyncPlayerDataMessage(msg, correctSnapshot);

            if (_TrySendMessageToPeer(netService, msg, targetNetId))
            {
                GD.Print($"[NCC] Rollback sent to {targetNetId} (multi-path SendMessage ok)");
                return;
            }

            GD.PushError("[NCC] _SendRollback: no matching SendMessage/SendToPeer on net service");
        }
        catch (Exception ex) { GD.PushError($"[NCC] _SendRollback error: {ex}"); }
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
        var prop = target.GetType().GetProperty(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        if (prop != null) return prop.GetValue(target);
        var field = target.GetType().GetField(memberName,
            BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(target);
    }

    private static ulong GetPlayerNetId(object player)
    {
        var val = GetMemberValue(player, "NetId");
        return val != null ? Convert.ToUInt64(val) : 0;
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
        => GetMemberValue(syncMessage, "player");

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
            return $"{deck.Count} cards ({CountUpgraded(deck)} upgraded)";
        }
        catch { return "unknown"; }
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
        try
        {
            var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) return null;

            object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("CurrentRun", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            state ??= inst.GetType().GetProperty("RunState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            if (state == null) return null;

            object playersObj = state.GetType().GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state);
            playersObj ??= GetMemberValue(state, "_players");
            if (playersObj is not IEnumerable players) return null;

            foreach (var p in players)
            {
                if (p == null) continue;
                if (GetPlayerNetId(p) == netId) return p;
            }
        }
        catch { }
        return null;
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
    private static string _RollbackPlayerDeck(object player, object preSnapshot)
    {
        if (player == null || preSnapshot == null) return "null_player_or_snapshot";

        var t = player.GetType();
        LogDiag("Rollback", $"playerType={t.FullName} playerNetId={GetPlayerNetId(player)}");

        // ── 1. 枚举 Player 的所有公开/私有方法（不含属性），找 Restore* / Reset* / Sync* ──
        var restoreMethodNames = new[]
        {
            "RestoreFromSerializable", "RestoreStateFromSerializable",
            "RestorePlayerFromSerializable", "RestoreDeck",
            "ApplySerializableState", "ApplySnapshot",
            "SyncFromSerializable", "SyncDeckFromSerializable",
            "ResetDeckFromSerializable", "ReloadSerializableState",
        };

        foreach (var name in restoreMethodNames)
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null)
            {
                LogDiag("Rollback", $"Found method: {name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
                try
                {
                    var ps = m.GetParameters();
                    object[] args;
                    if (ps.Length == 1)
                        args = new[] { preSnapshot };
                    else if (ps.Length == 2 && ps[1].ParameterType == typeof(bool))
                        args = new[] { preSnapshot, true };
                    else
                        args = Array.Empty<object>();

                    m.Invoke(player, args);
                    LogDiag("Rollback", $"SUCCESS via {name}");
                    _TryNotifyPlayerDeckChanged(player);
                    return $"method:{name}";
                }
                catch (Exception ex)
                {
                    LogDiag("Rollback", $"{name} invoke failed: {ex.Message}");
                }
            }
        }

        // ── 2. 直接写 Deck 属性 ──
        var deckProp = t.GetProperty("Deck", BindingFlags.Public | BindingFlags.Instance);
        if (deckProp != null)
        {
            LogDiag("Rollback", $"Deck prop type={deckProp.PropertyType.Name} canWrite={deckProp.CanWrite}");
            try
            {
                var preDeck = GetSerializableDeck(preSnapshot);
                if (preDeck != null)
                {
                    LogDiag("Rollback", $"preDeck count={preDeck.Count}");
                    deckProp.SetValue(player, preDeck);
                    LogDiag("Rollback", "SUCCESS via Deck property");
                    _TryNotifyPlayerDeckChanged(player);
                    return "property:Deck";
                }
            }
            catch (Exception ex)
            {
                LogDiag("Rollback", $"Deck prop write failed: {ex.Message}");
            }
        }

        // ── 3. 找 private _deck / _mutableDeck 字段 ──
        foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!f.Name.Contains("Deck", StringComparison.OrdinalIgnoreCase)) continue;
            LogDiag("Rollback", $"Found field: {f.Name} type={f.FieldType.Name}");
            try
            {
                var preDeck = GetSerializableDeck(preSnapshot);
                if (preDeck != null)
                {
                    f.SetValue(player, preDeck);
                    LogDiag("Rollback", $"SUCCESS via field {f.Name}");
                    _TryNotifyPlayerDeckChanged(player);
                    return $"field:{f.Name}";
                }
            }
            catch (Exception ex)
            {
                LogDiag("Rollback", $"field {f.Name} write failed: {ex.Message}");
            }
        }

        // ── 4. 枚举 Player 所有属性，列出名称+类型 ──
        LogDiag("Rollback", $"All properties of {t.Name}:");
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            LogDiag("Rollback", $"  prop: {p.Name} = {GetMemberValue(player, p.Name)?.ToString() ?? "null"} ({p.PropertyType.Name})");
        }

        LogDiag("Rollback", $"All fields of {t.Name}:");
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            try
            {
                var v = f.GetValue(player);
                LogDiag("Rollback", $"  field: {f.Name} = {v?.ToString() ?? "null"} ({f.FieldType.Name})");
            }
            catch { LogDiag("Rollback", $"  field: {f.Name} = (read error)"); }
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
                if (EventRules.TryGetValue(actionType, out var rule))
                {
                    lock (_playerAllowedDelta)
                    {
                        _playerAllowedDelta[actionOwnerId] = rule;
                    }
                    DIAG($"[FULLTRACE] Set allowed delta for actionOwnerId={actionOwnerId} action={actionType}: card={rule.maxCardDelta} upgrade={rule.maxUpgradeDelta}");
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
    /// 记录 OnReceivePlayerChoice 最近一次调用的时间戳（用于去重同一选卡会话内的重复触发）
    /// </summary>
    private static long _lastChoiceTimestamp = 0;

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

            System.Reflection.MethodInfo target = null;
            foreach (var method in foundType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != "OnReceivePlayerChoice" || method.GetParameters().Length != 3)
                    continue;
                var ps = method.GetParameters();
                if (ps[1].ParameterType == typeof(uint))
                    target = method;
            }

            if (target == null)
            {
                DIAG("Prepare: OnReceivePlayerChoice(Player, uint, NetPlayerChoiceResult) 未找到，跳过");
                return false;
            }

            _playerChoiceTarget = target;
            var ps2 = target.GetParameters();
            DIAG($"Prepare: Hook {target.Name}: {string.Join(", ", ps2.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");

            // 枚举该类所有方法，列出签名
            DIAG($"Prepare: All methods on {foundType.Name}:");
            foreach (var m in foundType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    var mps = m.GetParameters();
                    DIAG($"  {m.Name}({string.Join(",", mps.Select(p => p.ParameterType.Name))})");
                }
                catch { }
            }

            // 枚举该类所有字段
            DIAG($"Prepare: All fields on {foundType.Name}:");
            foreach (var f in foundType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                DIAG($"  {f.Name}: {f.FieldType.Name}");
            }

            // 枚举该类所有属性
            DIAG($"Prepare: All properties on {foundType.Name}:");
            foreach (var p in foundType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                DIAG($"  {p.Name}: {p.PropertyType.Name}");
            }

            // 找含 NetPlayerChoiceResult 的方法
            try
            {
                foreach (var method in foundType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    foreach (var p in method.GetParameters())
                    {
                        if (!p.ParameterType.Name.Contains("NetPlayerChoiceResult", StringComparison.Ordinal))
                            continue;
                        DIAG($"Prepare: NetPlayerChoiceResult in {foundType.Name}.{method.Name}");
                        break;
                    }
                }
            }
            catch { /* ignore */ }

            return true;
        }

        static MethodBase TargetMethod() => _playerChoiceTarget;

        /// <summary>
        /// Prefix 纯诊断：打一行关键参数，帮助定位为什么 IsLocal/IsRemote/IsMultiplayerHost 判断失败。
        /// 所有拦截逻辑保留在 Postfix 中。
        /// </summary>
        [HarmonyPriority(int.MaxValue)]
        static bool Prefix(object __instance, object player, uint choiceId, object result)
        {
            if (player == null || result == null) return true;

            try
            {
                ulong playerId = GetPlayerNetId(player);
                bool isLocal = _IsLocalPlayer(player);
                bool isRemote = _IsRemotePlayer(player);
                ulong localNetId = TryGetLocalControllingNetId();
                bool host = NoClientCheatsMod.IsMultiplayerHost();
                bool inMp = IsInMultiplayerRun();

                DIAG($"[PREFIX] playerId={playerId} IsLocal={isLocal} IsRemote={isRemote} "
                    + $"localNetId={localNetId} host={host} inMp={inMp} "
                    + $"playerType={player.GetType().Name}");

                // 诊断：打印 player 的所有 bool 属性/字段
                foreach (var f in player.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (f.FieldType != typeof(bool) && f.FieldType != typeof(Boolean)) continue;
                    try { var v = f.GetValue(player); DIAG($"[PREFIX] player.{f.Name}={v}"); } catch { }
                }
                foreach (var p in player.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (p.PropertyType != typeof(bool) && p.PropertyType != typeof(Boolean)) continue;
                    try { var v = p.GetValue(player); DIAG($"[PREFIX] player.{p.Name}={v}"); } catch { }
                }

                // 始终让原方法执行；Postfix 里做真正的计数与作弊检测
                return true;
            }
            catch (Exception ex)
            {
                DIAG($"Prefix error: {ex.Message}");
                return true;
            }
        }

        static void Postfix(object __instance, object player, uint choiceId, object result)
        {
            if (player == null || result == null) return;

            try
            {
                ulong playerId = GetPlayerNetId(player);
                bool isRemote = _IsRemotePlayer(player);
                long now = DateTime.Now.Ticks;

                DIAG($"[POSTFIX] playerId={playerId} isRemote={isRemote} choiceId={choiceId}");

                // 记录 remote player 引用
                if (isRemote)
                    lock (_lastRemotePlayerByNetId) { _lastRemotePlayerByNetId[playerId] = player; }

                // ── 选卡会话计时：超过 30s 视为新一轮选卡 ───────────────────────────────
                bool isNewSession = (now - _lastChoiceTimestamp) > TimeSpan.FromSeconds(30).Ticks;
                _lastChoiceTimestamp = now;

                if (isNewSession)
                    lock (_choiceCallCount) { _choiceCallCount[playerId] = 0; }

                int newCount;
                lock (_choiceCallCount)
                {
                    _choiceCallCount.TryGetValue(playerId, out int prev);
                    newCount = prev + 1;
                    _choiceCallCount[playerId] = newCount;
                }

                DIAG($"[POSTFIX] newCount={newCount} isRemote={isRemote} playerId={playerId}");

                // ── 第二次回调 = 多选作弊 ───────────────────────────────────────────────
                if (newCount >= 2)
                {
                    var safeName = GetPlayerDisplayName(player) ?? $"#{playerId % 10000}";
                    const string cheatCmd = "deck:transform_multi_select(calls>=2,immediate)";

                    // 仅对远程玩家记录作弊（主机视角下只有副机才是作弊来源）
                    if (isRemote)
                    {
                        NoClientCheatsMod.RecordCheat(playerId, safeName, null, cheatCmd, true);
                        lock (_immediateCheatNotifyTicks) { _immediateCheatNotifyTicks[playerId] = DateTime.Now.Ticks; }
                    }
                    DIAG($"[POSTFIX] CHEAT DETECTED newCount={newCount} isRemote={isRemote} playerId={playerId}");

                    // 回滚：取第一次选卡后的合法快照（_lastSerializablePlayer）
                    object prevSnap = null;
                    lock (_lastSerializablePlayer)
                        _lastSerializablePlayer.TryGetValue(playerId, out prevSnap);

                    if (prevSnap == null)
                    {
                        DIAG($"[POSTFIX] prevSnap null, cannot rollback playerId={playerId}");
                    }
                    else
                    {
                        DIAG($"[POSTFIX] Attempting rollback playerId={playerId} snap={GetDeckSummary(prevSnap)}");

                        object rollbackSnap = TryCloneSerializableSnapshot(prevSnap) ?? prevSnap;

                        // 回滚到「活」的 Player 实例（RunManager.State.Players 中那个）
                        object livePlayer = _TryResolveLivePlayerByNetId(playerId);
                        if (livePlayer == null)
                        {
                            DIAG($"[POSTFIX] livePlayer not found by netId, using __instance player");
                            livePlayer = player;
                        }
                        else
                        {
                            DIAG($"[POSTFIX] found livePlayer for netId={playerId}");
                        }

                        // 用诊断版回滚，记录实际使用的策略
                        string rollbackStrategy = _RollbackPlayerDeck(livePlayer, rollbackSnap);
                        DIAG($"[POSTFIX] Rollback strategy={rollbackStrategy} playerId={playerId}");

                        // 方案 B：更新 CombatStateSynchronizer._syncData
                        var sync = SyncReceivedPatch.GetCachedSynchronizer()
                            ?? ResolveCombatStateSynchronizer(__instance);
                        if (sync != null)
                        {
                            SyncReceivedPatch.RememberSynchronizer(sync);
                            _ReplaceSyncData(sync, playerId, rollbackSnap);

                            // 仅主机对远程玩家才发网络消息
                            if (isRemote)
                                _SendRollback(sync, playerId, rollbackSnap);
                        }
                        else
                            GD.PushError("[NCC] Postfix rollback: CombatStateSynchronizer not resolved");

                        SyncReceivedPatch.SetBaselineFromSerializable(playerId, rollbackSnap);
                        NoClientCheatsMod.SetExpectedDeckSnapshot(playerId, rollbackSnap);

                        DIAG($"[POSTFIX] Rollback applied playerId={playerId}");
                    }

                    // 重置计数，下次从 1 开始
                    lock (_choiceCallCount) { _choiceCallCount[playerId] = 1; }
                }
                else
                {
                    // ── 第一次回调：记录当前卡组快照（供第二次回滚用）────────────────────
                    try
                    {
                        var toSer = player?.GetType().GetMethod("ToSerializable",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        object snap = toSer?.Invoke(player, null);
                        if (snap != null)
                        {
                            lock (_lastSerializablePlayer)
                                _lastSerializablePlayer[playerId] = snap;
                            DIAG($"[POSTFIX] Snap saved playerId={playerId} deck={GetDeckSummary(snap)}");
                        }
                    }
                    catch (Exception ex) { DIAG($"Snap save error: {ex.Message}"); }
                }

                // ── 记录 preDeckSize，供 SyncReceivedPatch 检测作弊（无论第几次）────────
                try
                {
                    var deckProp = result.GetType().GetProperty("deckCards",
                        BindingFlags.Public | BindingFlags.Instance);
                    object deckObj = deckProp?.GetValue(result);
                    int deckSize = 0;
                    if (deckObj is IList dl) deckSize = dl.Count;
                    if (deckSize > 0)
                    {
                        lock (_pendingPreDeckSize)
                            _pendingPreDeckSize[playerId] = deckSize;
                        DIAG($"[POSTFIX] preDeckSize={deckSize} for playerId={playerId}");
                    }
                }
                catch { }
            }
            catch (Exception ex) { DIAG($"Postfix error: {ex.Message}"); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch F: SyncReceived —— 利用 PlayerChoiceReceivePatch 的 transform 前快照
    //           检测 transform/reward/remove 类作弊：SyncReceived 的卡数与快照不符即为作弊
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<ulong, long>
        _syncCheckTimestamps = new();
}
