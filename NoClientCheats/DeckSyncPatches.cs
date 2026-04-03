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
            try
            {
                var receivedPlayer = GetSyncMessagePlayer(syncMessage);
                if (receivedPlayer == null) return;

                var receivedDeck = GetSerializableDeck(receivedPlayer);
                var receivedUpgraded = CountUpgraded(receivedDeck);
                var receivedCards = receivedDeck.Count;
                var receivedNetId = GetPlayerNetId(receivedPlayer);
                var realName = GetPlayerDisplayName(receivedPlayer) ?? $"#{senderId % 10000}";

                // 取出上一次 SyncReceived 的状态
                int prevCards = 0, prevUpgraded = 0;
                string optionId = null;
                bool hasPrev = false;
                int allowedCardDelta = 0, allowedUpgradeDelta = 0;
                lock (_lastSyncState)
                {
                    if (_lastSyncState.TryGetValue(senderId, out var prev))
                    {
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

                if (!cheated)
                {
                    DIAG($"Check passed for {realName}");
                    return;
                }

                // 作弊检测到！
                NoClientCheatsMod.RecordCheat(senderId, realName, optionId, $"deck:{cheatType}", true);
                DIAG($"CHEAT from {realName}: {cheatType}");
            }
            catch (Exception ex) { DIAG($"Postfix error: {ex.Message}"); }
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

            // 设置 msg.player = correctSnapshot（property 或 field 均可）
            SetMemberValue(msg, "player", correctSnapshot);

            // 找到 SendMessage<T>(T msg, ulong playerId) 方法
            var sendMethod = netService.GetType().GetMethod("SendMessage",
                new[] { msgType, typeof(ulong) });

            if (sendMethod != null)
            {
                sendMethod.Invoke(netService, new[] { msg, targetNetId });
                GD.Print($"[NCC] Rollback sent to {targetNetId}");
            }
            else
            {
                GD.PushError("[NCC] _SendRollback: SendMessage not found");
            }
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
        GD.Print($"[NCC|DIAG|{ts}] [{source}] {msg}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 立即回滚：将玩家卡组恢复到 preSnapshot 状态
    // ─────────────────────────────────────────────────────────────────────────

    private static void _RollbackPlayerDeck(object player, object preSnapshot)
    {
        try
        {
            // 方案：通过 Player.RestoreFromSerializable 恢复状态
            var restoreMethod = player.GetType().GetMethod("RestoreFromSerializable",
                BindingFlags.Public | BindingFlags.Instance);
            if (restoreMethod != null)
            {
                restoreMethod.Invoke(player, new[] { preSnapshot });
                GD.Print($"[NCC] Rollback applied via RestoreFromSerializable");
                return;
            }

            // 方案 2：直接写 Deck 字段
            var deckProp = player.GetType().GetProperty("Deck",
                BindingFlags.Public | BindingFlags.Instance);
            if (deckProp != null)
            {
                var preDeck = GetSerializableDeck(preSnapshot);
                if (preDeck != null)
                {
                    // 将 SerializableDeck 直接赋给 Deck 属性（游戏内部会处理转换）
                    deckProp.SetValue(player, preDeck);
                    GD.Print($"[NCC] Rollback applied via Deck property");
                }
            }
            else
            {
                GD.PushError("[NCC] _RollbackPlayerDeck: no RestoreFromSerializable or Deck property found");
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[NCC] _RollbackPlayerDeck error: {ex}");
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
}
