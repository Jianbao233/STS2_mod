using System.Text.Json;
using DimensionalTraveler.Alchemy.State;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace DimensionalTraveler.TestAdapter;

internal static class TestCardGrantAction
{
    private const string ActionKey = "test_card_grant_v1";

    private static readonly HashSet<string> AllowedCardIds = new(StringComparer.Ordinal)
    {
        "DIMENSIONAL_TRAVELER_CARD_LOCAL_DIFFUSION",
        "DIMENSIONAL_TRAVELER_CARD_ECHO_REPLAY",
        "DIMENSIONAL_TRAVELER_CARD_SHIELD_POTION",
    };

    private const int TestEnergy = 10;

    private static readonly RitsuLibManagedNetActionDescriptor<TestCardGrantRequest> Descriptor = new(
        Entry.ModId,
        ActionKey,
        Serialize,
        Deserialize,
        Execute,
        GameActionType.CombatPlayPhaseOnly);

    public static void Register() => RitsuLibManagedNetActions.Register(Descriptor);

    public static bool Request(Player player, string cardId) =>
        CanRequest(player, cardId, out _)
        && RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            Descriptor,
            new TestCardGrantRequest(cardId),
            player.NetId);

    public static bool CanRequest(Player player, string cardId, out string failureCode)
    {
        if (player.Creature.CombatState is null || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
        {
            failureCode = "not_player_play_phase";
            return false;
        }
        if (!AllowedCardIds.Contains(cardId))
        {
            failureCode = "card_not_allowed";
            return false;
        }
        if (PileType.Hand.GetPile(player)?.Cards.Count >= CardPile.MaxCardsInHand)
        {
            failureCode = "hand_full";
            return false;
        }
        if (ModelDb.AllCards.All(card => !string.Equals(card.Id.Entry, cardId, StringComparison.Ordinal)))
        {
            failureCode = "card_not_found";
            return false;
        }

        failureCode = string.Empty;
        return true;
    }

    private static byte[] Serialize(TestCardGrantRequest request) => JsonSerializer.SerializeToUtf8Bytes(request);

    private static TestCardGrantRequest Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<TestCardGrantRequest>(payload)
        ?? throw new InvalidOperationException("测试卡牌注入动作载荷为空或格式无效。");

    private static async Task Execute(RitsuLibManagedNetActionContext<TestCardGrantRequest> context)
    {
        var player = context.Player;
        if (!CanRequest(player, context.Message.CardId, out var failureCode))
            throw new InvalidOperationException($"测试卡牌注入被拒绝：{failureCode}。");

        var canonical = ModelDb.AllCards.First(card =>
            string.Equals(card.Id.Entry, context.Message.CardId, StringComparison.Ordinal));
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("测试卡牌注入只能在战斗中执行。");
        player.PlayerCombatState!.Energy = Math.Max(player.PlayerCombatState.Energy, TestEnergy);
        var card = combatState.CreateCard(canonical.CanonicalInstance, player);
        var result = await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        if (!result.success)
            throw new InvalidOperationException($"测试卡牌 {context.Message.CardId} 未能加入手牌。");
    }

    private sealed record TestCardGrantRequest(string CardId);
}