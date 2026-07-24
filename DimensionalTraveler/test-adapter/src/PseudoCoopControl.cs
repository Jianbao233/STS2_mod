using System.Reflection;
using System.Text.Json.Nodes;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Characters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DimensionalTraveler.TestAdapter;

internal static class PseudoCoopControl
{
    private const int ExpectedPlayerCount = 2;
    private const int LaunchTimeoutSeconds = 90;

    public static async Task<JsonNode> Start(JsonObject args)
    {
        var contract = ResolveContract();
        var character = ModelDb.Character<Traveler>();
        var options = Activator.CreateInstance(contract.OptionsType)
            ?? throw Incompatible("无法创建 PseudoCoopLobbyHost.LaunchOptions");

        SetOption(contract.OptionsType, options, "Character", character);
        SetOption(contract.OptionsType, options, "PhantomCharacter", character);
        SetOption(contract.OptionsType, options, "Seed", null);
        SetOption(contract.OptionsType, options, "SyncBotEnabled", true);
        SetOption(contract.OptionsType, options, "SpawnPhantomPlayer", true);
        SetOption(contract.OptionsType, options, "SyncBotAutoEndTurn", true);
        SetOption(contract.OptionsType, options, "MpAiTeammateEnabled", false);
        SetOption(contract.OptionsType, options, "AutoPresetOnLaunch", false);

        var invocation = contract.StartMethod.Invoke(null, [options]) as Task
            ?? throw Incompatible("TryStartAsync 未返回 Task");
        await invocation;

        var result = invocation.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(invocation)
            ?? throw Incompatible("TryStartAsync Task 缺少 Result");
        var resultType = result.GetType();
        var ok = resultType.GetField("Item1")?.GetValue(result) as bool? ?? false;
        var error = resultType.GetField("Item2")?.GetValue(result) as string;
        return ok
            ? TestToolResult.Ok(new JsonObject { ["mode"] = "standard" })
            : TestToolResult.Fail(error ?? "KitLib 伪联机启动失败。", "pseudo_coop_launch_failed");
    }

    public static JsonNode InspectPlayers()
    {
        var runManager = RunManager.Instance;
        var state = runManager?.DebugOnlyGetState();
        var players = new JsonArray();
        if (state is not null)
        {
            foreach (var player in state.Players.OrderBy(static player => player.NetId))
                players.Add(CapturePlayer(player));
        }

        return TestToolResult.Ok(new JsonObject
        {
            ["runActive"] = runManager?.IsInProgress == true,
            ["netType"] = runManager?.NetService?.Type.ToString(),
            ["localNetId"] = runManager?.NetService?.NetId.ToString(),
            ["roomType"] = state?.CurrentRoom?.RoomType.ToString(),
            ["playerCount"] = players.Count,
            ["players"] = players,
        });
    }

