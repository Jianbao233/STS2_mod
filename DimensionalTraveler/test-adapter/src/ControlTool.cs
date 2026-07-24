using System.Text.Json.Nodes;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Choices;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;
using KitLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
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
              "enum": ["start_test_combat", "reset_scenario", "start_pseudo_coop", "inspect_players", "enter_pseudo_coop_test_combat", "apply_fixture", "apply_player_fixture", "play_player_card", "inspect_catalog", "set_principles", "clear_backpack", "brew_potion", "move_backpack_potion_to_hand", "reset_turn", "clear_payment_audit", "set_enemy_hp", "set_enemy_block"]
            },
            "seed": { "type": "string" },
            "player_net_id": { "type": "integer" },
            "card_id": { "type": "string" },
            "target_combat_id": { "type": "integer" },
            "fixture": { "type": "object" },
            "principles": { "type": "object", "additionalProperties": { "type": "integer" } },
            "family": { "type": "string" },
            "quality": { "type": "string" },
            "upgraded": { "type": "boolean" },
            "origin": { "type": "string" },
            "backpack_index": { "type": "integer" },
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

        if (!TryGetLocalTraveler(out var player, out var error))
            return error;

        return action switch
        {
            "apply_fixture" => await ScenarioFixture.Apply(player, args["fixture"] as JsonObject ?? new JsonObject()),
            "inspect_catalog" => InspectCatalog(),
            "set_principles" => await SetPrinciples(player, args),
            "clear_backpack" => await ClearBackpack(player),
            "brew_potion" => await BrewPotion(player, args),
            "move_backpack_potion_to_hand" => await MoveBackpackPotionToHand(player, args),
            "reset_turn" => ResetTurn(player),
            "clear_payment_audit" => ClearPaymentAudit(),
            "set_enemy_hp" => SetEnemyHp(player, args),
            "set_enemy_block" => SetEnemyBlock(player, args),
            _ => TestToolResult.Fail($"未知 action：{action ?? "<null>"}。", "invalid_action"),
        };
    }

    private static async Task<JsonNode> StartTestCombat(JsonObject args)
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
            ["seed"] = seed,
            ["characterId"] = player.Character.Id.Entry,
            ["roomType"] = runState.CurrentRoom?.RoomType.ToString(),
            ["enemyCount"] = combatState.Enemies.Count,
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