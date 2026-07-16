using Godot;
using MegaCrit.Sts2.Core.Helpers;
using STS2RitsuLib.Scaffolding.Content;

namespace DimensionalTraveler.Content.Pools;

public sealed class TravelerCardPool : TypeListCardPoolModel
{
    public override string Title => "dimensional_traveler";

    public override string EnergyColorName => "ironclad";

    public override Color DeckEntryCardColor => new("57C7C9");

    public override Color EnergyOutlineColor => new("174E59FF");

    public override bool IsColorless => false;
}

public sealed class TravelerRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "ironclad";

    public override Color LabOutlineColor => new("57C7C9");
}

public sealed class TravelerPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "ironclad";

    public override Color LabOutlineColor => new("57C7C9");
}