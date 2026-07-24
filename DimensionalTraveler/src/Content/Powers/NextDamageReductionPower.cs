using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DimensionalTraveler.Content.Powers;

[RegisterPower]
public sealed class NextDamageReductionPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("GUARDED_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner || amount <= 0m)
            return 1m;

        return Math.Clamp((100m - Amount) / 100m, 0m, 1m);
    }

    public override Task AfterModifyingDamageAmount(CardModel? cardSource) =>
        PowerCmd.Remove(this);
}