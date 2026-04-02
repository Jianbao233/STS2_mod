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
    // 诊断标志：确保 CombatStateSynchronizer 字段列表只打印一次
    private static bool _syncDataFieldsLogged = false;

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
            return t;
        }

        static void Prefix(object __instance, object player, int optionIndex)
        {
            try
            {
                // ── 核心判断：只对远程玩家的操作做快照 ──
                ulong remoteNetId = NoClientCheatsMod.GetCurrentRemotePlayer();
                DIAG($"Prefix: remoteNetId={remoteNetId} player={player?.GetType().Name ?? "null"} optionIndex={optionIndex}");
                if (remoteNetId == 0) return; // 本地玩家，不检测

                if (player == null) return;

                var netId = GetPlayerNetId(player);
                DIAG($"player.NetId={netId} remoteNetId={remoteNetId} match={netId == remoteNetId}");
                if (netId == 0) return;
                if (netId != remoteNetId) return; // 不是当前正在处理的远程玩家

                var toSerializable = player.GetType().GetMethod("ToSerializable",
                    BindingFlags.Public | BindingFlags.Instance);
                if (toSerializable == null) { DIAG("ToSerializable=null, returning"); return; }

                var preSnapshot = toSerializable.Invoke(player, null);
                NoClientCheatsMod.SetPreDeckSnapshot(netId, preSnapshot);

                var safeName = GetPlayerDisplayName(player) ?? $"#{netId % 10000}";
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

                DIAG($"PRE snapshot set: {safeName} netId={netId} option={optionName} deck={GetDeckSummary(preSnapshot)}");
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
            DIAG($"Postfix: remoteNetId={NoClientCheatsMod.GetCurrentRemotePlayer()} result={__result?.Result} player={player?.GetType().Name ?? "null"} optionIndex={optionIndex}");
            if (__result?.Result != true) return; // 仅在选项执行成功时记录快照

            try
            {
                // ── 核心判断：只对远程玩家的操作做检测 ──
                ulong remoteNetId = NoClientCheatsMod.GetCurrentRemotePlayer();
                if (remoteNetId == 0) { DIAG("Postfix: local player, skipping"); return; }
                if (player == null) return;

                var netId = GetPlayerNetId(player);
                DIAG($"Postfix player.NetId={netId} remoteNetId={remoteNetId} match={netId == remoteNetId}");
                if (netId == 0) return;
                if (netId != remoteNetId) { DIAG("Postfix: not current remote player, skipping"); return; }

                var safeName = GetPlayerDisplayName(player) ?? $"#{netId % 10000}";

                var toSerializable = player.GetType().GetMethod("ToSerializable",
                    BindingFlags.Public | BindingFlags.Instance);
                if (toSerializable == null) { DIAG("ToSerializable=null, returning"); return; }

                var postSnapshot = toSerializable.Invoke(player, null);

                // 1. 保留原有逻辑：记录 post 快照（用于 sync 备用）
                NoClientCheatsMod.SetExpectedDeckSnapshot(netId, postSnapshot);

                var optionName = GetOptionIdAtIndex(__instance, optionIndex);
                DIAG($"POST snapshot set: {safeName} netId={netId} option={optionName} deck={GetDeckSummary(postSnapshot)}");

                // 2. 立即对比：操作前快照 vs 操作后快照（核心检测）
                var preSnapshot = NoClientCheatsMod.ConsumePreDeckSnapshot(netId);
                DIAG($"ConsumePreDeckSnapshot({netId}) => {(preSnapshot != null ? "found " + GetDeckSummary(preSnapshot) : "null")}");
                if (preSnapshot != null)
                {
                    bool matches = _DecksMatch(preSnapshot, postSnapshot);
                    DIAG($"_DecksMatch={matches}");
                    if (!matches)
                    {
                        var cheatType = _DetectCheatType(preSnapshot, postSnapshot);
                        NoClientCheatsMod.RecordCheat(
                            netId, safeName, null,
                            $"ui_exploit:{cheatType}", true);

                        GD.Print($"[NCC] IMMEDIATE exploit detected for {safeName} "
                            + $"(netId={netId}) at {optionName}: "
                            + $"expected {GetDeckSummary(preSnapshot)}, "
                            + $"got {GetDeckSummary(postSnapshot)}, "
                            + $"type: {cheatType}");

                        // 立即回滚：将卡组恢复到操作前状态
                        DIAG($"Triggering rollback for {safeName}");
                        _RollbackPlayerDeck(player, preSnapshot);
                    }
                }
                else
                {
                    // 无 pre 快照说明不是通过 ChooseOption 触发的，跳过即时检测
                    DIAG($"No pre-snapshot — skipping immediate check");
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

    // ─── Patch B: CombatStateSynchronizer.OnSyncPlayerMessageReceived ─────────
    // 时机：主机收到客机的 SyncPlayerDataMessage 时
    // ─── Patch C: CombatStateSynchronizer.OnSyncPlayerMessageReceived ───────────
    // 时机：主机收到客机的 SyncPlayerDataMessage 时
    // 逻辑：在消息被处理前，取客机的当前卡组作为期望值，与消息中的数据进行对比
    [HarmonyPatch]
    private static class SyncReceivedPrefix
    {
        static void DIAG(string msg) => LogDiag("SyncReceived", msg);

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

        static void Prefix(object __instance, object syncMessage, ulong senderId)
        {
            if (syncMessage == null) return;

            try
            {
                // ── Step 1：获取消息中的玩家卡组数据 ──
                var receivedPlayer = GetSyncMessagePlayer(syncMessage);
                if (receivedPlayer == null) { DIAG("receivedPlayer=null"); return; }

                var receivedDeck = GetSerializableDeck(receivedPlayer);
                var receivedUpgraded = CountUpgraded(receivedDeck);
                DIAG($"Sync from senderId={senderId}: {receivedDeck.Count} cards, {receivedUpgraded} upgraded");

                // ── Step 2：通过 INetGameService.GetPlayer(senderId) 获取客机的实时卡组 ──
                var senderDeck = GetRemotePlayerCurrentDeck(__instance, senderId);
                if (senderDeck == null) { DIAG("senderDeck=null (GetPlayer failed)"); return; }

                var senderSerializable = senderDeck.GetType().GetMethod("ToSerializable",
                    BindingFlags.Public | BindingFlags.Instance)?.Invoke(senderDeck, null);
                if (senderSerializable == null) { DIAG("ToSerializable failed"); return; }

                var expectedDeck = GetSerializableDeck(senderSerializable);
                var expectedUpgraded = CountUpgraded(expectedDeck);
                var senderName = GetPlayerDisplayName(senderDeck) ?? $"#{senderId % 10000}";
                DIAG($"Expected from NetGameService: {expectedDeck.Count} cards, {expectedUpgraded} upgraded");

                // ── Step 3：对比检测 ──
                // 如果消息中的卡数 > 服务端的卡数（多了卡）：add_excess
                // 如果消息中的卡数 < 服务端的卡数（少了卡）：remove_excess
                // 如果消息中升级数 > 服务端的升级数（多了升级）：upgrade_excess
                // 如果卡组完全一致：正常
                bool isExcess = false;
                string cheatType = null;

                if (receivedDeck.Count > expectedDeck.Count)
                {
                    isExcess = true;
                    cheatType = $"add_excess({receivedDeck.Count - expectedDeck.Count})";
                }
                else if (receivedDeck.Count < expectedDeck.Count)
                {
                    isExcess = true;
                    cheatType = $"remove_excess({expectedDeck.Count - receivedDeck.Count})";
                }
                else if (receivedUpgraded > expectedUpgraded)
                {
                    isExcess = true;
                    cheatType = $"upgrade_excess({receivedUpgraded - expectedUpgraded})";
                }

                if (!isExcess)
                {
                    DIAG($"Check passed for {senderName}: decks match");
                    return;
                }

                // ── Step 4：作弊检测到！──
                NoClientCheatsMod.RecordCheat(
                    senderId, senderName, null,
                    $"deck:{cheatType}", true);

                DIAG($"CHEAT DETECTED from {senderName} (senderId={senderId}): {cheatType}");

                // 发送回滚消息
                _SendRollback(__instance, senderId, senderSerializable);
                _ReplaceSyncData(__instance, senderId, senderSerializable);
            }
            catch (Exception ex)
            {
                GD.PushError($"[NCC] SyncReceived Prefix error: {ex}");
                DIAG($"Exception: {ex.Message}");
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

    /// <summary>
    /// 获取远程玩家的当前 Player 对象。
    /// 策略1：从 CombatStateSynchronizer 的字段/属性中找 Player 集合
    /// 策略2：从 syncMessage 自身获取（player 字段）
    /// </summary>
    private static object GetRemotePlayerCurrentDeck(object synchronizer, ulong senderId)
    {
        try
        {
            // ── 策略1：枚举 CombatStateSynchronizer 的所有字段（一次性诊断）──
            if (!_syncDataFieldsLogged)
            {
                _syncDataFieldsLogged = true;
                foreach (var f in synchronizer.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    LogDiag("SyncReceived", $"  CSS field: {f.FieldType.Name} {f.Name}");
                    // 打印集合内容的类型（如果是 IDictionary/IList）
                    try {
                        var val = f.GetValue(synchronizer);
                        if (val is IDictionary d)
                        {
                            LogDiag("SyncReceived", $"    -> dict keys: {string.Join(",", d.Keys.Cast<object>().Take(5))}");
                            foreach (var k in d.Keys)
                                LogDiag("SyncReceived", $"    -> [{k}] = {d[k]?.GetType().Name}");
                        }
                    } catch { }
                }
                foreach (var p in synchronizer.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    LogDiag("SyncReceived", $"  CSS prop: {p.PropertyType.Name} {p.Name}");
                }
            }

            // ── 策略2：从同步消息自身取 player ──
            // syncMessage.player 就是发送方客机的 Player 对象（我们已有）
            // 但需要找另一个调用点传入 syncMessage 参数……
            // 这里我们换个思路：用 CombatStateSynchronizer._runState._players[senderId]
            try {
                var runStateField = AccessTools.Field(synchronizer.GetType(), "_runState");
                var runState = runStateField?.GetValue(synchronizer);
                if (runState != null)
                {
                    var playersField = runState.GetType().GetField("_players", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var players = playersField?.GetValue(runState) as IDictionary;
                    if (players != null && players.Contains(senderId))
                    {
                        var result = players[senderId];
                        LogDiag("SyncReceived", $"Found player via _runState._players: {result?.GetType().Name}");
                        return result;
                    }
                    LogDiag("SyncReceived", $"_runState._players: keys={players?.Count ?? -1}, contains={players?.Contains(senderId)}");
                }
            } catch (Exception ex) {
                LogDiag("SyncReceived", $"_runState._players error: {ex.Message}");
            }

            // ── 策略3：直接枚举所有已知 Player 相关字段 ──
            foreach (var f in synchronizer.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!f.FieldType.Name.Contains("Player") &&
                    !f.FieldType.Name.Contains("Dictionary") &&
                    !f.FieldType.Name.Contains("List")) continue;
                try {
                    var val = f.GetValue(synchronizer);
                    if (val is IDictionary d && d.Contains(senderId))
                    {
                        var result = d[senderId];
                        LogDiag("SyncReceived", $"Found via dict field {f.Name}: {result?.GetType().Name}");
                        return result;
                    }
                    if (val is IList l && senderId < (uint)l.Count)
                    {
                        var result = l[(int)senderId];
                        LogDiag("SyncReceived", $"Found via list field {f.Name}[{senderId}]: {result?.GetType().Name}");
                        return result;
                    }
                } catch { }
            }

            LogDiag("SyncReceived", "GetRemotePlayer: all strategies failed");
            return null;
        }
        catch (Exception ex)
        {
            LogDiag("SyncReceived", $"GetRemotePlayer error: {ex.Message}");
            return null;
        }
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
}
