using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using DimensionalTraveler.Characters;

namespace DimensionalTraveler.Alchemy.State;

public static class AlchemyCombatState
{
    public static bool IsTraveler(Player player) => player.Character is Traveler;

    public static AlchemyCombatStatePower Require(Player player)
    {
        if (!IsTraveler(player))
            throw new InvalidOperationException($"玩家 {player.NetId} 不是次元旅人，不能访问炼金战斗状态。");

        return player.Creature.GetPower<AlchemyCombatStatePower>()
            ?? throw new InvalidOperationException(
                $"玩家 {player.NetId} 缺少 AlchemyCombatStatePower；战斗状态必须由初始遗物显式挂载。");
    }

    public static async Task<AlchemyCombatStatePower> Attach(Player player)
    {
        if (!IsTraveler(player))
            throw new InvalidOperationException($"玩家 {player.NetId} 不是次元旅人，不能挂载炼金战斗状态。");

        var existing = player.Creature.GetPower<AlchemyCombatStatePower>();
        if (existing is not null)
            return existing;

        var attached = await PowerCmd.Apply<AlchemyCombatStatePower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            1m,
            player.Creature,
            cardSource: null,
            silent: true);
        if (attached is null)
            throw new InvalidOperationException($"无法为玩家 {player.NetId} 挂载炼金战斗状态。");

        attached.InitializeDigest();
        return attached;
    }
}