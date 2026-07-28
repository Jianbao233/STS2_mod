using System.Text.Json.Nodes;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Extraction;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Content.Cards.System;
using DimensionalTraveler.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace DimensionalTraveler.TestAdapter;

internal static class ScenarioFixture
{
    private static readonly IReadOnlyDictionary<string, PileType> CombatPiles =
        new Dictionary<string, PileType>(StringComparer.OrdinalIgnoreCase)
        {
            ["hand"] = PileType.Hand,
            ["draw"] = PileType.Draw,
            ["discard"] = PileType.Discard,
            ["exhaust"] = PileType.Exhaust,
        };

    public static async Task<JsonNode> Apply(Player player, JsonObject fixture)
    {
        if (!TryParse(player, fixture, out var plan, out var validationError))
            return validationError;

        try
        {
            await ClearScenario(player, plan.PreserveSystemCards);
            await ApplyPrinciples(player, plan.Principles);
            ApplyTurnState(player, plan.Turn);
            ApplyEnergy(player, plan.Energy);
            ApplyEnemies(player, plan.Enemies);
            await AddCards(player, plan.Cards);
            await AddBackpackPotions(player, plan.Backpack);
            PaymentAudit.Clear();
            ChoiceAudit.Clear();
            ExtractionAudit.Clear();

            return TestToolResult.Ok(new JsonObject
            {
                ["fixtureId"] = plan.Id,
                ["energy"] = player.PlayerCombatState?.Energy,
                ["cardCount"] = plan.Cards.Sum(static card => card.Count),
                ["backpackCount"] = AlchemyBackpack.GetPotions(player).Count,
                ["principles"] = CapturePrinciples(player),
            });
        }
        catch (Exception exception)
        {
            return TestToolResult.Fail(
                $"夹具 {plan.Id} 提交失败，当前战斗已标记为不可复用：{exception.Message}",
                "fixture_commit_failed").Also("tainted", true);
        }
    }

