using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// NetId 修正的注册 API，不再 Patch 任何 Serialize 方法。
/// 真正的修正在 <c>SerializablePlayerNetIdMismatchPatch</c> 中——
/// 找游戏的 NetId 不匹配检查点，在收到 NCC 回滚消息后跳过检查。
/// </summary>
internal static class NetIdFixTranspiler
{
    private static readonly object _lock = new();

    /// <summary>作弊玩家 NetId（副机 NetId）。收到回滚消息时用于告知游戏该 NetId 的消息是合法的。</summary>
    private static readonly Dictionary<ulong, ulong> _registeredFixes = new();

    /// <summary>
    /// 注册一个回滚修正：作弊玩家 (playerNetId) 的回滚消息即将到达。
    /// SerializablePlayerNetIdMismatchPatch 会用此信息跳过检查。
    /// </summary>
    internal static void RegisterFix(ulong playerNetId, ulong senderId)
    {
        lock (_lock)
        {
            _registeredFixes[playerNetId] = senderId;
        }
        ThreadSafeLog($"[NetIdFix] Registered fix: playerNetId={playerNetId} senderId={senderId}");
    }

    /// <summary>清除修正。</summary>
    internal static void ClearFix(ulong playerNetId)
    {
        lock (_lock)
        {
            _registeredFixes.Remove(playerNetId);
        }
        ThreadSafeLog($"[NetIdFix] Cleared fix for playerNetId={playerNetId}");
    }

    /// <summary>
    /// 检查给定 (msgPlayerNetId, senderId) 组合是否是已注册的回滚修正。
    /// </summary>
    internal static bool IsRegisteredFix(ulong msgPlayerNetId, ulong senderId)
    {
        lock (_lock)
        {
            bool result = _registeredFixes.TryGetValue(msgPlayerNetId, out var expectedSender)
                && expectedSender == senderId;
            if (result)
                ThreadSafeLog($"[NetIdFix] IsRegisteredFix({msgPlayerNetId},{senderId})=TRUE");
            return result;
        }
    }

    private static void ThreadSafeLog(string line)
    {
        try { NoClientCheatsMod.ThreadSafeLog(line); } catch { }
    }
}
