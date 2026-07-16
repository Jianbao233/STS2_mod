using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Combat.CardTargeting;
using DimensionalTraveler.Bootstrap;

namespace DimensionalTraveler.Alchemy;

public static class AlchemyTargetTypes
{
    public static TargetType FriendlyCreature { get; private set; }

    public static void Register()
    {
        if (FriendlyCreature != default)
            return;

        FriendlyCreature = CustomTargetType.RegisterSingleTargetType(
            Entry.ModId,
            "friendly_creature",
            static (creature, player) =>
                creature.IsAlive
                && !creature.IsPet
                && creature.Side == player.Creature.Side);
    }
}