    public static async Task<JsonNode> EnterTestCombat()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(LaunchTimeoutSeconds);
        RunState? state = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            state = RunManager.Instance?.DebugOnlyGetState();
            if (state is not null
                && state.Players.Count >= ExpectedPlayerCount
                && state.CurrentMapPointHistoryEntry is not null
                && RunManager.Instance?.EventSynchronizer.Events.Count >= ExpectedPlayerCount)
            {
                break;
            }
            await Task.Delay(200);
        }

        if (state is null
            || state.Players.Count < ExpectedPlayerCount
            || state.CurrentMapPointHistoryEntry is null
            || RunManager.Instance?.EventSynchronizer.Events.Count < ExpectedPlayerCount)
        {
            return TestToolResult.Fail(
                $"KitLib 伪联机未在 {LaunchTimeoutSeconds} 秒内形成双玩家事件状态。",
                "pseudo_coop_roster_timeout");
        }
        if (state.Players.Any(static player => !AlchemyCombatState.IsTraveler(player)))
            return TestToolResult.Fail("伪联机名册中存在非次元旅人角色。", "pseudo_coop_character_mismatch");

        var runManager = RunManager.Instance;
        if (runManager is null)
            return TestToolResult.Fail("RunManager 尚未初始化。", "run_manager_unavailable");

        try
        {
            await runManager.EnterRoomDebug(
                RoomType.Monster,
                model: null,
                showTransition: false);
        }
        catch (Exception exception)
        {
            return TestToolResult.Fail(exception.ToString(), "pseudo_coop_enter_combat_failed");
        }

        var combatRunState = runManager.DebugOnlyGetState();
        if (combatRunState is null
            || combatRunState.Players.Count < ExpectedPlayerCount
            || combatRunState.Players.Any(static player => player.Creature.CombatState is null))
        {
            return TestToolResult.Fail("双玩家名册未完整进入共享战斗。", "pseudo_coop_combat_unavailable");
        }

        var playPhaseDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < playPhaseDeadline
               && combatRunState.Players.Any(static player =>
                   player.PlayerCombatState?.Phase.ToString() != "Play"))
        {
            await Task.Delay(100);
        }
        if (combatRunState.Players.Any(static player =>
                player.PlayerCombatState?.Phase.ToString() != "Play"))
        {
            return TestToolResult.Fail(
                "双玩家未在 30 秒内进入稳定 Play 阶段。",
                "pseudo_coop_play_phase_timeout");
        }

        var players = new JsonArray(combatRunState.Players
            .OrderBy(static player => player.NetId)
            .Select(player => (JsonNode?)CapturePlayer(player))
            .ToArray());
        return TestToolResult.Ok(new JsonObject
        {
            ["playerCount"] = players.Count,
            ["players"] = players,
            ["enemyCount"] = combatRunState.Players[0].Creature.CombatState?.Enemies.Count,
        });
    }

    public static Task<JsonNode> ApplyPlayerFixture(JsonObject args)
    {
        if (!TryGetPlayer(args, "apply_player_fixture", out var player, out var error))
            return Task.FromResult(error);
        return ScenarioFixture.Apply(player, args["fixture"] as JsonObject ?? new JsonObject());
    }

    public static async Task<JsonNode> PlayPlayerCard(JsonObject args)
    {
        if (!TryGetPlayer(args, "play_player_card", out var player, out var error))
            return error;

        var cardId = args["card_id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(cardId))
            return TestToolResult.Fail("play_player_card 需要 card_id。", "missing_card_id");

        var hand = PileType.Hand.GetPile(player)?.Cards;
        var card = hand?.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
        if (card is null)
            return TestToolResult.Fail($"NetId={player.NetId} 手牌中找不到 {cardId}。", "card_not_found");

        var targetId = args["target_combat_id"]?.GetValue<uint?>();
        var target = targetId.HasValue
            ? player.Creature.CombatState?.GetCreature(targetId)
            : null;
        if (targetId.HasValue && target is null)
            return TestToolResult.Fail($"找不到 combatId={targetId} 的目标。", "target_not_found");
        if (!card.CanPlayTargeting(target))
            return TestToolResult.Fail($"{card.Id.Entry} 当前不能对所选目标打出。", "card_not_playable");

        var playAction = new PlayCardAction(
            player,
            NetCombatCard.FromModel(card),
            card.Id,
            target?.CombatId);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        playAction.BeforeCancelled += _ => cancelled.TrySetResult();
        RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(playAction);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (playAction.State == GameActionState.GatheringPlayerChoice
                && player.NetId != RunManager.Instance.NetService.NetId)
            {
                RunManager.Instance.ActionQueueSynchronizer
                    .RequestResumeActionAfterPlayerChoice(playAction);
            }

            var completed = await Task.WhenAny(
                playAction.CompletionTask,
                cancelled.Task,
                Task.Delay(50));
            if (completed == playAction.CompletionTask || completed == cancelled.Task)
                break;
        }

        if (cancelled.Task.IsCompleted || playAction.State == GameActionState.Canceled)
            return TestToolResult.Fail($"{card.Id.Entry} 的原生出牌动作被取消。", "play_cancelled");
        if (!playAction.CompletionTask.IsCompleted)
            return TestToolResult.Fail(
                $"{card.Id.Entry} 的原生出牌动作在 30 秒内未完成，state={playAction.State}。",
                "play_timeout");
        if (playAction.Exception is not null)
            return TestToolResult.Fail(playAction.Exception.ToString(), "play_failed");

        return TestToolResult.Ok(new JsonObject
        {
            ["playerNetId"] = player.NetId.ToString(),
            ["cardId"] = card.Id.Entry,
            ["targetCombatId"] = target?.CombatId,
            ["actionId"] = playAction.Id,
            ["state"] = playAction.State.ToString(),
        });
    }

    private static bool TryGetPlayer(
        JsonObject args,
        string action,
        out Player player,
        out JsonNode error)
    {
        player = null!;
        var requested = args["player_net_id"]?.GetValue<long>() ?? -1;
        if (requested < 0)
        {
            error = TestToolResult.Fail($"{action} 需要非负 player_net_id。", "missing_player_net_id");
            return false;
        }

        var state = RunManager.Instance?.DebugOnlyGetState();
        player = state?.Players.FirstOrDefault(candidate => candidate.NetId == (ulong)requested)!;
        if (player is null)
        {
            error = TestToolResult.Fail($"找不到 NetId={requested} 的玩家。", "player_not_found");
            return false;
        }
        if (!AlchemyCombatState.IsTraveler(player))
        {
            error = TestToolResult.Fail($"NetId={requested} 不是次元旅人。", "character_mismatch");
            return false;
        }
        if (player.Creature.CombatState is null)
        {
            error = TestToolResult.Fail($"NetId={requested} 当前不在战斗中。", "combat_unavailable");
            return false;
        }

        error = new JsonObject();
        return true;
    }

    private static JsonObject CapturePlayer(Player player) => new()
    {
        ["netId"] = player.NetId.ToString(),
        ["characterId"] = player.Character.Id.Entry,
        ["isTraveler"] = AlchemyCombatState.IsTraveler(player),
        ["inCombat"] = player.Creature.CombatState is not null,
        ["combatId"] = player.Creature.CombatId,
        ["currentHp"] = player.Creature.CurrentHp,
        ["block"] = player.Creature.Block,
        ["stateAttached"] = player.Creature.GetPower<AlchemyCombatStatePower>() is not null,
    };

    private static PseudoCoopContract ResolveContract()
    {
        var hostType = AccessTools.TypeByName("KitLib.Multiplayer.PseudoCoop.PseudoCoopLobbyHost")
            ?? throw Incompatible("找不到 PseudoCoopLobbyHost；请确认 KitLib.AI 已加载");
        var optionsType = AccessTools.Inner(hostType, "LaunchOptions")
            ?? throw Incompatible("找不到 PseudoCoopLobbyHost.LaunchOptions");
        var startMethod = AccessTools.Method(hostType, "TryStartAsync", [optionsType]);
        if (startMethod is null || !typeof(Task).IsAssignableFrom(startMethod.ReturnType))
            throw Incompatible("TryStartAsync(LaunchOptions) -> Task 签名不存在");
        return new PseudoCoopContract(optionsType, startMethod);
    }

    private static void SetOption(Type optionsType, object options, string name, object? value)
    {
        var property = optionsType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw Incompatible($"LaunchOptions.{name} 不存在");
        property.SetValue(options, value);
    }

    private static NotSupportedException Incompatible(string detail)
    {
        var assembly = AccessTools.TypeByName("KitLib.Multiplayer.PseudoCoop.PseudoCoopLobbyHost")
            ?.Assembly.GetName();
        return new NotSupportedException(
            $"DimensionalTraveler.TestAdapter 与当前 KitLib 伪联机接口不兼容：{detail}；KitLib.AI={assembly?.Version?.ToString() ?? "unknown"}。");
    }

    private sealed record PseudoCoopContract(Type OptionsType, MethodInfo StartMethod);
}