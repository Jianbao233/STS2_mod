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
public sealed class AttackAmplificationPower : ModPowerTemplate
{
    private int _charges30;
    private int _charges50;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("DOUBLE_DAMAGE_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public static async Task<AttackAmplificationPower?> Apply(
        PlayerChoiceContext choiceContext,
        Creature target,
        int percent,
        int charges,
        Creature? applier,
        CardModel? cardSource)
    {
        Validate(percent, charges);
        var power = target.GetPower<AttackAmplificationPower>();
        if (power is null)
        {
            power = (AttackAmplificationPower)ModelDb.Power<AttackAmplificationPower>().ToMutable();
            power.AddCharges(percent, charges);
            await PowerCmd.Apply(choiceContext, power, target, charges, applier, cardSource);
            power.ReconcileChargeCount(percent);
            return target.GetPower<AttackAmplificationPower>();
        }

        power.AddCharges(percent, charges);
        var after = await PowerCmd.ModifyAmount(
            choiceContext,
            power,
            charges,
            applier,
            cardSource);
        power.ReconcileChargeCount(percent);
        return after > 0 ? target.GetPower<AttackAmplificationPower>() : null;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner || cardSource?.Type != CardType.Attack || !props.IsPoweredAttack())
            return 1m;

        return 1m + ActivePercent / 100m;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Type != CardType.Attack
            || !cardPlay.IsLastInSeries)
        {
            return;
        }

        var percent = ConsumeActiveCharge();
        await PowerCmd.ModifyAmount(choiceContext, this, -1m, null, null);
        ReconcileChargeCount(percent);
    }

    private int ActivePercent => _charges50 > 0 ? 50 : _charges30 > 0 ? 30 : 0;

    private void AddCharges(int percent, int charges)
    {
        AssertMutable();
        if (percent == 50)
            _charges50 += charges;
        else
            _charges30 += charges;
    }

    private int ConsumeActiveCharge()
    {
        AssertMutable();
        if (_charges50 > 0)
        {
            _charges50 -= 1;
            return 50;
        }

        if (_charges30 > 0)
        {
            _charges30 -= 1;
            return 30;
        }

        throw new InvalidOperationException("攻击增幅层数与内部资格队列不一致。");
    }

    private void ReconcileChargeCount(int preferredPercent)
    {
        AssertMutable();
        var difference = Amount - (_charges30 + _charges50);
        if (difference > 0)
        {
            AddCharges(preferredPercent, difference);
        }
        else if (difference < 0)
        {
            var remaining = -difference;
            if (preferredPercent == 50)
            {
                RemoveCharges(ref _charges50, ref remaining);
                RemoveCharges(ref _charges30, ref remaining);
            }
            else
            {
                RemoveCharges(ref _charges30, ref remaining);
                RemoveCharges(ref _charges50, ref remaining);
            }
        }

        if (_charges30 + _charges50 != Amount)
            throw new InvalidOperationException("增幅 Power 层数与内部资格队列无法对账。");
    }

    private static void RemoveCharges(ref int stored, ref int remaining)
    {
        var removed = Math.Min(stored, remaining);
        stored -= removed;
        remaining -= removed;
    }

    private static void Validate(int percent, int charges)
    {
        if (percent is not (30 or 50))
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "攻击增幅仅支持 30% 或 50%。");
        if (charges <= 0)
            throw new ArgumentOutOfRangeException(nameof(charges), charges, "攻击增幅次数必须为正数。");
    }
}

[RegisterPower]
public sealed class BlockAmplificationPower : ModPowerTemplate
{
    private int _charges30;
    private int _charges50;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("GUARDED_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public static async Task<BlockAmplificationPower?> Apply(
        PlayerChoiceContext choiceContext,
        Creature target,
        int percent,
        int charges,
        Creature? applier,
        CardModel? cardSource)
    {
        Validate(percent, charges);
        var power = target.GetPower<BlockAmplificationPower>();
        if (power is null)
        {
            power = (BlockAmplificationPower)ModelDb.Power<BlockAmplificationPower>().ToMutable();
            power.AddCharges(percent, charges);
            await PowerCmd.Apply(choiceContext, power, target, charges, applier, cardSource);
            power.ReconcileChargeCount(percent);
            return target.GetPower<BlockAmplificationPower>();
        }

        power.AddCharges(percent, charges);
        var after = await PowerCmd.ModifyAmount(
            choiceContext,
            power,
            charges,
            applier,
            cardSource);
        power.ReconcileChargeCount(percent);
        return after > 0 ? target.GetPower<BlockAmplificationPower>() : null;
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        target == Owner && block > 0m
            ? 1m + ActivePercent / 100m
            : 1m;

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (creature != Owner || amount <= 0m)
            return;

        var percent = ConsumeActiveCharge();
        await PowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            this,
            -1m,
            null,
            null);
        ReconcileChargeCount(percent);
    }

    private int ActivePercent => _charges50 > 0 ? 50 : _charges30 > 0 ? 30 : 0;

    private void AddCharges(int percent, int charges)
    {
        AssertMutable();
        if (percent == 50)
            _charges50 += charges;
        else
            _charges30 += charges;
    }

    private int ConsumeActiveCharge()
    {
        AssertMutable();
        if (_charges50 > 0)
        {
            _charges50 -= 1;
            return 50;
        }

        if (_charges30 > 0)
        {
            _charges30 -= 1;
            return 30;
        }

        throw new InvalidOperationException("格挡增幅层数与内部资格队列不一致。");
    }

    private void ReconcileChargeCount(int preferredPercent)
    {
        AssertMutable();
        var difference = Amount - (_charges30 + _charges50);
        if (difference > 0)
        {
            AddCharges(preferredPercent, difference);
        }
        else if (difference < 0)
        {
            var remaining = -difference;
            if (preferredPercent == 50)
            {
                RemoveCharges(ref _charges50, ref remaining);
                RemoveCharges(ref _charges30, ref remaining);
            }
            else
            {
                RemoveCharges(ref _charges30, ref remaining);
                RemoveCharges(ref _charges50, ref remaining);
            }
        }

        if (_charges30 + _charges50 != Amount)
            throw new InvalidOperationException("增幅 Power 层数与内部资格队列无法对账。");
    }

    private static void RemoveCharges(ref int stored, ref int remaining)
    {
        var removed = Math.Min(stored, remaining);
        stored -= removed;
        remaining -= removed;
    }

    private static void Validate(int percent, int charges)
    {
        if (percent is not (30 or 50))
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "格挡增幅仅支持 30% 或 50%。");
        if (charges <= 0)
            throw new ArgumentOutOfRangeException(nameof(charges), charges, "格挡增幅次数必须为正数。");
    }
}