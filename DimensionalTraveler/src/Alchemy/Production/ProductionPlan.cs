using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Alchemy.Production;

public enum ProductionKind
{
    DirectedBasic,
    FlexibleBasic,
    Other,
}

public sealed record ProductionPlan(
    IReadOnlyList<ResourceProduction> Resources,
    int Energy = 0)
{
    public static ProductionPlan Resource(SecondaryResourceDefinition resource, int amount) =>
        new([new ResourceProduction(resource.Id, amount)]);

    public static ProductionPlan ResourcesOf(params (SecondaryResourceDefinition Resource, int Amount)[] resources) =>
        new(resources.Select(static item => new ResourceProduction(item.Resource.Id, item.Amount)).ToArray());

    public ProductionPlan WithEnergy(int amount) => this with { Energy = amount };
}

public interface IAlchemyProductionListener
{
    Task AfterExplicitProduction(
        Player player,
        ProductionSnapshot snapshot,
        CardModel source);
}