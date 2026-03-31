using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
    /// <summary>
    /// 多人玩家操作服务（骨架实现）：
    /// 
    /// 说明：游戏 API 中 Player 为只读对象、RunState.Players 为 IReadOnlyList，
    /// 因此直接实例化/修改 Player 不可行。此类提供操作记录和日志，真实实现
    /// 需要通过反射或游戏内部 Command 系统完成（参考 MODIFICATION_SCHEME.md）。
    /// 
    /// 对应 Python v2 `core.py` 的逻辑：
    /// - take_over_player()  → TakeOverPlayer(runState, idx, newSteamId)
    /// - add_player_copy()   → AddPlayerCopy(runState, idx, newSteamId)
    /// - add_player_fresh()  → AddPlayerFresh(runState, newSteamId, template)
    /// - remove_player()     → RemovePlayer(runState, idx)
    /// </summary>
    internal static class PlayerOpsService
    {
        /// <summary>
        /// 夺舍玩家：记录操作到日志。
        /// 真实实现需要通过反射修改 Player.NetId（只读属性需绕过）。
        /// </summary>
        internal static void TakeOverPlayer(RunState runState, int playerIndex, string newSteamId)
        {
            if (runState?.Players == null || playerIndex < 0 || playerIndex >= runState.Players.Count)
            {
                GD.PrintErr($"[MP_PlayerManager] TakeOverPlayer: invalid index {playerIndex}");
                return;
            }

            try
            {
                var player = runState.Players[playerIndex];
                string originalId = player.NetId ?? "?";
                GD.Print($"[MP_PlayerManager] TakeOverPlayer: would replace net_id '{originalId}' → '{newSteamId}' at index {playerIndex}");
                GD.Print($"  Player has {player.Creature?.MaxHp ?? 0} MaxHP, {player.Gold} Gold");
                // TODO: 反射修改 NetId（init-only 属性需通过 FieldInfo.SetValue）
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] TakeOverPlayer failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加玩家副本：记录操作到日志。
        /// IReadOnlyList 无法直接 Insert，需要通过 RunState 内部方法添加玩家。
        /// </summary>
        internal static void AddPlayerCopy(RunState runState, int insertIndex, string newSteamId, TemplateData template = null)
        {
            if (runState?.Players == null)
            {
                GD.PrintErr("[MP_PlayerManager] AddPlayerCopy: runState has no players.");
                return;
            }

            try
            {
                int targetIdx = Math.Max(0, Math.Min(insertIndex, runState.Players.Count));
                string sourceName = targetIdx < runState.Players.Count
                    ? (runState.Players[targetIdx].NetId ?? "?")
                    : (runState.Players.Count > 0 ? (runState.Players[0].NetId ?? "?") : "none");

                GD.Print($"[MP_PlayerManager] AddPlayerCopy: would insert copy of '{sourceName}' as '{newSteamId}' at index {targetIdx}");
                if (template != null)
                    GD.Print($"  Template: {template.Name} ({template.MaxHp} HP, {template.Gold} Gold, {template.CardIds.Count} cards)");
                // TODO: 通过游戏内部方法添加玩家（需研究 RunState.AddPlayer 或类似 API）
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] AddPlayerCopy failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加全新玩家：记录操作到日志。
        /// </summary>
        internal static void AddPlayerFresh(RunState runState, string newSteamId, TemplateData template = null)
        {
            if (runState?.Players == null)
            {
                GD.PrintErr("[MP_PlayerManager] AddPlayerFresh: runState has no players.");
                return;
            }

            try
            {
                GD.Print($"[MP_PlayerManager] AddPlayerFresh: would add new player with net_id '{newSteamId}', current count: {runState.Players.Count}");
                if (template != null)
                    GD.Print($"  Template: {template.Name} ({template.MaxHp} HP, {template.Gold} Gold, {template.CardIds.Count} cards, {template.RelicIds.Count} relics)");
                // TODO: 通过游戏内部方法添加玩家
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] AddPlayerFresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 移除玩家：记录操作到日志。
        /// </summary>
        internal static void RemovePlayer(RunState runState, int playerIndex)
        {
            if (runState?.Players == null || playerIndex < 0 || playerIndex >= runState.Players.Count)
            {
                GD.PrintErr($"[MP_PlayerManager] RemovePlayer: invalid index {playerIndex}");
                return;
            }

            try
            {
                var removed = runState.Players[playerIndex];
                string removedId = removed?.NetId ?? "?";
                GD.Print($"[MP_PlayerManager] RemovePlayer: would remove player '{removedId}' at index {playerIndex}, remaining: {runState.Players.Count - 1}");
                // TODO: 通过游戏内部方法移除玩家
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] RemovePlayer failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有玩家的 Steam ID 列表。
        /// </summary>
        internal static List<string> GetPlayerNetIds(RunState runState)
        {
            if (runState?.Players == null) return new List<string>();
            return runState.Players.Select(p => p.NetId ?? "").ToList();
        }

        /// <summary>
        /// 获取当前玩家数量。
        /// </summary>
        internal static int GetPlayerCount(RunState runState)
        {
            return runState?.Players?.Count ?? 0;
        }
    }
}