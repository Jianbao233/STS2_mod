using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Godot;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.Extraction;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;
using KitLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.SecondaryResources;

namespace DimensionalTraveler.TestAdapter;

internal static class ControlTool
{
    public static TestToolSchema Schema { get; } = new(
        McpIntegration.ControlToolName,
        "Set deterministic DimensionalTraveler combat state for acceptance tests.",
        """
        {
          "type": "object",
          "properties": {
            "action": {
              "type": "string",
              "enum": ["start_test_run", "enter_test_combat", "inspect_lan_lobby", "inspect_lan_run", "capture_lan_snapshot", "disable_lan_auto_driver", "proceed_lan_event", "select_lan_traveler_and_ready", "enter_lan_test_combat", "inspect_merchant_potion_price", "inspect_run_player_round_trip", "inspect_shared_potion_pool", "inspect_extraction_catalog", "grant_native_potion", "grant_test_card", "discard_native_potion", "extract_native_potion", "play_local_card", "force_end_player_turn", "grant_run_relic", "set_run_player_hp", "start_test_combat", "reset_scenario", "start_pseudo_coop", "inspect_players", "enter_pseudo_coop_test_combat", "apply_fixture", "apply_player_fixture", "play_player_card", "grant_relic", "grant_player_relic", "grant_player_native_potion", "extract_player_native_potion", "inspect_catalog", "set_principles", "clear_backpack", "brew_potion", "move_backpack_potion_to_hand", "reset_turn", "clear_payment_audit", "clear_extraction_audit", "set_enemy_hp", "set_enemy_block"]
            },
            "seed": { "type": "string" },
            "player_net_id": { "type": "integer" },
            "card_id": { "type": "string" },
            "relic_id": { "type": "string" },
            "target_combat_id": { "type": "integer" },
            "fixture": { "type": "object" },
            "principles": { "type": "object", "additionalProperties": { "type": "integer" } },
            "family": { "type": "string" },
            "quality": { "type": "string" },
            "upgraded": { "type": "boolean" },
            "return_when_gathering_choice": { "type": "boolean" },
            "return_when_targeting": { "type": "boolean" },
            "origin": { "type": "string" },
            "backpack_index": { "type": "integer" },
            "potion_id": { "type": "string" },
            "potion_slot_index": { "type": "integer" },
            "enemy_index": { "type": "integer" },
            "hp": { "type": "integer" },
            "block": { "type": "integer" }
          },
          "required": ["action"]
        }
        """);

    public static async Task<JsonNode> Execute(JsonObject args)
    {
        var action = args["action"]?.GetValue<string>()?.Trim().ToLowerInvariant();
        if (action == "start_test_run")
            return await StartTestRun(args);
        if (action == "enter_test_combat")
            return await EnterTestCombat();
        if (action == "inspect_lan_lobby")
            return InspectLanLobby();
        if (action == "inspect_lan_run")
            return InspectLanRun();
        if (action == "capture_lan_snapshot")
            return CaptureLanSnapshot();
        if (action == "disable_lan_auto_driver")
            return DisableLanAutoDriver();
        if (action == "proceed_lan_event")
            return await ProceedLanEvent();
        if (action == "select_lan_traveler_and_ready")
            return SelectLanTravelerAndReady();
        if (action == "enter_lan_test_combat")
            return await PseudoCoopControl.EnterTestCombat();
        if (action == "inspect_merchant_potion_price")
            return InspectMerchantPotionPrice();
        if (action == "inspect_run_player_round_trip")
            return InspectRunPlayerRoundTrip();
        if (action == "inspect_shared_potion_pool")
            return InspectSharedPotionPool();
        if (action == "inspect_extraction_catalog")
            return InspectExtractionCatalog();
        if (action == "grant_run_relic")
            return await GrantRunRelic(args);
        if (action == "set_run_player_hp")
            return SetRunPlayerHp(args);

        if (action is "start_test_combat" or "reset_scenario")
        {
            var started = await StartTestCombat(args);
            var startedOk = started is JsonObject startedObject
                && startedObject["ok"]?.GetValue<bool>() == true;
            if (action == "start_test_combat" || !startedOk)
                return started;
            if (!TryGetLocalTraveler(out var startedPlayer, out var startedError))
                return startedError;
            return await ScenarioFixture.Apply(startedPlayer, args["fixture"] as JsonObject ?? new JsonObject());
        }

        if (action == "start_pseudo_coop")
            return await PseudoCoopControl.Start(args);
        if (action == "inspect_players")
            return PseudoCoopControl.InspectPlayers();
        if (action == "enter_pseudo_coop_test_combat")
            return await PseudoCoopControl.EnterTestCombat();
        if (action == "apply_player_fixture")
            return await PseudoCoopControl.ApplyPlayerFixture(args);
        if (action == "play_player_card")
            return await PseudoCoopControl.PlayPlayerCard(args);
        if (action == "grant_player_relic")
            return await PseudoCoopControl.GrantPlayerRelic(args);
        if (action == "grant_player_native_potion")
        {
            if (!PseudoCoopControl.IsActive)
            {
                return TestToolResult.Fail(
                    "真实 LAN 不允许跨玩家直接注入药水；请由药水所有者调用 grant_native_potion。",
                    "cross_owner_grant_forbidden");
            }
            return await PseudoCoopControl.GrantPlayerNativePotion(args);
        }
        if (action == "extract_player_native_potion")
            return await PseudoCoopControl.ExtractPlayerNativePotion(args);

        if (!TryGetLocalTraveler(out var player, out var error))
            return error;

        return action switch
        {
            "apply_fixture" => await ScenarioFixture.Apply(player, args["fixture"] as JsonObject ?? new JsonObject()),
            "grant_relic" => await RelicTestControl.Grant(player, args),
            "inspect_catalog" => InspectCatalog(),
            "grant_native_potion" => await GrantNativePotion(player, args),
            "grant_test_card" => GrantTestCard(player, args),
            "discard_native_potion" => DiscardNativePotion(player, args),
            "extract_native_potion" => await ExtractNativePotion(player, args),
            "play_local_card" => await PlayLocalCard(player, args),
            "force_end_player_turn" => ForceEndPlayerTurn(player),
            "set_principles" => await SetPrinciples(player, args),
            "clear_backpack" => await ClearBackpack(player),
            "brew_potion" => await BrewPotion(player, args),
            "move_backpack_potion_to_hand" => await MoveBackpackPotionToHand(player, args),
            "reset_turn" => ResetTurn(player),
            "clear_payment_audit" => ClearPaymentAudit(),
            "clear_extraction_audit" => ClearExtractionAudit(),
            "set_enemy_hp" => SetEnemyHp(player, args),
            "set_enemy_block" => SetEnemyBlock(player, args),
            _ => TestToolResult.Fail($"未知 action：{action ?? "<null>"}。", "invalid_action"),
        };
    }

