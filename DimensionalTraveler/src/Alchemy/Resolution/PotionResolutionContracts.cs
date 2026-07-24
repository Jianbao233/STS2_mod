using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using DimensionalTraveler.Alchemy.State;

namespace DimensionalTraveler.Alchemy.Resolution;

public sealed record PotionResolutionResult(
    PotionDescriptor Descriptor,
    TargetSnapshot FrozenTargets,
    TargetSnapshot ResolvedTargets,
    DiffusionMode DiffusionMode)
{
    public bool AffectedMultipleTargets =>
        ResolvedTargets.CombatIds.Distinct().Skip(1).Any();
}

public interface IAlchemyPotionResolutionListener
{
    Task AfterOriginalPotionResolved(
        PlayerChoiceContext choiceContext,
        Player player,
        PotionResolutionResult result,
        CardModel source);
}