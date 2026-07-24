using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DimensionalTraveler.Content.Powers;

[RegisterPower]
public sealed class PerpetualEnergyPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("ENERGY_NEXT_TURN_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player == Owner.Player ? amount + Amount : amount;
}