    private static JsonNode InspectLanLobby()
    {
        if (!TryGetLanCharacterSelect(out var screen, out var error))
            return error;
        return TestToolResult.Ok(CaptureLanLobby(screen));
    }

    private static JsonArray CaptureLanEventButtons(Node? eventRoom) =>
        new(eventRoom is null
            ? []
            : Descendants(eventRoom)
                .OfType<NEventOptionButton>()
                .Select(button => (JsonNode?)new JsonObject
                {
                    ["textKey"] = button.Option.TextKey.ToString(),
                    ["visible"] = button.Visible,
                    ["visibleInTree"] = button.IsVisibleInTree(),
                    ["enabled"] = button.IsEnabled,
                    ["locked"] = button.Option.IsLocked,
                    ["proceed"] = button.Option.IsProceed,
                })
                .ToArray());

    private static int CountVisibleLanEventOptions()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var eventRoom = tree?.Root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        return eventRoom is null
            ? 0
            : Descendants(eventRoom)
                .OfType<NEventOptionButton>()
                .Count(button => button.IsVisibleInTree() && !button.Option.IsLocked);
    }

    private static JsonNode InspectLanRun()
    {
        var runManager = RunManager.Instance;
        var state = runManager?.DebugOnlyGetState();
        if (state is null || runManager?.IsInProgress != true)
        {
            var unavailable = TestToolResult.Fail("当前没有活动的 LAN 跑局。", "lan_run_unavailable");
            unavailable["runInProgress"] = runManager?.IsInProgress ?? false;
            unavailable["netType"] = runManager?.NetService?.Type.ToString();
            unavailable["localNetId"] = runManager?.NetService?.NetId.ToString();
            unavailable["netConnected"] = runManager?.NetService?.IsConnected ?? false;
            unavailable["combatManagerInProgress"] = CombatManager.Instance?.IsInProgress ?? false;
            unavailable["mapOpen"] = NMapScreen.Instance?.IsOpen ?? false;
            return unavailable;
        }

        var localEvent = runManager.EventSynchronizer.Events
            .FirstOrDefault(@event => @event.Owner?.NetId == runManager.NetService.NetId);
        var eventOptions = new JsonArray(localEvent?.CurrentOptions
            .Select((option, index) => (JsonNode?)new JsonObject
            {
                ["index"] = index,
                ["textKey"] = option.TextKey.ToString(),
            })
            .ToArray() ?? []);

        var eventRoom = (Engine.GetMainLoop() as SceneTree)?.Root
            .GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        var playerRelics = new JsonArray(state.Players
            .OrderBy(static player => player.NetId)
            .Select(player => (JsonNode?)new JsonObject
            {
                ["playerNetId"] = player.NetId.ToString(),
                ["relicIds"] = new JsonArray(player.Relics
                    .OrderBy(static relic => relic.Id.Entry, StringComparer.Ordinal)
                    .Select(relic => (JsonNode?)relic.Id.Entry)
                    .ToArray()),
            })
            .ToArray());
        return TestToolResult.Ok(new JsonObject
        {
            ["netType"] = runManager.NetService?.Type.ToString(),
            ["localNetId"] = runManager.NetService?.NetId.ToString(),
            ["roomType"] = state.CurrentRoom?.RoomType.ToString(),
            ["playerRelics"] = playerRelics,
            ["currentMapPoint"] = state.CurrentMapPointHistoryEntry?.ToString(),
            ["visitedMapPointCount"] = state.VisitedMapCoords.Count,
            ["mapOpen"] = NMapScreen.Instance?.IsOpen ?? false,
            ["eventOptionCount"] = CountVisibleLanEventOptions(),
            ["eventOptions"] = eventOptions,
            ["eventButtons"] = CaptureLanEventButtons(eventRoom),
            ["selection"] = CardSelectionControl.Capture(),
            ["combatActive"] = state.Players.Any(static player => player.Creature.CombatState is not null),
            ["autoDriver"] = new JsonObject
            {
                ["autoPlayEnabled"] = AiSessionSettings.AutoPlayEnabled,
                ["mpAiTeammateEnabled"] = AiSessionSettings.MpAiTeammateEnabled,
                ["mpAiTeammateDriveLiveEnet"] = AiSessionSettings.MpAiTeammateDriveLiveEnet,
                ["mpAiTeammateAfkClient"] = AiSessionSettings.MpAiTeammateAfkClient,
                ["syncBotEnabled"] = AiSessionSettings.SyncBotEnabled,
            },
        });
    }

    private static JsonNode CaptureLanSnapshot()
    {
        var runManager = RunManager.Instance;
        var state = runManager?.DebugOnlyGetState();
        if (state is null || runManager?.IsInProgress != true)
        {
            var unavailable = TestToolResult.Fail("当前没有可捕获快照的 LAN 跑局。", "lan_snapshot_unavailable");
            unavailable["runInProgress"] = runManager?.IsInProgress ?? false;
            unavailable["netConnected"] = runManager?.NetService?.IsConnected ?? false;
            unavailable["combatManagerInProgress"] = CombatManager.Instance?.IsInProgress ?? false;
            return unavailable;
        }

        var localPlayer = LocalContext.GetMe(state);
        if (localPlayer is null)
            return TestToolResult.Fail("当前 LAN 跑局没有本地玩家。", "lan_local_player_unavailable");

        var snapshot = new JsonObject
        {
            ["roomType"] = state.CurrentRoom?.RoomType.ToString(),
            ["mapOpen"] = NMapScreen.Instance?.IsOpen ?? false,
            ["selection"] = CardSelectionControl.Capture(),
            ["combat"] = new JsonObject
            {
                ["isPlayPhaseActive"] = localPlayer.PlayerCombatState?.Phase == PlayerTurnPhase.Play,
            },
        };
        DimensionalTravelerSnapshotContributor.Instance.Enrich(snapshot, localPlayer, "LanDirect");
        return TestToolResult.Ok(snapshot);
    }

    private static JsonNode DisableLanAutoDriver()
    {
        var runManager = RunManager.Instance;
        if (runManager?.NetService is null || !runManager.NetService.IsConnected)
            return TestToolResult.Fail("LAN 网络服务尚未连接。", "lan_not_connected");

        LanAcceptanceAutoDriverGuard.Disable();
        return TestToolResult.Ok(new JsonObject
        {
            ["netType"] = runManager.NetService.Type.ToString(),
            ["localNetId"] = runManager.NetService.NetId.ToString(),
            ["autoDriverDisabled"] = true,
            ["autoDriver"] = new JsonObject
            {
                ["autoPlayEnabled"] = AiSessionSettings.AutoPlayEnabled,
                ["mpAiTeammateEnabled"] = AiSessionSettings.MpAiTeammateEnabled,
                ["mpAiTeammateDriveLiveEnet"] = AiSessionSettings.MpAiTeammateDriveLiveEnet,
                ["mpAiTeammateAfkClient"] = AiSessionSettings.MpAiTeammateAfkClient,
                ["syncBotEnabled"] = AiSessionSettings.SyncBotEnabled,
            },
        });
    }

    private static async Task<JsonNode> ProceedLanEvent()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        var eventRoom = tree?.Root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        if (LanAcceptanceAutoDriverGuard.IsActive)
        {
            if (eventRoom is null)
                return TestToolResult.Fail("当前没有活动的原生事件房间。", "event_room_unavailable");

            await NEventRoom.Proceed();
            return TestToolResult.Ok(new JsonObject
            {
                ["mapOpen"] = NMapScreen.Instance?.IsOpen ?? false,
                ["proceeded"] = true,
                ["skippedEventReward"] = true,
            });
        }

        var proceed = eventRoom is null
            ? null
            : Descendants(eventRoom)
                .OfType<NEventOptionButton>()
                .FirstOrDefault(button =>
                    button.IsVisibleInTree() &&
                    !button.Option.IsLocked &&
                    button.Option.IsProceed &&
                    button.IsEnabled);
        if (proceed is null)
            return TestToolResult.Fail("当前没有可点击的原生事件继续按钮。", "event_proceed_unavailable");

        // Neow 的完成页不经过 EventSynchronizer；其原生实现只调用 NEventRoom.Proceed() 打开本地地图 UI。
        // 直接调用同一原生入口，避免自动化点击被输入动画/焦点状态吞掉。
        await NEventRoom.Proceed();
        return TestToolResult.Ok(new JsonObject
        {
            ["mapOpen"] = NMapScreen.Instance?.IsOpen ?? false,
            ["proceeded"] = true,
        });
    }

    private static JsonNode SelectLanTravelerAndReady()
    {
        if (!TryGetLanCharacterSelect(out var screen, out var error))
            return error;

        var lobby = screen.Lobby;
        if (lobby.Players.Count < 2)
            return TestToolResult.Fail("LAN 角色选择名册尚未形成双玩家。", "lan_roster_incomplete");

        var traveler = ModelDb.Character<Traveler>();
        var button = Descendants(screen)
            .OfType<NCharacterSelectButton>()
            .FirstOrDefault(candidate => candidate.Character == traveler);
        if (button is null)
            return TestToolResult.Fail("LAN 角色选择界面找不到次元旅人按钮。", "traveler_button_unavailable");
        if (button.IsLocked)
            return TestToolResult.Fail("当前测试 profile 未解锁次元旅人。", "traveler_locked");

        button.Select();
        lobby.SetReady(ready: true);
        var result = CaptureLanLobby(screen);
        result["selectedCharacterId"] = traveler.Id.Entry;
        return TestToolResult.Ok(result);
    }

    private static bool TryGetLanCharacterSelect(
        out NCharacterSelectScreen screen,
        out JsonNode error)
    {
        screen = null!;
        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu is null || !GodotObject.IsInstanceValid(mainMenu))
        {
            error = TestToolResult.Fail("主菜单尚未加载。", "main_menu_unavailable");
            return false;
        }

        screen = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
        if (!GodotObject.IsInstanceValid(screen) || screen.Lobby is null)
        {
            error = TestToolResult.Fail("当前尚未进入 LAN 角色选择大厅。", "lan_lobby_unavailable");
            return false;
        }
        if (!screen.Lobby.NetService.IsConnected)
        {
            error = TestToolResult.Fail("LAN 角色选择网络服务尚未连接。", "lan_not_connected");
            return false;
        }

        error = null!;
        return true;
    }

    private static JsonObject CaptureLanLobby(NCharacterSelectScreen screen)
    {
        var lobby = screen.Lobby;
        return new JsonObject
        {
            ["netType"] = lobby.NetService.Type.ToString(),
            ["localNetId"] = lobby.NetService.NetId.ToString(),
            ["connected"] = lobby.NetService.IsConnected,
            ["playerCount"] = lobby.Players.Count,
            ["players"] = new JsonArray(lobby.Players
                .OrderBy(static player => player.id)
                .Select(player => (JsonNode?)new JsonObject
                {
                    ["netId"] = player.id.ToString(),
                    ["slotId"] = player.slotId,
                    ["characterId"] = player.character.Id.Entry,
                    ["ready"] = player.isReady,
                    ["isLocal"] = player.id == lobby.NetService.NetId,
                })
                .ToArray()),
        };
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static async Task<JsonNode> StartTestCombat(JsonObject args)
    {
        var run = await StartTestRun(args);
        if (run is not JsonObject runObject || runObject["ok"]?.GetValue<bool>() != true)
            return run;
        return await EnterTestCombat();
    }

    private static async Task<JsonNode> StartTestRun(JsonObject args)
    {
        var game = NGame.Instance;
        if (game is null)
            return TestToolResult.Fail("NGame 尚未加载。", "game_unavailable");

        var runManager = RunManager.Instance;
        if (runManager.IsInProgress)
            runManager.CleanUp(graceful: true);

        KitLibState.InDevRun = true;
        var seed = args["seed"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(seed))
            seed = "DIMENSIONAL-TRAVELER-ACCEPTANCE";

        var character = ModelDb.Character<Traveler>();
        var runState = await game.StartNewSingleplayerRun(
            character,
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard);
        var player = runState.Players.Single();
        return TestToolResult.Ok(new JsonObject
        {
            ["seed"] = seed,
            ["characterId"] = player.Character.Id.Entry,
            ["roomType"] = runState.CurrentRoom?.RoomType.ToString(),
        });
    }

    private static async Task<JsonNode> EnterTestCombat()
    {
        var runManager = RunManager.Instance;
        var runState = runManager.DebugOnlyGetState();
        if (runState is null || !runManager.IsInProgress)
            return TestToolResult.Fail("当前没有可进入战斗的测试跑局。", "run_unavailable");

        await runManager.EnterRoomDebug(
            RoomType.Monster,
            model: null,
            showTransition: false);

        var player = runState.Players.Single();
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return TestToolResult.Fail("测试跑局已创建，但未进入战斗。", "combat_unavailable");

        return TestToolResult.Ok(new JsonObject
        {
            ["characterId"] = player.Character.Id.Entry,
            ["roomType"] = runState.CurrentRoom?.RoomType.ToString(),
            ["enemyCount"] = combatState.Enemies.Count,
        });
    }

    private static JsonNode InspectMerchantPotionPrice()
    {
        if (!TryGetRunTraveler(out var player, out var error))
            return error;

        var canonical = ModelDb.PotionPool<SharedPotionPool>()
            .AllPotions
            .OrderBy(static potion => potion.Id.Entry, StringComparer.Ordinal)
            .FirstOrDefault();
        if (canonical is null)
            return TestToolResult.Fail("共享原版药水池为空。", "potion_pool_unavailable");

        // 仅保留 Hook 所需的运行时类型，避免 MerchantPotionEntry 构造器写入图鉴状态。
        var entry = (MerchantPotionEntry)RuntimeHelpers.GetUninitializedObject(typeof(MerchantPotionEntry));
        const decimal rawCost = 100m;
        var effectiveCost = Hook.ModifyMerchantPrice(player.RunState, player, entry, rawCost);
        return TestToolResult.Ok(new JsonObject
        {
            ["potionId"] = canonical.Id.Entry,
            ["rawCost"] = rawCost,
            ["effectiveCost"] = effectiveCost,
        });
    }

    private static JsonNode InspectSharedPotionPool()
    {
        var sharedPotions = ModelDb.PotionPool<SharedPotionPool>()
            .AllPotions
            .OrderBy(static potion => potion.Id.Entry, StringComparer.Ordinal)
            .Select(static potion => (JsonNode)new JsonObject
            {
                ["id"] = potion.Id.Entry,
                ["type"] = potion.GetType().Name,
                ["rarity"] = potion.Rarity.ToString(),
            })
            .ToArray();

        PotionModel[] specialPotionModels =
        [
            ModelDb.Potion<GlowwaterPotion>(),
            ModelDb.Potion<FoulPotion>(),
            ModelDb.Potion<PotionShapedRock>(),
        ];
        var specialPotions = specialPotionModels
        .OrderBy(static potion => potion.Id.Entry, StringComparer.Ordinal)
        .Select(static potion => (JsonNode)new JsonObject
        {
            ["id"] = potion.Id.Entry,
            ["type"] = potion.GetType().Name,
            ["rarity"] = potion.Rarity.ToString(),
        })
        .ToArray();

        return TestToolResult.Ok(new JsonObject
        {
            ["count"] = sharedPotions.Length,
            ["potions"] = new JsonArray(sharedPotions),
            ["specialPotions"] = new JsonArray(specialPotions),
        });
    }

    private static JsonNode InspectExtractionCatalog()
    {
        var sharedIds = ModelDb.PotionPool<SharedPotionPool>()
            .AllPotions
            .Select(static potion => potion.Id.Entry)
            .ToArray();
        var validation = PotionExtractionCatalog.ValidateSharedPool(sharedIds);
        var plans = PotionExtractionCatalog.All
            .Select(static plan => (JsonNode)new JsonObject
            {
                ["potionId"] = plan.PotionId,
                ["scope"] = plan.Scope.ToString(),
                ["specialPrinciple"] = plan.SpecialPrinciple.ToString(),
                ["basicPrinciple"] = plan.BasicPrinciple.ToString(),
                ["basicAmount"] = plan.BasicAmount,
                ["choiceMode"] = plan.ChoiceMode.ToString(),
                ["gold"] = plan.Gold,
                ["maxHp"] = plan.MaxHp,
                ["rewards"] = new JsonArray(plan.PotionRewards
                    .Select(static reward => (JsonNode)new JsonObject
                    {
                        ["family"] = reward.Family.ToString(),
                        ["quality"] = reward.Quality.ToString(),
                        ["upgraded"] = reward.IsUpgraded,
                    })
                    .ToArray()),
            })
            .ToArray();

        return TestToolResult.Ok(new JsonObject
        {
            ["plans"] = new JsonArray(plans),
            ["validation"] = new JsonObject
            {
                ["valid"] = validation.IsValid,
                ["missingPlans"] = new JsonArray(validation.MissingPlans.Select(static id => (JsonNode?)id).ToArray()),
                ["staleSharedPlans"] = new JsonArray(validation.StaleSharedPlans.Select(static id => (JsonNode?)id).ToArray()),
                ["invalidPlans"] = new JsonArray(validation.InvalidPlans.Select(static id => (JsonNode?)id).ToArray()),
            },
        });
    }

    private static JsonNode InspectRunPlayerRoundTrip()
    {
        if (!TryGetRunTraveler(out var player, out var error))
            return error;

        var saved = player.ToSerializable();
        var restored = Player.FromSerializable(saved);
        return TestToolResult.Ok(new JsonObject
        {
            ["maxPotionCount"] = restored.MaxPotionCount,
            ["openSlotCount"] = restored.PotionSlots.Count(static potion => potion is null),
            ["backpackCapacity"] = AlchemyBackpack.GetCapacity(restored),
            ["relicIds"] = new JsonArray(restored.Relics
                .Select(static relic => (JsonNode?)relic.Id.Entry)
                .ToArray()),
        });
    }

    private static async Task<JsonNode> GrantRunRelic(JsonObject args)
    {
        if (!TryGetRunTraveler(out var player, out var error))
            return error;
        if (player.Creature.CombatState is not null)
            return TestToolResult.Fail("grant_run_relic 只能在进入战斗前调用。", "combat_already_started");
        return await RelicTestControl.Grant(player, args);
    }

    private static JsonNode SetRunPlayerHp(JsonObject args)
    {
        if (!TryGetRunTraveler(out var player, out var error))
            return error;
        if (player.Creature.CombatState is not null)
            return TestToolResult.Fail("set_run_player_hp 只能在进入战斗前调用。", "combat_already_started");

        var hp = args["hp"]?.GetValue<int>() ?? -1;
        if (hp < 1 || hp > player.Creature.MaxHp)
            return TestToolResult.Fail(
                $"生命必须在 1..{player.Creature.MaxHp}。",
                "invalid_hp");

        player.Creature.SetCurrentHpInternal(hp);
        return TestToolResult.Ok(new JsonObject
        {
            ["currentHp"] = player.Creature.CurrentHp,
            ["maxHp"] = player.Creature.MaxHp,
        });
    }

    private static JsonNode InspectCatalog()
    {
        var assembly = typeof(TravelerCardPool).Assembly;
        var cards = ModelDb.AllCards
            .Where(card => card.GetType().Assembly == assembly)
            .GroupBy(card => card.Id.Entry, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToArray();
        var selectionCards = cards
            .Where(card => card is PrincipleChoiceCard or PrincipleCategoryChoiceCard)
            .ToArray();
        var formalCards = cards.Except(selectionCards).ToArray();
        var rewardCards = formalCards
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToArray();
        var potionCards = formalCards.OfType<AlchemyPotionCard>().ToArray();

        return TestToolResult.Ok(new JsonObject
        {
            ["all"] = SerializeCards(cards),
            ["formal"] = SerializeCards(formalCards),
            ["selection"] = SerializeCards(selectionCards),
            ["rewards"] = SerializeCards(rewardCards),
            ["potions"] = SerializeCards(potionCards),
            ["counts"] = new JsonObject
            {
                ["all"] = cards.Length,
                ["formal"] = formalCards.Length,
                ["selection"] = selectionCards.Length,
                ["reward"] = rewardCards.Length,
                ["common"] = rewardCards.Count(card => card.Rarity == CardRarity.Common),
                ["uncommon"] = rewardCards.Count(card => card.Rarity == CardRarity.Uncommon),
                ["rare"] = rewardCards.Count(card => card.Rarity == CardRarity.Rare),
                ["potion"] = potionCards.Length,
            },
        });
    }

    private static JsonArray SerializeCards(IEnumerable<CardModel> cards)
    {
        var result = new JsonArray();
        foreach (var card in cards)
        {
            result.Add(new JsonObject
            {
                ["id"] = card.Id.Entry,
                ["type"] = card.GetType().Name,
                ["rarity"] = card.Rarity.ToString(),
                ["cardType"] = card.Type.ToString(),
                ["maxUpgradeLevel"] = card.MaxUpgradeLevel,
            });
        }
        return result;
    }

    private static async Task<JsonNode> GrantNativePotion(Player player, JsonObject args)
    {
        var potionId = args["potion_id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(potionId))
            return TestToolResult.Fail("grant_native_potion 需要 potion_id。", "missing_potion_id");
        if (RunManager.Instance?.NetService?.Type == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Singleplayer)
        {
            var canonical = ModelDb.AllPotions.FirstOrDefault(potion =>
                string.Equals(potion.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                return TestToolResult.Fail($"找不到原生药水 {potionId}。", "potion_not_found");

            var result = await PotionCmd.TryToProcure(canonical.ToMutable(), player);
            return result.success
                ? TestToolResult.Ok(new JsonObject
                {
                    ["potionId"] = result.potion.Id.Entry,
                    ["slotIndex"] = player.GetPotionSlotIndex(result.potion),
                    ["status"] = "committed",
                })
                : TestToolResult.Fail($"原生药水 {potionId} 获得失败：{result.failureReason}。", "potion_procure_failed");
        }

        if (!TestPotionGrantAction.CanRequest(player, potionId, out var failureCode))
            return TestToolResult.Fail($"原生药水 {potionId} 不可注入：{failureCode}。", failureCode);
        if (!TestPotionGrantAction.Request(player, potionId))
            return TestToolResult.Fail($"原生药水 {potionId} 的同步注入请求被拒绝。", "managed_action_request_rejected");

        // MCP 调用运行在游戏主线程，不能在这里等待动作队列；调用方应从快照审计观察最终提交状态。
        return TestToolResult.Ok(new JsonObject
        {
            ["potionId"] = potionId,
            ["status"] = "requested",
        });
    }

    private static JsonNode GrantTestCard(Player player, JsonObject args)
    {
        var cardId = args["card_id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(cardId))
            return TestToolResult.Fail("grant_test_card 需要 card_id。", "missing_card_id");
        if (!TestCardGrantAction.CanRequest(player, cardId, out var failureCode))
            return TestToolResult.Fail($"测试卡牌 {cardId} 不可注入：{failureCode}。", failureCode);
        if (!TestCardGrantAction.Request(player, cardId))
            return TestToolResult.Fail($"测试卡牌 {cardId} 的同步注入请求被拒绝。", "managed_action_request_rejected");

        return TestToolResult.Ok(new JsonObject
        {
            ["cardId"] = cardId,
            ["status"] = "requested",
        });
    }

    private static JsonNode DiscardNativePotion(Player player, JsonObject args)
    {
        var slotIndex = args["potion_slot_index"]?.GetValue<int>() ?? -1;
        if (slotIndex < 0 || slotIndex >= player.PotionSlots.Count)
        {
            return TestToolResult.Fail(
                $"potion_slot_index {slotIndex} 超出范围，槽位数为 {player.PotionSlots.Count}。",
                "potion_slot_out_of_range");
        }

        var potion = player.PotionSlots[slotIndex];
        if (potion is null)
            return TestToolResult.Fail("指定原生药水槽位已为空。", "potion_unavailable");

        potion.Discard();
        return TestToolResult.Ok(new JsonObject
        {
            ["potionId"] = potion.Id.Entry,
            ["slotIndex"] = slotIndex,
        });
    }

    private static async Task<JsonNode> ExtractNativePotion(Player player, JsonObject args)
    {
        var slotIndex = args["potion_slot_index"]?.GetValue<int>() ?? -1;
        if (!ExtractionFlow.TryGetPlan(player, slotIndex, out var plan, out var failureCode))
            return TestToolResult.Fail($"原生药水不可萃取：{failureCode}。", failureCode);
        if (!ExtractionFlow.Enqueue(player, slotIndex, out failureCode))
            return TestToolResult.Fail($"原生药水萃取请求被拒绝：{failureCode}。", failureCode);

        if (plan.ChoiceMode == ExtractionChoiceMode.AttackPotion)
        {
            await Task.Delay(100);
            return TestToolResult.Ok(new JsonObject
            {
                ["status"] = "awaiting_choice",
                ["potionSlotIndex"] = slotIndex,
            });
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (slotIndex >= player.PotionSlots.Count || player.PotionSlots[slotIndex] is null)
            {
                return TestToolResult.Ok(new JsonObject
                {
                    ["status"] = "completed",
                    ["potionSlotIndex"] = slotIndex,
                });
            }
            await Task.Delay(50);
        }

        return TestToolResult.Fail("受管萃取动作未在 1 秒内移除原生药水。", "action_timeout");
    }

    private static async Task<JsonNode> PlayLocalCard(Player player, JsonObject args)
    {
        var cardId = args["card_id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(cardId))
            return TestToolResult.Fail("play_local_card 需要 card_id。", "missing_card_id");

        var hand = PileType.Hand.GetPile(player)?.Cards;
        var card = hand?.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
        if (card is null)
            return TestToolResult.Fail($"本地玩家手牌中找不到 {cardId}。", "card_not_found");

        var targetId = args["target_combat_id"]?.GetValue<uint?>();
        var target = targetId.HasValue
            ? player.Creature.CombatState?.GetCreature(targetId)
            : null;
        if (targetId.HasValue && target is null)
            return TestToolResult.Fail($"找不到 combatId={targetId} 的目标。", "target_not_found");
        if (!card.CanPlayTargeting(target))
            return TestToolResult.Fail($"{card.Id.Entry} 当前不能对所选目标打出。", "card_not_playable");

        var waitForChoice = args["return_when_gathering_choice"]?.GetValue<bool>() ?? false;
        var waitForTargeting = args["return_when_targeting"]?.GetValue<bool>() ?? false;
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
            if (waitForChoice && (
                playAction.State == GameActionState.GatheringPlayerChoice ||
                CardSelectionControl.Capture()["active"]?.GetValue<bool>() == true))
            {
                return TestToolResult.Ok(new JsonObject
                {
                    ["cardId"] = card.Id.Entry,
                    ["targetCombatId"] = target?.CombatId,
                    ["actionId"] = playAction.Id,
                    ["state"] = "awaiting_choice",
                });
            }

            if (waitForTargeting)
            {
                if (TargetingControl.Capture()["active"]?.GetValue<bool>() == true)
                {
                    return TestToolResult.Ok(new JsonObject
                    {
                        ["cardId"] = card.Id.Entry,
                        ["targetCombatId"] = target?.CombatId,
                        ["actionId"] = playAction.Id,
                        ["state"] = "awaiting_target",
                    });
                }

                // 药剂的外层 PlayCardAction 可先完成，再由 OnPlay 的后续协程打开原生目标选择。
                // 因此显式等待目标时不能把外层动作完成误判为整个交互完成。
                if (cancelled.Task.IsCompleted || playAction.State == GameActionState.Canceled)
                    break;
                await Task.Delay(50);
                continue;
            }

            if (card.Pile?.Type != PileType.Hand && playAction.State == GameActionState.None)
            {
                return TestToolResult.Ok(new JsonObject
                {
                    ["cardId"] = card.Id.Entry,
                    ["targetCombatId"] = target?.CombatId,
                    ["actionId"] = playAction.Id,
                    ["state"] = "completed_without_task",
                });
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
        if (waitForTargeting)
        {
            return TestToolResult.Fail(
                $"{card.Id.Entry} 未在 30 秒内打开原生目标选择，state={playAction.State}。",
                "targeting_timeout");
        }
        if (!playAction.CompletionTask.IsCompleted)
            return TestToolResult.Fail(
                $"{card.Id.Entry} 的原生出牌动作在 30 秒内未完成，state={playAction.State}。",
                "play_timeout");
        if (playAction.Exception is not null)
            return TestToolResult.Fail(playAction.Exception.ToString(), "play_failed");

        return TestToolResult.Ok(new JsonObject
        {
            ["cardId"] = card.Id.Entry,
            ["targetCombatId"] = target?.CombatId,
            ["actionId"] = playAction.Id,
            ["state"] = playAction.State.ToString(),
        });
    }

    private static JsonNode ForceEndPlayerTurn(Player player)
    {
        var combat = CombatManager.Instance;
        var turnNumber = player.PlayerCombatState?.TurnNumber;
        PlayerCmd.EndTurn(player, canBackOut: false);
        return TestToolResult.Ok(new JsonObject
        {
            ["turnNumber"] = turnNumber,
            ["readyToEndTurn"] = combat.IsPlayerReadyToEndTurn(player),
        });
    }

    private static async Task<JsonNode> SetPrinciples(Player player, JsonObject args)
    {
        if (args["principles"] is not JsonObject requested)
            return TestToolResult.Fail("set_principles 需要 principles 对象。", "missing_principles");

        var definitions = AlchemyPrinciples.All.ToDictionary(
            definition => definition.LocalId,
            StringComparer.OrdinalIgnoreCase);
        var changes = new List<(string ResourceId, int Before, int Target)>();
        foreach (var (localId, valueNode) in requested)
        {
            if (!definitions.TryGetValue(localId, out var definition))
                return TestToolResult.Fail($"未知原理：{localId}。", "unknown_principle");
            if (valueNode is null || valueNode.GetValueKind() != System.Text.Json.JsonValueKind.Number)
                return TestToolResult.Fail($"原理 {localId} 的值必须是整数。", "invalid_principle_amount");

            var target = valueNode.GetValue<int>();
            if (target < 0)
                return TestToolResult.Fail($"原理 {localId} 的值不能为负数。", "invalid_principle_amount");
            changes.Add((definition.Id, AlchemyPrinciples.Get(player, definition), target));
        }

        try
        {
            foreach (var change in changes)
                await SecondaryResourceCmd.Set(player, change.ResourceId, change.Target);
        }
        catch
        {
            foreach (var change in changes)
                await SecondaryResourceCmd.Set(player, change.ResourceId, change.Before);
            throw;
        }

        return TestToolResult.Ok(new JsonObject { ["principles"] = CapturePrincipleAmounts(player) });
    }

    private static async Task<JsonNode> ClearBackpack(Player player)
    {
        var potions = AlchemyBackpack.GetPotions(player).ToArray();
        if (potions.Length > 0)
            await CardPileCmd.RemoveFromCombat(potions, skipVisuals: true);
        return TestToolResult.Ok(new JsonObject { ["removed"] = potions.Length });
    }

    private static async Task<JsonNode> BrewPotion(Player player, JsonObject args)
    {
        if (!TryParseEnum(args, "family", out PotionFamily family, PotionFamily.Shield, out var familyError))
            return TestToolResult.Fail(familyError, "invalid_family");
        if (!TryParseEnum(args, "quality", out PotionQuality quality, PotionQuality.Normal, out var qualityError))
            return TestToolResult.Fail(qualityError, "invalid_quality");
        if (!TryParseEnum(args, "origin", out PotionOrigin origin, PotionOrigin.Original, out var originError))
            return TestToolResult.Fail(originError, "invalid_origin");

        var upgraded = args["upgraded"]?.GetValue<bool>() ?? false;
        var potion = await AlchemyBackpack.Brew(
            player,
            family,
            quality,
            upgraded,
            origin,
            recordAsBrewed: false);
        return potion is null
            ? TestToolResult.Fail("药剂创建失败。", "brew_failed")
            : TestToolResult.Ok(new JsonObject { ["cardId"] = potion.Id.Entry });
    }

    private static async Task<JsonNode> MoveBackpackPotionToHand(Player player, JsonObject args)
    {
        var potions = AlchemyBackpack.GetPotions(player);
        var index = args["backpack_index"]?.GetValue<int>() ?? 0;
        if (index < 0 || index >= potions.Count)
        {
            return TestToolResult.Fail(
                $"backpack_index {index} 超出范围，背包药剂数为 {potions.Count}。",
                "backpack_index_out_of_range");
        }

        var hand = PileType.Hand.GetPile(player);
        if (hand is null || hand.Cards.Count >= CardPile.MaxCardsInHand)
            return TestToolResult.Fail("手牌已满或手牌堆未附加，不能装配测试药剂。", "hand_full");

        var potion = potions[index];
        var result = await CardPileCmd.Add(potion, PileType.Hand, skipVisuals: true);
        return result.success && potion.Pile?.Type == PileType.Hand
            ? TestToolResult.Ok(new JsonObject { ["cardId"] = potion.Id.Entry })
            : TestToolResult.Fail("将背包药剂移入手牌失败。", "move_failed");
    }

    private static JsonNode ResetTurn(Player player)
    {
        AlchemyCombatState.Require(player).Update(static turn => turn.Reset());
        return TestToolResult.Ok();
    }

    private static JsonNode ClearPaymentAudit()
    {
        PaymentAudit.Clear();
        return TestToolResult.Ok();
    }

    private static JsonNode ClearExtractionAudit()
    {
        ExtractionAudit.Clear();
        return TestToolResult.Ok();
    }

    private static JsonNode SetEnemyHp(Player player, JsonObject args)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return TestToolResult.Fail("当前不在战斗中。", "combat_unavailable");

        var enemyIndex = args["enemy_index"]?.GetValue<int>() ?? -1;
        var hp = args["hp"]?.GetValue<int>() ?? -1;
        if (enemyIndex < 0 || enemyIndex >= combatState.Enemies.Count)
        {
            return TestToolResult.Fail(
                $"enemy_index {enemyIndex} 超出范围，敌人数为 {combatState.Enemies.Count}。",
                "enemy_index_out_of_range");
        }
        if (hp < 1)
            return TestToolResult.Fail("测试目标生命必须至少为 1；死亡流程应由正常伤害触发。", "invalid_hp");

        var enemy = combatState.Enemies[enemyIndex];
        enemy.SetCurrentHpInternal(hp);
        return TestToolResult.Ok(new JsonObject
        {
            ["enemyIndex"] = enemyIndex,
            ["combatId"] = enemy.CombatId,
            ["hp"] = enemy.CurrentHp,
        });
    }

    private static JsonNode SetEnemyBlock(Player player, JsonObject args)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return TestToolResult.Fail("当前不在战斗中。", "combat_unavailable");

        var enemyIndex = args["enemy_index"]?.GetValue<int>() ?? -1;
        var block = args["block"]?.GetValue<int>() ?? -1;
        if (enemyIndex < 0 || enemyIndex >= combatState.Enemies.Count)
        {
            return TestToolResult.Fail(
                $"enemy_index {enemyIndex} 超出范围，敌人数为 {combatState.Enemies.Count}。",
                "enemy_index_out_of_range");
        }
        if (block < 0)
            return TestToolResult.Fail("测试目标格挡不能为负数。", "invalid_block");

        var enemy = combatState.Enemies[enemyIndex];
        if (enemy.Block > block)
            enemy.LoseBlockInternal(enemy.Block - block);
        else if (enemy.Block < block)
            enemy.GainBlockInternal(block - enemy.Block);
        return TestToolResult.Ok(new JsonObject
        {
            ["enemyIndex"] = enemyIndex,
            ["combatId"] = enemy.CombatId,
            ["block"] = enemy.Block,
        });
    }

    private static JsonObject CapturePrincipleAmounts(Player player)
    {
        var result = new JsonObject();
        foreach (var definition in AlchemyPrinciples.All)
            result[definition.LocalId] = AlchemyPrinciples.Get(player, definition);
        return result;
    }

    private static bool TryGetRunTraveler(out Player player, out JsonNode error)
    {
        player = null!;
        var state = RunManager.Instance?.DebugOnlyGetState();
        var localPlayer = LocalContext.GetMe(state);
        if (localPlayer is null)
        {
            error = TestToolResult.Fail("没有活动中的本地玩家。", "player_unavailable");
            return false;
        }
        if (!AlchemyCombatState.IsTraveler(localPlayer))
        {
            error = TestToolResult.Fail(
                $"本地角色不是次元旅人：{localPlayer.Character.Id.Entry}。",
                "wrong_character");
            return false;
        }

        player = localPlayer;
        error = null!;
        return true;
    }

    private static bool TryGetLocalTraveler(out Player player, out JsonNode error)
    {
        player = null!;
        var state = RunManager.Instance?.DebugOnlyGetState();
        var localPlayer = LocalContext.GetMe(state);
        if (localPlayer is null)
        {
            error = TestToolResult.Fail("没有活动中的本地玩家。", "player_unavailable");
            return false;
        }
        if (!AlchemyCombatState.IsTraveler(localPlayer))
        {
            error = TestToolResult.Fail(
                $"本地角色不是次元旅人：{localPlayer.Character.Id.Entry}。",
                "wrong_character");
            return false;
        }
        if (localPlayer.Creature.CombatState is null)
        {
            error = TestToolResult.Fail("次元旅人当前不在战斗中。", "combat_unavailable");
            return false;
        }

        player = localPlayer;
        error = new JsonObject();
        return true;
    }

    private static bool TryParseEnum<TEnum>(
        JsonObject args,
        string key,
        out TEnum value,
        TEnum defaultValue,
        out string error)
        where TEnum : struct, Enum
    {
        value = defaultValue;
        error = string.Empty;
        var raw = args[key]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (Enum.TryParse(raw, ignoreCase: true, out value))
            return true;

        error = $"{key} 的值 {raw} 无效，可用值：{string.Join(", ", Enum.GetNames<TEnum>())}。";
        return false;
    }
}