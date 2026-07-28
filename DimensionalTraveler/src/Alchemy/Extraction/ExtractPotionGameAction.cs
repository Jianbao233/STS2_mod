using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Runs;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Resources;
using STS2RitsuLib.Networking.ManagedActions;

namespace DimensionalTraveler.Alchemy.Extraction;

public static class ExtractPotionGameAction
{
    private const string ActionKey = "extract_potion_v1";

    private static readonly RitsuLibManagedNetActionDescriptor<ExtractPotionRequest> Descriptor = new(
        Bootstrap.Entry.ModId,
        ActionKey,
        Serialize,
        Deserialize,
        Execute,
        GameActionType.CombatPlayPhaseOnly);

    public static void Register() => RitsuLibManagedNetActions.Register(Descriptor);

    public static bool Request(Player player, int potionSlotIndex, string potionId) =>
        RitsuLibManagedNetActions.Request(
            RunManager.Instance,
            Descriptor,
            new ExtractPotionRequest(potionSlotIndex, potionId),
            player.NetId);

    private static byte[] Serialize(ExtractPotionRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(request);

    private static ExtractPotionRequest Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<ExtractPotionRequest>(payload)
        ?? throw new InvalidOperationException("萃取动作载荷为空或格式无效。");

    private static async Task Execute(RitsuLibManagedNetActionContext<ExtractPotionRequest> context)
    {
        var player = context.Player;
        if (!TryPrepare(player, context.Message, out var pending, out var preparationFailure))
        {
            ExtractionAudit.Record(
                player,
                context.Message.PotionSlotIndex,
                context.Message.PotionId,
                ExtractionAuditStage.Rejected,
                preparationFailure);
            return;
        }

        if (pending.AttackChoice is not null)
        {
            ExtractionAudit.Record(
                player,
                context.Message.PotionSlotIndex,
                context.Message.PotionId,
                ExtractionAuditStage.AwaitingChoice);
        }

        var selectedReward = await ChooseAttackPotionReward(
            player,
            context.PlayerChoiceContext,
            pending.Plan,
            pending.AttackChoice?.Candidates);
        if (pending.AttackChoice is not null && selectedReward is null)
        {
            ExtractionAudit.Record(
                player,
                context.Message.PotionSlotIndex,
                context.Message.PotionId,
                ExtractionAuditStage.Cancelled,
                "choice_not_committed");
            return;
        }

        if (!TryPrepareCommit(player, context.Message, pending, selectedReward, out var commitFailure))
        {
            ExtractionAudit.Record(
                player,
                context.Message.PotionSlotIndex,
                context.Message.PotionId,
                ExtractionAuditStage.Rejected,
                commitFailure);
            return;
        }

        await Commit(player, pending, selectedReward);
        ExtractionAudit.Record(
            player,
            context.Message.PotionSlotIndex,
            context.Message.PotionId,
            ExtractionAuditStage.Committed);
    }

    private static bool TryPrepare(
        Player player,
        ExtractPotionRequest request,
        out PreparedExtraction pending,
        out string failure)
    {
        pending = null!;
        if (!TryResolvePlan(player, request, out var plan, out var sourcePotion))
        {
            failure = "initial_recheck_failed";
            return false;
        }

        AttackChoicePreview? attackChoice = null;
        if (plan.ChoiceMode == ExtractionChoiceMode.AttackPotion
            && !TryCreateAttackChoicePreview(player, out attackChoice, out failure))
        {
            return false;
        }

        pending = new PreparedExtraction(
            plan,
            sourcePotion,
            player.PlayerCombatState!.TurnNumber,
            attackChoice);
        failure = string.Empty;
        return true;
    }

    private static bool TryPrepareCommit(
        Player player,
        ExtractPotionRequest request,
        PreparedExtraction pending,
        ExtractionPotionReward? selectedReward,
        out string failure)
    {
        if (!IsCurrentSource(player, request, pending.SourcePotion))
        {
            failure = "commit_source_changed";
            return false;
        }
        if (!player.Creature.IsAlive)
        {
            failure = "owner_not_alive";
            return false;
        }
        if (player.Creature.CombatState is null
            || CombatManager.Instance.IsEnding
            || CombatManager.Instance.IsPlayerReadyToEndTurn(player)
            || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play
            || player.PlayerCombatState.TurnNumber != pending.TurnNumber)
        {
            failure = "turn_no_longer_valid";
            return false;
        }
        if (!TryResolveRewards(pending, selectedReward, out var rewards, out failure))
            return false;

        try
        {
            foreach (var reward in rewards)
                _ = PotionCatalog.GetCanonical(reward.Family, reward.Quality);
        }
        catch (Exception exception)
        {
            failure = $"reward_model_unavailable:{exception.GetType().Name}";
            return false;
        }

        if (pending.AttackChoice is not null
            && !RngMatchesSnapshot(
                player.RunState.Rng.CombatCardGeneration.ToSerializable(),
                pending.AttackChoice.RngBefore))
        {
            failure = "attack_choice_rng_changed_while_pending";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool IsCurrentSource(
        Player player,
        ExtractPotionRequest request,
        PotionModel sourcePotion) =>
        request.PotionSlotIndex >= 0
        && request.PotionSlotIndex < player.PotionSlots.Count
        && ReferenceEquals(player.PotionSlots[request.PotionSlotIndex], sourcePotion)
        && sourcePotion.Owner == player
        && string.Equals(sourcePotion.Id.Entry, request.PotionId, StringComparison.Ordinal);

    private static bool TryResolveRewards(
        PreparedExtraction pending,
        ExtractionPotionReward? selectedReward,
        out IReadOnlyList<ExtractionPotionReward> rewards,
        out string failure)
    {
        rewards = [];
        failure = string.Empty;
        if (pending.AttackChoice is null)
        {
            if (selectedReward.HasValue)
            {
                failure = "unexpected_choice_reward";
                return false;
            }
            rewards = pending.Plan.PotionRewards;
            return true;
        }
        if (!selectedReward.HasValue || !pending.AttackChoice.Candidates.Contains(selectedReward.Value))
        {
            failure = "attack_choice_reward_invalid";
            return false;
        }

        rewards = [selectedReward.Value];
        return true;
    }

    private static async Task Commit(
        Player player,
        PreparedExtraction pending,
        ExtractionPotionReward? selectedReward)
    {
        if (pending.AttackChoice is not null)
            AdvanceAttackChoiceRng(player.RunState.Rng.CombatCardGeneration, pending.AttackChoice);

        // 提交前已验证全部业务条件；此处固定执行冻结收益，且不触发原版药水弃置 Hook。
        pending.SourcePotion.Discard();
        await ApplyPlan(player, pending.Plan, selectedReward);
    }

    private static void AdvanceAttackChoiceRng(
        MegaCrit.Sts2.Core.Random.Rng rng,
        AttackChoicePreview preview)
    {
        var committedCandidates = SelectDistinctAttackRewards(rng);
        if (!committedCandidates.SequenceEqual(preview.Candidates))
            throw new InvalidOperationException("攻击药水萃取提交时的 RNG 候选与预览不一致。");
    }

    private static bool TryResolvePlan(
        Player player,
        ExtractPotionRequest request,
        out ExtractionPlan plan,
        out PotionModel potion)
    {
        plan = null!;
        potion = null!;
        if (!ExtractionFlow.TryGetPlan(player, request.PotionSlotIndex, out plan, out _))
            return false;

        potion = player.PotionSlots[request.PotionSlotIndex]!;
        return string.Equals(potion.Id.Entry, request.PotionId, StringComparison.Ordinal);
    }

    private static bool TryCreateAttackChoicePreview(
        Player player,
        out AttackChoicePreview preview,
        out string failure)
    {
        preview = null!;
        failure = string.Empty;
        try
        {
            var snapshot = player.RunState.Rng.CombatCardGeneration.ToSerializable();
            var previewRng = new MegaCrit.Sts2.Core.Random.Rng(snapshot);
            preview = new AttackChoicePreview(snapshot, SelectDistinctAttackRewards(previewRng));
            return true;
        }
        catch (Exception exception)
        {
            failure = $"attack_choice_preview_failed:{exception.GetType().Name}";
            return false;
        }
    }

    private static async Task<ExtractionPotionReward?> ChooseAttackPotionReward(
        Player player,
        GameActionPlayerChoiceContext choiceContext,
        ExtractionPlan plan,
        IReadOnlyList<ExtractionPotionReward>? previewCandidates)
    {
        if (plan.ChoiceMode == ExtractionChoiceMode.None)
            return null;
        if (plan.ChoiceMode != ExtractionChoiceMode.AttackPotion || previewCandidates is null)
            throw new ArgumentOutOfRangeException(nameof(plan), plan.ChoiceMode, null);

        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("攻击药水萃取只能在活动战斗中执行。");
        var candidates = previewCandidates
            .Select(reward => (AlchemyPotionCard)combatState.CreateCard(
                PotionCatalog.GetCanonical(reward.Family, reward.Quality),
                player))
            .ToArray();
        foreach (var candidate in candidates)
            candidate.SetOrigin(PotionOrigin.Extracted);

        try
        {
            var selected = await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                candidates,
                player,
                canSkip: true) as AlchemyPotionCard;
            return selected is null
                ? null
                : new ExtractionPotionReward(selected.Family, selected.Quality, selected.IsUpgraded);
        }
        finally
        {
            foreach (var candidate in candidates)
                candidate.RemoveFromState();
        }
    }

    private static bool RngMatchesSnapshot(
        MegaCrit.Sts2.Core.Saves.SerializableRng left,
        MegaCrit.Sts2.Core.Saves.SerializableRng right) =>
        left.counter == right.counter
        && left.state0 == right.state0
        && left.state1 == right.state1
        && left.state2 == right.state2
        && left.state3 == right.state3;

    private static async Task ApplyPlan(
        Player player,
        ExtractionPlan plan,
        ExtractionPotionReward? selectedReward)
    {
        await AlchemyPrinciples.Gain(
            player,
            ResolvePrinciple(plan.SpecialPrinciple),
            1,
            source: null);
        await AlchemyPrinciples.Gain(
            player,
            ResolvePrinciple(plan.BasicPrinciple),
            plan.BasicAmount,
            source: null);

        if (plan.Gold > 0)
            await PlayerCmd.GainGold(plan.Gold, player);
        if (plan.MaxHp > 0)
            await CreatureCmd.GainMaxHp(player.Creature, plan.MaxHp);

        var rewards = selectedReward.HasValue
            ? new[] { selectedReward.Value }
            : plan.PotionRewards;
        foreach (var reward in rewards)
        {
            await AlchemyBackpack.Brew(
                player,
                reward.Family,
                reward.Quality,
                reward.IsUpgraded,
                PotionOrigin.Extracted,
                recordAsBrewed: false);
        }
    }

    private static IReadOnlyList<ExtractionPotionReward> SelectDistinctAttackRewards(
        MegaCrit.Sts2.Core.Random.Rng rng)
    {
        var remaining = new List<ExtractionPotionReward>
        {
            new(PotionFamily.Attack, PotionQuality.Normal),
            new(PotionFamily.Corruption, PotionQuality.Normal),
            new(PotionFamily.Weakness, PotionQuality.Normal),
            new(PotionFamily.StrengthReduction, PotionQuality.Normal),
        };
        var selected = new List<ExtractionPotionReward>(3);
        while (selected.Count < 3)
        {
            var next = rng.NextItem(remaining);
            selected.Add(next);
            remaining.Remove(next);
        }
        return selected;
    }

    private static STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceDefinition ResolvePrinciple(
        AlchemyPrincipleKind kind) => kind switch
        {
            AlchemyPrincipleKind.Vitality => AlchemyPrinciples.Vitality,
            AlchemyPrincipleKind.Volatility => AlchemyPrinciples.Volatility,
            AlchemyPrincipleKind.Corruption => AlchemyPrinciples.Corruption,
            AlchemyPrincipleKind.Catalysis => AlchemyPrinciples.Catalysis,
            AlchemyPrincipleKind.Diffusion => AlchemyPrinciples.Diffusion,
            AlchemyPrincipleKind.Echo => AlchemyPrinciples.Echo,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private sealed record PreparedExtraction(
        ExtractionPlan Plan,
        PotionModel SourcePotion,
        int TurnNumber,
        AttackChoicePreview? AttackChoice);

    private sealed record AttackChoicePreview(
        MegaCrit.Sts2.Core.Saves.SerializableRng RngBefore,
        IReadOnlyList<ExtractionPotionReward> Candidates);

    private sealed record ExtractPotionRequest(int PotionSlotIndex, string PotionId);
}