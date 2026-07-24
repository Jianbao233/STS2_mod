using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.CardTargeting;

namespace DimensionalTraveler.Alchemy.Choices;

public static class AlchemyTargetChoices
{
    public static async Task<Creature?> ChooseAdditionalTarget(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<Creature> candidates)
    {
        var frozen = candidates
            .Where(static creature => creature.CombatId.HasValue)
            .OrderBy(static creature => creature.CombatId)
            .ToArray();
        if (frozen.Length == 0)
            return null;

        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        var choiceId = synchronizer.ReserveChoiceId(player);
        await choiceContext.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
        try
        {
            if (!LocalContext.IsMe(player))
            {
                var remoteIndex = (await synchronizer.WaitForRemoteChoice(player, choiceId)).AsIndex();
                if (remoteIndex < 0 || remoteIndex >= frozen.Length)
                {
                    throw new InvalidOperationException(
                        $"局部扩散收到非法同步目标索引 {remoteIndex}，候选数为 {frozen.Length}。");
                }

                return frozen[remoteIndex];
            }

            var room = NCombatRoom.Instance
                ?? throw new InvalidOperationException("局部扩散选择只能在战斗房间中执行。");
            var ownerNode = room.GetCreatureNode(player.Creature)
                ?? throw new InvalidOperationException("找不到投药玩家的战斗节点。");
            var manager = NTargetManager.Instance;
            manager.StartTargeting(
                CustomTargetType.Anyone,
                ownerNode,
                TargetMode.ClickMouseToTarget,
                exitEarlyCondition: null,
                nodeFilter: node => node is NCreature creatureNode && frozen.Contains(creatureNode.Entity));
            var selected = await manager.SelectionFinished() as NCreature;
            var selectedCreature = selected?.Entity
                ?? throw new InvalidOperationException("局部扩散目标选择不可跳过。");
            var index = Array.IndexOf(frozen, selectedCreature);
            if (index < 0)
                throw new InvalidOperationException("局部扩散返回了不在冻结候选中的目标。");

            synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(index));
            return frozen[index];
        }
        finally
        {
            await choiceContext.SignalPlayerChoiceEnded();
        }
    }
}