    private static bool TryParse(
        Player player,
        JsonObject fixture,
        out FixturePlan plan,
        out JsonNode error)
    {
        plan = null!;
        error = new JsonObject();
        var id = fixture["id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(id))
            id = "anonymous";

        var energy = fixture["energy"]?.GetValue<int>() ?? 20;
        if (energy is < 0 or > 99)
            return Invalid($"夹具 {id} 的 energy 必须在 0..99。", "invalid_energy", out error);

        var principles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var definitions = AlchemyPrinciples.All.ToDictionary(
            static definition => definition.LocalId,
            StringComparer.OrdinalIgnoreCase);
        if (fixture["principles"] is JsonObject principleNode)
        {
            foreach (var (name, amountNode) in principleNode)
            {
                if (!definitions.ContainsKey(name))
                    return Invalid($"夹具 {id} 包含未知原理 {name}。", "unknown_principle", out error);
                if (amountNode?.GetValueKind() != System.Text.Json.JsonValueKind.Number)
                    return Invalid($"夹具 {id} 的原理 {name} 必须为整数。", "invalid_principle_amount", out error);
                var amount = amountNode.GetValue<int>();
                if (amount < 0)
                    return Invalid($"夹具 {id} 的原理 {name} 不能为负数。", "invalid_principle_amount", out error);
                principles[name] = amount;
            }
        }
        foreach (var name in definitions.Keys)
            principles.TryAdd(name, 0);

        if (!TryParseCards(id, fixture["cards"] as JsonArray, out var cards, out error))
            return false;
        if (!TryParseBackpack(id, fixture["backpack"] as JsonArray, out var backpack, out error))
            return false;
        if (!TryParseEnemies(player, id, fixture["enemies"] as JsonArray, out var enemies, out error))
            return false;
        if (!TryParseTurn(id, fixture["turn"] as JsonObject, out var turn, out error))
            return false;

        var preserveSystemCards = fixture["preserveSystemCards"]?.GetValue<bool>() ?? true;
        var systemCardCount = preserveSystemCards
            ? player.PlayerCombatState?.AllCards.Count(static card => card is PotionSatchel) ?? 0
            : 0;
        var requestedHandCount = cards
            .Where(static card => card.Pile == PileType.Hand)
            .Sum(static card => card.Count);
        var overflowToHand = Math.Max(0, backpack.Count - AlchemyBackpack.GetCapacity(player));
        if (systemCardCount + requestedHandCount + overflowToHand > CardPile.MaxCardsInHand)
        {
            return Invalid(
                $"夹具 {id} 会使初始手牌超过 {CardPile.MaxCardsInHand} 张。",
                "hand_capacity_exceeded",
                out error);
        }

        plan = new FixturePlan(
            id,
            energy,
            preserveSystemCards,
            principles,
            cards,
            backpack,
            enemies,
            turn);
        return true;
    }

    private static bool TryParseCards(
        string fixtureId,
        JsonArray? requested,
        out IReadOnlyList<CardFixture> cards,
        out JsonNode error)
    {
        var result = new List<CardFixture>();
        error = new JsonObject();
        if (requested is null)
        {
            cards = result;
            return true;
        }

        foreach (var node in requested)
        {
            if (node is not JsonObject cardNode)
                return Invalid($"夹具 {fixtureId} 的 cards 项必须是对象。", "invalid_card", out cards, out error);

            var cardId = cardNode["id"]?.GetValue<string>()?.Trim();
            var canonical = FindCard(cardId);
            if (canonical is null)
            {
                return Invalid(
                    $"夹具 {fixtureId} 找不到卡牌模型 {cardId ?? "<null>"}。",
                    "card_not_found",
                    out cards,
                    out error);
            }

            var pileName = cardNode["pile"]?.GetValue<string>()?.Trim() ?? "hand";
            if (!CombatPiles.TryGetValue(pileName, out var pile))
            {
                return Invalid(
                    $"夹具 {fixtureId} 的牌堆 {pileName} 无效。",
                    "invalid_pile",
                    out cards,
                    out error);
            }

            var upgrade = cardNode["upgrade"]?.GetValue<int>() ?? 0;
            var count = cardNode["count"]?.GetValue<int>() ?? 1;
            if (upgrade < 0 || count <= 0 || count > 20)
            {
                return Invalid(
                    $"夹具 {fixtureId} 的卡牌 {cardId} upgrade/count 无效。",
                    "invalid_card_quantity",
                    out cards,
                    out error);
            }

            result.Add(new CardFixture(canonical, pile, upgrade, count));
        }

        cards = result;
        return true;
    }

    private static bool TryParseBackpack(
        string fixtureId,
        JsonArray? requested,
        out IReadOnlyList<PotionFixture> potions,
        out JsonNode error)
    {
        var result = new List<PotionFixture>();
        error = new JsonObject();
        if (requested is null)
        {
            potions = result;
            return true;
        }

        foreach (var node in requested)
        {
            if (node is not JsonObject potionNode
                || !TryEnum(potionNode, "family", PotionFamily.Shield, out PotionFamily family)
                || !TryEnum(potionNode, "quality", PotionQuality.Normal, out PotionQuality quality)
                || !TryEnum(potionNode, "origin", PotionOrigin.Original, out PotionOrigin origin))
            {
                return Invalid(
                    $"夹具 {fixtureId} 包含无效的背包药剂描述。",
                    "invalid_potion",
                    out potions,
                    out error);
            }

            result.Add(new PotionFixture(
                family,
                quality,
                potionNode["upgraded"]?.GetValue<bool>() ?? false,
                origin));
        }

        potions = result;
        return true;
    }

    private static bool TryParseEnemies(
        Player player,
        string fixtureId,
        JsonArray? requested,
        out IReadOnlyList<EnemyFixture> enemies,
        out JsonNode error)
    {
        var result = new List<EnemyFixture>();
        error = new JsonObject();
        var currentEnemies = player.Creature.CombatState?.Enemies;
        if (requested is null)
        {
            enemies = result;
            return true;
        }

        foreach (var node in requested)
        {
            if (node is not JsonObject enemyNode)
                return Invalid($"夹具 {fixtureId} 的 enemies 项必须是对象。", "invalid_enemy", out enemies, out error);

            var index = enemyNode["index"]?.GetValue<int>() ?? -1;
            var hp = enemyNode["hp"]?.GetValue<int>() ?? 100;
            var block = enemyNode["block"]?.GetValue<int>() ?? 0;
            if (currentEnemies is null || index < 0 || index >= currentEnemies.Count)
            {
                return Invalid(
                    $"夹具 {fixtureId} 的 enemy index={index} 超出范围。",
                    "enemy_index_out_of_range",
                    out enemies,
                    out error);
            }
            if (hp < 1 || block < 0)
            {
                return Invalid(
                    $"夹具 {fixtureId} 的 enemy index={index} hp/block 无效。",
                    "invalid_enemy_stat",
                    out enemies,
                    out error);
            }
            result.Add(new EnemyFixture(index, hp, block));
        }

        enemies = result;
        return true;
    }

    private static bool TryParseTurn(
        string fixtureId,
        JsonObject? requested,
        out TurnFixture turn,
        out JsonNode error)
    {
        error = new JsonObject();
        if (requested is null)
        {
            turn = TurnFixture.Empty;
            return true;
        }

        if (!TryEnum(requested, "pendingDiffusion", DiffusionMode.None, out DiffusionMode diffusion))
        {
            return Invalid(
                $"夹具 {fixtureId} 的 pendingDiffusion 无效。",
                "invalid_turn_state",
                out turn,
                out error);
        }

        var discount = requested["nextFormulaEnergyDiscount"]?.GetValue<int>() ?? 0;
        var purification = requested["prePurificationCharges"]?.GetValue<int>() ?? 0;
        int? catalysis = requested["productionBoostCatalysisSnapshot"]?.GetValue<int>();
        if (discount < 0 || purification < 0 || catalysis < 0)
        {
            return Invalid(
                $"夹具 {fixtureId} 的回合计数不能为负数。",
                "invalid_turn_state",
                out turn,
                out error);
        }

        turn = new TurnFixture(discount, purification, catalysis, diffusion);
        return true;
    }

    private static async Task ClearScenario(Player player, bool preserveSystemCards)
    {
        var standardCards = player.PlayerCombatState?.AllCards
            .Where(card => !preserveSystemCards || card is not PotionSatchel)
            .ToArray() ?? [];
        if (standardCards.Length > 0)
            await CardPileCmd.RemoveFromCombat(standardCards, skipVisuals: true);

        var backpackCards = AlchemyBackpack.GetPotions(player).ToArray();
        if (backpackCards.Length > 0)
            await CardPileCmd.RemoveFromCombat(backpackCards, skipVisuals: true);
    }

    private static async Task ApplyPrinciples(Player player, IReadOnlyDictionary<string, int> requested)
    {
        var definitions = AlchemyPrinciples.All.ToDictionary(
            static definition => definition.LocalId,
            StringComparer.OrdinalIgnoreCase);
        foreach (var (name, amount) in requested)
            await SecondaryResourceCmd.Set(player, definitions[name].Id, amount);
    }

    private static void ApplyTurnState(Player player, TurnFixture fixture)
    {
        AlchemyCombatState.Require(player).Update(turn =>
        {
            turn.Reset();
            turn.NextFormulaEnergyDiscount = fixture.NextFormulaEnergyDiscount;
            turn.PrePurificationCharges = fixture.PrePurificationCharges;
            turn.ProductionBoostCatalysisSnapshot = fixture.ProductionBoostCatalysisSnapshot;
            turn.PendingDiffusion = fixture.PendingDiffusion;
        });
    }

    private static void ApplyEnergy(Player player, int energy)
    {
        if (player.PlayerCombatState is null)
            throw new InvalidOperationException("玩家战斗状态未附加。");
        player.PlayerCombatState.Energy = energy;
    }

    private static void ApplyEnemies(Player player, IReadOnlyList<EnemyFixture> fixtures)
    {
        var enemies = player.Creature.CombatState?.Enemies
            ?? throw new InvalidOperationException("当前战斗没有敌人集合。");
        foreach (var fixture in fixtures)
        {
            var enemy = enemies[fixture.Index];
            enemy.SetCurrentHpInternal(fixture.Hp);
            if (enemy.Block > fixture.Block)
                enemy.LoseBlockInternal(enemy.Block - fixture.Block);
            else if (enemy.Block < fixture.Block)
                enemy.GainBlockInternal(fixture.Block - enemy.Block);
        }
    }

    private static async Task AddCards(Player player, IReadOnlyList<CardFixture> fixtures)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("当前不在战斗中。");
        foreach (var fixture in fixtures)
        {
            for (var index = 0; index < fixture.Count; index++)
            {
                var card = combatState.CreateCard(fixture.Canonical.CanonicalInstance, player);
                for (var level = 0; level < fixture.Upgrade; level++)
                    CardCmd.Upgrade(card, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
                var result = await CardPileCmd.AddGeneratedCardToCombat(card, fixture.Pile, player);
                if (!result.success)
                    throw new InvalidOperationException($"无法把 {fixture.Canonical.Id.Entry} 加入 {fixture.Pile}。");
            }
        }
    }

    private static async Task AddBackpackPotions(Player player, IReadOnlyList<PotionFixture> fixtures)
    {
        foreach (var fixture in fixtures)
        {
            var potion = await AlchemyBackpack.Brew(
                player,
                fixture.Family,
                fixture.Quality,
                fixture.Upgraded,
                fixture.Origin,
                recordAsBrewed: false);
            if (potion is null)
                throw new InvalidOperationException($"无法创建 {fixture.Quality} {fixture.Family} 药剂。");
        }
    }

    private static JsonObject CapturePrinciples(Player player)
    {
        var result = new JsonObject();
        foreach (var definition in AlchemyPrinciples.All)
            result[definition.LocalId] = AlchemyPrinciples.Get(player, definition);
        return result;
    }

    private static CardModel? FindCard(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : ModelDb.AllCards.FirstOrDefault(card =>
                string.Equals(card.Id.Entry, id, StringComparison.OrdinalIgnoreCase));

    private static bool TryEnum<TEnum>(
        JsonObject source,
        string property,
        TEnum defaultValue,
        out TEnum value)
        where TEnum : struct, Enum
    {
        var raw = source[property]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = defaultValue;
            return true;
        }
        return Enum.TryParse(raw, ignoreCase: true, out value);
    }

    private static bool Invalid(string message, string code, out JsonNode error)
    {
        error = TestToolResult.Fail(message, code);
        return false;
    }

    private static bool Invalid<T>(string message, string code, out T value, out JsonNode error)
    {
        value = default!;
        error = TestToolResult.Fail(message, code);
        return false;
    }

    private sealed record FixturePlan(
        string Id,
        int Energy,
        bool PreserveSystemCards,
        IReadOnlyDictionary<string, int> Principles,
        IReadOnlyList<CardFixture> Cards,
        IReadOnlyList<PotionFixture> Backpack,
        IReadOnlyList<EnemyFixture> Enemies,
        TurnFixture Turn);

    private sealed record CardFixture(CardModel Canonical, PileType Pile, int Upgrade, int Count);

    private sealed record PotionFixture(
        PotionFamily Family,
        PotionQuality Quality,
        bool Upgraded,
        PotionOrigin Origin);

    private sealed record EnemyFixture(int Index, int Hp, int Block);

    private sealed record TurnFixture(
        int NextFormulaEnergyDiscount,
        int PrePurificationCharges,
        int? ProductionBoostCatalysisSnapshot,
        DiffusionMode PendingDiffusion)
    {
        public static TurnFixture Empty { get; } = new(0, 0, null, DiffusionMode.None);
    }

    private static JsonObject Also(this JsonObject result, string property, JsonNode value)
    {
        result[property] = value;
        return result;
    }
}