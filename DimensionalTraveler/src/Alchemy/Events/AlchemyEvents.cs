using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using DimensionalTraveler.Alchemy.Resolution;
using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.Events;

public interface IAlchemyOriginalPotionBrewListener
{
    Task AfterOriginalPotionBrewed(
        Player player,
        AlchemyPotionCard potion,
        AbstractModel? source);
}

public interface IAlchemyExistingPotionQualityListener
{
    Task AfterExistingPotionQualityChanged(
        Player player,
        AlchemyPotionCard potion,
        AbstractModel source);
}

public static class AlchemyEvents
{
    public static async Task NotifyOriginalPotionBrewed(
        Player player,
        AlchemyPotionCard potion,
        AbstractModel? source)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("原始药剂炼成只能在战斗中通知监听器。");
        foreach (var listener in combatState
                     .IterateHookListeners()
                     .OfType<IAlchemyOriginalPotionBrewListener>()
                     .ToArray())
        {
            await listener.AfterOriginalPotionBrewed(player, potion, source);
        }
    }

    public static async Task NotifyExistingPotionQualityChanged(
        Player player,
        AlchemyPotionCard potion,
        AbstractModel source)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("药剂品质变化只能在战斗中通知监听器。");
        foreach (var listener in combatState
                     .IterateHookListeners()
                     .OfType<IAlchemyExistingPotionQualityListener>()
                     .ToArray())
        {
            await listener.AfterExistingPotionQualityChanged(player, potion, source);
        }
    }
}