using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DimensionalTraveler.Content.Powers;

[RegisterPower]
public sealed class EtherealPower : ModPowerTemplate
{
    private bool _skipNextOwnerTurnStart;

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("DIAMOND_DIADEM_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        target == Owner && amount > 0m ? 0.5m : 1m;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _skipNextOwnerTurnStart = CombatState.CurrentSide != Owner.Side;
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0m && CombatState.CurrentSide != Owner.Side)
            _skipNextOwnerTurnStart = true;
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner))
            return;

        if (_skipNextOwnerTurnStart)
        {
            _skipNextOwnerTurnStart = false;
            return;
        }

        await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class CorrosionPower : ModPowerTemplate
{
    private const ValueProp DerivedCorrosionDamage = (ValueProp)(1 << 20);

    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("POISON_POWER");

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || result.TotalDamage <= 0
            || props.HasFlag(DerivedCorrosionDamage)
            || !Owner.IsAlive)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            Amount,
            ValueProp.Unpowered | DerivedCorrosionDamage,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class StrengthReductionPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("STRENGTH_POWER");

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource) =>
        PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            -amount,
            applier,
            cardSource,
            silent: true);

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount != Amount)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner,
                -amount,
                applier,
                cardSource,
                silent: true);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.ModifyAmount(choiceContext, this, -1m, Owner, null);
    }
}

[RegisterPower]
public sealed class AcceleratedRotationPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => ContentAssetProfiles.Power("DRAW_CARDS_NEXT_TURN_POWER");

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.Player is not { } player)
            return;

        Flash();
        await PlayerCmd.GainEnergy(1, player);
        await PowerCmd.Decrement(this);
    }
}